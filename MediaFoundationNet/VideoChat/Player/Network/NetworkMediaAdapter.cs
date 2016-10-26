using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using VideoPlayer.Utils;

namespace VideoPlayer.Network
{
    public class NetworkMediaAdapter : INetworkMediaAdapter
    {
        private bool _isAutoStart;
        private INetworkClient _networkSender;

        public bool IsAutoStart
        {
            get
            {
                return _isAutoStart;
            }
            set
            {
                _isAutoStart = value;
            }
        }

        public event OnMediaSampleReceivedEventHandler OnMediaSampleReceived;
        public event OnMediaHeaderReceivedEventHandler OnMediaHeaderReceived;

        public event OnOperationRequestReceivedEventHandler OnOperationRequestReceived;

        public NetworkMediaAdapter()
        {
            _isAutoStart = true;
            _networkSender = new NetworkClient();
            _networkSender.OnPacketReceived += _networkSender_OnPacketReceived;
        }

        private void _networkSender_OnPacketReceived(VideoStream_Operation option, IBufferPacket packet)
        {
            try
            {
                switch (option)
                {
                    case VideoStream_Operation.StspOperation_ServerDescription:
                        invokeMediaheaderEvent(packet);
                        if (_isAutoStart)
                        {
                            SendStartRequest();
                        }
                        break;
                    case VideoStream_Operation.StspOperation_ServerSample:
                        invokeMediaSampleEvent(packet);
                        break;
                    default:
                        invokeOperationRequestEvent(option, packet);
                        break;
                }
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private void invokeMediaheaderEvent(IBufferPacket packet)
        {
            var arg = new MediaHeaderEventArgs
            {
                MediaHeaders = createMediaHeader(packet)
            };

            OnMediaHeaderReceived?.Invoke(arg);
        }

        private MediaHeader[] createMediaHeader(IBufferPacket data)
        {
            var dataLen = data.GetLength();
            int descSize = Marshal.SizeOf(typeof(VideoStream_Description));
            int streamDescSize = Marshal.SizeOf(typeof(VideoStream_StreamDescription));

            // Copy description  
            var desc = BytesHelper.TakeObject<VideoStream_Description>(data);
            // Size of the packet should match size described in the packet (size of Description structure + size of attribute blob)
            var cbConstantSize = Convert.ToInt32(descSize + (desc.StreamCount - 1) * streamDescSize);
            // Check if the input parameters are valid. We only support 2 streams.
            if (cbConstantSize < Marshal.SizeOf(desc) || desc.StreamCount == 0 || desc.StreamCount > 2 || dataLen < cbConstantSize)
            {
                ThrowIfError(HResult.MF_E_UNSUPPORTED_FORMAT);
            }

            try
            {
                List<VideoStream_StreamDescription> streamDescs = new List<VideoStream_StreamDescription>(desc.StreamDescriptions);

                for (int i = 1; i < desc.StreamCount; i++)
                {
                    var sd = BytesHelper.TakeObject<VideoStream_StreamDescription>(data);
                    streamDescs.Add(sd);
                }

                int cbAttributeSize = 0;
                for (int i = 0; i < desc.StreamCount; ++i)
                {
                    cbAttributeSize += streamDescs[i].cbAttributesSize;
                }

                // Validate the parameters. Limit the total size of attributes to 64kB.
                if ((dataLen != (cbConstantSize + cbAttributeSize)) || (cbAttributeSize > 0x10000))
                {
                    Throw(HResult.MF_E_UNSUPPORTED_FORMAT);
                }

                MediaHeader[] headerData = new MediaHeader[desc.StreamCount];

                //only init for the first video stream.
                for (int i = 0; i < streamDescs.Count; i++)
                {
                    headerData[i] = createMediaHeader(streamDescs[i], data);
                }

                return headerData;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        private MediaHeader createMediaHeader(VideoStream_StreamDescription pStreamDescription, IBufferPacket attributesBuffer)
        {
            MediaHeader result = new MediaHeader
            {
                StreamId = pStreamDescription.dwStreamId,

                MajorType = pStreamDescription.guiMajorType,
                SubType = pStreamDescription.guiSubType,

                IsVideo = pStreamDescription.guiMajorType == MFMediaType.Video,

                VideoWidth = 0,
                VideoHeight = 0,
                VideoRatioD = 1,
                VideoRatioN = 1
            };

            IMFMediaType mediaType;

            //Create a media type object.
            ThrowIfError(MFExtern.MFCreateMediaType(out mediaType));

            if (attributesBuffer.GetLength() < pStreamDescription.cbAttributesSize || pStreamDescription.cbAttributesSize == 0)
            {
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
            ThrowIfError(mediaType.SetGUID(MFGuids.MF_MT_MAJOR_TYPE, pStreamDescription.guiMajorType));
            ThrowIfError(mediaType.SetGUID(MFGuids.MF_MT_SUBTYPE, pStreamDescription.guiSubType));

            if (result.IsVideo)
            {
                ThrowIfError(MFExtern.MFGetAttributeSize(mediaType, MFAttributesClsid.MF_MT_FRAME_SIZE, out result.VideoWidth, out result.VideoHeight));
                ThrowIfError(MFExtern.MFGetAttributeRatio(mediaType, MFAttributesClsid.MF_MT_PIXEL_ASPECT_RATIO, out result.VideoRatioN, out result.VideoRatioD));
            }
            result.MediaType = mediaType;

            return result;
        }

        private void invokeMediaSampleEvent(IBufferPacket packet)
        {
            var arg = new MediaSampleEventArgs
            {
                Sample = createSample(packet)
            };

            OnMediaSampleReceived?.Invoke(arg);
        }

        private IMFSample createSample(IBufferPacket packet)
        {
            // Only process samples when we are in started state
            VideoStream_SampleHeader sampleHead;

            // Copy the header object
            sampleHead = BytesHelper.TakeObject<VideoStream_SampleHeader>(packet);
            if (packet.GetLength() < 0)
            {
                ThrowIfError(HResult.E_INVALIDARG);
            }

            // Convert packet to MF sample
            IMFSample spSample;
            ThrowIfError(ToMFSample(packet, out spSample));
            SetSampleAttributes(sampleHead, spSample);

            return spSample;
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
            hr = BytesHelper.ConverToMediaBuffer(packet, out mediaBuffer);
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

        private void SetSampleAttributes(VideoStream_SampleHeader sampleHeader, IMFSample sample)
        {
            ThrowIfError(sample.SetSampleTime(sampleHeader.ullTimestamp));
            ThrowIfError(sample.SetSampleDuration(sampleHeader.ullDuration));

            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_BottomFieldFirst, SampleFlag.StspSampleFlag_BottomFieldFirst);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_CleanPoint, SampleFlag.StspSampleFlag_CleanPoint);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_DerivedFromTopField, SampleFlag.StspSampleFlag_DerivedFromTopField);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_Discontinuity, SampleFlag.StspSampleFlag_Discontinuity);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_Interlaced, SampleFlag.StspSampleFlag_Interlaced);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_RepeatFirstField, SampleFlag.StspSampleFlag_RepeatFirstField);
            SET_SAMPLE_ATTRIBUTE(sampleHeader, sample, MFGuids.MFSampleExtension_SingleField, SampleFlag.StspSampleFlag_SingleField);
        }

