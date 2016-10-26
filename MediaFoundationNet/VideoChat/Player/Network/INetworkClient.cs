namespace VideoPlayer.Network
{
    public interface INetworkClient : IStopable
    {
        event MediaBufferEventHandler OnPacketReceived;

        void Connect(string ip, int port); 
        void Disconnect();

        void Send(byte[] buffer);
    }
}
