using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using SolidWorks.Interop.sldworks;
using SwComAddin.Helpers;
using SwComAddin.Models;
using SwComAddin.Services;
namespace SwComAddin.Views
{
    public partial class MainTaskPaneView : UserControl
    {
        // === Tab 3: Parametric Modeling ===

        private void RefreshTemplateCombo()
        {
            var prevSelection = TemplateCombo.SelectedItem;
            TemplateCombo.Items.Clear();
            foreach (var name in Templates.Keys)
                TemplateCombo.Items.Add(new ComboBoxItem { Content = name });
            foreach (var template in _customData.Templates)
                TemplateCombo.Items.Add(new ComboBoxItem { Content = template.Name });
            if (prevSelection != null)
                TemplateCombo.SelectedItem = prevSelection;
            Log($"TemplateCombo: {TemplateCombo.Items.Count} items");
        }

        private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateCombo.SelectedItem == null) return;

            string selected;
            if (TemplateCombo.SelectedItem is ComboBoxItem item)
                selected = item.Content?.ToString() ?? "";
            else
                selected = TemplateCombo.SelectedItem.ToString() ?? "";
            if (string.IsNullOrEmpty(selected)) return;

            _selectedTemplate = selected;
            ParamInputs.Items.Clear();

            if (Templates.TryGetValue(selected, out var parameters))
            {
                foreach (var param in parameters)
                    ParamInputs.Items.Add(param);
                GenerateBtn.IsEnabled = true;
            }
            else
            {
                var custom = _customData.Templates.FirstOrDefault(t => t.Name == selected);
                if (custom != null)
                {
                    foreach (var param in custom.Parameters)
                    {
                        ParamInputs.Items.Add(new ParamInput
                        {
                            Key = param.Key,
                            Label = param.Label,
                            Value = param.DefaultValue,
                            Unit = param.Unit
                        });
                    }
                    GenerateBtn.IsEnabled = true;
                }
            }
        }

        private void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedTemplate)) return;

            SyncParametricInputValues();
            var parameters = CollectParametricInputs();

            var builder = new ParametricBuilder(_connector);
            bool ok;
            string msg;

            if (Templates.ContainsKey(_selectedTemplate))
            {
                (ok, msg) = _selectedTemplate switch
                {
                    "法兰" => builder.BuildFlange(parameters),
                    "阶梯轴" => builder.BuildSteppedShaft(parameters),
                    "连接板" => builder.BuildConnectionPlate(parameters),
                    "支架" => builder.BuildBracket(parameters),
                    "轴承座" => builder.BuildBearingBlock(parameters),
                    "六角螺栓" => builder.BuildBolt(parameters),
                    "六角螺母" => builder.BuildNut(parameters),
                    "平垫圈" => builder.BuildWasher(parameters),
                    "圆柱销" => builder.BuildDowelPin(parameters),
                    _ => (false, "未知模板")
                };
            }
            else
            {
                ok = false;
                msg = $"自定义模板 \"{_selectedTemplate}\" 参数已收集，建模功能开发中";
            }

            ResultText.Text = msg;
            ResultText.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                : new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }
        private void SyncParametricInputValues()
        {
            foreach (var container in ParamInputs.Items)
            {
                if (container is ParamInput paramInput)
                {
                    var element = ParamInputs.ItemContainerGenerator.ContainerFromItem(container);
                    if (element is FrameworkElement fe)
                    {
                        var textBox = UIHelpers.FindVisualChild<System.Windows.Controls.TextBox>(fe);
                        if (textBox != null)
                        {
                            var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                            binding?.UpdateSource();
                        }
                    }
                }
            }
        }

        private Dictionary<string, object> CollectParametricInputs()
        {
            var parameters = new Dictionary<string, object>();
            foreach (var item in ParamInputs.Items)
            {
                if (item is ParamInput paramInput)
                {
                    if (double.TryParse(paramInput.Value, out var doubleValue)) parameters[paramInput.Key] = doubleValue;
                    else if (int.TryParse(paramInput.Value, out var intValue)) parameters[paramInput.Key] = intValue;
                    else parameters[paramInput.Key] = paramInput.Value;
                }
            }
            return parameters;
        }
    }
}
