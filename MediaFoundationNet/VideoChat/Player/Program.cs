using System;
using System.Windows.Forms;
using VideoPlayer.MediaSource;

namespace VideoPlayer
{
    static class Program
    {
        static IPlayerUI _playerUi;

        [MTAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var frm = new frmPlayerContent();
            frm.Load += Frm_Load;
            _playerUi = frm;

            NetworkSource source = new NetworkSource();
            source.Open("192.168.13.210", 10010);

            Application.Run(frm);
        }

        private static void Frm_Load(object sender, EventArgs e)
        {
            var fileDialog = new OpenFileDialog();
            var dialog = new DialogInvoker(fileDialog);
            if (DialogResult.OK == dialog.Invoke())
            {
                _playerUi.Open(fileDialog.FileName);
            }
            (_playerUi as Form).Activate();
        }
    }
}