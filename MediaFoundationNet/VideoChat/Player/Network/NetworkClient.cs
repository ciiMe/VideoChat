using System;
using System.Diagnostics;
using System.Net.Sockets;
using System.Threading;
using VideoPlayer.Utils;

namespace VideoPlayer.Network
{
    class InvalidNetworkBufferException : Exception
    {
        private const string ExceptionMessage_InvalidBuffer = "Invalid data from network stream.";

        public InvalidNetworkBufferException() :
            base(ExceptionMessage_InvalidBuffer)
        {
            HResult = (int)MediaFoundation.HResult.E_INVALIDARG;
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
        private object _bufferLock;

        private INetworkBufferPacket _penddingPacket;
        private object _penddingPacketLock;

        private Thread _packetEventInvoker;
        private bool _isStarted;

        public bool IsStarted => _isStarted;

        public event MediaBufferEventHandler OnPacketReceived;

        public NetworkClient()
        {
            _bufferLock = new object();
            _isStarted = false;

            _penddingPacketLock = new object();
        }

        public void Connect(string ip, int port)
        {
            _ip = ip;
            _port = port;
            _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
            _socket.Connect(ip, port);

            Debug.WriteLine($"Connected {ip}:{port}");
        }

        public void Start()
        {
            if (_isStarted)
            {
                return;
            }

            if (_penddingPacket != null)
            {
                //todo: throw working exception...
            }
            _currentBuffer = new byte[ReceiveBufferSize];
            _penddingPacket = new NetworkBufferPacket();

            _packetEventInvoker = new Thread(new ThreadStart(eventInvokerHandler));
            _isStarted = true;
            _packetEventInvoker.Start();

            doReceive();
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }
            _isStarted = false;

            if (_socket != null)
            {
                _socket.Close();
                _socket = null;
            }
            Debug.WriteLine($"Disonnected from {_ip}:{_port}");
        }

        public void Disconnect()
        {
            if (!_isStarted)
            {
                return;
            }
            _socket.Disconnect(true);
        }

        public void Send(byte[] buffer)
        {
            if (!_isStarted)
            {
                return;
            }
            //var len = _socket.Send(buffer);
            _socket.BeginSend(buffer, 0, buffer.Length, SocketFlags.None, handleDataSend, _socket);
        }

        private void doReceive()
        {
            if (!_isStarted)
            {
                return;
            }

            if (!_socket.Connected)
            {
                //bad connection...
                _isStarted = false;
                raiseBadConnectionEvent();
                return;
            }

            lock (_bufferLock)
            {
                _socket.BeginReceive(_currentBuffer, 0, ReceiveBufferSize, SocketFlags.None, handleDateReceived, _socket);
            }
        }

        private void handleDateReceived(IAsyncResult ar)
        {
            if (!_isStarted)
            {
                return;
            }

            var socket = ar.AsyncState as Socket;
            if (!socket.Connected)
            {
                _isStarted = false;
                raiseBadConnectionEvent();
                return;
            }

            byte[] data;

            lock (_bufferLock)
            {
                var dataLen = socket.EndReceive(ar);
                if (dataLen == 0)
                {
                    return;
                }
                data = new byte[dataLen];
                Array.Copy(_currentBuffer, data, dataLen);
            }
            lock (_penddingPacketLock)
            {
                _penddingPacket.AddBuffer(data);
            }
            doReceive();
        }

        private void raiseBadConnectionEvent()
        {
            //todo: raise the event...
        }

        private void eventInvokerHandler()
        {
            var hasOption = false;

            while (_isStarted)
            {
                lock (_penddingPacketLock)
                {
                    hasOption = _penddingPacket.HasOptionData();
                }

                if (hasOption)
                {
                    invokePacketComplete();
                }
                else
                {
                    Thread.Sleep(1);
                }
            }
        }

        private void invokePacketComplete()
        {
            if (null == OnPacketReceived)
            {
                return;
            }

            //Debug.WriteLine($"Buffer complete. buffer length:{_penddingPacket.GetLength()} data length:{_penddingPacket.GetFirstOptionDataLength()}");
            IBufferPacket p;
            lock (_penddingPacketLock)
            {
                p = _penddingPacket.TakeFirstOption();
            }

            var header = BytesHelper.TakeObject<VideoStream_OperationHeader>(p);
            if (header.cbDataSize != p.GetLength())
            {
                throw new InvalidNetworkBufferException();
            }

            try
            {
                OnPacketReceived(header.eOperation, p);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Exception when invoke PacketComplete event handler:\r{ex.Message} \r {ex.StackTrace}");
            }
        }

        private void handleDataSend(IAsyncResult ar)
        {
            ((Socket)ar.AsyncState).EndSend(ar);
        }
    }
}
