using System;
using System.Collections.Generic;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
    public delegate void MediaBufferEventHandler(StspOperation operation, BufferPacket data);

    public interface INetworkClient
    {
        void Connect(string ip, int port);
        void Close();
        void Disconnect();

        void Send(IList<ArraySegment<byte>> buffer);
        void Send(byte[] buffer);
        void StartReceive(MediaBufferEventHandler callback);
    }
}
