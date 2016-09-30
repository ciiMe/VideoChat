using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using System;
using SimpleCommunication.Common;
using Windows.Media;
using Windows.UI.Xaml.Media;

namespace SimpleCommunication
{
    public sealed partial class VideoGuest : Page
    {
        MediaExtensionManager mediaExtensionMgr;

        public VideoGuest()
        {
            InitializeComponent();
            EnsureMediaExtensionManager();
            RemoteVideo.MediaFailed += RemoteVideo_MediaFailed;
        }

        public void EnsureMediaExtensionManager()
        {
            if (mediaExtensionMgr == null)
            {
                mediaExtensionMgr = new MediaExtensionManager();
                mediaExtensionMgr.RegisterSchemeHandler("Microsoft.Samples.SimpleCommunication.StspSchemeHandler", "stsp:");
            }
        }

        void RemoteVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            NotifyUser("RemoteVideo_MediaFailed:" + e.ErrorMessage, NotifyType.ErrorMessage);
        }

        private void Call_Click(object sender, RoutedEventArgs e)
        {
            NotifyUser("", NotifyType.StatusMessage);

            var address = HostName.Text;

            if (!string.IsNullOrEmpty(address))
            {
                RemoteVideo.Source = new Uri("stsp://" + address);
                NotifyUser("Initiating connection... Please wait.", NotifyType.StatusMessage);
            }
        }

        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            StatusBlock.Text = strMessage;
             
            if (StatusBlock.Text != string.Empty)
            {
                StatusBorder.Visibility = Visibility.Visible;
            }
            else
            {
                StatusBorder.Visibility = Visibility.Collapsed;
            }
        }
    }
}
