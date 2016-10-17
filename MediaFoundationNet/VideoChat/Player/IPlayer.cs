using MediaFoundation;

namespace VideoPlayer
{
    public interface IPlayer
    {
        HResult Open(string url);
        HResult Open(string ip, int port);
    }
}
