using Windows.UI.Xaml.Controls;
using System;

namespace SDKTemplate
{
    public partial class MainPage : Page
    {
        public bool DeviceChecked = false;
        public bool CameraFound = false;

        Windows.Media.MediaExtensionManager mediaExtensionMgr;

        public void EnsureMediaExtensionManager()
        {
            if (mediaExtensionMgr == null)
            {
                mediaExtensionMgr = new Windows.Media.MediaExtensionManager();
                mediaExtensionMgr.RegisterSchemeHandler("Microsoft.Samples.SimpleCommunication.StspSchemeHandler", "stsp:");
            }
        }
    }

    public class Scenario
    {
        public string Title { get; set; }

        public Type ClassType { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }
}
