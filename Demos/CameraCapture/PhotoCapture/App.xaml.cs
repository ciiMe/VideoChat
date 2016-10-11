using PhotoCapture;
using PhotoCapture.Common;
using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace SDKTemplate
{
    sealed partial class App : Application
    {
        public App()
        {
            this.InitializeComponent();
            this.Suspending += OnSuspending;
        }

        private Frame CreateRootFrame()
        {
            Frame rootFrame = Window.Current.Content as Frame;

            // Do not repeat app initialization when the Window already has content, 
            // just ensure that the window is active 
            if (rootFrame == null)
            {
                // Create a Frame to act as the navigation context and navigate to the first page 
                rootFrame = new Frame();

                // Set the default language 
                rootFrame.Language = Windows.Globalization.ApplicationLanguages.Languages[0];
                rootFrame.NavigationFailed += OnNavigationFailed;

                SuspensionManager.RegisterFrame(rootFrame, "AppFrame");

                // Place the frame in the current Window 
                Window.Current.Content = rootFrame;
            }

            return rootFrame;
        }

        void OnNavigationFailed(object sender, NavigationFailedEventArgs e)
        {
            throw new Exception("Failed to load Page " + e.SourcePageType.FullName);
        }

        private async void RestoreStatus(ApplicationExecutionState previousExecutionState)
        {
            // Do not repeat app initialization when the Window already has content, 
            // just ensure that the window is active 
            if (previousExecutionState == ApplicationExecutionState.Terminated)
            {
                // Restore the saved session state only when appropriate 
                try
                {
                    await SuspensionManager.RestoreAsync();
                }
                catch (SuspensionManagerException)
                {
                    //Something went wrong restoring state. 
                    //Assume there is no state and continue 
                }
            }
        }

        protected override void OnFileActivated(FileActivatedEventArgs e)
        {
            Frame rootFrame = CreateRootFrame();
            RestoreStatus(e.PreviousExecutionState);

            if (rootFrame.Content == null)
            {
                if (!rootFrame.Navigate(typeof(CapturePhoto)))
                {
                    throw new Exception("Failed to create initial page");
                }
            }
            Window.Current.Activate();
        }

        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = CreateRootFrame();
            RestoreStatus(e.PreviousExecutionState);

            //MainPage is always in rootFrame so we don't have to worry about restoring the navigation state on resume 
            rootFrame.Navigate(typeof(CapturePhoto), e.Arguments);
            Window.Current.Activate();
        }

        protected override void OnActivated(IActivatedEventArgs e)
        {
            if (e.Kind == ActivationKind.Protocol)
            {
                ProtocolActivatedEventArgs protocolArgs = e as ProtocolActivatedEventArgs;
                Frame rootFrame = CreateRootFrame();
                RestoreStatus(e.PreviousExecutionState);

                if (rootFrame.Content == null)
                {
                    if (!rootFrame.Navigate(typeof(CapturePhoto)))
                    {
                        throw new Exception("Failed to create initial page");
                    }
                    try
                    {
                        ConstParameters.VideoStreamReceiverHost = protocolArgs.Uri.Host;
                        ConstParameters.VideoStreamReceiverPort = protocolArgs.Uri.Port;
                    }
                    catch
                    {
                        throw new Exception("Invalid start protocol.");
                    }

                }
                Window.Current.Activate();
            }
        }

        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await SuspensionManager.SaveAsync();
            deferral.Complete();
        }
    }
}
