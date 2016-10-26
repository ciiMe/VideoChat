namespace VideoPlayer.Network
{
    public interface INetworkClient
    {
        event MediaBufferEventHandler OnPacketReceived;

        void Connect(string ip, int port);
        void Start();
        void Close();
        void Disconnect();
        
        void Send(byte[] buffer);
    }
}
