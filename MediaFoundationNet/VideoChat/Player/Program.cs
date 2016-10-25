using System;
using System.Windows.Forms;
using VideoPlayer.Render;

namespace VideoPlayer
{
    static class Program
    {
        [MTAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            RenderForm frm = new RenderForm();
            frm.Open("192.168.13.195", 10010);
            Application.Run(frm);
        }
    }
}