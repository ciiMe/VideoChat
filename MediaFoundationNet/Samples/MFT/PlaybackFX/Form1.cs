/****************************************************************************
While the underlying library is covered by LGPL or BSD, this sample is released
as public domain.  It is distributed in the hope that it will be useful, but 
WITHOUT ANY WARRANTY; without even the implied warranty of MERCHANTABILITY 
or FITNESS FOR A PARTICULAR PURPOSE.  
*****************************************************************************/

using System;
using System.Drawing;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading;

using MediaFoundation.Misc;
using MediaFoundation;

namespace MF_BasicPlayback
{
    public partial class Form1 : Form
    {
        const int WM_APP = 0x8000;
        const int WM_APP_NOTIFY = WM_APP + 1;   // wparam = state

        private CPlayer g_pPlayer;
        private bool g_bRepaintClient = true;

        protected override void WndProc(ref Message m)
        {
            switch (m.Msg)
            {
                case WM_APP_NOTIFY:
                    if (m.LParam != IntPtr.Zero)
                    {
                        NotifyError("An error occurred.", (HResult)m.LParam);
                    }
                    else
                    {
                        UpdateUI((CPlayer.PlayerState)m.WParam);
                    }
                    break;

                default:
                    base.WndProc(ref m);
                    break;
            }
        }

        public Form1()
        {
            InitializeComponent();

            g_pPlayer = new CPlayer(this.Handle);
        }

        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HResult hr = 0;

            openFileDialog1.Filter = "Windows Media|*.wmv;*.wma;*.asf;*.mov|Wave|*.wav|MP3|*.mp3|All files|*.*";

            // File dialog windows must be on STA threads.  ByteStream handlers are happier if
            // they are opened on MTA.  So, the application stays MTA and we call OpenFileDialog
            // on its own thread.
            Invoker I = new Invoker(openFileDialog1);

            // Show the File Open dialog.
            if (I.Invoke() == DialogResult.OK)
            {
                // Open the file with the playback object.
                //openFileDialog1.FileName = "C:\\sourceforge\\mflib\\Test\\Media\\Welcome.wavx";
                hr = g_pPlayer.OpenURL(openFileDialog1.FileName);

                if (hr >= 0)
                {
                    UpdateUI(CPlayer.PlayerState.OpenPending);
                }
                else
                {
                    NotifyError("Could not open the file.", hr);
                }
            }
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Cursor.Current = Cursors.Default;

            if (g_pPlayer != null)
            {
                g_pPlayer.Shutdown();
                g_pPlayer = null;
            }
        }

        private void openUrlToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HResult hr;

            fmURL f = new fmURL();

