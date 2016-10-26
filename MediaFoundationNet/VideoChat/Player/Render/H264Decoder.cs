using System;
using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;
using System.Runtime.InteropServices;

namespace VideoPlayer.Render
{
    enum CLSCTX
    {
        CLSCTX_INPROC_SERVER = 0x1,
        CLSCTX_INPROC_HANDLER = 0x2,
        CLSCTX_LOCAL_SERVER = 0x4,
        CLSCTX_INPROC_SERVER16 = 0x8,
        CLSCTX_REMOTE_SERVER = 0x10,
        CLSCTX_INPROC_HANDLER16 = 0x20,
        CLSCTX_RESERVED1 = 0x40,
        CLSCTX_RESERVED2 = 0x80,
        CLSCTX_RESERVED3 = 0x100,
        CLSCTX_RESERVED4 = 0x200,
        CLSCTX_NO_CODE_DOWNLOAD = 0x400,
        CLSCTX_RESERVED5 = 0x800,
        CLSCTX_NO_CUSTOM_MARSHAL = 0x1000,
        CLSCTX_ENABLE_CODE_DOWNLOAD = 0x2000,
        CLSCTX_NO_FAILURE_LOG = 0x4000,
        CLSCTX_DISABLE_AAA = 0x8000,
        CLSCTX_ENABLE_AAA = 0x10000,
        CLSCTX_FROM_DEFAULT_CONTEXT = 0x20000,
        CLSCTX_ACTIVATE_32_BIT_SERVER = 0x40000,
        CLSCTX_ACTIVATE_64_BIT_SERVER = 0x80000,
        CLSCTX_ENABLE_CLOAKING = 0x100000,
        CLSCTX_APPCONTAINER = 0x400000,
        CLSCTX_ACTIVATE_AAA_AS_IU = 0x800000,
        //CLSCTX_PS_DLL = (int)0x80000000
    };

    public struct MFT_OUTPUT_DATA_BUFFER
    {
        public int dwStreamID;
        public IMFSample pSample;
        public int dwStatus;
        public IMFCollection pEvents;
    }

    public delegate void OnSampleDecodeCompleteEventHandler(object sender, IMFMediaBuffer buffer);

    /// <summary>
    /// H264 decoder that only supports one stream.
    /// </summary>
    public class H264Decoder
    {
        // This is H264 Decoder MFT.
        private IMFTransform pDecoderTransform = null;
        //decoded sample buffer cache objects.
        private MFTOutputDataBuffer[] _mftOutBufferContainer;
        private IMFSample _mftOutSample;

        // Note the frmae width and height need to be set based on the frame size in the MP4 file.
        private int _videoStreamId;
        private IMFMediaType _streamMediaType;

        private int _videoWitdh, _videoHeight; 
        private int _videoRatioN, _videoRatioD;

        public event OnSampleDecodeCompleteEventHandler OnSampleDecodeComplete;

        public H264Decoder()
        {
            var obj = Activator.CreateInstance(Type.GetTypeFromCLSID(MFGuids.CLSID_CMSH264DecoderMFT));
            if (obj == null)
            {
                //todo: warn no MFT is avilalbe
            }

            pDecoderTransform = obj as IMFTransform; 
        }

        public void initialize(int streamId, IMFMediaType mediaType)
        {
            //process input.
            ThrowIfError(MFExtern.MFGetAttributeSize(mediaType, MFAttributesClsid.MF_MT_FRAME_SIZE, out _videoWitdh, out _videoHeight));
            ThrowIfError(MFExtern.MFGetAttributeRatio(mediaType, MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, out _videoRatioN, out _videoRatioD));
            ThrowIfError(pDecoderTransform.SetInputType(streamId, mediaType, MFTSetTypeFlags.None));

            //process output
            IMFMediaType h264OutputType;
            ThrowIfError(MFExtern.MFCreateMediaType(out h264OutputType));
            ThrowIfError(h264OutputType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, MFMediaType.Video));
            ThrowIfError(h264OutputType.SetGUID(MFGuids.MF_MT_SUBTYPE, MFMediaType.YUY2));

            IMFAttributes attr = h264OutputType as IMFAttributes;

