using System;
using System.Runtime.InteropServices;

namespace VideoPlayer
{
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

    public struct GUID
    {
        long Data1;
        short Data2;
        short Data3;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 8)]
        char[] Data4 ;
    }

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
        public StspStreamDescription[] aStreams ;
    };
}
