using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPlayer;
using VideoPlayer.Stream;

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
            var result = StreamConvertor.BuildOperationBytes(StspOperation.StspOperation_ClientRequestDescription);
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
    }
}
