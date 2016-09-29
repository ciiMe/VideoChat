// Copyright (c) Microsoft. All rights reserved.
using System;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Markup;
using Windows.UI.Xaml.Navigation;


namespace SDKTemplate
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : SDKTemplate.Common.LayoutAwarePage
    {
        public event System.EventHandler ScenarioLoaded;
        public event EventHandler<MainPageSizeChangedEventArgs> MainPageResized;

        public static MainPage Current;
        
        public MainPage()
        {
            this.InitializeComponent();

            // This is a static public property that will allow downstream pages to get 
            // a handle to the MainPage instance in order to call methods that are in this class.
            Current = this;
            
            Scenarios.SelectionChanged += Scenarios_SelectionChanged;
        }

        /// <summary>
        /// Invoked when this page is about to be displayed in a Frame.
        /// </summary>
        /// <param name="e">Event data that describes how this page was reached.  The Parameter
        /// property is typically used to configure the page.</param>
        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            PopulateScenarios();
        }

        private void PopulateScenarios()
        {
            System.Collections.ObjectModel.ObservableCollection<object> ScenarioList = new System.Collections.ObjectModel.ObservableCollection<object>();
            int i = 0;

            // Populate the ListBox with the list of scenarios as defined in Constants.cs.
            foreach (Scenario s in scenarios)
            {
                ListBoxItem item = new ListBoxItem();
                s.Title = (++i).ToString() + ") " + s.Title;
                item.Content = s;
                item.Name = s.ClassType.FullName;
                ScenarioList.Add(item);
            }

            // Bind the ListBox to the scenario list.
            Scenarios.ItemsSource = ScenarioList;

            // Starting scenario is the first or based upon a previous selection.
            int startingScenarioIndex = -1;
            
            if (SuspensionManager.SessionState.ContainsKey("SelectedScenarioIndex"))
            {
                int selectedScenarioIndex = Convert.ToInt32(SuspensionManager.SessionState["SelectedScenarioIndex"]);
                startingScenarioIndex = selectedScenarioIndex;
            }
            Scenarios.SelectedIndex = startingScenarioIndex != -1 ? startingScenarioIndex : 0;
            Scenarios.ScrollIntoView(Scenarios.SelectedItem);
        }

        /// <summary>
        /// This method is responsible for loading the individual input and output sections for each scenario.  This 
        /// is based on navigating a hidden Frame to the ScenarioX.xaml page and then extracting out the input
        /// and output sections into the respective UserControl on the main page.
        /// </summary>
        /// <param name="scenarioName"></param>
        public void LoadScenario(Type scenarioClass)
        { 
            ContentFrame.Navigate(scenarioClass, this); 
        }

        void Scenarios_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Scenarios.SelectedItem != null)
            {
                NotifyUser("", NotifyType.StatusMessage);

                ListBoxItem selectedListBoxItem = Scenarios.SelectedItem as ListBoxItem;
                SuspensionManager.SessionState["SelectedScenarioIndex"] = Scenarios.SelectedIndex;

                Scenario scenario = selectedListBoxItem.Content as Scenario;
                LoadScenario(scenario.ClassType);

                // Fire the ScenarioLoaded event since we know that everything is loaded now.
                if (ScenarioLoaded != null)
                {
                    ScenarioLoaded(this, new EventArgs());
                }
            }
        }

        public void NotifyUser(string strMessage, NotifyType type)
        {
            switch (type)
            {
                // Use the status message style.
                case NotifyType.StatusMessage:
                    StatusBlock.Style = Resources["StatusStyle"] as Style;
                    break;
                // Use the error message style.
                case NotifyType.ErrorMessage:
                    StatusBlock.Style = Resources["ErrorStyle"] as Style;
                    break;
            }
            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            if (StatusBlock.Text != String.Empty)
            {
                StatusBlock.Visibility = Windows.UI.Xaml.Visibility.Visible;
            }
            else
            {
                StatusBlock.Visibility = Windows.UI.Xaml.Visibility.Collapsed;
            }
        }

        async void Footer_Click(object sender, RoutedEventArgs e)
        {
            await Windows.System.Launcher.LaunchUriAsync(new Uri(((HyperlinkButton)sender).Tag.ToString()));
        }
    }

    public class MainPageSizeChangedEventArgs : EventArgs
    {
        private double width;

        public double Width
        {
            get { return width; }
            set { width = value; }
        }
    }

    public enum NotifyType
    {
        StatusMessage,
        ErrorMessage
    };
}
