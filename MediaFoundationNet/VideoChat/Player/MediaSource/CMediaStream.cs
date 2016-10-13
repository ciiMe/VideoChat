using System;
using MediaFoundation;
using MediaFoundation.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VideoPlayer.Stream;

namespace VideoPlayer.MediaSource
{

    /*
//
// core info for all types
//
// {48eba18e-f8c9-4687-bf11-0a74c9f96a8f}   MF_MT_MAJOR_TYPE                {GUID}
DEFINE_GUID(MF_MT_MAJOR_TYPE,
0x48eba18e, 0xf8c9, 0x4687, 0xbf, 0x11, 0x0a, 0x74, 0xc9, 0xf9, 0x6a, 0x8f);

// {f7e34c9a-42e8-4714-b74b-cb29d72c35e5}   MF_MT_SUBTYPE                   {GUID}
DEFINE_GUID(MF_MT_SUBTYPE,
0xf7e34c9a, 0x42e8, 0x4714, 0xb7, 0x4b, 0xcb, 0x29, 0xd7, 0x2c, 0x35, 0xe5);

// {c9173739-5e56-461c-b713-46fb995cb95f}   MF_MT_ALL_SAMPLES_INDEPENDENT   {UINT32 (BOOL)}
DEFINE_GUID(MF_MT_ALL_SAMPLES_INDEPENDENT,
0xc9173739, 0x5e56, 0x461c, 0xb7, 0x13, 0x46, 0xfb, 0x99, 0x5c, 0xb9, 0x5f);

// {b8ebefaf-b718-4e04-b0a9-116775e3321b}   MF_MT_FIXED_SIZE_SAMPLES        {UINT32 (BOOL)}
DEFINE_GUID(MF_MT_FIXED_SIZE_SAMPLES,
0xb8ebefaf, 0xb718, 0x4e04, 0xb0, 0xa9, 0x11, 0x67, 0x75, 0xe3, 0x32, 0x1b);

// {3afd0cee-18f2-4ba5-a110-8bea502e1f92}   MF_MT_COMPRESSED                {UINT32 (BOOL)}
DEFINE_GUID(MF_MT_COMPRESSED,
0x3afd0cee, 0x18f2, 0x4ba5, 0xa1, 0x10, 0x8b, 0xea, 0x50, 0x2e, 0x1f, 0x92);

//
// MF_MT_SAMPLE_SIZE is only valid if MF_MT_FIXED_SIZED_SAMPLES is TRUE
//
// {dad3ab78-1990-408b-bce2-eba673dacc10}   MF_MT_SAMPLE_SIZE               {UINT32}
DEFINE_GUID(MF_MT_SAMPLE_SIZE,
0xdad3ab78, 0x1990, 0x408b, 0xbc, 0xe2, 0xeb, 0xa6, 0x73, 0xda, 0xcc, 0x10);

// 4d3f7b23-d02f-4e6c-9bee-e4bf2c6c695d     MF_MT_WRAPPED_TYPE              {Blob}
DEFINE_GUID(MF_MT_WRAPPED_TYPE,
0x4d3f7b23, 0xd02f, 0x4e6c, 0x9b, 0xee, 0xe4, 0xbf, 0x2c, 0x6c, 0x69, 0x5d);     
         */
    public class CMediaStream : IMFMediaStream
    {
        private Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

        //reference count
        private long _cRef;
        //Flag to indicate if Shutdown() method was called.       
        private SourceState _eSourceState;
        private NetworkSource _spSource;
        private IMFMediaEventQueue _spEventQueue;
        private IMFStreamDescriptor _spStreamDescriptor;

        //private List<IUnknown> _samples;
        //private List<IUnknown, true> _tokens;

        private int _dwId;
        private bool _fActive;
        private bool _fVideo;
        private float _flRate;

        private MFQualityDropMode _eDropMode;
        private bool _fDiscontinuity;
        private bool _fDropTime;
        private bool _fInitDropTime;
        private bool _fWaitingForCleanPoint;
        private long _hnsStartDroppingAt;
        private long _hnsAmountToDrop;

