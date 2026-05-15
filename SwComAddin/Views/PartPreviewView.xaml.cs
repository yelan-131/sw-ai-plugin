using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using SwComAddin.Models;
using SwComAddin.Services;

namespace SwComAddin.Views
{
    public partial class PartPreviewView : UserControl
    {
        public PartPreviewView()
        {
            InitializeComponent();
        }

        public void ShowPart(string partName, string categoryName,
            Dictionary<string, object> specs, string featureTemplate)
        {
            PreviewTitle.Text = string.Format("数模预览 — {0}", partName);

            if (string.IsNullOrEmpty(featureTemplate) || specs == null)
            {
                NoPreviewText.Visibility = Visibility.Visible;
                PreviewViewport.Visibility = Visibility.Collapsed;
                return;
            }

            var features = PartFeatureTemplates.Build(featureTemplate, specs);
            if (features == null || features.Features.Count == 0)
            {
                NoPreviewText.Visibility = Visibility.Visible;
                PreviewViewport.Visibility = Visibility.Collapsed;
                return;
            }

            NoPreviewText.Visibility = Visibility.Collapsed;
            PreviewViewport.Visibility = Visibility.Visible;

            var renderer = new PreviewRenderer();
            renderer.Render(features, PreviewViewport);
        }

        public void ShowPart(string partName, string categoryName,
            Dictionary<string, object> specs)
        {
            ShowPart(partName, categoryName, specs, null);
        }
    }
}
