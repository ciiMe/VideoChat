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
using System.Collections.Generic;
using System.Linq;

namespace SimpleCommunication
{
    public sealed partial class VideoHost : Page
    {
        private const string VideoSourceSubType = "YUY2";
        VideoEncodingProperties previewEncodingProperties;
        VideoEncodingProperties recordEncodingProperties;

        CaptureDevice device = null;
        bool _isRecording = false;

        List<MediaEncodingProfile> _encodingProfiles;
        List<VideoEncodingProperties> _validVideoRecordProperties;

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
            await readCameraProperties();
        }

        private async Task readCameraProperties()
        {
            var cameraFound = await CaptureDevice.CheckForRecordingDeviceAsync();
            if (cameraFound)
            {
                device = new CaptureDevice();
                await device.InitializeAsync();
                loadAllVideoProperties();
                await device.CleanUpAsync();
                device = null;
            }
            else
            {
                NotifyUser("A machine with a camera and a microphone is required to run this sample.", NotifyType.ErrorMessage);
            }
        }

        private void loadAllVideoProperties()
        {
            //we only support standard recording profiles.
            _encodingProfiles = new List<MediaEncodingProfile>();
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD1080p));
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.HD720p));
            //720*480
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Ntsc));
            //720*576
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Pal));
            //320*240
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Qvga));
            //640*480
            _encodingProfiles.Add(MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Vga));

            var properties = device.CaptureSource.VideoDeviceController.GetAvailableMediaStreamProperties(MediaStreamType.VideoRecord);
            SupportedFormat.Items.Clear();
            _validVideoRecordProperties = new List<VideoEncodingProperties>();
            foreach (var p in properties)
            {
                var pp = p as VideoEncodingProperties;

                if (pp.Subtype != VideoSourceSubType)
                {
                    continue;
                }

                if (isEncodingPropertyInProfileList(pp, _encodingProfiles))
                {
                    _validVideoRecordProperties.Add(pp);
                    SupportedFormat.Items.Add($"{pp.Width}*{pp.Height}  {Convert.ToInt32(0.5 + pp.FrameRate.Numerator / pp.FrameRate.Denominator)}FPS");
                }
            }
        }

        private bool isEncodingPropertyInProfileList(VideoEncodingProperties p, List<MediaEncodingProfile> profile)
        {
            return profile.Exists(item => item.Video.Width == p.Width && item.Video.Height == p.Height);
        }

        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            await EndRecording();
        }

        private async Task startVideoRecord(CancellationToken cancel = default(CancellationToken))
        {
            NotifyUser("Initializing...", NotifyType.StatusMessage);

            var recordFormat = _validVideoRecordProperties[SupportedFormat.SelectedIndex];

            try
            {
                var rSetting = await device.SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoRecord, ((x) =>
                {
                    var p = x as VideoEncodingProperties;
                    return (p.Width == recordFormat.Width && p.Height == recordFormat.Height && p.Subtype == recordFormat.Subtype);
                }));
                recordEncodingProperties = rSetting as VideoEncodingProperties;
                
                var pSetting = await device.SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoPreview, ((x) =>
                {
                    var p = x as VideoEncodingProperties;
                    return (p.Width <= recordFormat.Width);
                }));
                previewEncodingProperties = pSetting as VideoEncodingProperties;

                await StartRecordToCustomSink();

                NotifyUser("", NotifyType.StatusMessage);
            }
            catch (Exception)
            {
                NotifyUser("Initialization error. Restart the sample to try again.", NotifyType.ErrorMessage);
            }
        }

        private async Task StartRecordToCustomSink()
        {
            MediaEncodingProfile mep = getSelectedProfile();

            if (mep == null)
            {
                NotifyUser("No valid date is found when try to choose video record format.", NotifyType.ErrorMessage);
                await EndRecording();
            }
            previewElement.Source = device.CaptureSource;
            await device.CaptureSource.StartPreviewAsync();
            await device.StartRecordingAsync(mep);

            _isRecording = true;
        }

        private MediaEncodingProfile getSelectedProfile()
        {
            var recordFormat = _validVideoRecordProperties[SupportedFormat.SelectedIndex];
            var mep = _encodingProfiles.FirstOrDefault(p => p.Video.Width == recordFormat.Width && p.Video.Height == recordFormat.Height);
            mep.Video.FrameRate.Numerator = 15;
            mep.Video.FrameRate.Denominator = 1;
            mep.Container = null;
            return mep;
        }

        private async Task EndRecording()
        {
            if (device == null)
            {
                return;
            }
            await device.CaptureSource.StopPreviewAsync();
            await device.CleanUpAsync();
            device = null;

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() =>
            {
                previewElement.Source = null;
            }));
            _isRecording = false;
        }

        private async void StartVideo_Click(object sender, RoutedEventArgs e)
        {
            if (SupportedFormat.Items.Count == 0)
            {
                NotifyUser("No video record format is avilable.", NotifyType.ErrorMessage);
                return;
            }
            if (SupportedFormat.SelectedIndex < 0)
            {
                NotifyUser("Please select a record format.", NotifyType.ErrorMessage);
                SupportedFormat.Focus(FocusState.Pointer);
                return;
            }

            if (!await createRecordingObject())
            {
                return;
            }
            await startVideoRecord();
        }

        private async Task<bool> createRecordingObject()
        {
            var cameraFound = await CaptureDevice.CheckForRecordingDeviceAsync();
            if (cameraFound)
            {
                device = new CaptureDevice();
                await device.InitializeAsync();

                device.IncomingConnectionArrived += device_IncomingConnectionArrived;
                device.CaptureFailed += device_CaptureFailed;
                return true;
            }
            else
            {
                NotifyUser("A machine with a camera and a microphone is required to run this sample.", NotifyType.ErrorMessage);
                return false;
            }
        }

        async void device_IncomingConnectionArrived(object sender, IncomingConnectionEventArgs e)
        {
            e.Accept();

            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal, (() =>
            {
                _isRecording = true;
                NotifyUser("Connected. Remote machine address: " + e.RemoteUrl.Replace("stsp://", ""), NotifyType.StatusMessage);
            }));
        }

        async void device_CaptureFailed(object sender, MediaCaptureFailedEventArgs e)
        {
            NotifyUser("Device CaptureFailed:" + e.Message, NotifyType.ErrorMessage);
            await EndRecording();
        }

        private async void StopVideo_Click(object sender, RoutedEventArgs e)
        {
            await EndRecording();
        }
    }
}
