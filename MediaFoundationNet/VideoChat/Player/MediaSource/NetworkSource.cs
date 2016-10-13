using MediaFoundation;
using System;
using MediaFoundation.Misc;
using VideoPlayer.Stream;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;

namespace VideoPlayer.MediaSource
{
    public class NetworkSource : IMFMediaEventGenerator, IMFMediaSource
    {
        private SourceState _eSourceState;

        // Collection of streams associated with the source
        List<IMFMediaStream> _streams;
        INetworkMediaAdapter _networkStreamAdapter;

        IMFPresentationDescriptor _spPresentationDescriptor;

        public NetworkSource()
        {
            _streams = new List<IMFMediaStream>();

            // Create the event queue helper.
            _networkStreamAdapter = new NetworkMediaAdapter();
            _networkStreamAdapter.OnDataArrived += _networkStreamAdapter_OnDataArrived;
        }

        public void Open(string ip, int port)
        {
            _networkStreamAdapter.Open(ip, port);
        }

        private void _networkStreamAdapter_OnDataArrived(StspOperation operation, BufferPacket data)
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

        private void ProcessServerDescription(BufferPacket data)
        {
            Debug.WriteLine($"ProcessServerDescription(...) Received buffer:{data.Length}");

            int cbTotalLen = 0;
            int cbConstantSize = 0;
            StspDescription desc = new StspDescription();
            int descLen = 0;
            int streamDescLen = 0;

            cbTotalLen = data.Length;
            descLen = Marshal.SizeOf(desc);
            streamDescLen = Marshal.SizeOf(typeof(StspStreamDescription));

            // Copy description  
            desc = StreamConvertor.ByteToStructure<StspDescription>(data.MoveLeft(descLen));
            // Size of the packet should match size described in the packet (size of Description structure + size of attribute blob)
            cbConstantSize = Convert.ToInt32(descLen + (desc.cNumStreams - 1) * streamDescLen);
            // Check if the input parameters are valid. We only support 2 streams.
            if (cbConstantSize < Marshal.SizeOf(desc) || desc.cNumStreams == 0 || desc.cNumStreams > 2 || cbTotalLen < cbConstantSize)
            {
                ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            try
            {
                List<StspStreamDescription> allStreamDescs = new List<StspStreamDescription>(desc.aStreams);

                for (int i = 1; i < desc.cNumStreams; i++)
                {
                    var sd = StreamConvertor.ByteToStructure<StspStreamDescription>(data.MoveLeft(streamDescLen));
                    allStreamDescs.Add(sd);
                }

                int cbAttributeSize = 0;
                for (int i = 0; i < desc.cNumStreams; ++i)
                {
                    cbAttributeSize += allStreamDescs[i].cbAttributesSize;
                    /* todo: check out of range on cbAttributeSize
                    if (out of range)
                    {
                        Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                    }*/
                }

                // Validate the parameters. Limit the total size of attributes to 64kB.
                if ((cbTotalLen != (cbConstantSize + cbAttributeSize)) || (cbAttributeSize > 0x10000))
                {
                    Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                }

                // Create stream for every stream description sent by the server.
                foreach (var sd in allStreamDescs)
                {
                    CMediaStream spStream;
                    ThrowIfError(CMediaStream.CreateInstance(sd, data, this, out spStream));
                    _streams.Add(spStream);
                }

                InitPresentationDescription();
                // Everything succeeded we are in stopped state now
                _eSourceState = SourceState.SourceState_Stopped;
                CompleteOpen(HResult.S_OK);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void InitPresentationDescription()
        {
            IMFPresentationDescriptor spPresentationDescriptor;
            IMFStreamDescriptor[] aStreams = new IMFStreamDescriptor[_streams.Count];

            for (int i = 0; i < _streams.Count; i++)
            {
                ThrowIfError(_streams[i].GetStreamDescriptor(out aStreams[i]));
            }

            ThrowIfError(MFExtern.MFCreatePresentationDescriptor(_streams.Count, aStreams, out spPresentationDescriptor));

            for (int nStream = 0; nStream < _streams.Count; ++nStream)
            {
                ThrowIfError(spPresentationDescriptor.SelectStream(nStream));
            }

            _spPresentationDescriptor = spPresentationDescriptor;
        }

        private void ProcessServerSample(BufferPacket data)
        {
            Debug.WriteLine($"ProcessServerSample(...) Received buffer:{data.Length}");
        }

        private void ProcessServerFormatChange(BufferPacket data)
        {
            Debug.WriteLine($"ProcessServerFormatChange(...) Received buffer:{data.Length}");
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
