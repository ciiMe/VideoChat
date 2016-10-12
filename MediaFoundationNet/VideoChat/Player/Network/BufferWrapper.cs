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
    }
}
