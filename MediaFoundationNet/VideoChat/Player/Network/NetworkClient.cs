using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
    public struct ReceivedBuffer
    {
        public byte[] Buffer;
        public int LengthForCurrentPacket;
    }

    public class NetworkPacket
    {
        private StspOperation _option;
        private int _length;

        private List<ReceivedBuffer> _receivedBuffers;
        private int _receivedLength;
        private int _extraDataLength;
        private MediaBufferEventHandler _callBack;

        public StspOperation Option
        {
            get
            {
                return _option;
            }
            set
            {
                _option = value;
            }
        }

        public int Length
        {
            get
            {
                return _length;
            }
            set
            {
                _length = value;
            }
        }

        public MediaBufferEventHandler Callback => _callBack;

        public NetworkPacket(MediaBufferEventHandler callback)
        {
            _option = StspOperation.StspOperation_Unknown;
            _length = 0;
            _receivedBuffers = new List<ReceivedBuffer>();
            _receivedLength = 0;

            _callBack = callback;
        }

        /// <summary>
        /// Add received buffer to packet list.
        /// </summary>
        /// <param name="buffer">The received buffer.</param>
        /// <param name="startPosition">The start position of the valid data.</param>
        /// <param name="dataLength">The total length of the valid data.</param>
        public void AddBuffer(byte[] buffer, int dataLength)
        {
            var offset = 0;
            if (IsEmpty())
            {
                _length = BitConverter.ToInt32(buffer, 0);
                _option = (StspOperation)BitConverter.ToInt32(buffer, 0 + 4);
                offset = 8;
            }
            var bufferValidLen = dataLength - offset;
            var data = new byte[bufferValidLen];
            Array.Copy(buffer, offset, data, 0, bufferValidLen);

            _receivedBuffers.Add(new ReceivedBuffer
            {
                Buffer = data,
                LengthForCurrentPacket = bufferValidLen <= _length - _receivedLength ? bufferValidLen : _length - _receivedLength
            });

            _extraDataLength = bufferValidLen <= _length - _receivedLength ? 0 : dataLength - (_length - _receivedLength);
            _receivedLength += bufferValidLen;
        }

        public bool IsEmpty()
        {
            return _receivedBuffers.Count == 0;
        }

        public bool IsFull()
        {
            return _receivedLength >= _length;
        }

        /// <summary>
        /// Get the extra data which is not for this packet.
        /// </summary>
        public int GetExtraDataLength()
        {
            return _extraDataLength;
        }

        public byte[] ExportWholePacket()
        {
            if (_length <= 0)
            {
                return new byte[] { };
            }

            var result = new byte[_length];
            var pos = 0;
            foreach (var buffer in _receivedBuffers)
            {
                Array.Copy(buffer.Buffer, 0, result, pos, buffer.LengthForCurrentPacket);
                pos += buffer.LengthForCurrentPacket;
            }

            return result;
        }

        public void Reset()
        {
            _option = StspOperation.StspOperation_Unknown;
            _length = 0;

            _receivedBuffers.Clear();
            _receivedLength = 0;
            _extraDataLength = 0;
        }
    }

    /// <summary>
    /// Handle the special data for network stream.
    /// </summary>
    public class NetworkClient : INetworkClient
    {
        private const int ReceiveBufferSize = 2 * 1024;
        private const int MaxPacketSize = 1024 * 1024;

        private Socket _socket;
        private string _ip;
        private int _port;

        private byte[] _currentBuffer;
        private int _currentBufferDataLength;

        private NetworkPacket _penddingPacket;

        private object _critSec;

        public NetworkClient()
        {
            _critSec = new object();
        }

        public void Connect(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(ip, port);
        }

        public void Close()
        {
            _socket.Close();
        }

        public void Disconnect()
        {
            _socket.Disconnect(true);
        }

        public void Send(byte[] buffer)
        {
            //var len = _socket.Send(buffer);
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, handleDataSend, _socket);
        }

        public void Send(IList<ArraySegment<byte>> buffer)
        {
            _socket.Send(buffer);
        }

        public void StartReceive(MediaBufferEventHandler callback)
        {
            if (_penddingPacket != null)
            {
                //todo: throw working exception...
            }

            lock (_critSec)
            {
                _currentBuffer = new byte[ReceiveBufferSize];
                _currentBufferDataLength = 0;
                _penddingPacket = new NetworkPacket(callback);

                _socket.BeginReceive(_currentBuffer, 0, ReceiveBufferSize, SocketFlags.None, handleDateReceived, _socket);
            }
        }

        private void handleDateReceived(IAsyncResult ar)
        {
            lock (_critSec)
            {
                var socket = ar.AsyncState as Socket;
                _currentBufferDataLength = socket.EndReceive(ar);
                if (_currentBufferDataLength == 0)
                {
                    return;
                }

                processReceivedData(_currentBuffer, _currentBufferDataLength);
                _socket.BeginReceive(_currentBuffer, 0, ReceiveBufferSize, SocketFlags.None, handleDateReceived, _socket);
            }
        }

        private void processReceivedData(byte[] data, int validLength)
        {
            _penddingPacket.AddBuffer(data, validLength);
            if (_penddingPacket.IsFull())
            {
                invokePacketComplete();
                PrepareNextReceive();
            }
        }

        private void invokePacketComplete()
        {
            var data = _penddingPacket.ExportWholePacket();
            var option = _penddingPacket.Option;
            var handler = _penddingPacket.Callback;

            try
            {
                Debug.WriteLine($"Buffer complete. Length:{_penddingPacket.Length}");
                handler(option, new BufferPacket(data));
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception:{ex.Message} \r {ex.StackTrace}");
            }
        }

        private void PrepareNextReceive()
        {
            var extraLen = _penddingPacket.GetExtraDataLength();
            if (extraLen <= 0 || extraLen > _currentBufferDataLength)
            {
                _penddingPacket.Reset();
                return;
            }

            _penddingPacket.Reset();

            var data = new byte[extraLen];
            Array.Copy(_currentBuffer, _currentBufferDataLength - extraLen, data, 0, extraLen);
            processReceivedData(data, extraLen);
        }

        private void handleDataSend(IAsyncResult ar)
        {
            ((Socket)ar.AsyncState).EndSend(ar);
        }
    }
}
