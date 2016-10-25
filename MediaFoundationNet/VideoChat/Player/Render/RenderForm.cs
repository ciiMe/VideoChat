using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoPlayer.MediaSource;
using VideoPlayer.Stream;

namespace VideoPlayer.Render
{
    public partial class RenderForm : Form, IPlayer
    {
        private Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

        INetworkMediaAdapter _networkStreamAdapter;
        private int _videoStreamId;
        private IMFStreamDescriptor _videoStreamDescriptor;
        private IMFMediaType _videoMediaType;
        private int _videoWitdh, _videoHeight;
        private int _videoRatioN, _videoRatioD;

        DrawDevice _drawDevice;
        H264Decoder _decoder;

        public RenderForm()
        {
            InitializeComponent();

            _drawDevice = new DrawDevice();
            ThrowIfError(_drawDevice.CreateDevice(Handle));

            _networkStreamAdapter = new NetworkMediaAdapter();
            _networkStreamAdapter.OnDataArrived += _networkStreamAdapter_OnDataArrived;

            _decoder = new H264Decoder();
            _decoder.OnSampleDecodeComplete += _decoder_OnSampleDecodeComplete;
        }

        private void _decoder_OnSampleDecodeComplete(object sender, IMFMediaBuffer buffer)
        {
            //todo: check status. closed or stopped...
            _drawDevice.DrawFrame(buffer);
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
                        _decoder.initialize(_videoStreamId, _videoMediaType);
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
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

            _drawDevice.InitializeSetVideoSize(_videoWitdh, _videoHeight, new MFRatio(_videoRatioN, _videoRatioD));
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

            _decoder.ProcessSample(spSample);
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
