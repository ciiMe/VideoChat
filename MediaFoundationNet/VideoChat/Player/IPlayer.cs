namespace VideoPlayer
{
    public interface IPlayer
    {
        //todo: add more event, such as OnConnectionError,OnStart,OnStop etc.

        void Open(string ip, int port);
    }
}
