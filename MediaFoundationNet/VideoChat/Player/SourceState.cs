namespace VideoPlayer
{
    // Possible states of the stsp source object
    public enum SourceState
    {
        // Invalid state, source cannot be used 
        SourceState_Invalid,
        // Opening the connection
        SourceState_Opening,
        // Streaming started
        SourceState_Starting,
        // Streaming started
        SourceState_Started,
        // Streanung stopped
        SourceState_Stopped,
        // Source is shut down
        SourceState_Shutdown,
    };
}
