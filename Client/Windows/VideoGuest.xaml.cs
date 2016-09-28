//*********************************************************
//
// Copyright (c) Microsoft. All rights reserved.
// THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF
// ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY
// IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR
// PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT.
//
//*********************************************************

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using SDKTemplate;
using System;

namespace SimpleCommunication
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class VideoGuest : Page
    {
        // A pointer back to the main page.  This is needed if you want to call methods in MainPage such
        // as NotifyUser()
        MainPage rootPage = MainPage.Current;
        
        public VideoGuest()
        {
            this.InitializeComponent();
            rootPage.EnsureMediaExtensionManager();
            RemoteVideo.MediaFailed += RemoteVideo_MediaFailed;
        }

        void RemoteVideo_MediaFailed(object sender, ExceptionRoutedEventArgs e)
        {
            rootPage.NotifyUser("RemoteVideo_MediaFailed:" + e.ErrorMessage, NotifyType.ErrorMessage);
        }


        /// <summary>
        /// This is the click handler for the 'Default' button.  You would replace this with your own handler
        /// if you have a button or buttons on this page.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Call_Click(object sender, RoutedEventArgs e)
        {
            Button b = sender as Button;
            var address = HostName.Text;

            if (b != null && !String.IsNullOrEmpty(address))
            { 
                RemoteVideo.Source = new Uri("stsp://" + address); 
                rootPage.NotifyUser("Initiating connection... Please wait.", NotifyType.StatusMessage);
            }
        }
 
 
    }
}
