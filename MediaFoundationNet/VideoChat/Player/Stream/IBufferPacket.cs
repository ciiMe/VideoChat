using MediaFoundation;

namespace VideoPlayer.Stream
{
    public delegate void MediaBufferEventHandler(IBufferPacket packet);
    public delegate HResult BufferEventHandler(byte[] buffer);
    /// <summary>
    /// A packet to cache received buffer list.
    /// </summary>
    public interface IBufferPacket
    {
        /// <summary>
        /// Visit each item in buffer list, and then pass them as the parameter of handler.
        /// </summary>
        HResult Each(BufferEventHandler handler);

        /// <summary>
        /// Add buffer data to the end if list.
        /// </summary> 
        void AddBuffer(byte[] data);

        /// <summary>
        /// Get the total length of received buffers.
        /// </summary>
        int GetBufferLength();

        /// <summary>
        /// Get the Option data length of the first option request.
        /// </summary>
        int GetFirstOptionDataLength();

        /// <summary>
        /// Get the Option type of the first option request.
        /// </summary>
        StspOperation GetFirstOperationDataType();

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

        /// <summary>
        /// Get the head buffer in the list.
        /// </summary>
        byte[] GetBuffer(int len);

        /// <summary>
        /// Take the head buffer out of the list.
        /// </summary>
        byte[] TakeBuffer(int len);
    }
}
