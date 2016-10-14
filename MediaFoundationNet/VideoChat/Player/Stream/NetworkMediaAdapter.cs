using MediaFoundation;
using MediaFoundation.Misc;
using System;
using VideoPlayer.Network;

namespace VideoPlayer.Stream
{
    public class NetworkMediaAdapter : INetworkMediaAdapter
    {
        private INetworkClient _networkSender;

        public event MediaBufferEventHandler OnDataArrived;

        public NetworkMediaAdapter()
        {
            _networkSender = new NetworkClient();
        }

        public HResult Open(string ip, int port)
        {
            try
            {
                ThrowIfError(CheckShutdown());

                _networkSender.Connect(ip, port);
                SendDescribeRequest();
                _networkSender.StartReceive(invokeDataArrived);

                return HResult.S_OK;
            }
            catch (Exception ex)
            {
                HandleError(ex.HResult);
                return HResult.E_FAIL;
            }
        }

        public HResult Close()
        {
            HResult hr = HResult.S_OK;
            _networkSender.Close();
            return hr;
        }

        public HResult CheckShutdown()
        {
            return HResult.S_OK;
        }

        private void invokeDataArrived(StspOperation option, BufferPacket data)
        {
            OnDataArrived?.Invoke(option, data);
        }

        public void SendStartRequest()
        {
            SendRequest(StspOperation.StspOperation_ClientRequestStart);
        }

        public void SendDescribeRequest()
        {
            SendRequest(StspOperation.StspOperation_ClientRequestDescription);
        }

        public void SendRequest(StspOperation operation)
        {
            var bytes = BufferWrapper.BuildOperationBytes(operation);
            _networkSender.Send(bytes);
        }

        private void HandleError(int hr)
        {
            Throw((HResult)hr);
        }

        private void ThrowIfError(HResult hr)
        {
            if (!MFError.Succeeded(hr))
            {
                MFError.ThrowExceptionForHR(hr);
            }
        }

        private void Throw(HResult hr)
        {
            MFError.ThrowExceptionForHR(hr);
        }
    }
}
