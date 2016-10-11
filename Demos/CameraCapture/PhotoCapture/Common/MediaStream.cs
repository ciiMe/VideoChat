using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Networking;
using Windows.Networking.Sockets;
using Windows.Storage.Streams;

namespace SDKTemplate.Common
{
    public enum BufferActions
    {
        Seek,
        Read,
        Write,
        Flush
    }

    public class BufferWriteEventArgs : EventArgs
    {
        private BufferActions _action;
        private bool _isAllowed;
        private ulong _position;
        private ulong _ByteLength;

        public BufferActions Action => _action;

        public bool IsAllowed
        {
            get
            {
                return _isAllowed;
            }

            set
            {
                _isAllowed = value;
            }
        }

        public ulong ByteLength => _ByteLength;
        public ulong Position => _position;

        public BufferWriteEventArgs(BufferActions action, ulong position, ulong byteLength)
        {
            _action = action;
            _position = position;
            _ByteLength = byteLength;
        }
    }

    public delegate void BufferActionEventHandler(object sender, ref BufferWriteEventArgs args);

    internal class MediaStream : IRandomAccessStream
    {
        private int MaxBufferSendLen = 32 * 1024;

        private string _hostName;
        private int _port;

        private ulong _position;
        private ulong _streamSize;
        private ulong _totalSendLength;

        private StreamSocket _socket;
        private object _outStreamLock;
        private Stream _outStream;
        private bool _isSending;
        private bool _isClosingConnection;

        public DateTime StartSendingTime;

        public event EventHandler DataSendingFailed;
        public event BufferActionEventHandler OnDataWriting;

        public MediaStream(string host, int port)
        {
            _hostName = host;
            _port = port;

            _outStreamLock = new object();
            _isSending = false;
            _isClosingConnection = false;
        }

        public async Task<bool> Connect()
        {
            if (_isClosingConnection)
            {
                return false;
            }

            Disconnect();
            try
            {
                _socket = new StreamSocket();
                await _socket.ConnectAsync(new HostName(_hostName), _port.ToString());
                _outStream = _socket.OutputStream.AsStreamForWrite();
                return true;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public void Disconnect()
        {
            if (_isClosingConnection)
            {
                return;
            }

            if (_isSending)
            {
                _isClosingConnection = true;
                return;
            }

            doDisconnect();
        }

        private void doDisconnect()
        {
            if (_outStream != null)
            {
                _outStream.Dispose();
                _outStream = null;
            }
            if (_socket != null)
            {
                _socket.Dispose();
                _socket = null;
            }
            _isClosingConnection = false;
        }

        public bool CanRead { get { return false; } }
        public bool CanWrite { get { return true; } }

        public IRandomAccessStream CloneStream()
        { throw new NotSupportedException(); }

        public IInputStream GetInputStreamAt(ulong position)
        { throw new NotSupportedException(); }

        public IOutputStream GetOutputStreamAt(ulong position)
        { throw new NotSupportedException(); }

        public ulong Position { get { return _position; } }

        public void Seek(ulong position)
        {
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
            Disconnect();
        }

        public IAsyncOperationWithProgress<IBuffer, uint> ReadAsync(IBuffer buffer, uint count, InputStreamOptions options)
        {
            throw new NotSupportedException();
        }

        public IAsyncOperation<bool> FlushAsync()
        {
            return null;
        }

        public IAsyncOperationWithProgress<uint, uint> WriteAsync(IBuffer buffer)
        {
            _totalSendLength += buffer.Length;

            if (_socket == null)
            {
                throw new Exception("Please call OpenConnection() before WriteAsync().");
            }

            lock (_outStreamLock)
            {
                if (_isClosingConnection)
                {
                    return null;
                }
                _isSending = true;
                Task<uint> saveTask = createSaveBufferTask(buffer);
                saveTask.RunSynchronously();
                var result = AsyncInfo.Run<uint, uint>((token, progress) => saveTask);
                _isSending = false;

                if (_isClosingConnection)
                {
                    doDisconnect();
                }

                return result;
            }
        }

        private Task<uint> createSaveBufferTask(IBuffer buffer)
        {
            if (OnDataWriting != null)
            {
                var arg = new BufferWriteEventArgs(BufferActions.Write, _position, buffer.Length);
                OnDataWriting(this, ref arg);

                if (!arg.IsAllowed)
                {
                    return new Task<uint>(() => { return buffer.Length; });
                }
            }

            return new Task<uint>(() =>
                {
                    uint len = buffer.Length;
                    var data = new byte[len];
                    buffer.CopyTo(data);

                    try
                    {
                        doSend(data);
                        if (_position + len > _streamSize)
                        {
                            _streamSize = _position + len;
                        }
                        return len;
                    }
                    catch
                    {
                        DataSendingFailed?.Invoke(this, new EventArgs());
                        return 0;
                    }
                });
        }

        private void doSend(byte[] data)
        {
            if (data.Length <= MaxBufferSendLen)
            {
                _outStream.Write(data, 0, data.Length);
                _outStream.Flush();
                return;
            }

            int p = 0, len = data.Length, sendLen = MaxBufferSendLen;

            while (!_isClosingConnection && p < len)
            {
                if (p + MaxBufferSendLen <= len)
                {
                    sendLen = MaxBufferSendLen;
                }
                else
                {
                    sendLen = len - p;
                }

                _outStream.Write(data, p, sendLen);
                _outStream.Flush();
                p += sendLen;
            }
        }
    }


}