            ThrowIfError(MFExtern.MFSetAttributeSize(attr, MFGuids.MF_MT_FRAME_SIZE, _videoWitdh, _videoHeight));
            ThrowIfError(MFExtern.MFSetAttributeRatio(attr, MFGuids.MF_MT_FRAME_RATE, 30, 1));
            ThrowIfError(MFExtern.MFSetAttributeRatio(attr, MFGuids.MF_MT_PIXEL_ASPECT_RATIO, _videoRatioN, _videoRatioD));
            ThrowIfError(attr.SetUINT32(MFGuids.MF_MT_INTERLACE_MODE, 2));

            ThrowIfError(pDecoderTransform.SetOutputType(streamId, h264OutputType, MFTSetTypeFlags.None));

            MFTInputStatusFlags mftStatus;
            ThrowIfError(pDecoderTransform.GetInputStatus(streamId, out mftStatus));

            if (MFTInputStatusFlags.AcceptData != mftStatus)
            {
                throw new Exception("H.264 decoder MFT is not accepting data.\n");
            }

            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.CommandFlush, IntPtr.Zero));
            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.NotifyBeginStreaming, IntPtr.Zero));
            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.NotifyStartOfStream, IntPtr.Zero));

            ThrowIfError(MFExtern.MFCreateSample(out _mftOutSample));

            _mftOutBufferContainer = new MFTOutputDataBuffer[1];
            //todo:set the stream id again when receive media stream later.
            _mftOutBufferContainer[0].dwStreamID = streamId;
            _mftOutBufferContainer[0].dwStatus = 0;
            _mftOutBufferContainer[0].pEvents = null;
            _mftOutBufferContainer[0].pSample = Marshal.GetIUnknownForObject(_mftOutSample);

            _videoStreamId = streamId;
            _streamMediaType = mediaType;
        }

        //todo: put the process work to background thread to speed up.
        public HResult ProcessSample(IMFSample videoSample)
        {
            HResult hr = HResult.S_OK;

            MFTOutputStatusFlags mftOutFlags;
            MFTOutputStreamInfo StreamInfo;

            if (videoSample == null)
            {
                return hr;
            }

            pDecoderTransform.ProcessInput(0, videoSample, 0);
            pDecoderTransform.GetOutputStatus(out mftOutFlags);
            pDecoderTransform.GetOutputStreamInfo(0, out StreamInfo);

            while (true)
            {
                IMFMediaBuffer resultBuffer;
                //reset the cache buffer.
                MFExtern.MFCreateMemoryBuffer(StreamInfo.cbSize, out resultBuffer);
                _mftOutSample.RemoveAllBuffers();
                _mftOutSample.AddBuffer(resultBuffer);

                ProcessOutputStatus outputStatus;
                var mftProcessOutput = pDecoderTransform.ProcessOutput(0, 1, _mftOutBufferContainer, out outputStatus);
                if (mftProcessOutput == HResult.MF_E_TRANSFORM_NEED_MORE_INPUT)
                {
                    //continue provice input data.
                    break;
                }
                else if (_mftOutBufferContainer[0].dwStatus == MFTOutputDataBufferFlags.Incomplete)
                {
                    //todo: the decoded data include more than one samples,we need to receive all data items.
                }
                else
                {
                    IMFMediaBuffer buffer;
                    _mftOutSample.ConvertToContiguousBuffer(out buffer);
                    invokeDecodeComplete(buffer, StreamInfo.cbSize);
                }
            }

            return hr;
        }

        private void invokeDecodeComplete(IMFMediaBuffer buffer, int bufferLen)
        {
            if (buffer == null)
            {
                return;
            }

            int len = 0;
            buffer.GetCurrentLength(out len);
            if (bufferLen != len)
            {
                //todo: throw invalid data exception.
                return;
            }

            invokeDecodeCompleteEvent(buffer);
        }

        private void invokeDecodeCompleteEvent(IMFMediaBuffer buffer)
        {
            try
            {
                OnSampleDecodeComplete?.Invoke(this, buffer);
            }
            catch
            {
                //todo: throw event to notify the client.
            }
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
