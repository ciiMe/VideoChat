using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Windows.Forms;
using VideoPlayer.Stream;

namespace VideoPlayer.Render
{
    public partial class RenderForm : Form, IPlayer
    {
        private Guid MF_MT_MAJOR_TYPE = Guid.Parse("48eba18e-f8c9-4687-bf11-0a74c9f96a8f");
        private Guid MF_MT_SUBTYPE = Guid.Parse("f7e34c9a-42e8-4714-b74b-cb29d72c35e5");

        INetworkMediaAdapter _networkStreamAdapter;

        private int _videoStreamId;
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
            _networkStreamAdapter.OnMediaHeaderReceived += _networkStreamAdapter_OnMediaHeaderReceived;
            _networkStreamAdapter.OnMediaSampleReceived += _networkStreamAdapter_OnMediaSampleReceived;
            _networkStreamAdapter.OnOperationRequestReceived += _networkStreamAdapter_OnOperationRequestReceived;

            _decoder = new H264Decoder();
            _decoder.OnSampleDecodeComplete += _decoder_OnSampleDecodeComplete;
        }

        private void _networkStreamAdapter_OnMediaHeaderReceived(MediaHeaderEventArgs arg)
        {
            foreach (var header in arg.MediaHeaders)
            {
                if (!header.IsVideo)
                {
                    continue;
                }

                _videoStreamId = header.StreamId;
                _videoMediaType = header.MediaType;

                _videoWitdh = header.VideoWidth;
                _videoHeight = header.VideoHeight;
                _videoRatioD = header.VideoRatioD;
                _videoRatioN = header.VideoRatioN;

                _drawDevice.InitializeSetVideoSize(_videoWitdh, _videoHeight, new MFRatio(_videoRatioN, _videoRatioD));
                _decoder.initialize(_videoStreamId, _videoMediaType);

                break;
            }
        }

        private void _networkStreamAdapter_OnMediaSampleReceived(MediaSampleEventArgs arg)
        {
            _decoder.ProcessSample(arg.Sample);
        }

        private void _networkStreamAdapter_OnOperationRequestReceived(MediaOperationEventArgs arg)
        {
            //todo: add more action handlers.
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
