using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Runtime.InteropServices;

namespace VideoPlayer
{
    public enum StspSampleFlags
    {
        StspSampleFlag_BottomFieldFirst,
        StspSampleFlag_CleanPoint,
        StspSampleFlag_DerivedFromTopField,
        StspSampleFlag_Discontinuity,
        StspSampleFlag_Interlaced,
        StspSampleFlag_RepeatFirstField,
        StspSampleFlag_SingleField,
    };

    public struct StspOperationHeader
    {
        public int cbDataSize;
        public StspOperation eOperation;
    };

    public enum StspOperation
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
    
    public struct StspStreamDescription
    {
        public Guid guiMajorType;
        public Guid guiSubType;
        public int dwStreamId;
        public int cbAttributesSize;
    };

    public struct StspDescription
    {
        public uint cNumStreams;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 1)]
        public StspStreamDescription[] aStreams;
    };

    // Possible states of the stsp source object
    public enum SourceState
    {
        // Invalid state, source cannot be used 
        SourceState_Invalid,
        // Opening the connection
        SourceState_Opening,
        // Streaming started
        SourceState_Starting,
        // Streaming started
        SourceState_Started,
        // Streanung stopped
        SourceState_Stopped,
        // Source is shut down
        SourceState_Shutdown,
    };

    public enum SourceOperationType
    {
        // Start the source
        Operation_Start,
        // Stop the source
        Operation_Stop,
        // Set rate
        Operation_SetRate,
    };

    public struct CSourceOperation
    {
        public SourceOperationType Type;
        public IMFPresentationDescriptor PresentationDescriptor;
        public ConstPropVariant Data;
    }
}
