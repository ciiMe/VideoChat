using System;
using System.Runtime.InteropServices;

namespace VideoPlayer
{
    public struct VideoStream_SampleHeader
    {
        public int dwStreamId;
        public long ullTimestamp;
        public long ullDuration;
        public int dwFlags;
        public int dwFlagMasks;
    };

    public enum SampleFlag
    {
        StspSampleFlag_BottomFieldFirst,
        StspSampleFlag_CleanPoint,
        StspSampleFlag_DerivedFromTopField,
        StspSampleFlag_Discontinuity,
        StspSampleFlag_Interlaced,
        StspSampleFlag_RepeatFirstField,
        StspSampleFlag_SingleField,
    };

    public struct VideoStream_OperationHeader
    {
        public int cbDataSize;
        public VideoStream_Operation eOperation;
    };

    public enum VideoStream_Operation
    {
        StspOperation_Unknown,
        StspOperation_ClientRequestDescription,
        StspOperation_ClientRequestStart,
        StspOperation_ClientRequestStop,
        StspOperation_ServerDescription,
        StspOperation_ServerSample,
        StspOperation_ServerFormatChange,
        StspOperation_Last,
    };
    
    public struct VideoStream_StreamDescription
    {
        public Guid guiMajorType;
        public Guid guiSubType;
        public int dwStreamId;
        public int cbAttributesSize;
    };

    public struct VideoStream_Description
    {
        public uint StreamCount;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public VideoStream_StreamDescription[] StreamDescriptions;
    };
}
