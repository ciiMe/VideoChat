﻿using MediaFoundation;
using System;

namespace VideoPlayer.Network
{
    public struct MediaDescriptionHeader
    {
        public int StreamId;

        public IMFMediaType MediaType;

        //extra fields that we can read from MediaType, set them just for easy to use.
        public Guid MajorType;
        public Guid SubType;

        public bool IsVideo;
        public int VideoWidth;
        public int VideoHeight;
        public int VideoRatioD;
        public int VideoRatioN;
    }

    public class MediaHeaderEventArgs : EventArgs
    {
        public MediaDescriptionHeader[] MediaHeaders;
    }

    public class MediaSampleEventArgs : EventArgs
    {
        public IMFSample Sample;
    }

    public class MediaOperationEventArgs : EventArgs
    {
        public VideoStream_Operation Operation;
        public IBufferPacket Data;
    }

    public class ExceptionEventArg : EventArgs
    {
        public Exception ExceptionData;
    }

    public delegate void OnMediaHeaderReceivedEventHandler(MediaHeaderEventArgs arg);
    public delegate void OnMediaSampleReceivedEventHandler(MediaSampleEventArgs arg);
    public delegate void OnOperationRequestReceivedEventHandler(MediaOperationEventArgs arg);

    public delegate void OnExceptionEventHandler(ExceptionEventArg arg);

    public interface INetworkMediaAdapter : IStopable
    {
        /// <summary>
        /// Will be invoked when sample data received from server.
        /// </summary>
        event OnMediaSampleReceivedEventHandler OnMediaSampleReceived;

        /// <summary>
        /// Will be invoked when header data is received. Media header data is always received before media stream data, 
        /// we should init the plaver or render in this event to make sure it's ready for the next coming media strem data.
        /// </summary>
        event OnMediaHeaderReceivedEventHandler OnMediaHeaderReceived;

        /// <summary>
        /// Operation request is received.
        /// </summary>
        event OnOperationRequestReceivedEventHandler OnOperationRequestReceived;

        /// <summary>
        /// Will be raised when exception is catched.
        /// </summary>
        event OnExceptionEventHandler OnException;

        /// <summary>
        /// Set true to call SendStartRequest() automatically after the OnMediaSampleReceived event is handled,
        /// or you have to send the start request manuly.
        /// </summary>
        bool IsAutoStart { get; set; }

        HResult Open(string ip, int port);

        void SendStartRequest();
        void SendDescribeRequest();
        void SendRequest(VideoStream_Operation eOperation);
    }
}
