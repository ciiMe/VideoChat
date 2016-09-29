using System.Collections.Generic;
using System;

namespace SDKTemplate
{
    public partial class MainPage : SDKTemplate.Common.LayoutAwarePage
    {
        public const string FEATURE_NAME = "Video and Photo capture.";
        List<Scenario> scenarios = new List<Scenario>
        {
            new Scenario() { Title = "Capture Video", ClassType = typeof(CameraCapture.BasicCapture) },
            new Scenario() { Title = "Capture Photo", ClassType = typeof(CameraCapture.CapturePhoto) }
        };
    }

    public class Scenario
    {
        public string Title { get; set; }

        public Type ClassType { get; set; }

        public override string ToString()
        {
            return Title;
        }
    }
}
