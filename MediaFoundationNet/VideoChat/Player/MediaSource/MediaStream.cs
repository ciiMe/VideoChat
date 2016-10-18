using System;
using MediaFoundation;
using MediaFoundation.Misc;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using VideoPlayer.Stream;
using System.Diagnostics;

namespace VideoPlayer.MediaSource
{

    /*
    //
    //core info for all types
    //
    //{48eba18e-f8c9-4687-bf11-0a74c9f96a8f}   MF_MT_MAJOR_TYPE                {GUID}
    DEFINE_GUID(MF_MT_MAJOR_TYPE,
    0x48eba18e, 0xf8c9, 0x4687, 0xbf, 0x11, 0x0a, 0x74, 0xc9, 0xf9, 0x6a, 0x8f);

    //{f7e34c9a-42e8-4714-b74b-cb29d72c35e5}   MF_MT_SUBTYPE                   {GUID}
    DEFINE_GUID(MF_MT_SUBTYPE,
    0xf7e34c9a, 0x42e8, 0x4714, 0xb7, 0x4b, 0xcb, 0x29, 0xd7, 0x2c, 0x35, 0xe5);

    //{c9173739-5e56-461c-b713-46fb995cb95f}   MF_MT_ALL_SAMPLES_INDEPENDENT   {UINT32 (BOOL)}
    DEFINE_GUID(MF_MT_ALL_SAMPLES_INDEPENDENT,
    0xc9173739, 0x5e56, 0x461c, 0xb7, 0x13, 0x46, 0xfb, 0x99, 0x5c, 0xb9, 0x5f);

    //{b8ebefaf-b718-4e04-b0a9-116775e3321b}   MF_MT_FIXED_SIZE_SAMPLES        {UINT32 (BOOL)}
    DEFINE_GUID(MF_MT_FIXED_SIZE_SAMPLES,
    0xb8ebefaf, 0xb718, 0x4e04, 0xb0, 0xa9, 0x11, 0x67, 0x75, 0xe3, 0x32, 0x1b);

    //{3afd0cee-18f2-4ba5-a110-8bea502e1f92}   MF_MT_COMPRESSED                {UINT32 (BOOL)}
    DEFINE_GUID(MF_MT_COMPRESSED,
    0x3afd0cee, 0x18f2, 0x4ba5, 0xa1, 0x10, 0x8b, 0xea, 0x50, 0x2e, 0x1f, 0x92);

    //
    //MF_MT_SAMPLE_SIZE is only valid if MF_MT_FIXED_SIZED_SAMPLES is TRUE
    //
    //{dad3ab78-1990-408b-bce2-eba673dacc10}   MF_MT_SAMPLE_SIZE               {UINT32}
    DEFINE_GUID(MF_MT_SAMPLE_SIZE,
    0xdad3ab78, 0x1990, 0x408b, 0xbc, 0xe2, 0xeb, 0xa6, 0x73, 0xda, 0xcc, 0x10);

    //4d3f7b23-d02f-4e6c-9bee-e4bf2c6c695d     MF_MT_WRAPPED_TYPE              {Blob}
    DEFINE_GUID(MF_MT_WRAPPED_TYPE,
    0x4d3f7b23, 0xd02f, 0x4e6c, 0x9b, 0xee, 0xe4, 0xbf, 0x2c, 0x6c, 0x69, 0x5d);     
         */
    public class MediaStream : IMFMediaStream
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

        private Queue<object> _samples;
        private Queue<object> _tokens;

        private int _id;
        private bool _fActive;
        private bool _isVideo;
        private float _flRate;

        private MFQualityDropMode _eDropMode;
        private bool _fDiscontinuity;
        private bool _fDropTime;
        private bool _fInitDropTime;
        private bool _fWaitingForCleanPoint;
        private long _hnsStartDroppingAt;
        private long _hnsAmountToDrop;

        public int Id => _id;
        public bool IsActive => _fActive;

