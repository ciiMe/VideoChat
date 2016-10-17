using System;
using System.Windows.Forms;
using VideoPlayer.MediaSource;

namespace VideoPlayer
{
    static class Program
    {
        static IPlayer _playerUi;

        [MTAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            var frm = new frmPlayerContent();
            frm.Load += Frm_Load;
            _playerUi = frm;

            Application.Run(frm);
        }

        private static void Frm_Load(object sender, EventArgs e)
        {
            _playerUi.Open("192.168.13.210", 10010);
            (_playerUi as Form).Activate();
        }
    }
}