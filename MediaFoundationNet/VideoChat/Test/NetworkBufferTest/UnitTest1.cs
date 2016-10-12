using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPlayer.Network;
using VideoPlayer;

namespace NetworkBufferTest
{
    [TestClass]
    public class UnitTest1
    {
        private byte[] Bytes_ClientRequestDescription = new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 };

        [TestMethod]
        public void TestBytesConvert_StspOperationHeader()
        {
            //result should be:[0,0,0,0,1,0,0,0]
            var result = BufferWrapper.BuildOperationBytes(StspOperation.StspOperation_ClientRequestDescription);
            Assert.IsTrue(isBytesSame(result, Bytes_ClientRequestDescription));
        }

        private bool isBytesSame(byte[] b1, byte[] b2)
        {
            if (b1.Length != b2.Length)
            {
                return false;
            }

            for (int i = 0; i < b1.Length; i++)
            {
                if (b1[i] != b2[i])
                {
                    return false;
                }
            }

            return true;
        }

        [TestMethod]
        public void TestTcpConnection()
        {
            NetworkSource source = new NetworkSource();
            source.Open("192.168.13.210", 10010); 
        }
    }
}
