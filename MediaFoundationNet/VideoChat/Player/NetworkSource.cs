using MediaFoundation;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MediaFoundation.Misc;
using VideoPlayer.Network;
using VideoPlayer.Stream;

namespace VideoPlayer
{
    public class NetworkSource : IMFMediaEventGenerator, IMFMediaSource
    {
        INetworkStreamAdapter _networkStreamAdapter;

        //ComPtr<IMFPresentationDescriptor> _spPresentationDescriptor;

        /*
        // Collection of streams associated with the source
        StreamContainer _streams;
        */

        public NetworkSource()
        {
            // Create the event queue helper.
            _networkStreamAdapter = new NetworkStreamAdapter();
            _networkStreamAdapter.OnDataArrived += _networkStreamAdapter_OnDataArrived;
        }

        public void Open(string ip, int port)
        {
            _networkStreamAdapter.Open(ip, port);
        }

        private void _networkStreamAdapter_OnDataArrived(StspOperation operation, byte[] data)
        {
            ProcessPacket(operation, data);
        }

        private void ProcessPacket(StspOperation operation, byte[] data)
        {
            switch (operation)
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
            throw new NotImplementedException();
        }

        private void ProcessServerSample(byte[] data)
        {
            throw new NotImplementedException();
        }

        private void ProcessServerFormatChange(byte[] data)
        {
            throw new NotImplementedException();
        }

        #region IMFMediaEventGenerator
        public HResult BeginGetEvent(IMFAsyncCallback pCallback, object o)
        {
            throw new NotImplementedException();
        }

        public HResult EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            throw new NotImplementedException();
        }

        public HResult GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            throw new NotImplementedException();
        }

        public HResult QueueEvent(MediaEventType met, Guid guidExtendedType, HResult hrStatus, ConstPropVariant pvValue)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region IMFMediaSource
        public HResult CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
        {
            throw new NotImplementedException();
        }

        public HResult GetCharacteristics(out MFMediaSourceCharacteristics pdwCharacteristics)
        {
            throw new NotImplementedException();
        }

        public HResult Pause()
        {
            throw new NotImplementedException();
        }

        public HResult Shutdown()
        {
            throw new NotImplementedException();
        }

        public HResult Start(IMFPresentationDescriptor pPresentationDescriptor, Guid pguidTimeFormat, ConstPropVariant pvarStartPosition)
        {
            throw new NotImplementedException();
        }

        public HResult Stop()
        {
            throw new NotImplementedException();
        }
        #endregion

        private void HandleError(int hr)
        {
            HandleError((HResult)hr);
        }

        // Handle errors
        private void HandleError(HResult hResult)
        {
            /*todo:
            if (_eSourceState == SourceState.SourceState_Opening)
            {
                // If we have an error during opening operation complete it and pass the error to client.
                CompleteOpen(hResult);
            }
            else if (_eSourceState != SourceState.SourceState_Shutdown)
            {
                // If we received an error at any other time (except shutdown) send MEError event.
                //QueueEvent(MEError, GUID_NULL, hResult, nullptr);
            }
            */
        }

        private void CompleteOpen(HResult hResult)
        {

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
