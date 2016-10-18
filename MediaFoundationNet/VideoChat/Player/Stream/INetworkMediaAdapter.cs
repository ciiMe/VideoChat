using MediaFoundation;

namespace VideoPlayer.Stream
{
    public interface INetworkMediaAdapter
    {
        HResult Open(string ip, int port);
        HResult Close();

        void SendStartRequest();
        void SendDescribeRequest();
        void SendRequest(StspOperation eOperation);

        event MediaBufferEventHandler OnDataArrived;
    }
}
