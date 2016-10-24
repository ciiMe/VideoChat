using MediaFoundation;
using System;
using MediaFoundation.Misc;
using VideoPlayer.Stream;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Collections.Generic;
using VideoPlayer.Network;
using static MediaFoundation.Misc.ConstPropVariant;
using System.Threading.Tasks;
using System.Threading;

namespace VideoPlayer.MediaSource
{
    public class NetworkSource : IMFMediaSource, IMFGetService, IMFRateControl
    {
        public static Guid IID_IUnknown = new Guid("00000000-0000-0000-C000-000000000046");

        private SourceState _eSourceState;
        IMFMediaEventQueue _spEventQueue;
        INetworkMediaAdapter _networkStreamAdapter;

        IMFPresentationDescriptor _spPresentationDescriptor;
        // Collection of streams associated with the source
        List<IMFMediaStream> _streams;

        float _flRate;

        private bool _isOpenEventInvoked;
        public event EventHandler Opened;

        public NetworkSource()
        {
            _eSourceState = SourceState.SourceState_Invalid;
            _streams = new List<IMFMediaStream>();
        }

        public static HResult CreateInstance(out NetworkSource pSource)
        {
            HResult hr = HResult.S_OK;

            try
            {
                var spSource = new NetworkSource();
                spSource.Initialize();
                pSource = spSource;
            }
            catch (Exception ex)
            {
                pSource = null;
                hr = (HResult)ex.HResult;
            }

            return hr;
        }

        private void Initialize()
        {
            // Create the event queue helper.
            _networkStreamAdapter = new NetworkMediaAdapter();
            _networkStreamAdapter.OnDataArrived += _networkStreamAdapter_OnDataArrived;
            _isOpenEventInvoked = false;

            // Create the event queue helper.
            ThrowIfError(MFExtern.MFCreateEventQueue(out _spEventQueue));
        }

        public HResult Open(string ip, int port)
        {
            if (_eSourceState != SourceState.SourceState_Invalid)
            {
                Throw(HResult.MF_E_INVALIDREQUEST);
            }

            // If everything is ok now we are waiting for network client to connect. 
            // Change state to opening.
            _eSourceState = SourceState.SourceState_Opening;
            return _networkStreamAdapter.Open(ip, port);
        }
        
        private void _networkStreamAdapter_OnDataArrived(StspOperation option, IBufferPacket packet)
        {
            ThrowIfError(CheckShutdown());
            try
            {
                processPacket(option, packet);
            }
            catch (Exception ex)
            {
                HandleError(ex.HResult);
            }
        }

        private void processPacket(StspOperation option, IBufferPacket packet)
        {
            switch (option)
            {
                // We received server description
                case StspOperation.StspOperation_ServerDescription:
                    ThrowIfError(CheckShutdown());
                    if (_eSourceState != SourceState.SourceState_Opening)
                    {
                        Throw(HResult.MF_E_UNEXPECTED);
                    }
                    ProcessServerDescription(packet);
                    invokeOpenedEvent();
                    break;
                // We received a media sample
                case StspOperation.StspOperation_ServerSample:
                    ProcessServerSample(packet);
                    break;
                case StspOperation.StspOperation_ServerFormatChange:
                    ProcessServerFormatChange(packet);
                    break;
                // No supported operation
                default:
                    Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                    break;
            }
        }

        private void invokeOpenedEvent()
        {
            if (_isOpenEventInvoked)
            {
                return;
            }
            try
            {
                Opened?.Invoke(this, new EventArgs());
            }
            catch (Exception ex)
            {

            }
            _isOpenEventInvoked = true;
        }

