using MediaFoundation;

namespace VideoPlayer.Stream
{
    public delegate void OnDataArrivedHandler(StspOperation operation, byte[] data);

    public interface INetworkStreamAdapter
    {
        HResult Open(string ip, int port);
        HResult CheckShutdown();

        event OnDataArrivedHandler OnDataArrived;

        void SendRequest(StspOperation eOperation);
        void SendDescribeRequest();
    }
}