            if (f.ShowDialog(this) == DialogResult.OK)
            {
                // Open the file with the playback object.
                hr = g_pPlayer.OpenURL(f.tbURL.Text);

                if (hr >= 0)
                {
                    UpdateUI(CPlayer.PlayerState.OpenPending);
                }
                else
                {
                    NotifyError("Could not open this URL.", hr);
                }
            }
        }

        private void VideoToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = sender as ToolStripMenuItem;
            if (!mi.Checked)
            {
                foreach (object o in mi.GetCurrentParent().Items)
                {
                    ToolStripMenuItem tsi = o as ToolStripMenuItem;
                    if (tsi != null)
                    {
                        tsi.Checked = false;
                    }
                }
                string s = mi.Tag as string;
                Guid g;
                if (!string.IsNullOrEmpty(s))
                    g = new Guid(s);
                else
                    g = Guid.Empty;

                g_pPlayer.Settings(g);
                mi.Checked = true;
            }
        }

        private void AudioToolStripMenuItem_Click(object sender, EventArgs e)
        {
            ToolStripMenuItem mi = sender as ToolStripMenuItem;
            foreach (object o in mi.GetCurrentParent().Items)
            {
                ToolStripMenuItem tsi = o as ToolStripMenuItem;
                if (tsi != null)
                {
                    tsi.Checked = false;
                }
            }

            string s = mi.Tag as string;
            Guid g;

            if (!string.IsNullOrEmpty(s))
                g = new Guid(s);
            else
                g = Guid.Empty;


            if (mi.Text == "Disabled")
            {
                g_pPlayer.Settings(true, g);
            }
            else if (mi.Text == "Normal")
            {
                g_pPlayer.Settings(false, g);
            }
            else
            {
                g_pPlayer.Settings(false, g);
            }
            mi.Checked = true;
        }

        private void Form1_Paint(object sender, PaintEventArgs e)
        {
            if (!g_bRepaintClient)
            {
                // Video is playing. Ask the player to repaint.
                g_pPlayer.Repaint();
            }
            else
            {
                this.BackColor = Color.Black;
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {
            Control c = sender as Control;
            g_pPlayer.ResizeVideo((short)c.Width, (short)c.Height);
        }

        private void Form1_KeyPress(object sender, KeyPressEventArgs e)
        {
            switch (e.KeyChar)
            {
                // Space key toggles between running and paused
                case ' ':
                    e.Handled = true;
                    if (g_pPlayer.GetState() == CPlayer.PlayerState.Started)
                    {
                        g_pPlayer.Pause();
                    }
                    else if (g_pPlayer.GetState() == CPlayer.PlayerState.Paused)
                    {
                        g_pPlayer.Play();
                    }
                    break;
                case 'r':
                    e.Handled = true;
                    if (rotateAsyncToolStripMenuItem.Checked)
                    {
                        IMFAttributes ia = g_pPlayer.GetVideoAttributes();
                        if (ia != null)
                        {
                            Guid ClsidRotate = new Guid("AC776FB5-858F-4891-A5DC-FD01E79B5AD6");
                            int i;
                            HResult hr;
                            hr = ia.GetUINT32(ClsidRotate, out i);
                            if (hr >= 0)
                            {
                                i = i < 7 ? i + 1 : 0;
                                hr = ia.SetUINT32(ClsidRotate, i);
                            }
                            System.Runtime.InteropServices.Marshal.ReleaseComObject(ia);
                        }
                    }
                    break;
            }
        }

        void UpdateUI(CPlayer.PlayerState state)
        {
            bool bWaiting = false;
            bool bPlayback = false;
            bool bBuilt = false;

            Debug.Assert(g_pPlayer != null);

            switch (state)
            {
                case CPlayer.PlayerState.OpenPending:
                    bBuilt = true;
                    bWaiting = true;
                    break;

                case CPlayer.PlayerState.Started:
                    bBuilt = true;
                    bPlayback = true;
                    break;

                case CPlayer.PlayerState.Paused:
                    bBuilt = true;
                    bPlayback = true;
                    break;

                case CPlayer.PlayerState.PausePending:
                    bBuilt = true;
                    bWaiting = true;
                    bPlayback = true;
                    break;

                case CPlayer.PlayerState.StartPending:
                    bBuilt = true;
                    bWaiting = true;
                    bPlayback = true;
                    break;

                case CPlayer.PlayerState.Ready:
                    bBuilt = false;
                    break;
            }

            bool uEnable = !bWaiting;

            openToolStripMenuItem.Enabled = uEnable;
            openToolStripMenuItem.Enabled = uEnable;
            openUrlToolStripMenuItem.Enabled = uEnable;

            videoEffectToolStripMenuItem.Enabled = !bBuilt;
            audioToolStripMenuItem.Enabled = !bBuilt;

            if (bWaiting)
            {
                Cursor.Current = Cursors.WaitCursor;
            }
            else
            {
                Cursor.Current = Cursors.Default;
            }

            if (bPlayback && g_pPlayer.HasVideo())
            {
                g_bRepaintClient = false;
            }
            else
            {
                g_bRepaintClient = true;
            }
        }

        void NotifyError(string sErrorMessage, HResult hrErr)
        {
            string s;
            const int REGDB_E_CLASSNOTREG = unchecked((int)0x80040154);

            if (hrErr != (HResult)REGDB_E_CLASSNOTREG)
            {
                s = string.Format("{0} (HRESULT = 0x{1:x} {2})", sErrorMessage, hrErr, MFError.GetErrorText(hrErr));
            }
            else
            {
                string e;

                if (Environment.Is64BitProcess)
                {
                    e = "64";
                }
                else
                {
                    e = "32";
                }

                s = string.Format("The CLSID for that MFT is not registered for {0}bit.  Please rebuild/reregister.", e);
            }

            MessageBox.Show(this, s, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            UpdateUI(CPlayer.PlayerState.Ready);
        }

    }

    /// <summary>
    /// Opens a specified FileOpenDialog box on an STA thread
    /// </summary>
    public class Invoker
    {
        private OpenFileDialog m_Dialog;
        private DialogResult m_InvokeResult;
        private Thread m_InvokeThread;

        // Constructor is passed the dialog to use
        public Invoker(OpenFileDialog Dialog)
        {
            m_InvokeResult = DialogResult.None;
            m_Dialog = Dialog;

            // No reason to waste a thread if we aren't MTA
            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.MTA)
            {
                m_InvokeThread = new Thread(new ThreadStart(InvokeMethod));
                m_InvokeThread.SetApartmentState(ApartmentState.STA);
            }
            else
            {
                m_InvokeThread = null;
            }
        }

        // Start the thread and get the result
        public DialogResult Invoke()
        {
            if (m_InvokeThread != null)
            {
                m_InvokeThread.Start();
                m_InvokeThread.Join();
            }
            else
            {
                m_InvokeResult = m_Dialog.ShowDialog();
            }

            return m_InvokeResult;
        }

        // The thread entry point
        private void InvokeMethod()
        {
            m_InvokeResult = m_Dialog.ShowDialog();
        }
    }

}