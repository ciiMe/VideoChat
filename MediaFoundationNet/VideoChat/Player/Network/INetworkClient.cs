using System;
using System.Collections.Generic;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
    public interface INetworkClient
    {
        event MediaBufferEventHandler OnPacketReceived;

        void Connect(string ip, int port);
        void Start();
        void Close();
        void Disconnect();

        void Send(IList<ArraySegment<byte>> buffer);
        void Send(byte[] buffer);
    }
}
