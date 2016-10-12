using System;
using System.Collections.Generic;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
    public interface INetworkClient
    {
        void Connect(string ip, int port);
        void Close();
        void Disconnect();

        void Send(IList<ArraySegment<byte>> buffer);
        void Send(byte[] buffer);
        void StartReceive(OnDataArrivedHandler callback); 
    }
}
