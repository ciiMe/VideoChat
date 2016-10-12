using System;

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

    struct StspStreamDescription
    {
        Guid guiMajorType;
        Guid guiSubType;
        int dwStreamId;
        uint cbAttributesSize;
    };

    public struct StspDescription
    {
        uint cNumStreams;
        StspStreamDescription[] aStreams;
    };
}
