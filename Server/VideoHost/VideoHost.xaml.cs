using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using System;

using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.Media.MediaProperties;
using SimpleCommunication.Common;
using Microsoft.Samples.SimpleCommunication;
using Windows.Media.Capture;
using Windows.UI.Xaml.Media;

namespace SimpleCommunication
{
    public sealed partial class VideoHost : Page
    {
        private const uint VideoPreviewMinWidth = 600;
        private const uint VideoRecordMinWidth = 1200;
        private const string VideoSourceSubType = "YUY2";
        VideoEncodingProperties previewEncodingProperties;
        VideoEncodingProperties recordEncodingProperties;
        
        CaptureDevice device = null;
        bool? roleIsActive = null;
        int isTerminator = 0;
        bool activated = false;

        public VideoHost()
        {
            InitializeComponent();
        }

        private void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                case NotifyType.StatusMessage:
                    msgBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    msgBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }
            msgContent.Text = strMessage;

            if (msgContent.Text != string.Empty)
            {
                msgBorder.Visibility = Visibility.Visible;
            }
            else
            {
                msgBorder.Visibility = Visibility.Collapsed;
            }
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            var cameraFound = await CaptureDevice.CheckForRecordingDeviceAsync();

            if (cameraFound)
            {
                device = new CaptureDevice();
                await InitializeAsync();
                device.IncomingConnectionArrived += device_IncomingConnectionArrived;
                device.CaptureFailed += device_CaptureFailed;
                RemoteVideo.MediaFailed += RemoteVideo_MediaFailed;
            }
            else
            {
                NotifyUser("A machine with a camera and a microphone is required to run this sample.", NotifyType.ErrorMessage);
            }
        }

        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            if (activated)
            {
                RemoteVideo.Stop();
                RemoteVideo.Source = null;
            }

            await device.CleanUpAsync();
            device = null;
        }

        private async Task InitializeAsync(CancellationToken cancel = default(CancellationToken))
        {
            NotifyUser("Initializing...", NotifyType.StatusMessage);

            try
            {
                await device.InitializeAsync();

                var rSetting = await device.SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoRecord, ((x) =>
                {
                    var previewStreamEncodingProperty = x as VideoEncodingProperties;

                    return (previewStreamEncodingProperty.Width >= VideoRecordMinWidth &&
                        previewStreamEncodingProperty.Subtype == VideoSourceSubType);
                }));
                recordEncodingProperties = rSetting as VideoEncodingProperties;

                await StartRecordToCustomSink();

                RemoteVideo.Source = null;

                roleIsActive = false;
                Interlocked.Exchange(ref isTerminator, 0);

                NotifyUser("Tap 'Call' button to start call", NotifyType.StatusMessage);
            }
            catch (Exception)
            {
                NotifyUser("Initialization error. Restart the sample to try again.", NotifyType.ErrorMessage);
            }
        }

        async void RemoteVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref isTerminator, 1, 0) == 0)
            {
                await EndCallAsync();
            }
        }

        async void device_IncomingConnectionArrived(object sender, IncomingConnectionEventArgs e)
        {
            e.Accept();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() =>
            {
                activated = true;
                NotifyUser("Connected. Remote machine address: " + e.RemoteUrl.Replace("stsp://", ""), NotifyType.StatusMessage);
            }));
        }

        async void device_CaptureFailed(object sender, MediaCaptureFailedEventArgs e)
        {
            if (Interlocked.CompareExchange(ref isTerminator, 1, 0) == 0)
            {
                await EndCallAsync();
            }
        }

        private async Task StartRecordToCustomSink()
        {
            MediaEncodingProfile mep = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p);
            mep.Video.FrameRate.Numerator = 15;
            mep.Video.FrameRate.Denominator = 1;
            mep.Container = null;

            await device.StartRecordingAsync(mep);
        }

        private async Task EndCallAsync()
        {
            await device.CleanUpAsync();

            // end the call session
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() =>
            {
                RemoteVideo.Stop();
                RemoteVideo.Source = null;
            }));

            // Start waiting for a new call
            await InitializeAsync();
        }
    }
}
