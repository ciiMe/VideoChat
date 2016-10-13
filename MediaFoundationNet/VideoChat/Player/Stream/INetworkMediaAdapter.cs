using MediaFoundation;
using VideoPlayer.Network;

namespace VideoPlayer.Stream
{
    public interface INetworkMediaAdapter
    {
        HResult Open(string ip, int port); 

        void SendRequest(StspOperation eOperation);
        void SendDescribeRequest();

        event MediaBufferEventHandler OnDataArrived;
    }
}
