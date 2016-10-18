using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;

namespace VideoPlayer.Stream
{
    public static class StreamConvertor
    {
        public static byte[] BuildOperationBytes(StspOperation operation)
        {
            var opHeader = new StspOperationHeader { cbDataSize = 0, eOperation = operation };

            var bytes = new byte[Marshal.SizeOf(opHeader)];

            var f1 = BitConverter.GetBytes(opHeader.cbDataSize);
            var f2 = BitConverter.GetBytes((int)opHeader.eOperation);

            Array.Copy(f1, bytes, f1.Length);
            Array.Copy(f2, 0, bytes, f1.Length, f2.Length);

            return bytes;
        }

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
        public static T TakeObject<T>(IBufferPacket packet)
        {
            //todo: convert return type as HResult.
            var size = Marshal.SizeOf(typeof(T));
            var buffer = packet.TakeBuffer(size);
            return BytesToStruct<T>(buffer, size);
        }

        /// <summary>
        /// Create the struct entity from buffer.
        /// </summary>
        public static T ByteToStructure<T>(byte[] buffer)
        {
            //todo: convert return type as HResult.
            int size = Marshal.SizeOf(typeof(T));
            return BytesToStruct<T>(buffer, size);
        }

        /// <summary>
        /// For internal useage only, convert byte array to struct entity, the entity size must be calculated before call this method.
        /// </summary> 
        private static T BytesToStruct<T>(byte[] buffer, int size)
        {
            //todo: convert return type as HResult.
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
            //todo: convert return type as HResult.
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

        public static int ReadInt32(IBufferPacket packet)
        {
            var dataSize = 4;
            var data = packet.GetBuffer(dataSize);

            if (data == null || data.Length < dataSize)
            {
                return -1;
            }

            IntPtr p = Marshal.AllocHGlobal(dataSize);
            Marshal.Copy(data, 0, p, dataSize);
            var result = Marshal.ReadInt32(p);
            Marshal.FreeHGlobal(p);
            return result;
        }

        public static StspOperation ReadOption(IBufferPacket packet)
        {
            var dataSize = 8;
            var data = packet.GetBuffer(dataSize);
            if (data == null || data.Length < dataSize)
            {
                return StspOperation.StspOperation_Unknown;
            }

            IntPtr p = Marshal.AllocHGlobal(dataSize);
            Marshal.Copy(data, 0, p, dataSize);
            var result = Marshal.ReadInt32(p, 4);
            Marshal.FreeHGlobal(p);

            if (result < (int)StspOperation.StspOperation_Unknown || result > (int)StspOperation.StspOperation_Last)
            {
                return StspOperation.StspOperation_Unknown;
            }

            return (StspOperation)result;
        }

        public static HResult ConverToMediaBuffer(byte[] buffer, out IMFMediaBuffer mediaBuffer)
        {
            mediaBuffer = null;
            if (buffer == null || buffer.Length == 0)
            {
                return HResult.E_INVALIDARG;
            }

            IMFMediaBuffer spMediaBuffer;
            HResult hr = MFExtern.MFCreateMemoryBuffer(buffer.Length, out spMediaBuffer);
            if (MFError.Failed(hr))
            {
                return hr;
            }

            IntPtr pBuffer;
            int cbMaxLength;
            int cbCurrentLength;
            //todo: call lock2d on a 2d buffer because the lock2d is more efficient.
            /*
            if (MFError.Succeeded(Marshal.intp spMediaBuffer.QueryInterface(IID_PPV_ARGS(&_sp2DBuffer))))
            {
                LONG lPitch;
                hr = _sp2DBuffer.Lock2D(&_pBuffer, &lPitch);
            }
            else
            {
                hr = pMediaBuffer->Lock(&_pBuffer, &cbMaxLength, &cbCurrentLength);
            }*/
            hr = spMediaBuffer.Lock(out pBuffer, out cbMaxLength, out cbCurrentLength);
            if (MFError.Failed(hr))
            {
                return hr;
            }
            Marshal.Copy(buffer, 0, pBuffer, buffer.Length);
            spMediaBuffer.SetCurrentLength(buffer.Length);
            spMediaBuffer.Unlock();
            mediaBuffer = spMediaBuffer;

            return hr;
        }
    }
}
