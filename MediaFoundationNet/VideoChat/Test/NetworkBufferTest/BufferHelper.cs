using System;

namespace NetworkBufferTest
{
    public static class BufferHelper
    {
        public static bool isBytesSame(byte[] buffer1, byte[] buffer2)
        {
            if (buffer1.Length != buffer2.Length)
            {
                return false;
            }

            for (int i = 0; i < buffer1.Length; i++)
            {
                if (buffer1[i] != buffer2[i])
                {
                    return false;
                }
            }

            return true;
        }

        public static byte[] Combine(byte[] buffer1, byte[] buffer2)
        {
            var result = new byte[buffer1.Length + buffer2.Length];
            Array.Copy(buffer1, result, buffer1.Length);
            Array.Copy(buffer2, 0, result, buffer1.Length, buffer2.Length);

            return result;
        }
    }
}
