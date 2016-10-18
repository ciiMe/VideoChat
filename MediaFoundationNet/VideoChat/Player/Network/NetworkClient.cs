using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Sockets;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
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
        private IBufferPacket _penddingPacket;
        private MediaBufferEventHandler _callBack;

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
                _penddingPacket = new BufferPacket();
                _callBack = callback;

                _socket.BeginReceive(_currentBuffer, 0, ReceiveBufferSize, SocketFlags.None, handleDateReceived, _socket);
            }
        }

        private void handleDateReceived(IAsyncResult ar)
        {
            lock (_critSec)
            {
                var socket = ar.AsyncState as Socket;
                var dataLen = socket.EndReceive(ar);
                if (dataLen == 0)
                {
                    return;
                }

                var data = new byte[dataLen];
                Array.Copy(_currentBuffer, data, dataLen);

                _penddingPacket.AddBuffer(data);
                if (_penddingPacket.HasOptionData())
                {
                    invokePacketComplete();
                }
                _socket.BeginReceive(_currentBuffer, 0, ReceiveBufferSize, SocketFlags.None, handleDateReceived, _socket);
            }
        }

        private void invokePacketComplete()
        {
            if (null == _callBack)
            {
                return;
            }

            var p = _penddingPacket.TakeFirstOption();

            try
            {
                Debug.WriteLine($"Buffer complete. buffer length:{p.GetBufferLength()} data length:{p.GetFirstOptionDataLength()}");
                _callBack(p);
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
