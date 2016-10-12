using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using VideoPlayer.Network;

namespace VideoPlayer.Stream
{
    public class NetworkStreamAdapter : INetworkStreamAdapter
    {
        private SourceState _eSourceState;

        private INetworkClient _networkSender;

        public event OnDataArrivedHandler OnDataArrived;

        public NetworkStreamAdapter()
        {
            _networkSender = new NetworkClient();
        }

        public HResult Open(string ip, int port)
        {
            if (_eSourceState != SourceState.SourceState_Invalid)
            {
                Throw(HResult.MF_E_INVALIDREQUEST);
            }

            if (string.IsNullOrEmpty(ip) || port <= 0)
            {
                Throw(HResult.E_INVALIDARG);
            }

            _eSourceState = SourceState.SourceState_Opening;
            return Connect(ip, port);
        }

        private HResult Connect(string ip, int port)
        {
            try
            {
                ThrowIfError(CheckShutdown());
                if (_eSourceState != SourceState.SourceState_Opening)
                {
                    Throw(HResult.MF_E_UNEXPECTED);
                }

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

        public HResult CheckShutdown()
        {
            return HResult.S_OK;
        }

        public void Receive()
        {
            /*todo: the receiving check...
            if (_spCurrentReceiveBuffer)
            {
                Throw(HResult.MF_E_INVALIDREQUEST);
            }
            */
        }

        private void invokeDataArrived(StspOperation option, byte[] data)
        {
            Debug.WriteLine($"Data Received:{option},{data.Length}");
            //ProcessPacket(option, data);
        }

        private void ProcessPacket(StspOperation option, byte[] data)
        {
            switch (option)
            {
                // We received server description
                case StspOperation.StspOperation_ServerDescription:
                    ProcessServerDescription(data);
                    break;
                // We received a media sample
                case StspOperation.StspOperation_ServerSample:
                    ProcessServerSample(data);
                    break;
                case StspOperation.StspOperation_ServerFormatChange:
                    ProcessServerFormatChange(data);
                    break;
                // No supported operation
                default:
                    Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                    break;
            }
        }

        private void ProcessServerDescription(byte[] data)
        {
            int cbTotalLen = 0;
            int cbConstantSize = 0;
            StspDescription desc = new VideoPlayer.StspDescription();
            StspDescription pDescription;

            if (_eSourceState != SourceState.SourceState_Opening)
            {
                // Server description should only be sent during opening state
                Throw(HResult.MF_E_UNEXPECTED);
            }

            cbTotalLen = data.Length;

            // Minimum size of the operation payload is size of Description structure
            if (cbTotalLen < Marshal.SizeOf(desc))
            {
                ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            // Copy description.
            //ThrowIfError(pPacket->MoveLeft(sizeof(desc), &desc));
        }

        private void ProcessServerSample(byte[] data)
        {
            throw new NotImplementedException();
        }

        private void ProcessServerFormatChange(byte[] data)
        {
            throw new NotImplementedException();
        }

        public void SendRequest(StspOperation operation)
        {
            var bytes = BufferWrapper.BuildOperationBytes(operation);
            _networkSender.Send(bytes);
        }

        public void SendDescribeRequest()
        {
            SendRequest(StspOperation.StspOperation_ClientRequestDescription);
        }

        private void HandleError(int hr)
        {
            HandleError((HResult)hr);
        }

        // Handle errors
        private void HandleError(HResult hResult)
        {
            if (_eSourceState == SourceState.SourceState_Opening)
            {
                // If we have an error during opening operation complete it and pass the error to client.
                //CompleteOpen(hResult);
            }
            else if (_eSourceState != SourceState.SourceState_Shutdown)
            {
                // If we received an error at any other time (except shutdown) send MEError event.
                //QueueEvent(MEError, GUID_NULL, hResult, nullptr);
            }
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