        public MediaStream(NetworkSource pSource)
        {
            _cRef = 1;
            _spSource = pSource;
            _eSourceState = SourceState.SourceState_Invalid;
            _fActive = false;
            _flRate = 1.0f;
            _isVideo = false;
            _eDropMode = MFQualityDropMode.None;
            _fDiscontinuity = false;
            _fDropTime = false;
            _fInitDropTime = false;
            _fWaitingForCleanPoint = true;
            _hnsStartDroppingAt = 0;
            _hnsAmountToDrop = 0;

            _samples = new Queue<object>();
            _tokens = new Queue<object>();

            _spSource = pSource;
        }

        public static HResult CreateInstance(StspStreamDescription pStreamDescription, IBufferPacket pAttributesBuffer, NetworkSource pSource, out MediaStream ppStream)
        {
            ppStream = null;
            HResult hr = HResult.S_OK;
            try
            {
                MediaStream stream = new MediaStream(pSource);
                if (null == stream)
                {
                    MFError.ThrowExceptionForHR(HResult.E_OUTOFMEMORY);
                }

                stream.Initialize(pStreamDescription, pAttributesBuffer);
                ppStream = stream;
            }
            catch (Exception ex)
            {
                hr = (HResult)ex.HResult;
            }

            return hr;
        }

