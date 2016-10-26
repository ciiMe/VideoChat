namespace VideoPlayer
{
    public interface IStopable
    {
        void Start();
        bool IsStarted { get; }
        void Stop();
    }
}
