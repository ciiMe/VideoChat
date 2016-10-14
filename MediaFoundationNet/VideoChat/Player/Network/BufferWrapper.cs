using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;
using VideoPlayer.Stream;

namespace VideoPlayer.Network
{
    public static class BufferWrapper
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

        public static HResult ConverToMediaBuffer(BufferPacket packet, out IMFMediaBuffer mediaBuffer)
        {
            mediaBuffer = null;

            IMFMediaBuffer spMediaBuffer;
            HResult hr = MFExtern.MFCreateMemoryBuffer(packet.Length, out spMediaBuffer);

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
            var data = packet.Get();
            Marshal.Copy(data, 0, pBuffer, packet.Length);
            spMediaBuffer.Unlock();

            return hr;
        }
    }
}
