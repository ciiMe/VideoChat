using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace VideoPlayer.WindowsExtern
{
    public static class Win32
    {
        public const int WM_PAINT = 0x000F;
        public const int WM_SIZE = 0x0005;
        public const int WM_ERASEBKGND = 0x0014;
        public const int WM_CHAR = 0x0102;
        public const int WM_SETCURSOR = 0x0020;
        public const int WM_APP = 0x8000;

        [DllImport("user32")]
        public extern static int PostMessage(IntPtr handle, int msg, IntPtr wParam, IntPtr lParam);

        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        public static extern bool GetClientRect(IntPtr hWnd, out Rectangle lpRect); 
    }
}
