using Windows.Media.MediaProperties;
using Windows.UI.Xaml.Navigation;
using SDKTemplate;
using System;
using Windows.Media.Capture;
using System.Threading.Tasks;
using SDKTemplate.Common;
using System.Linq;
using CameraCapture80.Common;
using Windows.UI.Xaml;
using Windows.Storage;
using System.Text;
using Windows.UI.Core;
using System.Diagnostics;

namespace CameraCapture
{
    public sealed partial class BasicCapture : SDKTemplate.Common.LayoutAwarePage
    {
        private const uint VideoPreviewMinWidth = 600;
        private const uint VideoRecordMinWidth = 1200;
        private const string VideoSourceSubType = "YUY2";

        private MediaCapture m_mediaCaptureMgr;
        VideoEncodingProperties previewEncodingProperties;
        VideoEncodingProperties recordEncodingProperties;

        private bool _isRecording;
        private bool _isPostToServer;
        private bool _isTracePost;
        private StringBuilder _traceLog;

        MainPage rootPage = MainPage.Current;

        private MediaStream _mediaStream;
        private TestMediaStream _testStream;
        private StorageFile _localFile;

        public BasicCapture()
        {
            InitializeComponent();
            ScenarioInit();

            _mediaStream = new MediaStream(ConstParameters.VideoStreamReceiverHost, ConstParameters.VideoStreamReceiverPort);
            _mediaStream.OnDataWriting += _mediaStream_OnDataWriting;

            _testStream = new TestMediaStream();
            _testStream.OnSeekCalled += _testStream_OnDataWriting;
            _testStream.OnReadCalled += _testStream_OnDataWriting;
            _testStream.OnWriteCalled += _testStream_OnDataWriting;
            _testStream.OnFlushCalled += _testStream_OnDataWriting;
            _traceLog = new StringBuilder();
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            ScenarioClose();
        }

        private void ScenarioInit()
        {
            _isRecording = false;
            previewElement1.Source = null;
            ShowStatusMessage("");
        }

        private async void ScenarioClose()
        {
            await StopRecord();
        }

        private async Task<bool> initNetwork()
        {
            if (!_isPostToServer)
            {
                return true;
            }

            try
            {
                ShowStatusMessage("Initializing network connection...");
                return await _mediaStream.Connect();
            }
            catch (Exception ex)
            {
                ShowExceptionMessage("Initialize network", ex);
                return false;
            }
        }

        internal async Task<bool> initCaptureDevice()
        {
            try
            {
                ShowStatusMessage("Create capture device manager...");
                m_mediaCaptureMgr = new Windows.Media.Capture.MediaCapture();

                ShowStatusMessage("Initializing capture device manager...");
                await m_mediaCaptureMgr.InitializeAsync();

                ShowStatusMessage("Select preview Setting...");
                var pSetting = await SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoPreview, ((x) =>
                {
                    var previewStreamEncodingProperty = x as Windows.Media.MediaProperties.VideoEncodingProperties;

                    return (previewStreamEncodingProperty.Width >= VideoPreviewMinWidth &&
                        previewStreamEncodingProperty.Subtype == VideoSourceSubType);
                }));
                previewEncodingProperties = pSetting as VideoEncodingProperties;
                ShowStatusMessage("Preview Setting: " + pSetting.ToString());

                ShowStatusMessage("Select record Setting...");
                var rSetting = await SelectPreferredCameraStreamSettingAsync(MediaStreamType.VideoRecord, ((x) =>
                {
                    var previewStreamEncodingProperty = x as Windows.Media.MediaProperties.VideoEncodingProperties;

                    return (previewStreamEncodingProperty.Width >= VideoRecordMinWidth &&
                        previewStreamEncodingProperty.Subtype == VideoSourceSubType);
                }));
                recordEncodingProperties = rSetting as VideoEncodingProperties;
                ShowStatusMessage("Record Setting: " + rSetting.ToString());

                ShowStatusMessage(string.Format("Capture device initialized, preview:{0}*{1}, record:{2}*{3}.",
                    previewEncodingProperties.Width, previewEncodingProperties.Height,
                    recordEncodingProperties.Width, recordEncodingProperties.Height
                    ));

                if (m_mediaCaptureMgr.MediaCaptureSettings.VideoDeviceId != "" && m_mediaCaptureMgr.MediaCaptureSettings.AudioDeviceId != "")
                {
                    m_mediaCaptureMgr.RecordLimitationExceeded += RecordLimitationExceeded;
                    m_mediaCaptureMgr.Failed += Failed;
                    return true;
                }
                else
                {
                    return false;
                }
            }
            catch (Exception exception)
            {
                ShowStatusMessage("Fail to initialize capture device." + exception.Message);
                return false;
            }
        }

