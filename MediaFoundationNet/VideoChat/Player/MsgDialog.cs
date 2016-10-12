using MediaFoundation.Misc;
using System.Windows.Forms;

namespace VideoPlayer
{
    public static class MsgDialog
    {
        public static void NotifyError(IWin32Window parentWindow, string errorMessage, int errorHr)
        {
            string s = string.Format("{0} (HRESULT = 0x{1:x} {2})", errorMessage, errorHr, MFError.GetErrorText(errorHr));
            MessageBox.Show(parentWindow, s, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