        public CMediaStream(NetworkSource pSource)
        {
            _cRef = 1;
            _spSource = pSource;
            _eSourceState = SourceState.SourceState_Invalid;
            _fActive = false;
            _flRate = 1.0f;
            _fVideo = false;
            _eDropMode = MFQualityDropMode.None;
            _fDiscontinuity = false;
            _fDropTime = false;
            _fInitDropTime = false;
            _fWaitingForCleanPoint = true;
            _hnsStartDroppingAt = 0;
            _hnsAmountToDrop = 0;

            _spSource = pSource;
        }

        public static HResult CreateInstance(StspStreamDescription pStreamDescription, BufferPacket pAttributesBuffer, NetworkSource pSource, out CMediaStream ppStream)
        {
            ppStream = null;
            /*
            if (pStreamDescription == null || pSource == nullptr || ppStream == nullptr)
            {
                return HResult.E_INVALIDARG;
            }*/

            HResult hr = HResult.S_OK;
            try
            {
                CMediaStream spStream = new CMediaStream(pSource);
                if (null == spStream)
                {
                    MFError.ThrowExceptionForHR(HResult.E_OUTOFMEMORY);
                }

                spStream.Initialize(pStreamDescription, pAttributesBuffer);
                ppStream = spStream;
            }
            catch (Exception ex)
            {
                hr = (HResult)ex.HResult;
            }

            return hr;
        }

        private void Initialize(StspStreamDescription pStreamDescription, BufferPacket pAttributesBuffer)
        {
            // Create the media event queue. 

            ThrowIfError(MFExtern.MFCreateEventQueue(out _spEventQueue));

            IMFMediaType spMediaType;
            IMFStreamDescriptor spSD;
            IMFMediaTypeHandler spMediaTypeHandler;
            int cbAttributesSize = 0;

            _fVideo = (pStreamDescription.guiMajorType == MFMediaType.Video);

            // Create a media type object.
            ThrowIfError(MFExtern.MFCreateMediaType(out spMediaType));
            cbAttributesSize = pAttributesBuffer.Length;

            if (cbAttributesSize < pStreamDescription.cbAttributesSize || pStreamDescription.cbAttributesSize == 0)
            {
                // Invalid stream description
                Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            // Prepare buffer where we will copy attributes to
            var pAttributes = Marshal.AllocHGlobal(pStreamDescription.cbAttributesSize);
            // Move the memory
            Marshal.Copy(pAttributesBuffer.MoveLeft(pStreamDescription.cbAttributesSize), 0, pAttributes, pStreamDescription.cbAttributesSize);

            // Initialize media type's attributes
            ThrowIfError(MFExtern.MFInitAttributesFromBlob(spMediaType, pAttributes, pStreamDescription.cbAttributesSize));

            Validation.ValidateInputMediaType(pStreamDescription.guiMajorType, pStreamDescription.guiSubType, spMediaType);
            ThrowIfError(spMediaType.SetGUID(MF_MT_MAJOR_TYPE, pStreamDescription.guiMajorType));

            ThrowIfError(spMediaType.SetGUID(MF_MT_SUBTYPE, pStreamDescription.guiSubType));

            // Now we can create MF stream descriptor.
            ThrowIfError(MFExtern.MFCreateStreamDescriptor(pStreamDescription.dwStreamId, 1, new IMFMediaType[] { spMediaType }, out spSD));
            ThrowIfError(spSD.GetMediaTypeHandler(out spMediaTypeHandler));

            // Set current media type
            ThrowIfError(spMediaTypeHandler.SetCurrentMediaType(spMediaType));

            _spStreamDescriptor = spSD;
            _dwId = pStreamDescription.dwStreamId;
            // State of the stream is started.
            _eSourceState = SourceState.SourceState_Stopped;
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

        #region IMFMediaStream
        public HResult GetMediaSource(out IMFMediaSource ppMediaSource)
        {
            throw new NotImplementedException();
        }

        public HResult GetStreamDescriptor(out IMFStreamDescriptor ppStreamDescriptor)
        {
            throw new NotImplementedException();
        }

        public HResult RequestSample(object pToken)
        {
            throw new NotImplementedException();
        }
        #endregion

        private void ThrowIfError(HResult hr)
        {
            if (MFError.Failed(hr))
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