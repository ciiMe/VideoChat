using MediaFoundation;
using MediaFoundation.Misc;
using MediaFoundation.Transform;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using VideoPlayer.MediaSource;
using VideoPlayer.Stream;

namespace VideoPlayer.Render
{
    public partial class RenderForm : Form
    {
        private Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");
        private Guid MF_MT_FRAME_SIZE = new Guid(0x1652c33d, 0xd6b2, 0x4012, 0xb8, 0x34, 0x72, 0x03, 0x08, 0x49, 0xa3, 0x7d);
        private Guid MF_MT_FRAME_RATE = new Guid(0xc459a2e8, 0x3d2c, 0x4e44, 0xb1, 0x32, 0xfe, 0xe5, 0x15, 0x6c, 0x7b, 0xb0);
        private Guid MF_MT_PIXEL_ASPECT_RATIO = new Guid(0xc6376a1e, 0x8d0a, 0x4027, 0xbe, 0x45, 0x6d, 0x9a, 0x0a, 0xd3, 0x9b, 0xb6);
        private Guid MF_MT_INTERLACE_MODE = new Guid(0xe2724bb8, 0xe676, 0x4806, 0xb4, 0xb2, 0xa8, 0xd6, 0xef, 0xb4, 0x4c, 0xcd);

        INetworkMediaAdapter _networkStreamAdapter;
        private int _videoStreamId;
        private IMFStreamDescriptor _videoStreamDescriptor;
        private IMFMediaType _videoMediaType;

        // This is H264 Decoder MFT.
        IMFTransform pDecoderTransform = null;
        MFTOutputDataBuffer[] outputDataBuffer;
        IMFSample mftOutSample;

        // Note the frmae width and height need to be set based on the frame size in the MP4 file.
        public int _videoWitdh, _videoHeight;
        private int _videoRatioN, _videoRatioD;

        DrawDevice _drawDevice;
        private readonly Guid CLSID_CMSH264DecoderMFT = new Guid("62CE7E72-4C71-4d20-B15D-452831A87D9D");

        public RenderForm()
        {
            InitializeComponent();

            _drawDevice = new DrawDevice();
            ThrowIfError(_drawDevice.CreateDevice(Handle));

            _networkStreamAdapter = new NetworkMediaAdapter();
            _networkStreamAdapter.OnDataArrived += _networkStreamAdapter_OnDataArrived;

            initH264Decode();
        }

        private void initH264Decode()
        {
            var obj = Activator.CreateInstance(Type.GetTypeFromCLSID(CLSID_CMSH264DecoderMFT));
            if (obj == null)
            {
                //todo: warn no MFT is avilalbe
            }

            pDecoderTransform = obj as IMFTransform;
        }

        public void Open(string ip, int port)
        {
            _networkStreamAdapter.Open(ip, port);
        }

        private void _networkStreamAdapter_OnDataArrived(StspOperation option, IBufferPacket packet)
        {
            switch (option)
            {
                case StspOperation.StspOperation_ServerDescription:
                    ProcessServerDescription(packet);
                    _networkStreamAdapter.SendStartRequest();
                    break;
                case StspOperation.StspOperation_ServerSample:
                    ProcessServerSample(packet);
                    break;
            }
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

                //only init for the first video stream.
                foreach (var sd in streamDescs)
                {
                    if (sd.guiMajorType == MFMediaType.Video)
                    {
                        initVideoDesctriptor(sd, data);
                        initH264Decoder();
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private HResult initH264Decoder()
        {
            var hr = HResult.S_OK;
            hr = pDecoderTransform.SetInputType(_videoStreamId, _videoMediaType, MFTSetTypeFlags.None);
            ThrowIfError(hr);

            IMFMediaType h264OutputType;
            ThrowIfError(MFExtern.MFCreateMediaType(out h264OutputType));
            ThrowIfError(h264OutputType.SetGUID(MF_MT_MAJOR_TYPE, MFMediaType.Video));
            ThrowIfError(h264OutputType.SetGUID(MF_MT_SUBTYPE, MFMediaType.YUY2));

            IMFAttributes attr = h264OutputType as IMFAttributes;

            MFExtern.MFSetAttributeSize(attr, MF_MT_FRAME_SIZE, _videoWitdh, _videoHeight);
            MFExtern.MFSetAttributeRatio(attr, MF_MT_FRAME_RATE, 30, 1);
            MFExtern.MFSetAttributeRatio(attr, MF_MT_PIXEL_ASPECT_RATIO, _videoRatioN, _videoRatioD);
            attr.SetUINT32(MF_MT_INTERLACE_MODE, 2);

            hr = pDecoderTransform.SetOutputType(_videoStreamId, h264OutputType, MFTSetTypeFlags.None);

            MFTInputStatusFlags mftStatus;
            ThrowIfError(pDecoderTransform.GetInputStatus(_videoStreamId, out mftStatus));

            if (MFTInputStatusFlags.AcceptData != mftStatus)
            {
                throw new Exception("H.264 decoder MFT is not accepting data.\n");
            }

            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.CommandFlush, IntPtr.Zero));
            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.NotifyBeginStreaming, IntPtr.Zero));
            ThrowIfError(pDecoderTransform.ProcessMessage(MFTMessageType.NotifyStartOfStream, IntPtr.Zero));

