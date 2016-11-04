using MediaFoundation;
using MediaFoundation.Misc;
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using VideoPlayer.Network;
using VideoPlayer.Utils;

namespace VideoPlayer.Render
{
    public partial class RenderForm : Form, IPlayer
    {
        INetworkMediaAdapter _networkStreamAdapter;

        private int _videoStreamId;
        private IMFMediaType _videoMediaType;
        private int _videoWitdh, _videoHeight;
        private int _videoRatioN, _videoRatioD;

        private H264Decoder _decoder;
        private byte[] _lastSampleBuffer;

        private DrawDevice _drawDevice;

        public bool IsStarted => _networkStreamAdapter.IsStarted;

        public RenderForm()
        {
            InitializeComponent();

            _drawDevice = new DrawDevice();
            ThrowIfError(_drawDevice.Initialize(Handle));

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

                _drawDevice.InitializeRenderSize(_videoWitdh, _videoHeight, new MFRatio(_videoRatioN, _videoRatioD));
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
            //cache last sample, used for resize.
            BytesHelper.ConvertToByteArray(buffer, out _lastSampleBuffer);
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
            _drawDevice.Destroy();
        }

        private void RenderForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            Stop();
        }

        private void RenderForm_Resize(object sender, EventArgs e)
        {
            IMFSample sample;
            if (MFError.Failed(BytesHelper.ConvertToSample(_lastSampleBuffer, out sample)))
            {
                return;
            }

            if (MFError.Failed(_drawDevice.FitVideoClientSize()))
            {
                return;
            }

            _decoder.ProcessSample(sample);
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
