using MediaFoundation;

namespace VideoPlayer
{
    public delegate void MediaBufferEventHandler(VideoStream_Operation option, IBufferPacket packet);
    public delegate HResult BufferEventHandler(byte[] buffer);
    /// <summary>
    /// A packet to cache received buffer list.
    /// </summary>
    public interface IBufferPacket
    {
        /// <summary>
        /// Add buffer data to the end if list.
        /// </summary> 
        void AddBuffer(byte[] data);

        /// <summary>
        /// Get the total length of received buffers.
        /// </summary>
        int GetLength();

        /// <summary>
        /// Get the head buffer in the list.
        /// </summary>
        byte[] GetBuffer(int len);

        /// <summary>
        /// Take the head buffer out of the list.
        /// </summary>
        byte[] TakeBuffer(int len);
    }

    /// <summary>
    /// Buffer packet for network cache, it provide more management feature.
    /// </summary>
    public interface INetworkBufferPacket : IBufferPacket
    {
        /// <summary>
        /// Get the Option data length of the first option request.
        /// </summary>
        int GetFirstOptionDataLength();

        /// <summary>
        /// Get the Option type of the first option request.
        /// </summary>
        VideoStream_Operation GetFirstOperationDataType();

        /// <summary>
        /// Return true when the packet contains at least one entire option data.
        /// </summary>
        bool HasOptionData();

        /// <summary>
        /// Return true if the packet only contains one option request.
        /// </summary> 
        bool IsSingleOption();

        /// <summary>
        /// Take the first option from the received buffer list.
        /// </summary>
        IBufferPacket TakeFirstOption();
    }
}