        private void ProcessServerDescription(IBufferPacket data)
        {
            StspDescription desc = new StspDescription();
            var dataLen = data.GetLength();
            int descSize = Marshal.SizeOf(typeof(StspDescription));
            int streamDescSize = Marshal.SizeOf(typeof(StspStreamDescription));

            // Copy description  
            desc = StreamConvertor.TakeObject<StspDescription>(data);
            // Size of the packet should match size described in the packet (size of Description structure + size of attribute blob)
            var cbConstantSize = Convert.ToInt32(descSize + (desc.cNumStreams - 1) * streamDescSize);
            // Check if the input parameters are valid. We only support 2 streams.
            if (cbConstantSize < Marshal.SizeOf(desc) || desc.cNumStreams == 0 || desc.cNumStreams > 2 || dataLen < cbConstantSize)
            {
                ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            try
            {
                List<StspStreamDescription> streamDescs = new List<StspStreamDescription>(desc.aStreams);

                for (int i = 1; i < desc.cNumStreams; i++)
                {
                    var sd = StreamConvertor.TakeObject<StspStreamDescription>(data);
                    streamDescs.Add(sd);
                }

                int cbAttributeSize = 0;
                for (int i = 0; i < desc.cNumStreams; ++i)
                {
                    cbAttributeSize += streamDescs[i].cbAttributesSize;
                    /* todo: check out of range on cbAttributeSize
                    if (out of range)
                    {
                        Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                    }*/
                }

                // Validate the parameters. Limit the total size of attributes to 64kB.
                if ((dataLen != (cbConstantSize + cbAttributeSize)) || (cbAttributeSize > 0x10000))
                {
                    Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                }

                // Create stream for every stream description sent by the server.
                foreach (var sd in streamDescs)
                {
                    MediaStream spStream;
                    ThrowIfError(MediaStream.CreateInstance(sd, data, this, out spStream));
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

        private HResult ValidatePresentationDescriptor(IMFPresentationDescriptor pPD)
        {
            HResult hr = HResult.S_OK;
            bool fSelected = false;
            int cStreams = 0;

            if (_streams.Count == 0)
            {
                return HResult.E_UNEXPECTED;
            }

            // The caller's PD must have the same number of streams as ours.
            hr = pPD.GetStreamDescriptorCount(out cStreams);

            if (MFError.Succeeded(hr))
            {
                if (cStreams != _streams.Count)
                {
                    hr = HResult.E_INVALIDARG;
                }
            }

            // The caller must select at least one stream.
            if (MFError.Succeeded(hr))
            {
                for (int i = 0; i < cStreams; i++)
                {
                    IMFStreamDescriptor spSD;
                    hr = pPD.GetStreamDescriptorByIndex(i, out fSelected, out spSD);
                    if (MFError.Failed(hr))
                    {
                        break;
                    }
                }
            }

            return hr;
        }

        private void ProcessServerSample(IBufferPacket packet)
        {
            if (_eSourceState == SourceState.SourceState_Started)
            {
                // Only process samples when we are in started state
                StspSampleHeader sampleHead;

                // Copy the header object
                sampleHead = StreamConvertor.TakeObject<StspSampleHeader>(packet);
                if (packet.GetLength() < 0)
                {
                    ThrowIfError(HResult.E_INVALIDARG);
                }

                MediaStream spStream;
                ThrowIfError(GetStreamById(sampleHead.dwStreamId, out spStream));

                if (spStream.IsActive)
                {
                    // Convert packet to MF sample
                    IMFSample spSample;
                    ThrowIfError(ToMFSample(packet, out spSample));
                    // Forward sample to a proper stream.
                    spStream.ProcessSample(sampleHead, spSample);
                }
            }
            else
            {
                Throw(HResult.MF_E_UNEXPECTED);
            }
        }

        private HResult ToMFSample(IBufferPacket packet, out IMFSample sample)
        {
            sample = null;
            IMFSample spSample;

            var hr = MFExtern.MFCreateSample(out spSample);
            if (MFError.Failed(hr))
            {
                return hr;
            }

            //Get the media buffer
            IMFMediaBuffer mediaBuffer;
            hr = StreamConvertor.ConverToMediaBuffer(packet, out mediaBuffer);
            if (MFError.Failed(hr))
            {
                return hr;
            }
            var len = 0;
            mediaBuffer.GetCurrentLength(out len);
            hr = spSample.AddBuffer(mediaBuffer);
            if (MFError.Failed(hr))
            {
                return hr;
            }

            sample = spSample;
            return hr;
        }

        private HResult GetStreamById(int streamId, out MediaStream streamEntity)
        {
            var hr = HResult.S_OK;

            foreach (var stream in _streams)
            {
                var s = stream as MediaStream;
                if (s.Id == streamId)
                {
                    streamEntity = s;
                    return hr;
                }
            }
            streamEntity = null;
            return hr;
        }

        private void ProcessServerFormatChange(IBufferPacket packet)
        {
            IntPtr ptr;
            try
            {
                if (_eSourceState != SourceState.SourceState_Started)
                {
                    Throw(HResult.MF_E_UNEXPECTED);
                }
                int cbTotalLen = packet.GetLength();
                if (cbTotalLen <= 0)
                {
                    Throw(HResult.E_INVALIDARG);
                }

                // Minimum size of the operation payload is size of Description structure
                if (cbTotalLen < Marshal.SizeOf(typeof(StspStreamDescription)))
                {
                    ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
                }

                //todo: add try or use enhanced method to judge the HResult received from TakeObject(...)
                StspStreamDescription streamDesc = StreamConvertor.TakeObject<StspStreamDescription>(packet);
                if (cbTotalLen != Marshal.SizeOf(typeof(StspStreamDescription)) + streamDesc.cbAttributesSize || streamDesc.cbAttributesSize == 0)
                {
                    ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
                }

                // Prepare buffer where we will copy attributes to
                ptr = Marshal.AllocHGlobal(streamDesc.cbAttributesSize);
                var data = packet.TakeBuffer(streamDesc.cbAttributesSize);
                Marshal.Copy(data, 0, ptr, streamDesc.cbAttributesSize);

                IMFMediaType spMediaType;
                // Create a media type object.
                ThrowIfError(MFExtern.MFCreateMediaType(out spMediaType));
                // Initialize media type's attributes
                ThrowIfError(MFExtern.MFInitAttributesFromBlob(spMediaType, ptr, streamDesc.cbAttributesSize));
            }
            catch (Exception ex)
            {
                throw ex;
            }

            Marshal.Release(ptr);
        }

        #region IMFMediaEventGenerator
        public HResult BeginGetEvent(IMFAsyncCallback pCallback, object punkState)
        {
            HResult hr = HResult.S_OK;
            hr = CheckShutdown();
            if (MFError.Succeeded(hr))
            {
                hr = _spEventQueue.BeginGetEvent(pCallback, punkState);
            }
            return hr;
        }

        public HResult EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            HResult hr = HResult.S_OK;
            ppEvent = null;
            hr = CheckShutdown();
            if (MFError.Succeeded(hr))
            {
                hr = _spEventQueue.EndGetEvent(pResult, out ppEvent);
            }
            return hr;
        }

        public HResult GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            // NOTE:
            // GetEvent can block indefinitely, so we don't hold the lock.
            // This requires some juggling with the event queue pointer.
            var hr = HResult.S_OK;
            IMFMediaEventQueue spQueue = null;
            ppEvent = null;
            // Check shutdown
            hr = CheckShutdown();
            // Get the pointer to the event queue.
            if (MFError.Succeeded(hr))
            {
                spQueue = _spEventQueue;
            }

            // Now get the event.
            if (MFError.Succeeded(hr))
            {
                hr = spQueue.GetEvent(dwFlags, out ppEvent);
            }

            return hr;
        }

        public HResult QueueEvent(MediaEventType met, Guid guidExtendedType, HResult hrStatus, ConstPropVariant pvValue)
        {
            HResult hr = HResult.S_OK;
            hr = CheckShutdown();
            if (MFError.Succeeded(hr))
            {
                hr = _spEventQueue.QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
            }
            return hr;
        }
        #endregion

        #region IMFMediaSource
        public HResult CreatePresentationDescriptor(out IMFPresentationDescriptor ppPresentationDescriptor)
        {
            ppPresentationDescriptor = null;
            HResult hr = CheckShutdown();
            if (MFError.Succeeded(hr) && (_eSourceState == SourceState.SourceState_Opening || _eSourceState == SourceState.SourceState_Invalid || null == _spPresentationDescriptor))
            {
                hr = HResult.MF_E_NOT_INITIALIZED;
            }

            if (MFError.Succeeded(hr))
            {
                hr = _spPresentationDescriptor.Clone(out ppPresentationDescriptor);
            }

            return hr;
        }

        public HResult GetCharacteristics(out MFMediaSourceCharacteristics pdwCharacteristics)
        {
            pdwCharacteristics = MFMediaSourceCharacteristics.None;
            HResult hr = CheckShutdown();
            if (MFError.Succeeded(hr))
            {
                pdwCharacteristics = MFMediaSourceCharacteristics.IsLive;
            }

            return hr;
        }

        public HResult Pause()
        {
            return HResult.MF_E_INVALID_STATE_TRANSITION;
        }

        public HResult Shutdown()
        {
            HResult hr = CheckShutdown();

            if (MFError.Succeeded(hr))
            {
                if (_spEventQueue != null)
                {
                    _spEventQueue.Shutdown();
                }
                if (_networkStreamAdapter != null)
                {
                    _networkStreamAdapter.Close();
                }

                foreach (var stream in _streams)
                {
                    (stream as MediaStream).Shutdown();
                }

                _eSourceState = SourceState.SourceState_Shutdown;
                _streams.Clear();
                _spEventQueue.Shutdown();
                _networkStreamAdapter = null;
            }

            return hr;
        }

        public HResult Start(IMFPresentationDescriptor pPresentationDescriptor, Guid pguidTimeFormat, ConstPropVariant pvarStartPos)
        {
            HResult hr = HResult.S_OK;

            // Check parameters.

            // Start position and presentation descriptor cannot be NULL.
            if (pvarStartPos == null || pPresentationDescriptor == null)
            {
                return HResult.E_INVALIDARG;
            }

            // Check the time format.
            if ((pguidTimeFormat != null) && (pguidTimeFormat != Guid.Empty))
            {
                // Unrecognized time format GUID.
                return HResult.MF_E_UNSUPPORTED_TIME_FORMAT;
            }

            // Check the data type of the start position.
            if (pvarStartPos.GetVariantType() != VariantType.None && pvarStartPos.GetVariantType() != VariantType.Int64)
            {
                return HResult.MF_E_UNSUPPORTED_TIME_FORMAT;
            }

            if (_eSourceState != SourceState.SourceState_Stopped && _eSourceState != SourceState.SourceState_Started)
            {
                hr = HResult.MF_E_INVALIDREQUEST;
            }

            if (MFError.Succeeded(hr))
            {
                // Check if the presentation description is valid.
                hr = ValidatePresentationDescriptor(pPresentationDescriptor);
            }

            if (MFError.Succeeded(hr))
            {
                CSourceOperation op = new CSourceOperation
                {
                    Type = SourceOperationType.Operation_Start,
                    PresentationDescriptor = pPresentationDescriptor,
                    Data = pvarStartPos
                };
                doStart(op);
            }
            return hr;
        }

        private void callDoStart(object option)
        {
            doStart((CSourceOperation)option);
        }

        private void doStart(CSourceOperation pOp)
        {
            Debug.Assert(pOp.Type == SourceOperationType.Operation_Start);

            IMFPresentationDescriptor spPD = pOp.PresentationDescriptor;

            try
            {
                SelectStreams(spPD);

                _eSourceState = SourceState.SourceState_Starting;
                _networkStreamAdapter.SendStartRequest();
                _eSourceState = SourceState.SourceState_Started;

                ThrowIfError(_spEventQueue.QueueEventParamVar(MediaEventType.MESourceStarted, Guid.Empty, HResult.S_OK, pOp.Data));
            }
            catch (Exception ex)
            {
                _spEventQueue.QueueEventParamVar(MediaEventType.MESourceStarted, Guid.Empty, (HResult)ex.HResult, null);
            }
        }

        private void SelectStreams(IMFPresentationDescriptor pPD)
        {
            for (int nStream = 0; nStream < _streams.Count; ++nStream)
            {
                IMFStreamDescriptor spSD;
                MediaStream spStream;
                int nStreamId;
                bool fSelected;

                // Get next stream descriptor
                ThrowIfError(pPD.GetStreamDescriptorByIndex(nStream, out fSelected, out spSD));

                // Get stream id
                ThrowIfError(spSD.GetStreamIdentifier(out nStreamId));

                // Get simple net media stream
                ThrowIfError(GetStreamById(nStreamId, out spStream));

                // Remember if stream was selected
                bool fWasSelected = spStream.IsActive;
                ThrowIfError(spStream.SetActive(fSelected));

                if (fSelected)
                {
                    // Choose event type to send
                    MediaEventType met = fWasSelected ? MediaEventType.MEUpdatedStream : MediaEventType.MENewStream;
                    ThrowIfError(_spEventQueue.QueueEventParamUnk(met, Guid.Empty, HResult.S_OK, spStream));

                    // Start the stream. The stream will send the appropriate event.
                    ThrowIfError(spStream.Start());
                }
            }
        }

        public HResult Stop()
        {
            HResult hr = HResult.S_OK;
            CSourceOperation spStopOp = new CSourceOperation
            {
                Type = SourceOperationType.Operation_Stop
            };
            // Queue asynchronous stop
            //doStop(spStopOp);
            return hr;
        }

        private void callDoStop(object option)
        {
            doStop((CSourceOperation)option);
        }

        private void doStop(CSourceOperation pOp)
        {
            Debug.Assert(pOp.Type == SourceOperationType.Operation_Stop);

            HResult hr = HResult.S_OK;
            try
            {
                foreach (var stream in _streams)
                {
                    var cs = (stream as MediaStream);
                    if (cs.IsActive)
                    {
                        ThrowIfError(cs.Flush());
                        ThrowIfError(cs.Stop());
                    }
                }
            }
            catch (Exception ex)
            {
                hr = (HResult)ex.HResult;
            }
            // Send the "stopped" event. This might include a failure code.
            _spEventQueue.QueueEventParamVar(MediaEventType.MESourceStopped, Guid.Empty, hr, null);
        }
        #endregion

        #region IMFRateControl
        public HResult GetRate(ref bool pfThin, out float pflRate)
        {
            lock (this)
            {
                pfThin = false;
                pflRate = _flRate;

                return HResult.S_OK;
            }
        }

        public HResult SetRate(bool fThin, float flRate)
        {
            if (fThin)
            {
                return HResult.MF_E_THINNING_UNSUPPORTED;
            }
            if (!isRateSupported(flRate, out flRate))
            {
                return HResult.MF_E_UNSUPPORTED_RATE;
            }

            lock (this)
            {
                HResult hr = HResult.S_OK;

                if (flRate == _flRate)
                {
                    return HResult.S_OK;
                }
                asyncRun(startSetRate, _flRate);
                return hr;
            }
        }

        private void startSetRate(object rateValue)
        {
            doSetRate(Convert.ToSingle(rateValue));
        }

        delegate void ThreadHandler(object value);

        private void asyncRun(ThreadHandler handler, object parameter)
        {
            ParameterizedThreadStart ts = new ParameterizedThreadStart(handler);
            Thread thread = new Thread(ts);
            thread.Start();
        }

        private bool isRateSupported(float flRate, out float pflAdjustedRate)
        {
            pflAdjustedRate = 1;
            if (flRate < 0.00001f && flRate > -0.00001f)
            {
                pflAdjustedRate = 0.0f;
                return true;
            }
            else if (flRate < 1.0001f && flRate > 0.9999f)
            {
                pflAdjustedRate = 1.0f;
                return true;
            }
            return false;
        }

        private void doSetRate(float rate)
        {
            HResult hr = HResult.S_OK;
            try
            {
                for (int i = 0; i < _streams.Count; i++)
                {
                    MediaStream pStream = _streams[i] as MediaStream;

                    if (pStream.IsActive)
                    {
                        ThrowIfError(pStream.Flush());
                        ThrowIfError(pStream.SetRate(rate));
                    }
                }

                _flRate = rate;
            }
            catch (Exception ex)
            {
                hr = (HResult)ex.HResult;
            }
            // Send the "rate changed" event. This might include a failure code.
            _spEventQueue.QueueEventParamVar(MediaEventType.MESourceRateChanged, Guid.Empty, hr, null);
        }
        #endregion

        #region IMFGetService
        public HResult GetService(Guid guidService, Guid riid, out object ppvObject)
        {
            HResult hr = HResult.MF_E_UNSUPPORTED_SERVICE;
            ppvObject = null;

            if (guidService == MFServices.MF_RATE_CONTROL_SERVICE)
            {
                hr = QueryInterface(riid, out ppvObject);
            }

            return hr;
        }

        private HResult QueryInterface(Guid riid, out object ppv)
        {
            ppv = null;

            HResult hr = HResult.S_OK;

            if (riid == IID_IUnknown || riid == typeof(IMFMediaSource).GUID || riid == typeof(IMFMediaEventGenerator).GUID)
            {
                ppv = this;
            }
            else if (riid == typeof(IMFGetService).GUID)
            {
                ppv = this;
            }
            else if (riid == typeof(IMFRateControl).GUID)
            {
                ppv = this;
            }
            else
            {
                hr = HResult.E_NOINTERFACE;
            }

            return hr;
        }
        #endregion

        private HResult CheckShutdown()
        {
            if (_eSourceState == SourceState.SourceState_Shutdown)
            {
                return HResult.MF_E_SHUTDOWN;
            }
            else
            {
                return HResult.S_OK;
            }
        }

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
