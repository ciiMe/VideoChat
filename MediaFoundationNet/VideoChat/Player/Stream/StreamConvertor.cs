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

        public static T ByteToStructure<T>(byte[] buffer)
        {
            int size = Marshal.SizeOf(typeof(T));
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
