using System;
using System.Collections.Generic;
using VideoPlayer.Utils;

namespace VideoPlayer
{
    public class BufferPacket : IBufferPacket
    {
        public const int PacketHeadSize = 8;

        protected List<byte[]> _buffers;

        public BufferPacket()
        {
            _buffers = new List<byte[]>();
        }

        public void AddBuffer(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            _buffers.Add(data);
        }

        public int GetLength()
        {
            var len = 0;
            for (int i = 0; i < _buffers.Count; i++)
            {
                len += _buffers[i].Length;
            }

            return len;
        }

        public byte[] GetBuffer(int len)
        {
            return readBuffer(len, false);
        }

        public byte[] TakeBuffer(int len)
        {
            return readBuffer(len, true);
        }

        protected byte[] readBuffer(int len, bool isRemove)
        {
            if (GetLength() < len)
            {
                return new byte[] { };
            }

            var result = new byte[len];
            var pos = 0;
            var extraLen = len;
            for (int i = 0; i < _buffers.Count; i++)
            {
                var buffer = _buffers[i];
                if (buffer == null || buffer.Length == 0)
                {
                    _buffers.RemoveAt(i);
                    i--;
                }

                if (buffer.Length == extraLen)
                {
                    if (pos == 0)
                    {
                        result = buffer;
                    }
                    else
                    {
                        Array.Copy(buffer, 0, result, pos, extraLen);
                        pos += extraLen;
                    }
                    if (isRemove)
                    {
                        _buffers.RemoveAt(0);
                    }
                    break;
                }
                else if (buffer.Length > extraLen)
                {
                    Array.Copy(buffer, 0, result, pos, extraLen);
                    pos += extraLen;
                    if (isRemove)
                    {
                        var leftBuffer = new byte[buffer.Length - extraLen];
                        Array.Copy(buffer, extraLen, leftBuffer, 0, leftBuffer.Length);
                        _buffers[0] = leftBuffer;
                    }
                    break;
                }
                else
                {
                    Array.Copy(buffer, 0, result, pos, buffer.Length);
                    pos += buffer.Length;
                    extraLen -= buffer.Length;
                    if (isRemove)
                    {
                        _buffers.RemoveAt(0);
                        i--;
                    }
                }
            }
            return result;
        }
    }

    public class NetworkBufferPacket : BufferPacket, INetworkBufferPacket
    {
        public int GetFirstOptionDataLength()
        {
            return BytesHelper.ReadInt32(this);
        }

        public VideoStream_Operation GetFirstOperationDataType()
        {
            return BytesHelper.ReadOperation(this);
        }

        public bool HasOptionData()
        {
            return GetLength() >= PacketHeadSize + GetFirstOptionDataLength();
        }

        public bool IsSingleOption()
        {
            return PacketHeadSize + GetFirstOptionDataLength() == GetLength();
        }

        public IBufferPacket TakeFirstOption()
        {
            var packet = new BufferPacket();
            if (!HasOptionData())
            {
                return packet;
            }

            var extraLen = PacketHeadSize + GetFirstOptionDataLength();
            while (extraLen > 0)
            {
                var buffer = _buffers[0];
                if (buffer == null || buffer.Length == 0)
                {
                    _buffers.RemoveAt(0);
                    continue;
                }

                if (buffer.Length <= extraLen)
                {
                    packet.AddBuffer(buffer);
                    extraLen -= buffer.Length;

                    _buffers.RemoveAt(0);
                }
                else
                {
                    var take = new byte[extraLen];
                    var left = new byte[buffer.Length - extraLen];
                    Array.Copy(buffer, take, extraLen);
                    Array.Copy(buffer, extraLen, left, 0, left.Length);
                    packet.AddBuffer(take);
                    extraLen -= extraLen;

                    _buffers[0] = left;
                }
            }

            return packet;
        }
    }
}