        void SET_SAMPLE_ATTRIBUTE(VideoStream_SampleHeader sampleHeader, IMFSample pSample, Guid flag, SampleFlag flagValue)
        {
            var value = (int)flagValue;
            if ((value & sampleHeader.dwFlagMasks) == value)
            {
                ThrowIfError(pSample.SetUINT32(flag, ((value & sampleHeader.dwFlags) == value) ? 1 : 0));
            }
        }

        private void invokeOperationRequestEvent(VideoStream_Operation option, IBufferPacket packet)
        {
            var arg = new MediaOperationEventArgs
            {
                Operation = option,
                Data = packet
            };
            OnOperationRequestReceived?.Invoke(arg);
        }

        public HResult Open(string ip, int port)
        {
            try
            {
                ThrowIfError(CheckShutdown());

                _networkSender.Connect(ip, port);
                _networkSender.Start();
                SendDescribeRequest();

                return HResult.S_OK;
            }
            catch (SocketException sex)
            {
                switch (sex.NativeErrorCode)
                {
                    case 10060:
                        return HResult.MF_E_NET_TIMEOUT;
                    case 10061:
                        return HResult.MF_E_NET_REDIRECT;
                    default:
                        return HResult.MF_E_NETWORK_RESOURCE_FAILURE;
                }
            }
            catch (Exception ex)
            {
                return HResult.MF_E_NETWORK_RESOURCE_FAILURE;
            }
        }

        public HResult Close()
        {
            HResult hr = HResult.S_OK;
            _networkSender.Close();
            return hr;
        }

        public HResult CheckShutdown()
        {
            return HResult.S_OK;
        }

        public void SendStartRequest()
        {
            SendRequest(VideoStream_Operation.StspOperation_ClientRequestStart);
        }

        public void SendDescribeRequest()
        {
            SendRequest(VideoStream_Operation.StspOperation_ClientRequestDescription);
        }

        public void SendRequest(VideoStream_Operation operation)
        {
            var bytes = BytesHelper.BuildOperationBytes(operation);
            _networkSender.Send(bytes);
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
