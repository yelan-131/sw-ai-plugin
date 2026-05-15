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
        // === Tab 2: Custom Library ===

        private void LoadCustomLibrary()
        {
            try
            {
                if (File.Exists(CustomLibPath))
                {
                    var json = File.ReadAllText(CustomLibPath);
                    _customData = JsonSerializer.Deserialize<CustomLibraryData>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch { }

            // Also load into category service
            _categoryService.Load();
            RefreshCustomLists();
        }

        private void SaveCustomLibrary()
        {
            try
            {
                var dir = Path.GetDirectoryName(CustomLibPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_customData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(CustomLibPath, json);
            }
            catch { }
        }

        private void RefreshCustomLists()
        {
            CustomPartsList.ItemsSource = null;
            CustomPartsList.ItemsSource = _customData.Parts;
            CustomTemplatesList.ItemsSource = null;
            CustomTemplatesList.ItemsSource = _customData.Templates;
        }

        private void AddCustomPart_Click(object sender, RoutedEventArgs e)
        {
            var input = UIHelpers.ShowInputDialog("添加零件", "请输入零件名称:");
            if (string.IsNullOrWhiteSpace(input)) return;

            var category = UIHelpers.ShowInputDialog("分类", "请输入分类 (可选):") ?? "";
            _customData.Parts.Add(new CustomPart
            {
                Name = input.Trim(),
                Category = category.Trim(),
                Notes = ""
            });
            SaveCustomLibrary();
            RefreshCustomLists();
        }

        private void DeleteCustomPart_Click(object sender, RoutedEventArgs e)
        {
            if (CustomPartsList.SelectedItem is CustomPart part)
            {
                _customData.Parts.Remove(part);
                SaveCustomLibrary();
                RefreshCustomLists();
            }
            else
            {
                MessageBox.Show("请先选择要删除的零件", "提示");
            }
        }

        private void AddCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            var name = UIHelpers.ShowInputDialog("添加模板", "请输入模板名称:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var template = new CustomTemplate { Name = name.Trim() };
            while (true)
            {
                var paramStr = UIHelpers.ShowInputDialog("添加参数",
                    $"模板: {name}\n输入参数 (格式: 键,标签,默认值,单位)\n留空结束");
                if (string.IsNullOrWhiteSpace(paramStr)) break;

                var parts = paramStr.Split(',');
                if (parts.Length >= 2)
                {
                    template.Parameters.Add(new TemplateParam
                    {
                        Key = parts[0].Trim(),
                        Label = parts[1].Trim(),
                        DefaultValue = parts.Length > 2 ? parts[2].Trim() : "",
                        Unit = parts.Length > 3 ? parts[3].Trim() : "mm"
                    });
                }
            }

            _customData.Templates.Add(template);
            SaveCustomLibrary();
            RefreshCustomLists();
            RefreshTemplateCombo();
        }

        private void DeleteCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (CustomTemplatesList.SelectedItem is CustomTemplate template)
            {
                _customData.Templates.Remove(template);
                SaveCustomLibrary();
                RefreshCustomLists();
                RefreshTemplateCombo();
            }
            else
            {
                MessageBox.Show("请先选择要删除的模板", "提示");
            }
        }
    }
}
