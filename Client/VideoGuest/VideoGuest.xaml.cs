using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using SDKTemplate;
using System;

namespace SimpleCommunication
{
    public sealed partial class VideoGuest : Page
    {
        MainPage rootPage = MainPage.Current;
        
        public VideoGuest()
        {
            InitializeComponent();
            rootPage.EnsureMediaExtensionManager();
            RemoteVideo.MediaFailed += RemoteVideo_MediaFailed;
        }

        void RemoteVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            rootPage.NotifyUser("RemoteVideo_MediaFailed:" + e.ErrorMessage, NotifyType.ErrorMessage);
        }
        
        private void Call_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            var address = HostName.Text;

            if (b != null && !string.IsNullOrEmpty(address))
            { 
                RemoteVideo.Source = new Uri("stsp://" + address); 
                rootPage.NotifyUser("Initiating connection... Please wait.", NotifyType.StatusMessage);
            }
        } 
    }
}
