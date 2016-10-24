using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPlayer;
using VideoPlayer.Stream;

namespace NetworkBufferTest
{
    [TestClass]
    public class StreamConvertorTest
    {
        private byte[] Bytes_ClientRequestDescription = new byte[] { 0, 0, 0, 0, 1, 0, 0, 0 };

        [TestMethod]
        public void TestBytesConvert_StspOperationHeader()
        {
            //result should be:[0,0,0,0,1,0,0,0]
            var result = StreamConvertor.BuildOperationBytes(StspOperation.StspOperation_ClientRequestDescription);
            Assert.IsTrue(BufferHelper.isBytesSame(result, Bytes_ClientRequestDescription));
        }
    }
}
