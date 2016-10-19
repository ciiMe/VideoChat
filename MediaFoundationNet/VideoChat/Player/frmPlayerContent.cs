using System;
using System.Windows.Forms;
using System.Diagnostics;
using MediaFoundation;

namespace VideoPlayer
{
    public partial class frmPlayerContent : Form, IPlayer
    {
        const int WM_PAINT = 0x000F;
        const int WM_SIZE = 0x0005;
        const int WM_ERASEBKGND = 0x0014;
        const int WM_CHAR = 0x0102;
        const int WM_SETCURSOR = 0x0020;
        const int WM_APP = 0x8000;

        const int WM_APP_NOTIFY = WM_APP + 1;   // wparam = state
        const int WM_APP_ERROR = WM_APP + 2;    // wparam = HRESULT

        private BasePlayer _player;
        private bool _isRepaintClient = true;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_PAINT:
                    OnPaint(m.HWnd);
                    base.WndProc(ref m);
                    break;

                case WM_SIZE:
                    _player.ResizeVideo((short)(m.LParam.ToInt32() & 65535), (short)(m.LParam.ToInt32() >> 16));
                    break;

                case WM_CHAR:
                    //OnKeyPress(m.WParam.ToInt32());
                    break;

                case WM_SETCURSOR:
                    m.Result = new IntPtr(1);
                    break;

                case WM_APP_NOTIFY:
                    UpdateUI(m.HWnd, (BasePlayer.PlayerState)m.WParam);
                    break;

                case WM_APP_ERROR:
                    Text = "An error occurred:" + DateTime.Now.ToString("HH:mm:ss.fff");
                    UpdateUI(m.HWnd, BasePlayer.PlayerState.Ready);
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        public frmPlayerContent()
        {
            InitializeComponent();
            _player = new BasePlayer(Handle, Handle);
        }

        private void OnPaint(IntPtr hwnd)
        {
            if (!_isRepaintClient)
            {
                // Video is playing. Ask the player to repaint.
                _player.Repaint();
            }
        }

        private void UpdateUI(IntPtr hwnd, BasePlayer.PlayerState state)
        {
            bool isWaiting = false;
            bool isPlayback = false;

            Debug.Assert(_player != null);

            switch (state)
            {
                case BasePlayer.PlayerState.OpenPending:
                    isWaiting = true;
                    break;

                case BasePlayer.PlayerState.Started:
                    isPlayback = true;
                    break;

                case BasePlayer.PlayerState.Paused:
                    isPlayback = true;
                    break;

                case BasePlayer.PlayerState.PausePending:
                    isWaiting = true;
                    isPlayback = true;
                    break;

                case BasePlayer.PlayerState.StartPending:
                    isWaiting = true;
                    isPlayback = true;
                    break;
            }

            if (isWaiting)
            {
                Cursor.Current = Cursors.WaitCursor;
            }
            else
            {
                Cursor.Current = Cursors.Default;
            }

            if (isPlayback && _player.HasVideo())
            {
                _isRepaintClient = false;
            }
            else
            {
                _isRepaintClient = true;
            }
        }

        public HResult Open(string url)
        {
            var hr = _player.Open(url);

            if (hr >= 0)
            {
                UpdateUI(Handle, BasePlayer.PlayerState.OpenPending);
            }
            else
            {
                Text = "Could not open the file.";
                UpdateUI(Handle, BasePlayer.PlayerState.Ready);
            }

            return hr;
        }

        public HResult Open(string ip, int port)
        {
            var hr = _player.Open(ip, port);

            if (hr >= 0)
            {
                UpdateUI(Handle, BasePlayer.PlayerState.OpenPending);
            }
            else
            {
                Text = "Could not open the file.";
                UpdateUI(Handle, BasePlayer.PlayerState.Ready);
            }

            return hr;
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Cursor.Current = Cursors.Default;

            if (_player != null)
            {
                _player.Shutdown();
                _player = null;
            }
        }
    }
}