        private void Initialize(StspStreamDescription pStreamDescription, IBufferPacket attributesBuffer)
        {
            //Create the media event queue. 
            ThrowIfError(MFExtern.MFCreateEventQueue(out _spEventQueue));

            IMFMediaType mediaType;
            IMFStreamDescriptor spSD;
            IMFMediaTypeHandler spMediaTypeHandler;

            _isVideo = (pStreamDescription.guiMajorType == MFMediaType.Video);

            //Create a media type object.
            ThrowIfError(MFExtern.MFCreateMediaType(out mediaType));

            if (attributesBuffer.GetFirstOptionDataLength() < pStreamDescription.cbAttributesSize || pStreamDescription.cbAttributesSize == 0)
            {
                //Invalid stream description
                Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            //Prepare buffer where we will copy attributes to, then initialize media type's attributes
            var pAttributes = Marshal.AllocHGlobal(pStreamDescription.cbAttributesSize);
            try
            {
                Marshal.Copy(attributesBuffer.TakeBuffer(pStreamDescription.cbAttributesSize), 0, pAttributes, pStreamDescription.cbAttributesSize);
                ThrowIfError(MFExtern.MFInitAttributesFromBlob(mediaType, pAttributes, pStreamDescription.cbAttributesSize));
            }
            finally
            {
                Marshal.FreeHGlobal(pAttributes);
            }

            Validation.ValidateInputMediaType(pStreamDescription.guiMajorType, pStreamDescription.guiSubType, mediaType);
            ThrowIfError(mediaType.SetGUID(MF_MT_MAJOR_TYPE, pStreamDescription.guiMajorType));
            ThrowIfError(mediaType.SetGUID(MF_MT_SUBTYPE, pStreamDescription.guiSubType));

            //Now we can create MF stream descriptor.
            ThrowIfError(MFExtern.MFCreateStreamDescriptor(pStreamDescription.dwStreamId, 1, new IMFMediaType[] { mediaType }, out spSD));
            ThrowIfError(spSD.GetMediaTypeHandler(out spMediaTypeHandler));

            //Set current media type
            ThrowIfError(spMediaTypeHandler.SetCurrentMediaType(mediaType));

            _spStreamDescriptor = spSD;
            _id = pStreamDescription.dwStreamId;
            //State of the stream is started.
            _eSourceState = SourceState.SourceState_Stopped;
        }

        // Processes media sample received from the header
        internal void ProcessSample(StspSampleHeader sampleHead, IMFSample sample)
        {
            Debug.Assert(sample != null);

            try
            {
                ThrowIfError(CheckShutdown());
                // Set sample attributes
                SetSampleAttributes(sampleHead, sample);

                // Check if we are in propper state if so deliver the sample otherwise just skip it and don't treat it as an error.
                if (_eSourceState == SourceState.SourceState_Started)
                {
                    // Put sample on the list
                    _samples.Enqueue(sample);
                    // Deliver samples
                    DeliverSamples();
                }
                else
                {
                    Throw(HResult.MF_E_UNEXPECTED);
                }
            }
            catch (Exception ex)
            {
                HandleError(ex.HResult);
            }
        }

        #region Sample attribute definitions

        /*
        //
        // Sample attributes
        // These are the well-known attributes that can be present on an MF Sample's
        // IMFAttributes store
        //

        // MFSampleExtension_CleanPoint {9cdf01d8-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample is a clean point (key
        // frame), and decoding can begin at this sample.
        DEFINE_GUID(MFSampleExtension_CleanPoint,
        0x9cdf01d8, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // MFSampleExtension_Discontinuity {9cdf01d9-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample data represents the first
        // sample following a discontinuity (gap) in the stream of samples.
        // This can happen, for instance, if the previous sample was lost in
        // transmission.
        DEFINE_GUID(MFSampleExtension_Discontinuity,
        0x9cdf01d9, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // MFSampleExtension_Token {8294da66-f328-4805-b551-00deb4c57a61}
        // Type: IUNKNOWN
        // When an IMFMediaStream delivers a sample via MEMediaStream, this attribute
        // should be set to the IUnknown *pToken argument that was passed with the
        // IMFMediaStream::RequestSample call to which this sample corresponds.
        DEFINE_GUID(MFSampleExtension_Token,
        0x8294da66, 0xf328, 0x4805, 0xb5, 0x51, 0x00, 0xde, 0xb4, 0xc5, 0x7a, 0x61);

        // MFSampleExtension_DecodeTimestamp {73A954D4-09E2-4861-BEFC-94BD97C08E6E}
        // Type : UINT64
        // If present, contains the DTS (Decoding Time Stamp) of the sample.
        DEFINE_GUID(MFSampleExtension_DecodeTimestamp,
        0x73a954d4, 0x9e2, 0x4861, 0xbe, 0xfc, 0x94, 0xbd, 0x97, 0xc0, 0x8e, 0x6e);

        // MFSampleExtension_VideoEncodeQP {B2EFE478-F979-4C66-B95E-EE2B82C82F36}
        // Type: UINT64
        // Used by video encoders to specify the QP used to encode the output sample.
        DEFINE_GUID(MFSampleExtension_VideoEncodeQP,
        0xb2efe478, 0xf979, 0x4c66, 0xb9, 0x5e, 0xee, 0x2b, 0x82, 0xc8, 0x2f, 0x36);

        // MFSampleExtension_VideoEncPictureType {973704E6-CD14-483C-8F20-C9FC0928BAD5}
        // Type: UINT32
        // Used by video encoders to specify the output sample's picture type.
        DEFINE_GUID(MFSampleExtension_VideoEncodePictureType,
        0x973704e6, 0xcd14, 0x483c, 0x8f, 0x20, 0xc9, 0xfc, 0x9, 0x28, 0xba, 0xd5);

        // MFSampleExtension_FrameCorruption {B4DD4A8C-0BEB-44C4-8B75-B02B913B04F0}
        // Type: UINT32
        // Indicates whether the frame in the sample has corruption or not
        // value 0 indicates that there is no corruption, or it is unknown
        // Value 1 indicates that some corruption was detected e.g, during decoding
        DEFINE_GUID(MFSampleExtension_FrameCorruption,
        0xb4dd4a8c, 0xbeb, 0x44c4, 0x8b, 0x75, 0xb0, 0x2b, 0x91, 0x3b, 0x4, 0xf0);

        /////////////////////////////////////////////////////////////////////////////
        //
        // The following sample attributes are used for encrypted samples
        //
        /////////////////////////////////////////////////////////////////////////////

        // MFSampleExtension_DescrambleData {43483BE6-4903-4314-B032-2951365936FC}
        // Type: UINT64
        DEFINE_GUID(MFSampleExtension_DescrambleData,
        0x43483be6, 0x4903, 0x4314, 0xb0, 0x32, 0x29, 0x51, 0x36, 0x59, 0x36, 0xfc);

        // MFSampleExtension_SampleKeyID {9ED713C8-9B87-4B26-8297-A93B0C5A8ACC}
        // Type: UINT32
        DEFINE_GUID(MFSampleExtension_SampleKeyID,
        0x9ed713c8, 0x9b87, 0x4b26, 0x82, 0x97, 0xa9, 0x3b, 0x0c, 0x5a, 0x8a, 0xcc);

        // MFSampleExtension_GenKeyFunc {441CA1EE-6B1F-4501-903A-DE87DF42F6ED}
        // Type: UINT64
        DEFINE_GUID(MFSampleExtension_GenKeyFunc,
        0x441ca1ee, 0x6b1f, 0x4501, 0x90, 0x3a, 0xde, 0x87, 0xdf, 0x42, 0xf6, 0xed);

        // MFSampleExtension_GenKeyCtx {188120CB-D7DA-4B59-9B3E-9252FD37301C}
        // Type: UINT64
        DEFINE_GUID(MFSampleExtension_GenKeyCtx,
        0x188120cb, 0xd7da, 0x4b59, 0x9b, 0x3e, 0x92, 0x52, 0xfd, 0x37, 0x30, 0x1c);

        // MFSampleExtension_PacketCrossOffsets {2789671D-389F-40BB-90D9-C282F77F9ABD}
        // Type: BLOB
        DEFINE_GUID(MFSampleExtension_PacketCrossOffsets,
        0x2789671d, 0x389f, 0x40bb, 0x90, 0xd9, 0xc2, 0x82, 0xf7, 0x7f, 0x9a, 0xbd);

        // MFSampleExtension_Encryption_SampleID {6698B84E-0AFA-4330-AEB2-1C0A98D7A44D}
        // Type: UINT64
        DEFINE_GUID(MFSampleExtension_Encryption_SampleID,
        0x6698b84e, 0x0afa, 0x4330, 0xae, 0xb2, 0x1c, 0x0a, 0x98, 0xd7, 0xa4, 0x4d);

        // MFSampleExtension_Encryption_KeyID {76376591-795F-4DA1-86ED-9D46ECA109A9}
        // Type: BLOB
        DEFINE_GUID(MFSampleExtension_Encryption_KeyID,
        0x76376591, 0x795f, 0x4da1, 0x86, 0xed, 0x9d, 0x46, 0xec, 0xa1, 0x09, 0xa9);

        // MFSampleExtension_Content_KeyID {C6C7F5B0-ACCA-415B-87D9-10441469EFC6}
        // Type: GUID
        DEFINE_GUID(MFSampleExtension_Content_KeyID,
        0xc6c7f5b0, 0xacca, 0x415b, 0x87, 0xd9, 0x10, 0x44, 0x14, 0x69, 0xef, 0xc6);

        // MFSampleExtension_Encryption_SubSampleMappingSplit {FE0254B9-2AA5-4EDC-99F7-17E89DBF9174}
        // Type: BLOB
        DEFINE_GUID(MFSampleExtension_Encryption_SubSampleMappingSplit,
        0xfe0254b9, 0x2aa5, 0x4edc, 0x99, 0xf7, 0x17, 0xe8, 0x9d, 0xbf, 0x91, 0x74);
         /////////////////////////////////////////////////////////////////////////////
        //
        // MFSample STANDARD EXTENSION ATTRIBUTE GUIDs
        //
        /////////////////////////////////////////////////////////////////////////////

        // {b1d5830a-deb8-40e3-90fa-389943716461}   MFSampleExtension_Interlaced                {UINT32 (BOOL)}
        DEFINE_GUID(MFSampleExtension_Interlaced,
        0xb1d5830a, 0xdeb8, 0x40e3, 0x90, 0xfa, 0x38, 0x99, 0x43, 0x71, 0x64, 0x61);

        // {941ce0a3-6ae3-4dda-9a08-a64298340617}   MFSampleExtension_BottomFieldFirst          {UINT32 (BOOL)}
        DEFINE_GUID(MFSampleExtension_BottomFieldFirst,
        0x941ce0a3, 0x6ae3, 0x4dda, 0x9a, 0x08, 0xa6, 0x42, 0x98, 0x34, 0x06, 0x17);

        // {304d257c-7493-4fbd-b149-9228de8d9a99}   MFSampleExtension_RepeatFirstField          {UINT32 (BOOL)}
        DEFINE_GUID(MFSampleExtension_RepeatFirstField,
        0x304d257c, 0x7493, 0x4fbd, 0xb1, 0x49, 0x92, 0x28, 0xde, 0x8d, 0x9a, 0x99);

        // {9d85f816-658b-455a-bde0-9fa7e15ab8f9}   MFSampleExtension_SingleField               {UINT32 (BOOL)}
        DEFINE_GUID(MFSampleExtension_SingleField,
        0x9d85f816, 0x658b, 0x455a, 0xbd, 0xe0, 0x9f, 0xa7, 0xe1, 0x5a, 0xb8, 0xf9);

        // {6852465a-ae1c-4553-8e9b-c3420fcb1637}   MFSampleExtension_DerivedFromTopField       {UINT32 (BOOL)}
        DEFINE_GUID(MFSampleExtension_DerivedFromTopField,
        0x6852465a, 0xae1c, 0x4553, 0x8e, 0x9b, 0xc3, 0x42, 0x0f, 0xcb, 0x16, 0x37);

        // MFSampleExtension_MeanAbsoluteDifference {1cdbde11-08b4-4311-a6dd-0f9f371907aa}
        // Type: UINT32
        DEFINE_GUID(MFSampleExtension_MeanAbsoluteDifference,
        0x1cdbde11, 0x08b4, 0x4311, 0xa6, 0xdd, 0x0f, 0x9f, 0x37, 0x19, 0x07, 0xaa);

        // MFSampleExtension_LongTermReferenceFrameInfo {9154733f-e1bd-41bf-81d3-fcd918f71332}
        // Type: UINT32
        DEFINE_GUID(MFSampleExtension_LongTermReferenceFrameInfo,
        0x9154733f, 0xe1bd, 0x41bf, 0x81, 0xd3, 0xfc, 0xd9, 0x18, 0xf7, 0x13, 0x32);

        // MFSampleExtension_ROIRectangle {3414a438-4998-4d2c-be82-be3ca0b24d43}
        // Type: BLOB
        DEFINE_GUID(MFSampleExtension_ROIRectangle,
        0x3414a438, 0x4998, 0x4d2c, 0xbe, 0x82, 0xbe, 0x3c, 0xa0, 0xb2, 0x4d, 0x43);
        */

        // {941ce0a3-6ae3-4dda-9a08-a64298340617}   MFSampleExtension_BottomFieldFirst          {UINT32 (BOOL)}
        Guid MFSampleExtension_BottomFieldFirst = Guid.Parse("941ce0a3-6ae3-4dda-9a08-a64298340617");

        // MFSampleExtension_CleanPoint {9cdf01d8-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample is a clean point (key
        // frame), and decoding can begin at this sample.
        Guid MFSampleExtension_CleanPoint = new Guid(0x9cdf01d8, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // {6852465a-ae1c-4553-8e9b-c3420fcb1637}   MFSampleExtension_DerivedFromTopField       {UINT32 (BOOL)}
        Guid MFSampleExtension_DerivedFromTopField = Guid.Parse("6852465a-ae1c-4553-8e9b-c3420fcb1637");

        // MFSampleExtension_Discontinuity {9cdf01d9-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample data represents the first
        // sample following a discontinuity (gap) in the stream of samples.
        // This can happen, for instance, if the previous sample was lost in
        // transmission.
        Guid MFSampleExtension_Discontinuity = new Guid(0x9cdf01d9, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // {b1d5830a-deb8-40e3-90fa-389943716461}   MFSampleExtension_Interlaced                {UINT32 (BOOL)}
        Guid MFSampleExtension_Interlaced = Guid.Parse("b1d5830a-deb8-40e3-90fa-389943716461");

        // {304d257c-7493-4fbd-b149-9228de8d9a99}   MFSampleExtension_RepeatFirstField          {UINT32 (BOOL)}
        Guid MFSampleExtension_RepeatFirstField = Guid.Parse("304d257c-7493-4fbd-b149-9228de8d9a99");

        // {9d85f816-658b-455a-bde0-9fa7e15ab8f9}   MFSampleExtension_SingleField               {UINT32 (BOOL)}
        Guid MFSampleExtension_SingleField = Guid.Parse("9d85f816-658b-455a-bde0-9fa7e15ab8f9");
        #endregion
        private void SetSampleAttributes(StspSampleHeader sampleHeader, IMFSample sample)
        {
            ThrowIfError(sample.SetSampleTime(sampleHeader.ullTimestamp));
            ThrowIfError(sample.SetSampleDuration(sampleHeader.ullDuration));

            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_BottomFieldFirst, StspSampleFlags.StspSampleFlag_BottomFieldFirst);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_CleanPoint, StspSampleFlags.StspSampleFlag_CleanPoint);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_DerivedFromTopField, StspSampleFlags.StspSampleFlag_DerivedFromTopField);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_Discontinuity, StspSampleFlags.StspSampleFlag_Discontinuity);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_Interlaced, StspSampleFlags.StspSampleFlag_Interlaced);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_RepeatFirstField, StspSampleFlags.StspSampleFlag_RepeatFirstField);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFSampleExtension_SingleField, StspSampleFlags.StspSampleFlag_SingleField);

            int cbTotalLen;
            sample.GetTotalLength(out cbTotalLen);
            var isKeyFrame = Convert.ToBoolean(sampleHeader.dwFlags & (int)StspSampleFlags.StspSampleFlag_CleanPoint);
            Debug.WriteLine($"Received sample {sampleHeader.ullTimestamp} Duration-{sampleHeader.ullDuration} Length-{cbTotalLen} " + (isKeyFrame ? "key frame" : ""));
        }

        void SET_SAMPLE_ATTRIBUTE(StspSampleHeader sampleHeader, IMFSample pSample, Guid flag, StspSampleFlags flagValue)
        {
            var value = (int)flagValue;
            if ((value & sampleHeader.dwFlagMasks) == value)
            {
                ThrowIfError(pSample.SetUINT32(flag, ((value & sampleHeader.dwFlags) == value) ? 1 : 0));
            }
        }

        #region Guid for deliver sample

        // MFSampleExtension_Token {8294da66-f328-4805-b551-00deb4c57a61}
        // Type: IUNKNOWN
        // When an IMFMediaStream delivers a sample via MEMediaStream, this attribute
        // should be set to the IUnknown *pToken argument that was passed with the
        // IMFMediaStream::RequestSample call to which this sample corresponds.
        Guid MFSampleExtension_Token = new Guid(0x8294da66, 0xf328, 0x4805, 0xb5, 0x51, 0x00, 0xde, 0xb4, 0xc5, 0x7a, 0x61);
        #endregion

        // Deliver samples for every request client made
        private void DeliverSamples()
        {
            // Check if we have both: samples available in the queue and requests in request list.
            while (_samples.Count != 0 && _tokens.Count != 0)
            {
                var spEntry = _samples.Dequeue();
                var spSample = spEntry as IMFSample;

                if (spSample != null)
                {
                    if (!ShouldDropSample(spSample))
                    {
                        // Get the request token
                        var spToken = _tokens.Dequeue();
                        if (spToken != null)
                        {
                            // If token was not null set the sample attribute.
                            ThrowIfError(spSample.SetUnknown(MFSampleExtension_Token, spToken));
                        }

                        if (_fDiscontinuity)
                        {
                            // If token was not null set the sample attribute.
                            ThrowIfError(spSample.SetUINT32(MFSampleExtension_Discontinuity, 1));
                            _fDiscontinuity = false;
                        }

                        // Send a sample event.
                        ThrowIfError(_spEventQueue.QueueEventParamUnk(MediaEventType.MEMediaSample, Guid.Empty, HResult.S_OK, spSample));
                    }
                    else
                    {
                        _fDiscontinuity = true;
                    }
                }
                else
                {
                    IMFMediaType spMediaType = spEntry as IMFMediaType;
                    if (spMediaType == null)
                    {
                        ThrowIfError(HResult.S_FALSE);
                    }
                    // Send a sample event.
                    ThrowIfError(_spEventQueue.QueueEventParamUnk(MediaEventType.MEStreamFormatChanged, Guid.Empty, HResult.S_OK, spMediaType));
                }
            }
        }

        private bool ShouldDropSample(IMFSample pSample)
        {
            if (!_isVideo)
            {
                return false;
            }

            bool fCleanPoint = MFExtern.MFGetAttributeUINT32(pSample, MFSampleExtension_CleanPoint, 0) > 0;
            bool fDrop = _flRate != 1.0f && !fCleanPoint;

            long hnsTimeStamp = 0;
            ThrowIfError(pSample.GetSampleTime(out hnsTimeStamp));

            if (!fDrop && _fDropTime)
            {
                if (_fInitDropTime)
                {
                    _hnsStartDroppingAt = hnsTimeStamp;
                    _fInitDropTime = false;
                }

                fDrop = hnsTimeStamp < (_hnsStartDroppingAt + _hnsAmountToDrop);
                if (!fDrop)
                {
                    Debug.WriteLine($"Ending dropping time on sample ts={hnsTimeStamp} _hnsStartDroppingAt={_hnsStartDroppingAt} _hnsAmountToDrop={_hnsAmountToDrop}");
                    ResetDropTime();
                }
                else
                {
                    Debug.WriteLine($"Dropping sample ts={hnsTimeStamp} _hnsStartDroppingAt={_hnsStartDroppingAt} _hnsAmountToDrop={_hnsAmountToDrop}");
                }
            }

            if (!fDrop && (_eDropMode == MFQualityDropMode.Mode1 || _fWaitingForCleanPoint))
            {
                // Only key frames
                fDrop = !fCleanPoint;
                if (fCleanPoint)
                {
                    _fWaitingForCleanPoint = false;
                }

                if (fDrop)
                {
                    Debug.WriteLine($"Dropping sample ts={hnsTimeStamp}");
                }
            }

            return fDrop;
        }

        #region IMFMediaEventGenerator
        public HResult BeginGetEvent(IMFAsyncCallback pCallback, object punkState)
        {
            HResult hr = HResult.S_OK;

            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    hr = _spEventQueue.BeginGetEvent(pCallback, punkState);
                }

                return hr;
            }
        }

        public HResult EndGetEvent(IMFAsyncResult pResult, out IMFMediaEvent ppEvent)
        {
            HResult hr = HResult.S_OK;
            ppEvent = null;

            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    hr = _spEventQueue.EndGetEvent(pResult, out ppEvent);
                }

                return hr;
            }
        }

        public HResult GetEvent(MFEventFlag dwFlags, out IMFMediaEvent ppEvent)
        {
            // NOTE:
            // GetEvent can block indefinitely, so we don't hold the lock.
            // This requires some juggling with the event queue pointer.
            HResult hr = HResult.S_OK;
            IMFMediaEventQueue spQueue = null;
            ppEvent = null;

            lock (_spSource)
            {
                // Check shutdown
                hr = CheckShutdown();

                // Get the pointer to the event queue.
                if (MFError.Succeeded(hr))
                {
                    spQueue = _spEventQueue;
                }
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

            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    hr = _spEventQueue.QueueEventParamVar(met, guidExtendedType, hrStatus, pvValue);
                }

                return hr;
            }
        }
        #endregion

        #region IMFMediaStream
        public HResult GetMediaSource(out IMFMediaSource ppMediaSource)
        {
            HResult hr = HResult.S_OK;
            ppMediaSource = null;

            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    ppMediaSource = _spSource;
                }

                return hr;
            }
        }

        public HResult GetStreamDescriptor(out IMFStreamDescriptor ppStreamDescriptor)
        {
            HResult hr = HResult.S_OK;
            ppStreamDescriptor = null;

            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    ppStreamDescriptor = _spStreamDescriptor;
                }

                return hr;
            }
        }

        public HResult RequestSample(object pToken)
        {
            lock (_spSource)
            {
                try
                {
                    ThrowIfError(CheckShutdown());

                    if (_eSourceState != SourceState.SourceState_Started)
                    {
                        // We cannot be asked for a sample unless we are in started state
                        Throw(HResult.MF_E_INVALIDREQUEST);
                    }

                    // Put token onto the list to return it when we have a sample ready
                    _tokens.Enqueue(pToken);

                    // Trigger sample delivery
                    DeliverSamples();
                }
                catch (Exception ex)
                {
                    HandleError(ex.HResult);
                }

                return HResult.S_OK;
            }
        }
        #endregion

        internal HResult SetActive(bool fActive)
        {
            lock (_spSource)
            {
                HResult hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    if (_eSourceState != SourceState.SourceState_Stopped && _eSourceState != SourceState.SourceState_Started)
                    {
                        hr = HResult.MF_E_INVALIDREQUEST;
                    }
                }

                if (MFError.Succeeded(hr))
                {
                    _fActive = fActive;
                }

                return hr;
            }
        }

        public HResult Start()
        {
            HResult hr = HResult.S_OK;
            lock (_spSource)
            {
                hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    if (_eSourceState == SourceState.SourceState_Stopped ||
                        _eSourceState == SourceState.SourceState_Started)
                    {
                        _eSourceState = SourceState.SourceState_Started;
                        // Inform the client that we've started
                        hr = QueueEvent(MediaEventType.MEStreamStarted, Guid.Empty, HResult.S_OK, null);
                    }
                    else
                    {
                        hr = HResult.MF_E_INVALID_STATE_TRANSITION;
                    }
                }

                if (MFError.Failed(hr))
                {
                    HandleError(hr);
                }

                return HResult.S_OK;
            }
        }

        public HResult Stop()
        {
            HResult hr = HResult.S_OK;
            lock (_spSource)
            {
                hr = CheckShutdown();
                if (MFError.Succeeded(hr))
                {
                    if (_eSourceState == SourceState.SourceState_Started)
                    {
                        _eSourceState = SourceState.SourceState_Stopped;
                        _tokens.Clear();
                        _samples.Clear();
                        // Inform the client that we've stopped.
                        hr = QueueEvent(MediaEventType.MEStreamStopped, Guid.Empty, HResult.S_OK, null);
                    }
                    else
                    {
                        hr = HResult.MF_E_INVALID_STATE_TRANSITION;
                    }
                }

                if (MFError.Failed(hr))
                {
                    HandleError(hr);
                }

                return HResult.S_OK;
            }
        }

        public HResult Flush()
        {
            lock (_spSource)
            {
                _tokens.Clear();
                _samples.Clear();

                _fDiscontinuity = false;
                _eDropMode = MFQualityDropMode.None;
                ResetDropTime();

                return HResult.S_OK;
            }
        }

        public HResult Shutdown()
        {
            lock (_spSource)
            {
                HResult hr = CheckShutdown();

                if (MFError.Succeeded(hr))
                {
                    Flush();
                    if (null != _spEventQueue)
                    {
                        _spEventQueue.Shutdown();
                    }

                    _spStreamDescriptor = null;
                    _eSourceState = SourceState.SourceState_Shutdown;
                }

                return hr;
            }
        }

        private void ResetDropTime()
        {
            _fDropTime = false;
            _fInitDropTime = false;
            _hnsStartDroppingAt = 0;
            _hnsAmountToDrop = 0;
            _fWaitingForCleanPoint = true;
        }

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

        private void HandleError(HResult hErrorCode)
        {
            if (hErrorCode != HResult.MF_E_SHUTDOWN)
            {
                // Send MEError to the client
                hErrorCode = QueueEvent(MediaEventType.MEError, Guid.Empty, hErrorCode, null);
            }
        }

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