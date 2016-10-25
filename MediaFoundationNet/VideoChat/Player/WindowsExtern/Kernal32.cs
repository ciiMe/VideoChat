using System.Runtime.InteropServices;

namespace VideoPlayer.WindowsExtern
{
    public static class Kernal32
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        public static extern int MulDiv(int nNumber, int nNumerator, int nDenominator);
    }
}