        private async Task<IMediaEncodingProperties> SelectPreferredCameraStreamSettingAsync(MediaStreamType mediaStreamType, Func<IMediaEncodingProperties, bool> filterSettings)
        {
            if (mediaStreamType == MediaStreamType.Audio || mediaStreamType == MediaStreamType.Photo)
            {
                throw new ArgumentException("mediaStreamType value of MediaStreamType.Audio or MediaStreamType.Photo is not supported", "mediaStreamType");
            }
            if (filterSettings == null)
            {
                throw new ArgumentNullException("filterSettings");
            }

            var properties = m_mediaCaptureMgr.VideoDeviceController.GetAvailableMediaStreamProperties(mediaStreamType);
            var filterredProperties = properties.Where(filterSettings)
                .OrderBy(p => (p as VideoEncodingProperties).Width)
                .ToArray();

            if (filterredProperties.Length == 0)
            {
                return null;
            }

            await m_mediaCaptureMgr.VideoDeviceController.SetMediaStreamPropertiesAsync(mediaStreamType, filterredProperties[0]);
            return filterredProperties[0];
        }

        private async void RecordLimitationExceeded(MediaCapture currentCaptureObject)
        {
            if (_isRecording)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
                {
                    ShowStatusMessage("Stopping Record on exceeding max record duration");
                    await m_mediaCaptureMgr.StopRecordAsync();
                    _isRecording = false;
                });
            }
        }

        private async void Failed(MediaCapture currentCaptureObject, MediaCaptureFailedEventArgs currentFailure)
        {
            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ShowStatusMessage("Fatal error" + currentFailure.Message);
            });
        }

        private async Task StartRecord()
        {
            if (_isRecording || !await initNetwork() || !await initCaptureDevice())
            {
                return;
            }

            await startPreview();
            await startRecord();
        }

        private async Task startPreview()
        {
            try
            {
                ShowStatusMessage("Starting preview...");
                reviewElement.Visibility = Visibility.Collapsed;
                previewElement1.Visibility = Visibility.Visible;
                previewElement1.Source = m_mediaCaptureMgr;
                await m_mediaCaptureMgr.StartPreviewAsync();
            }
            catch (Exception ex)
            {
                previewElement1.Source = null;
                ShowExceptionMessage("Start preview", ex);
            }
        }

        private async Task startRecord()
        {
            try
            {
                ShowStatusMessage("Starting record...");
                MediaEncodingProfile recordProfile = MediaEncodingProfile.CreateMp4(VideoEncodingQuality.Auto);
                recordProfile.Video.FrameRate.Numerator = 25;
                recordProfile.Video.FrameRate.Denominator = 1;
                recordProfile.Container = null;
                _mediaStream.StartSendingTime = DateTime.Now;


                if (_isPostToServer)
                {
                    await m_mediaCaptureMgr.StartRecordToStreamAsync(recordProfile, _testStream);
                }
                else
                {
                    _localFile = await KnownFolders.VideosLibrary.CreateFileAsync("MyVideo.mp4", CreationCollisionOption.GenerateUniqueName);
                    await m_mediaCaptureMgr.StartRecordToStorageFileAsync(recordProfile, _localFile);
                }
                _isRecording = true;

                btnStartRecord.IsEnabled = false;
                btnSaveLocal.IsEnabled = false;
                btnStopRecord.IsEnabled = true;

                ShowStatusMessage(string.Format("Start Record successful, preview:{0}*{1}, record:{2}*{3}.",
                    previewEncodingProperties.Width, previewEncodingProperties.Height,
                    recordEncodingProperties.Width, recordEncodingProperties.Height
                    ));
            }
            catch (Exception ex)
            {
                _isRecording = false;
                ShowExceptionMessage("Start record video", ex);
            }
        }

        private async Task StopRecord()
        {
            if (!_isRecording)
            {
                return;
            }

            try
            {
                ShowStatusMessage("Stopping Video Preview");
                //await m_mediaCaptureMgr.StopPreviewAsync();
                ShowStatusMessage("Stopping Video Record");
                await m_mediaCaptureMgr.StopRecordAsync();

                if (m_mediaCaptureMgr != null)
                {
                    ShowStatusMessage("Cleaning Preview");
                    m_mediaCaptureMgr.Dispose();
                    previewElement1.Source = null;
                    m_mediaCaptureMgr = null;
                }

                if (_isPostToServer)
                {
                    ShowStatusMessage("Closing network");
                    _mediaStream.Disconnect();
                }
                else
                {
                    ShowStatusMessage($"File is saved:{_localFile.Name}");
                    await _localFile.OpenReadAsync();

                    var stream = await _localFile.OpenAsync(FileAccessMode.Read);
                    reviewElement.Visibility = Visibility.Visible;
                    previewElement1.Visibility = Visibility.Collapsed;

                    reviewElement.AutoPlay = true;
                    reviewElement.SetSource(stream, _localFile.FileType);
                    reviewElement.Play();
                }
                _isRecording = false;

                btnStartRecord.IsEnabled = true;
                btnSaveLocal.IsEnabled = true;
                btnStopRecord.IsEnabled = false;

                ShowStatusMessage("Record stopped.");
            }
            catch (Exception ex)
            {
                ShowExceptionMessage("StopRecord", ex);
            }
        }

        internal async void btnStartRecord_Click(object sender, RoutedEventArgs e)
        {
            _isPostToServer = true;
            await StartRecord();
        }

        internal async void btnSaveLocal_Click(object sender, RoutedEventArgs e)
        {
            _isPostToServer = false;
            await StartRecord();
        }

        private async void btnStop_Click(object sender, RoutedEventArgs e)
        {
            await StopRecord();
        }

        private void ShowStatusMessage(string text)
        {
            rootPage.NotifyUser(text, NotifyType.StatusMessage);
        }

        private void ShowExceptionMessage(string context, Exception ex)
        {
            rootPage.NotifyUser("Error! " + context + ":" + ex.Message, NotifyType.ErrorMessage);
        }

        private async void btnPlayLocal_Click(object sender, RoutedEventArgs e)
        {
            var file = await KnownFolders.VideosLibrary.GetFileAsync("Local.mp4");

            var stream = await file.OpenAsync(FileAccessMode.Read);

            reviewElement.Visibility = Visibility.Visible;
            previewElement1.Visibility = Visibility.Collapsed;

            reviewElement.AutoPlay = true;
            reviewElement.SetSource(stream, file.FileType); 
        }

        private async void btnPlayVirtual_Click(object sender, RoutedEventArgs e)
        {
/* this is not working, I should enhance it later.
            var file = await KnownFolders.VideosLibrary.GetFileAsync("Local.mp4");
            var stream = await file.OpenAsync(FileAccessMode.Read);

            _testStream.SetVirtualStream(stream);

            uint seekCount = 1024;
            var buffer = new Windows.Storage.Streams.Buffer(seekCount);
            var bufferB = await _testStream.ReadAsync(buffer, seekCount, Windows.Storage.Streams.InputStreamOptions.None);
            
            reviewElement.Visibility = Visibility.Visible;
            previewElement1.Visibility = Visibility.Collapsed;

            reviewElement.AutoPlay = true;
            reviewElement.SetSource(_testStream, file.FileType); 
*/
        }

        private void TraceToServer_Checked(object sender, RoutedEventArgs e)
        {
            _isTracePost = true == TraceToServer.IsChecked;
            TraceLog.Visibility = _isTracePost ? Visibility.Visible : Visibility.Collapsed;
            if (_isTracePost)
            {
                _traceLog.Clear();
            }
        }

        private void _testStream_OnDataWriting(object sender, ref BufferWriteEventArgs args)
        {
            Debug.WriteLine($"{args.Action},{args.Position},{args.ByteLength}");
        }

        private void _mediaStream_OnDataWriting(object sender, ref BufferWriteEventArgs args)
        {
            if (!_isTracePost)
            {
                return;
            }

            args.IsAllowed = false;
            _traceLog.Append($"{args.Action},{args.Position},{args.ByteLength}");

            Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => { TraceLog.Text = _traceLog.ToString(); });
        }
    }
}
