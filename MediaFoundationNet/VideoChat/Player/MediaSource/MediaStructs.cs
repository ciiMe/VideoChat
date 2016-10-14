namespace VideoPlayer.MediaSource
{
    public struct StspSampleHeader
    {
        public int dwStreamId;
        public long ullTimestamp;
        public long ullDuration;
        public int dwFlags;
        public int dwFlagMasks;
    };
}
