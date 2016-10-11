using Windows.Foundation;
using Windows.Media.Capture;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using System;
using System.Threading.Tasks;
using Windows.Networking.Sockets;
using System.IO;
using Windows.Networking;
using PhotoCapture.Common;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace PhotoCapture
{
    public sealed partial class CapturePhoto : Page
    {
        private Windows.Foundation.Collections.IPropertySet appSettings;
        private const String photoKey = "capturedPhoto";

        private List<Size> _listedRatio;
        private List<CameraCaptureUIMaxPhotoResolution> _listedSize;

        public CapturePhoto()
        {
            InitializeComponent();
            appSettings = ApplicationData.Current.LocalSettings.Values;
            fillParameters();
        }

        private void fillParameters()
        {
            NotifyUser("Filling parameters...", NotifyType.StatusMessage);
            cmbRatio.Items.Clear();
            cmbRatio.Items.Add("4:3");
            cmbRatio.Items.Add("16:9");
            cmbRatio.Items.Add("16:10");
            cmbRatio.SelectedIndex = 1;

            _listedRatio = new List<Size>();
            _listedRatio.Add(new Size(4, 3));
            _listedRatio.Add(new Size(16, 9));
            _listedRatio.Add(new Size(16, 10));

            cmbSize.Items.Clear();
            cmbSize.Items.Add("1024 x 768");
            cmbSize.Items.Add("1920 x 1080");
            cmbSize.Items.Add("2560 x 1920");
            cmbSize.SelectedIndex = 2;

            _listedSize = new List<CameraCaptureUIMaxPhotoResolution>();
            _listedSize.Add(CameraCaptureUIMaxPhotoResolution.MediumXga);
            _listedSize.Add(CameraCaptureUIMaxPhotoResolution.Large3M);
            _listedSize.Add(CameraCaptureUIMaxPhotoResolution.VeryLarge5M);
        }

        protected override async void OnNavigatedTo(NavigationEventArgs e)
        {
            if (appSettings.ContainsKey(photoKey))
            {
                object filePath;
                if (appSettings.TryGetValue(photoKey, out filePath) && filePath.ToString() != "")
                {
                    CaptureButton.IsEnabled = false;
                    await ReloadPhoto(filePath.ToString());
                    CaptureButton.IsEnabled = true;
                }
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
        }

        private async void CapturePhoto_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                NotifyUser("Capture Starting...", NotifyType.StatusMessage);
                var dialog = new CameraCaptureUI();
                dialog.PhotoSettings.MaxResolution = _listedSize[cmbSize.SelectedIndex];
                dialog.PhotoSettings.Format = CameraCaptureUIPhotoFormat.Jpeg;
                dialog.PhotoSettings.AllowCropping = false;

                if (tAllotCut.IsChecked.Value)
                {
                    dialog.PhotoSettings.AllowCropping = true;
                    dialog.PhotoSettings.CroppedAspectRatio = _listedRatio[cmbRatio.SelectedIndex];
                }
                else
                {
                    dialog.PhotoSettings.AllowCropping = false;
                }

                //dialog.PhotoSettings.CroppedAspectRatio = _listedRatio[cmbRatio.SelectedIndex];

                StorageFile file = await dialog.CaptureFileAsync(CameraCaptureUIMode.Photo);
                if (file != null)
                {
                    BitmapImage bitmapImage = new BitmapImage();
                    using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                    {
                        bitmapImage.SetSource(fileStream);
                    }
                    CapturedPhoto.Source = bitmapImage;

                    appSettings[photoKey] = file.Path;

                    NotifyUser("Photo is saved at:" + file.Path, NotifyType.StatusMessage);

                    var StreamRandom = await file.OpenAsync(FileAccessMode.Read);
                    pushToServer(StreamRandom);
                    StreamRandom.Dispose();
                }
                else
                {
                    NotifyUser("No photo captured.", NotifyType.StatusMessage);
                }
            }
            catch (Exception ex)
            {
                NotifyUser(ex.Message, NotifyType.ErrorMessage);
            }
        }

        private async void pushToServer(IRandomAccessStream randomstream)
        {
            var sourceStream = randomstream.GetInputStreamAt(0).AsStreamForRead();
            try
            {
                var socket = new StreamSocket();
                await socket.ConnectAsync(new HostName(ConstParameters.VideoStreamReceiverHost), ConstParameters.VideoStreamReceiverPort.ToString());

                //Write data to the echo server.
                Stream streamOut = socket.OutputStream.AsStreamForWrite();

                var buffer = new byte[16 * 1024];

                int read;
                while ((read = sourceStream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    streamOut.Write(buffer, 0, read);
                    streamOut.Flush();
                }
                streamOut.Dispose();
                sourceStream.Dispose();
                socket.Dispose();
            }
            catch (Exception e)
            {
                NotifyUser(e.Message, NotifyType.ErrorMessage);
            }
        }

        private async Task ReloadPhoto(String filePath)
        {
            try
            {
                StorageFile file = await StorageFile.GetFileFromPathAsync(filePath);
                BitmapImage bitmapImage = new BitmapImage();
                using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
                {
                    bitmapImage.SetSource(fileStream);
                }
                CapturedPhoto.Source = bitmapImage;
                NotifyUser("", NotifyType.StatusMessage);
            }
            catch (Exception ex)
            {
                appSettings.Remove(photoKey);
                NotifyUser(ex.Message, NotifyType.ErrorMessage);
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

        private void tAllotCut_PointerPressed(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            cmbRatio.Visibility = Visibility.Visible;
        }

        private void tAllotCut_PointerReleased(object sender, Windows.UI.Xaml.Input.PointerRoutedEventArgs e)
        {
            cmbRatio.Visibility = Visibility.Collapsed;
        }

        private void tAllotCut_Clicked(object sender, RoutedEventArgs e)
        {
            if (tAllotCut.IsChecked.Value)
            {
                cmbRatio.Visibility = Visibility.Visible;
            }
            else
            {
                cmbRatio.Visibility = Visibility.Collapsed;
            }
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
