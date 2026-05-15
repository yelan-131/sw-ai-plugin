using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace SwComAddin.Helpers
{
    public static class UIHelpers
    {
        public static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        public static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T result) return result;
                current = VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        public static string ShowInputDialog(string title, string prompt)
        {
            var dialog = new Window
            {
                Title = title,
                Width = 320,
                Height = 180,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };
            var stack = new StackPanel { Margin = new Thickness(12) };
            stack.Children.Add(new TextBlock { Text = prompt, Margin = new Thickness(0, 0, 0, 8) });
            var input = new TextBox { Padding = new Thickness(6, 4, 6, 4) };
            stack.Children.Add(input);

            string result = null;
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var okButton = new Button { Content = "确定", Width = 60, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
            var cancelButton = new Button { Content = "取消", Width = 60, Height = 26 };
            okButton.Click += (_, _) => { result = input.Text; dialog.Close(); };
            cancelButton.Click += (_, _) => { dialog.Close(); };
            buttonPanel.Children.Add(okButton);
            buttonPanel.Children.Add(cancelButton);
            stack.Children.Add(buttonPanel);
            dialog.Content = stack;
            input.Focus();
            dialog.ShowDialog();
            return result;
        }
    }
}
