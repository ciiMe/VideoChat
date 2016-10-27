using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.Windows.Forms;
using VideoPlayer.Network;

namespace VideoPlayer.Render
{
    public partial class RenderForm : Form, IPlayer
    {
        INetworkMediaAdapter _networkStreamAdapter;

        private int _videoStreamId;
        private IMFMediaType _videoMediaType;
        private int _videoWitdh, _videoHeight;
        private int _videoRatioN, _videoRatioD;

        private DrawDevice _drawDevice;
        private H264Decoder _decoder;

        public bool IsStarted => _networkStreamAdapter.IsStarted;

        public RenderForm()
        {
            InitializeComponent();

            _drawDevice = new DrawDevice();
            ThrowIfError(_drawDevice.CreateDevice(Handle));

            _networkStreamAdapter = new NetworkMediaAdapter();
            _networkStreamAdapter.IsAutoStart = true;
            _networkStreamAdapter.OnMediaHeaderReceived += _networkStreamAdapter_OnMediaHeaderReceived;
            _networkStreamAdapter.OnMediaSampleReceived += _networkStreamAdapter_OnMediaSampleReceived;
            _networkStreamAdapter.OnOperationRequestReceived += _networkStreamAdapter_OnOperationRequestReceived;
            _networkStreamAdapter.OnException += _networkStreamAdapter_OnException;

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

                _drawDevice.Initialize(_videoWitdh, _videoHeight, new MFRatio(_videoRatioN, _videoRatioD));
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

        private void _networkStreamAdapter_OnException(ExceptionEventArg arg)
        {
            //todo: show exception in correct method...
            Text = arg.ExceptionData.Message;
        }

        public void Open(string ip, int port)
        {
            _networkStreamAdapter.Open(ip, port);
        }

        public void Start()
        {
            _networkStreamAdapter.Start();
        }

        public void Stop()
        {
            _networkStreamAdapter.Stop();
            _decoder.Release();
            _drawDevice.DestroyDevice();
        }

        private void RenderForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Stop();
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