            MFExtern.MFCreateSample(out mftOutSample);

            outputDataBuffer = new MFTOutputDataBuffer[1];
            //todo:set the stream id again when receive media stream later.
            outputDataBuffer[0].dwStreamID = _videoStreamId;
            outputDataBuffer[0].dwStatus = 0;
            outputDataBuffer[0].pEvents = null;
            outputDataBuffer[0].pSample = Marshal.GetIUnknownForObject(mftOutSample);

            return hr;
        }

        private void initVideoDesctriptor(StspStreamDescription pStreamDescription, IBufferPacket attributesBuffer)
        {
            IMFMediaType mediaType;
            IMFStreamDescriptor spSD;
            IMFMediaTypeHandler spMediaTypeHandler;

            //Create a media type object.
            ThrowIfError(MFExtern.MFCreateMediaType(out mediaType));

            if (attributesBuffer.GetLength() < pStreamDescription.cbAttributesSize || pStreamDescription.cbAttributesSize == 0)
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

            ThrowIfError(mediaType.SetGUID(MF_MT_MAJOR_TYPE, pStreamDescription.guiMajorType));
            ThrowIfError(mediaType.SetGUID(MF_MT_SUBTYPE, pStreamDescription.guiSubType));

            //Now we can create MF stream descriptor.
            ThrowIfError(MFExtern.MFCreateStreamDescriptor(pStreamDescription.dwStreamId, 1, new IMFMediaType[] { mediaType }, out spSD));
            ThrowIfError(spSD.GetMediaTypeHandler(out spMediaTypeHandler));

            //Set current media type
            ThrowIfError(spMediaTypeHandler.SetCurrentMediaType(mediaType));

            _videoMediaType = mediaType;
            _videoStreamDescriptor = spSD;
            _videoStreamId = pStreamDescription.dwStreamId;

            ThrowIfError(MFExtern.MFGetAttributeSize(mediaType, MFAttributesClsid.MF_MT_FRAME_SIZE, out _videoWitdh, out _videoHeight));
            ThrowIfError(MFExtern.MFGetAttributeRatio(mediaType, MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, out _videoRatioN, out _videoRatioD));

            _drawDevice.SetVideoType(_videoWitdh, _videoHeight, new MFRatio(_videoRatioN, _videoRatioD));
        }

        private void ProcessServerSample(IBufferPacket packet)
        {
            // Only process samples when we are in started state
            StspSampleHeader sampleHead;

            // Copy the header object
            sampleHead = StreamConvertor.TakeObject<StspSampleHeader>(packet);
            if (packet.GetLength() < 0)
            {
                ThrowIfError(HResult.E_INVALIDARG);
            }

            if (sampleHead.dwStreamId != _videoStreamId)
            {
                return;
            }

            // Convert packet to MF sample
            IMFSample spSample;
            ThrowIfError(ToMFSample(packet, out spSample));
            SetSampleAttributes(sampleHead, spSample);

            ThrowIfError(DecodeAndDispatch(spSample));
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

        private void SetSampleAttributes(StspSampleHeader sampleHeader, IMFSample sample)
        {
            ThrowIfError(sample.SetSampleTime(sampleHeader.ullTimestamp));
            ThrowIfError(sample.SetSampleDuration(sampleHeader.ullDuration));
        }

        void SET_SAMPLE_ATTRIBUTE(StspSampleHeader sampleHeader, IMFSample pSample, Guid flag, StspSampleFlags flagValue)
        {
            var value = (int)flagValue;
            if ((value & sampleHeader.dwFlagMasks) == value)
            {
                ThrowIfError(pSample.SetUINT32(flag, ((value & sampleHeader.dwFlags) == value) ? 1 : 0));
            }
        }

        public struct MFT_OUTPUT_DATA_BUFFER
        {
            public int dwStreamID;
            public IMFSample pSample;
            public int dwStatus;
            public IMFCollection pEvents;
        }

        public HResult DecodeAndDispatch(IMFSample videoSample)
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
                ProcessOutputStatus outputStatus;
                var mftProcessOutput = pDecoderTransform.ProcessOutput(0, 1, outputDataBuffer, out outputStatus);
                if (mftProcessOutput == HResult.MF_E_TRANSFORM_NEED_MORE_INPUT)
                {
                    break;
                }
                else
                {
                    IMFMediaBuffer buffer;
                    //todo: how can it be null???
                    mftOutSample.ConvertToContiguousBuffer(out buffer);
                    invokeDecodeComplete(buffer);
                }
            }

            return hr;
        }

        private void invokeDecodeComplete(IMFMediaBuffer buffer)
        {
            _drawDevice.DrawFrame(buffer);
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
