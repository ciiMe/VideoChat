using System;

namespace VideoPlayer
{
    public static class MFGuids
    {
        public static Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        public static Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

        public static Guid MF_MT_FRAME_SIZE = new Guid(0x1652c33d, 0xd6b2, 0x4012, 0xb8, 0x34, 0x72, 0x03, 0x08, 0x49, 0xa3, 0x7d);
        public static Guid MF_MT_FRAME_RATE = new Guid(0xc459a2e8, 0x3d2c, 0x4e44, 0xb1, 0x32, 0xfe, 0xe5, 0x15, 0x6c, 0x7b, 0xb0);
        public static Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid(0xc6376a1e, 0x8d0a, 0x4027, 0xbe, 0x45, 0x6d, 0x9a, 0x0a, 0xd3, 0x9b, 0xb6);
        public static Guid MF_MT_INTERLACE_MODE = new Guid(0xe2724bb8, 0xe676, 0x4806, 0xb4, 0xb2, 0xa8, 0xd6, 0xef, 0xb4, 0x4c, 0xcd);

        public static Guid CLSID_CMSH264DecoderMFT = new Guid("62CE7E72-4C71-4d20-B15D-452831A87D9D");

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
        public static Guid MFSampleExtension_BottomFieldFirst = new Guid(0x941ce0a3, 0x6ae3, 0x4dda, 0x9a, 0x08, 0xa6, 0x42, 0x98, 0x34, 0x06, 0x17);

        // MFSampleExtension_CleanPoint {9cdf01d8-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample is a clean point (key frame), and decoding can begin at this sample.
        public static Guid MFSampleExtension_CleanPoint = new Guid(0x9cdf01d8, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // {6852465a-ae1c-4553-8e9b-c3420fcb1637}   MFSampleExtension_DerivedFromTopField       {UINT32 (BOOL)}
        public static Guid MFSampleExtension_DerivedFromTopField = new Guid(0x6852465a, 0xae1c, 0x4553, 0x8e, 0x9b, 0xc3, 0x42, 0x0f, 0xcb, 0x16, 0x37);

        // MFSampleExtension_Discontinuity {9cdf01d9-a0f0-43ba-b077-eaa06cbd728a}
        // Type: UINT32
        // If present and nonzero, indicates that the sample data represents the first
        // sample following a discontinuity (gap) in the stream of samples.
        // This can happen, for instance, if the previous sample was lost in
        // transmission.
        public static Guid MFSampleExtension_Discontinuity = new Guid(0x9cdf01d9, 0xa0f0, 0x43ba, 0xb0, 0x77, 0xea, 0xa0, 0x6c, 0xbd, 0x72, 0x8a);

        // {b1d5830a-deb8-40e3-90fa-389943716461}   MFSampleExtension_Interlaced                {UINT32 (BOOL)}
        public static Guid MFSampleExtension_Interlaced = new Guid(0xb1d5830a, 0xdeb8, 0x40e3, 0x90, 0xfa, 0x38, 0x99, 0x43, 0x71, 0x64, 0x61);

        // {304d257c-7493-4fbd-b149-9228de8d9a99}   MFSampleExtension_RepeatFirstField          {UINT32 (BOOL)}
        public static Guid MFSampleExtension_RepeatFirstField = new Guid(0x304d257c, 0x7493, 0x4fbd, 0xb1, 0x49, 0x92, 0x28, 0xde, 0x8d, 0x9a, 0x99);

        // {9d85f816-658b-455a-bde0-9fa7e15ab8f9}   MFSampleExtension_SingleField               {UINT32 (BOOL)}
        public static Guid MFSampleExtension_SingleField = new Guid(0x9d85f816, 0x658b, 0x455a, 0xbd, 0xe0, 0x9f, 0xa7, 0xe1, 0x5a, 0xb8, 0xf9);
        #endregion
    }
}
