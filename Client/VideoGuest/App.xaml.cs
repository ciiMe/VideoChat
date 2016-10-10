using System;
using Windows.ApplicationModel;
using Windows.ApplicationModel.Activation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using SDKTemplate.Common;
using SimpleCommunication;

namespace SDKTemplate
{
    /// <summary>
    /// Provides application-specific behavior to supplement the default Application class.
    /// </summary>
    sealed partial class App : Application
    {
        /// <summary>
        /// Initializes the singleton Application object.  This is the first line of authored code
        /// executed, and as such is the logical equivalent of main() or WinMain().
        /// </summary>
        public App()
        {
            InitializeComponent();
            Suspending += OnSuspending;
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
        
        /// <summary> 
        /// Invoked when Navigation to a certain page fails 
        /// </summary> 
        /// <param name="sender">The Frame which failed navigation</param> 
        /// <param name="e">Details about the navigation failure</param> 
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

        /// <summary> 
        // Handle file activations. 
        /// </summary> 
        protected override void OnFileActivated(FileActivatedEventArgs e)
        {
            Frame rootFrame = CreateRootFrame();
            RestoreStatus(e.PreviousExecutionState);

            if (rootFrame.Content == null)
            {
                if (!rootFrame.Navigate(typeof(VideoGuest)))
                {
                    throw new Exception("Failed to create initial page");
                }
            }

            var p = rootFrame.Content as VideoGuest;
 
            // Ensure the current window is active 
            Window.Current.Activate();
        }

        /// <summary> 
        /// Invoked when the application is launched normally by the end user.  Other entry points 
        /// will be used such as when the application is launched to open a specific file. 
        /// </summary> 
        /// <param name="e">Details about the launch request and process.</param> 
        protected override void OnLaunched(LaunchActivatedEventArgs e)
        {
            Frame rootFrame = CreateRootFrame();
            RestoreStatus(e.PreviousExecutionState);

            //MainPage is always in rootFrame so we don't have to worry about restoring the navigation state on resume 
            rootFrame.Navigate(typeof(VideoGuest), e.Arguments);

            // Ensure the current window is active 
            Window.Current.Activate();
        }

        /// <summary> 
        // Handle protocol activations and continuation activations. 
        /// </summary> 
        protected override void OnActivated(IActivatedEventArgs e)
        {
            if (e.Kind == ActivationKind.Protocol)
            {
                ProtocolActivatedEventArgs protocolArgs = e as ProtocolActivatedEventArgs;
                Frame rootFrame = CreateRootFrame();
                RestoreStatus(e.PreviousExecutionState);

                if (rootFrame.Content == null)
                {
                    if (!rootFrame.Navigate(typeof(VideoGuest)))
                    {
                        throw new Exception("Failed to create initial page");
                    }
                } 

                // Ensure the current window is active 
                Window.Current.Activate();
            }
        }

        /// <summary>
        /// Invoked when application execution is being suspended.  Application state is saved
        /// without knowing whether the application will be terminated or resumed with the contents
        /// of memory still intact.
        /// </summary>
        /// <param name="sender">The source of the suspend request.</param>
        /// <param name="e">Details about the suspend request.</param>
        private async void OnSuspending(object sender, SuspendingEventArgs e)
        {
            var deferral = e.SuspendingOperation.GetDeferral();
            await SuspensionManager.SaveAsync();
            deferral.Complete();
        }
    }
}
