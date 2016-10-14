using System;
using System.Runtime.InteropServices;

namespace VideoPlayer.Stream
{
    public static class StreamConvertor
    {
        public static byte[] StructureToByte<T>(T structure)
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] buffer = new byte[size];
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.StructureToPtr(structure, p, true);
                Marshal.Copy(p, buffer, 0, size);
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
            return buffer;
        }

        /// <summary>
        /// Take the object buffer from the packet and convert object buffer to struct entity.
        /// </summary>
        /// <param name="packet">The buffer where struct bytes are sasved.</param>
        public static T TakeObject<T>(BufferPacket packet)
        {
            var size = Marshal.SizeOf(typeof(T));
            var buffer = packet.MoveLeft(size);
            return BytesToStruct<T>(buffer, size);
        }

        /// <summary>
        /// Create the struct entity from buffer.
        /// </summary>
        public static T ByteToStructure<T>(byte[] buffer)
        {
            int size = Marshal.SizeOf(typeof(T));
            return BytesToStruct<T>(buffer, size);
        }

        /// <summary>
        /// For internal useage only, convert byte array to struct entity, the entity size must be calculated before call this method.
        /// </summary> 
        private static T BytesToStruct<T>(byte[] buffer, int size)
        {
            IntPtr p = Marshal.AllocHGlobal(size);
            try
            {
                Marshal.Copy(buffer, 0, p, size);
                return (T)Marshal.PtrToStructure(p, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }

        public static T ByteToStructure<T>(byte[] buffer, int positioin)
        {
            int size = Marshal.SizeOf(typeof(T));
            IntPtr p = Marshal.AllocHGlobal(size);

            try
            {
                Marshal.Copy(buffer, positioin, p, size);
                return (T)Marshal.PtrToStructure(p, typeof(T));
            }
            finally
            {
                Marshal.FreeHGlobal(p);
            }
        }
    }
}
