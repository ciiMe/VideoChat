using System;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace SDKTemplate.Common
{
    public delegate void OnSeekEventHandler(ulong value);
    public delegate void IoEventHandler(ulong position, uint len);

    internal class TestMediaStream : IRandomAccessStream
    {
        private ulong _position;
        private ulong _streamSize;

        private IRandomAccessStream _virtualStream;

        public bool CanRead { get { return true; } }
        public bool CanWrite { get { return true; } }

        public event BufferActionEventHandler OnSeekCalled;
        public event BufferActionEventHandler OnReadCalled;
        public event BufferActionEventHandler OnWriteCalled;
        public event BufferActionEventHandler OnFlushCalled;

        public TestMediaStream()
        {
        }

        /// <summary>
        /// Set a virtual stream to be used in Read(...), the read method will read data from the virtual sreadm instead of the real one.
        /// </summary>
        public void SetVirtualStream(IRandomAccessStream stream)
        {
            _virtualStream = stream;
        }

        public IRandomAccessStream CloneStream()
        {
            throw new NotSupportedException();
        }

        public IInputStream GetInputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public IOutputStream GetOutputStreamAt(ulong position)
        {
            throw new NotSupportedException();
        }

        public ulong Position { get { return _position; } }

        public void Seek(ulong position)
        {
            BufferWriteEventArgs arg = new BufferWriteEventArgs(BufferActions.Seek, position, 0);
            OnSeekCalled?.Invoke(this, ref arg);

            _position = position;

            if (_position >= _streamSize)
            {
                _streamSize = _position + 1;
            }
        }

        public ulong Size
        {
            get { return _streamSize; }
            set { throw new NotSupportedException(); }
        }

        public void Dispose()
        {
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            BufferWriteEventArgs arg = new BufferWriteEventArgs(BufferActions.Read, _position, count);
            OnReadCalled?.Invoke(this, ref arg);
            
            var t = new Task<IBuffer>(() =>
            {
                var d = _virtualStream.ReadAsync(buffer, count, options);
                while (d.Status != AsyncStatus.Completed) ;

                return buffer;
            });

            t.RunSynchronously();

            Func<CancellationToken, IProgress<uint>, Task<IBuffer>> tp = (token, progress) => t;
            return AsyncInfo.Run(tp);
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            BufferWriteEventArgs arg = new BufferWriteEventArgs(BufferActions.Flush, _position, 0);
            OnFlushCalled?.Invoke(this, ref arg);

            var t = new Task<bool>(() =>
            {
                return true;
            });

            t.RunSynchronously();

            Func<CancellationToken, Task<bool>> tp = (token) => t;
            return AsyncInfo.Run(tp);
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            BufferWriteEventArgs arg = new BufferWriteEventArgs(BufferActions.Write, _position, buffer.Length);
            OnWriteCalled?.Invoke(this, ref arg);

            Task<uint> aTask = new Task<uint>(() =>
            {
                uint len = buffer.Length;
                // Calculate new size of the stream.
                if (_position + len > _streamSize)
                {
                    _streamSize = _position + len;
                }

                return len;
            });

            aTask.RunSynchronously();

            Func<CancellationToken, IProgress<uint>, Task<uint>> aTaskProvider = (token, progress) => aTask;
            return AsyncInfo.Run(aTaskProvider);
        }
    }
}
