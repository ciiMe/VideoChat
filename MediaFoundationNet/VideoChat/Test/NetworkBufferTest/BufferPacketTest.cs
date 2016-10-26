using Microsoft.VisualStudio.TestTools.UnitTesting;
using VideoPlayer;

namespace NetworkBufferTest
{
    /// <summary>
    /// BufferPacketTest 的摘要说明
    /// </summary>
    [TestClass]
    public class BufferPacketTest
    {
        private IBufferPacket packet;

        private byte[] data1 = new byte[] { 0x01, 0x02, 0x03, 0x04, 0x05, 0x06 };
        private byte[] data2 = new byte[] { 0x07, 0x08 };
        private byte[] data3 = new byte[] { 0x09, 0x0A };
        private byte[] data4 = new byte[] { 0x0B, 0x0C, 0x0D, 0x0E, 0x0F, 0x10, 0x11, 0x12, 0x13, 0x14 };

        public BufferPacketTest()
        {
            packet = new BufferPacket();
        }

        private void clearPacket()
        {
            packet = new BufferPacket();
        }

        [TestMethod]
        public void TestAdd()
        {
            clearPacket();
            packet.AddBuffer(data1);

            Assert.IsTrue(true);
        }

        [TestMethod]
        public void TestGetLength()
        {
            clearPacket();

            Assert.AreEqual(0, packet.GetLength());

            packet.AddBuffer(data1);
            Assert.AreEqual(data1.Length, packet.GetLength());

            packet.AddBuffer(data2);
            Assert.AreEqual(data1.Length + data2.Length, packet.GetLength());
        }

        [TestMethod]
        public void TestGetBuffer0()
        {
            clearPacket();

            var buffer = packet.GetBuffer(10);

            Assert.IsTrue((buffer == null) || (buffer.Length == 0));
        }

        [TestMethod]
        public void TestGetBuffer()
        {
            clearPacket();

            packet.AddBuffer(data1);
            Assert.IsTrue(BufferHelper.isBytesSame(data1, packet.GetBuffer(data1.Length)));

            packet.AddBuffer(data2);
            Assert.IsTrue(BufferHelper.isBytesSame(data1, packet.GetBuffer(data1.Length)));

            var allBuffers = BufferHelper.Combine(data1, data2);
            Assert.IsTrue(BufferHelper.isBytesSame(allBuffers, packet.GetBuffer(data1.Length + data2.Length)));
        }

        [TestMethod]
        public void TestTakeBuffer()
        {
            clearPacket();

            packet.AddBuffer(data1);
            packet.AddBuffer(data2);
            packet.AddBuffer(data3);
            packet.AddBuffer(data4);

            Assert.AreEqual(data1.Length + data2.Length + data3.Length + data4.Length, packet.GetLength());

            Assert.IsTrue(BufferHelper.isBytesSame(data1, packet.TakeBuffer(data1.Length)));
            Assert.AreEqual(data2.Length + data3.Length + data4.Length, packet.GetLength());

            Assert.IsTrue(BufferHelper.isBytesSame(data2, packet.TakeBuffer(data2.Length)));
            Assert.AreEqual(data3.Length + data4.Length, packet.GetLength());

            Assert.IsTrue(BufferHelper.isBytesSame(data3, packet.TakeBuffer(data3.Length)));
            Assert.AreEqual(data4.Length, packet.GetLength());

            Assert.IsTrue(BufferHelper.isBytesSame(data4, packet.TakeBuffer(data4.Length)));
            Assert.AreEqual(0, packet.GetLength());
        }

        [TestMethod]
        public void TestTakeBuffer2()
        {
            clearPacket();
            packet.AddBuffer(data1);
            packet.AddBuffer(data2);

            var totalLen = data1.Length + data2.Length;
            byte[] buffer;
            foreach (var b in data1)
            {
                buffer = packet.TakeBuffer(1);
                Assert.AreNotEqual(null, buffer);
                Assert.AreEqual(buffer.Length, 1);
                Assert.AreEqual(buffer[0], b);

                Assert.AreEqual(--totalLen, packet.GetLength());
            }
            Assert.AreEqual(totalLen, data2.Length);
            Assert.AreEqual(data2.Length, packet.GetLength());

            Assert.AreEqual(data2, packet.TakeBuffer(data2.Length));
            Assert.AreEqual(0, packet.GetLength());
        }
    }
}
