using System.Threading;
using System.Windows.Forms;

namespace VideoPlayer
{
    /// <summary>
    /// Opens a specified FileOpenDialog box on an STA thread
    /// </summary>
    public class DialogInvoker
    {
        private OpenFileDialog m_Dialog;
        private DialogResult m_InvokeResult;
        private Thread m_InvokeThread;

        // Constructor is passed the dialog to use
        public DialogInvoker(OpenFileDialog Dialog)
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
