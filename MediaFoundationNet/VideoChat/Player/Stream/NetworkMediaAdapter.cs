using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Net.Sockets;
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
            _networkSender.OnPacketReceived += invokeDataArrived;
        }

        public HResult Open(string ip, int port)
        {
            try
            {
                ThrowIfError(CheckShutdown());

                _networkSender.Connect(ip, port);
                _networkSender.Start();
                SendDescribeRequest();

                return HResult.S_OK;
            }
            catch (SocketException sex)
            {
                switch (sex.NativeErrorCode)
                {
                    case 10060:
                        return HResult.MF_E_NET_TIMEOUT;
                    case 10061:
                        return HResult.MF_E_NET_REDIRECT;
                    default:
                        return HResult.MF_E_NETWORK_RESOURCE_FAILURE;
                }
            }
            catch (Exception ex)
            {
                return HResult.MF_E_NETWORK_RESOURCE_FAILURE;
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

        private void invokeDataArrived(StspOperation option, IBufferPacket data)
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
            var bytes = StreamConvertor.BuildOperationBytes(operation);
            _networkSender.Send(bytes);
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
