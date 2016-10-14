using System;

namespace VideoPlayer.Stream
{
    /// <summary>
    /// The original buffer list received from network.
    /// </summary>
    public class BufferPacket
    {
        private byte[] _buffer;
        private int _currentPosition;

        public int Length
        {
            get
            {
                return _buffer.Length - _currentPosition;
            }
        }

        public BufferPacket(byte[] buffer)
        {
            _buffer = buffer;
            _currentPosition = 0;
        }

        public byte[] MoveLeft(int len)
        {
            if (_currentPosition + len > _buffer.Length)
            {
                return new byte[] { };
            }

            var result = new byte[len];
            Array.Copy(_buffer, _currentPosition, result, 0, len);
            _currentPosition += len;
            return result;
        }

        //todo: the copy cost too much time, should be enhanced.
        public byte[] Get()
        {
            if (Length <= 0)
            {
                return new byte[] { };
            }
            var result = new byte[Length];
            Array.Copy(_buffer, _currentPosition, result, 0, Length);
            return result;
        }
    }
}
