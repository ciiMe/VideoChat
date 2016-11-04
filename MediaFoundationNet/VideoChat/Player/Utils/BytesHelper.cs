using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;

namespace VideoPlayer.Utils
{
    public static class BytesHelper
    {
        public static byte[] BuildOperationBytes(VideoStream_Operation operation)
        {
            var opHeader = new VideoStream_OperationHeader { cbDataSize = 0, eOperation = operation };

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

        public static VideoStream_Operation ReadOperation(IBufferPacket packet)
        {
            var dataSize = 8;
            var data = packet.GetBuffer(dataSize);
            if (data == null || data.Length < dataSize)
            {
                return VideoStream_Operation.StspOperation_Unknown;
            }

            IntPtr p = Marshal.AllocHGlobal(dataSize);
            Marshal.Copy(data, 0, p, dataSize);
            var result = Marshal.ReadInt32(p, 4);
            Marshal.FreeHGlobal(p);

            if (result < (int)VideoStream_Operation.StspOperation_Unknown || result > (int)VideoStream_Operation.StspOperation_Last)
            {
                return VideoStream_Operation.StspOperation_Unknown;
            }

            return (VideoStream_Operation)result;
        }

        public static HResult ConverToMediaBuffer(IBufferPacket packet, out IMFMediaBuffer mediaBuffer)
        {
            mediaBuffer = null;
            var dataLength = packet.GetLength();
            if (packet == null || dataLength == 0)
            {
                return HResult.E_INVALIDARG;
            }

            IMFMediaBuffer spMediaBuffer;
            HResult hr = MFExtern.MFCreateMemoryBuffer(dataLength, out spMediaBuffer);
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
            var buffer = packet.TakeBuffer(dataLength);
            Marshal.Copy(buffer, 0, pBuffer, buffer.Length);
            spMediaBuffer.SetCurrentLength(buffer.Length);
            spMediaBuffer.Unlock();
            mediaBuffer = spMediaBuffer;
            buffer = null;
            return hr;
        }

        public static HResult ConvertToSample(byte[] sampleBuffer, out IMFSample outSample)
        {
            HResult hr = HResult.S_OK;
            outSample = null;

            IMFMediaBuffer buffer;
            IntPtr pBuffer;
            int len = sampleBuffer.Length;
            int maxLen;

            if (MFError.Failed(hr = MFExtern.MFCreateMemoryBuffer(len, out buffer)))
            {
                return hr;
            }

            if (MFError.Failed(hr = buffer.SetCurrentLength(len)))
            {
                return hr;
            }

            if (MFError.Failed(hr = buffer.Lock(out pBuffer, out maxLen, out len)))
            {
                return hr;
            }
            Marshal.Copy(sampleBuffer, 0, pBuffer, len);
            if (MFError.Failed(hr = buffer.Unlock()))
            {
                return hr;
            }

            IMFSample sample;
            if (MFError.Failed(hr = MFExtern.MFCreateSample(out sample)))
            {
                return hr;
            }
            if (MFError.Failed(hr = sample.AddBuffer(buffer)))
            {
                return hr;
            }
            outSample = sample;
            return hr;
        }

        public static HResult ConvertToByteArray(IMFMediaBuffer buffer, out byte[] outBuffer)
        {
            HResult hr = HResult.S_OK;
            outBuffer = null;

            int len, maxLen;
            IntPtr pBuffer;

            if (MFError.Failed(hr = buffer.Lock(out pBuffer, out maxLen, out len)))
            {
                return hr;
            }

            outBuffer = new byte[len];
            Marshal.Copy(pBuffer, outBuffer, 0, len);
            hr = buffer.Unlock();

            return hr;
        }
    }
}
