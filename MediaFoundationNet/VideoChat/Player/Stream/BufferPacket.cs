using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;

namespace VideoPlayer.Stream
{

    public class BufferPacket : IBufferPacket
    {
        public const int PacketHeadSize = 8;

        private List<byte[]> _buffers;
        private int _bufferTotalLength;

        public BufferPacket()
        {
            _buffers = new List<byte[]>();
            _bufferTotalLength = 0;
        }

        public HResult Each(BufferEventHandler handler)
        {
            if (null == handler)
            {
                return HResult.E_INVALIDARG;
            }
            HResult hr = HResult.S_OK;

            for (int i = 0; i < _buffers.Count; i++)
            {
                try
                {
                    hr = handler(_buffers[i]);
                    if (MFError.Failed(hr))
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    hr = (HResult)ex.HResult;
                }
            }

            return hr;
        }

        public void AddBuffer(byte[] data)
        {
            if (data == null || data.Length == 0)
            {
                return;
            }

            _buffers.Add(data);
            _bufferTotalLength += data.Length;
        }

        public int GetBufferLength()
        {
            return _bufferTotalLength;
        }

        public int GetFirstOptionDataLength()
        {
            return StreamConvertor.ReadInt32(this);
        }

        public StspOperation GetFirstOperationDataType()
        {
            return StreamConvertor.ReadOption(this);
        }

        public bool HasOptionData()
        {
            return _bufferTotalLength >= PacketHeadSize + GetFirstOptionDataLength();
        }

        public bool IsSingleOption()
        {
            return PacketHeadSize + GetFirstOptionDataLength() == _bufferTotalLength;
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

                if (buffer.Length <= extraLen)
                {
                    packet.AddBuffer(buffer);
                    extraLen -= buffer.Length;
                    _buffers.RemoveAt(0);
                }
                else
                {
                    var take = new byte[extraLen];
                    var tmp = new byte[buffer.Length - extraLen];
                    Array.Copy(buffer, take, extraLen);
                    Array.Copy(buffer, extraLen, tmp, 0, tmp.Length);
                    _buffers[0] = tmp;
                    packet.AddBuffer(take);
                    extraLen -= extraLen;
                }
            }

            return packet;
        }

        public byte[] GetBuffer(int len)
        {
            return readBuffer(len, false);
        }

        public byte[] TakeBuffer(int len)
        {
            return readBuffer(len, true);
        }

        private byte[] readBuffer(int len, bool isRemove)
        {
            if (_bufferTotalLength < len)
            {
                return new byte[] { };
            }

            var result = new byte[len];
            var pos = 0;
            var extraLen = len;
            for (int i = 0; i < _buffers.Count; i++)
            {
                var buffer = _buffers[i];
                if (buffer.Length >= extraLen)
                {
                    Array.Copy(buffer, result, extraLen);
                    if (isRemove)
                    {
                        var newBuffer = new byte[buffer.Length - extraLen];
                        Array.Copy(buffer, extraLen, newBuffer, 0, newBuffer.Length);
                        _buffers[0] = newBuffer;
                        _bufferTotalLength -= extraLen;
                    }
                    break;
                }
                else
                {
                    Array.Copy(buffer, result, buffer.Length);
                    pos += buffer.Length;
                    extraLen -= buffer.Length;
                    if (isRemove)
                    {
                        _buffers.RemoveAt(0);
                        _bufferTotalLength -= buffer.Length;
                        i--;
                    }
                }
            }
            return result;
        }
    }
}
