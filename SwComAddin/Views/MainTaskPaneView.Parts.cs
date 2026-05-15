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
        // === Search Box Handling (delegates to PartsSearchService) ===

        private void PartsSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            _searchService.OnSearchGotFocus(PartsSearch);
        }

        private void PartsSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            _searchService.OnSearchLostFocus(PartsSearch);
        }

        private void PartsSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var keyword = _searchService.GetSearchText(PartsSearch);
                if (string.IsNullOrEmpty(keyword))
                    LoadPartsLibrary();
            }
        }

        private void PartsSearch_TextChanged(object sender, TextChangedEventArgs e)
        {
            var keyword = _searchService.GetSearchText(PartsSearch);
            _searchService.ApplySearch(PartsTree, keyword);
        }
        // === Tab 1: Standard Parts Tree ===

        private void SetupTreeViewTemplates()
        {
            // Level 3: StandardPart (leaf node)
            var partTemplate = new DataTemplate();
            var partFactory = new FrameworkElementFactory(typeof(StackPanel));
            partFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            partFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(4, 1, 2, 1));

            var partText = new FrameworkElementFactory(typeof(TextBlock));
            partText.SetValue(TextBlock.FontSizeProperty, 11.0);
            partText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x55)));
            var dashRun = new FrameworkElementFactory(typeof(Run));
            dashRun.SetValue(Run.TextProperty, "- ");
            dashRun.SetValue(Run.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99)));
            partText.AppendChild(dashRun);
            var nameRun = new FrameworkElementFactory(typeof(Run));
            nameRun.SetBinding(Run.TextProperty, new System.Windows.Data.Binding("Name"));
            partText.AppendChild(nameRun);
            partText.SetValue(FrameworkElement.CursorProperty, Cursors.Hand);
            partText.AddHandler(TextBlock.MouseDownEvent, new MouseButtonEventHandler(Part_MouseDown));
            partFactory.AppendChild(partText);
            partTemplate.VisualTree = partFactory;

            // Level 2: SubCategory -> bind Parts
            var subCatTemplate = new HierarchicalDataTemplate();
            subCatTemplate.ItemsSource = new System.Windows.Data.Binding("Parts");
            var subCatFactory = new FrameworkElementFactory(typeof(StackPanel));
            subCatFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            subCatFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 2, 1));
            var subCatIcon = new FrameworkElementFactory(typeof(TextBlock));
            subCatIcon.SetValue(TextBlock.TextProperty, "");
            subCatIcon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            subCatIcon.SetValue(TextBlock.FontSizeProperty, 11.0);
            subCatIcon.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x6A, 0xA8, 0xD7)));
            subCatIcon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            subCatIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            subCatFactory.AppendChild(subCatIcon);
            var subCatText = new FrameworkElementFactory(typeof(TextBlock));
            subCatText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            subCatText.SetValue(TextBlock.FontSizeProperty, 12.0);
            subCatText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x44, 0x44, 0x44)));
            subCatFactory.AppendChild(subCatText);
            subCatTemplate.VisualTree = subCatFactory;
            subCatTemplate.ItemTemplate = partTemplate;

            // Level 1: Category -> bind SubCategories
            var catTemplate = new HierarchicalDataTemplate();
            catTemplate.ItemsSource = new System.Windows.Data.Binding("SubCategories");
            var catFactory = new FrameworkElementFactory(typeof(StackPanel));
            catFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            catFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(2));
            var catIcon = new FrameworkElementFactory(typeof(TextBlock));
            catIcon.SetValue(TextBlock.TextProperty, "");
            catIcon.SetValue(TextBlock.FontFamilyProperty, new FontFamily("Segoe MDL2 Assets"));
            catIcon.SetValue(TextBlock.FontSizeProperty, 12.0);
            catIcon.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0xDA, 0xA5, 0x20)));
            catIcon.SetValue(TextBlock.MarginProperty, new Thickness(0, 0, 5, 0));
            catIcon.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            catFactory.AppendChild(catIcon);
            var catText = new FrameworkElementFactory(typeof(TextBlock));
            catText.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Name"));
            catText.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            catText.SetValue(TextBlock.FontSizeProperty, 12.0);
            catText.SetValue(TextBlock.ForegroundProperty, new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)));
            catFactory.AppendChild(catText);
            catTemplate.VisualTree = catFactory;
            catTemplate.ItemTemplate = subCatTemplate;

            PartsTree.ItemTemplate = catTemplate;
        }
        private void LoadPartsLibrary()
        {
            try
            {
                var path = Path.Combine(BaseDir, "Data", "standard_parts.json");
                if (!File.Exists(path)) return;

                var json = File.ReadAllText(path);
                var catalog = JsonSerializer.Deserialize<StandardPartsCatalog>(json,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (catalog == null) return;

                // Ensure SubCategories are populated for categories that only have flat Parts
                foreach (var category in catalog.Categories)
                {
                    _categoryService.RegisterDefaultCategory(category.Name);

                    if ((category.SubCategories == null || category.SubCategories.Count == 0)
                        && category.Parts != null && category.Parts.Count > 0)
                    {
                        category.SubCategories = new List<SubCategory>
                        {
                            new() { Name = category.Name, Parts = category.Parts }
                        };
                    }
                }

                _allCategories = catalog.Categories;

                // Load custom categories alongside default ones
                LoadCombinedTree();

                Log($"Parts loaded: {catalog.Categories.Count} categories");
            }
            catch (Exception ex)
            {
                Log($"LoadPartsLibrary FAILED: {ex.Message}");
            }
        }
        private void LoadCombinedTree()
        {
            var combined = new List<Category>(_allCategories);

            // Add custom categories
            foreach (var customCat in _categoryService.GetCategories())
            {
                combined.Add(new Category
                {
                    Name = customCat.Name,
                    SubCategories = customCat.SubCategories,
                    Parts = customCat.Parts
                });
            }

            PartsTree.ItemsSource = combined;
        }
        private void PartsTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem item)
                item.IsExpanded = true;
        }
        private void PartsTree_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            // Right-click context menu for custom categories
            if (e.OriginalSource is FrameworkElement element)
            {
                // Walk up to find the TreeViewItem and its DataContext
                var treeViewItem = UIHelpers.FindAncestor<TreeViewItem>(element);
                if (treeViewItem?.DataContext is Category category)
                {
                    ShowCategoryContextMenu(treeViewItem, category, e);
                }
            }
        }

        private void ShowCategoryContextMenu(TreeViewItem item, Category category, ContextMenuEventArgs e)
        {
            if (_categoryService.IsDefaultCategory(category.Name)) return;

            var menu = new ContextMenu();
            var renameItem = new MenuItem { Header = "重命名" };
            renameItem.Click += (_, _) => RenameCategoryDialog(category.Name);
            menu.Items.Add(renameItem);

            var deleteItem = new MenuItem { Header = "删除" };
            deleteItem.Click += (_, _) => DeleteCustomCategory(category.Name);
            menu.Items.Add(deleteItem);

            item.ContextMenu = menu;
        }
        private void AddCategoryBtn_Click(object sender, RoutedEventArgs e)
        {
            var name = UIHelpers.ShowInputDialog("添加自定义分类", "请输入分类名称:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var error = _categoryService.AddCategory(name);
            if (error != null)
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadCombinedTree();
        }
        private void RenameCategoryDialog(string oldName)
        {
            var newName = UIHelpers.ShowInputDialog("重命名分类", $"将 \"{oldName}\" 重命名为:");
            if (string.IsNullOrWhiteSpace(newName)) return;

            var error = _categoryService.RenameCategory(oldName, newName);
            if (error != null)
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadCombinedTree();
        }
        private void DeleteCustomCategory(string name)
        {
            var result = MessageBox.Show($"确定要删除自定义分类 \"{name}\" 吗？", "确认删除",
                MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            var error = _categoryService.DeleteCategory(name);
            if (error != null)
            {
                MessageBox.Show(error, "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            LoadCombinedTree();
        }
        private void Part_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var element = sender as FrameworkElement;
            if (element?.DataContext is StandardPart part)
            {
                _selectedPart = part;
                ShowPartDetail(part);
                e.Handled = true;
            }
        }
        private void ShowPartDetail(StandardPart part)
        {
            PartDetailEmpty.Visibility = Visibility.Collapsed;
            PartDetailBorder.Visibility = Visibility.Visible;
            PartName.Text = part.Name;
            PartStandard.Text = part.Standard;
            PartDesc.Text = part.Description;

            // Show specs table
            PartSpecs.Items.Clear();
            if (part.Specs != null)
            {
                var skipKeys = new HashSet<string> { "l_options", "lg_min" };
                foreach (var entry in part.Specs)
                {
                    if (!skipKeys.Contains(entry.Key))
                        PartSpecs.Items.Add(new { Key = entry.Key, Value = entry.Value?.ToString() ?? "" });
                }
            }

            // Show schematic preview
            try { PartPreview.ShowPart(part.Name, part.Standard, part.Specs, part.FeatureTemplate); }
            catch (Exception ex) { Log($"PartPreview failed: {ex.Message}"); }

            // Toggle parametric vs external UI
            bool isParametric = part.Geometric;
            if (isParametric)
            {
                ShowParametricUI(part);
            }
            else
            {
                ShowExternalUI();
            }

            GenerateResultText.Visibility = Visibility.Collapsed;
        }
        private void ShowParametricUI(StandardPart part)
        {
            PartSpecs.Visibility = Visibility.Collapsed;
            ParamEditPanel.Visibility = Visibility.Visible;
            GeneratePartBtn.Visibility = Visibility.Visible;
            InsertBtn.Visibility = Visibility.Collapsed;

            PartParamInputs.Items.Clear();
            var editableKeys = new Dictionary<string, string>
            {
                ["d"] = "直径", ["diameter"] = "直径",
                ["l_default"] = "长度", ["length"] = "长度",
                ["s"] = "对边宽度", ["width_across_flats"] = "对边宽度",
                ["k"] = "头部高度", ["head_height"] = "头部高度",
                ["e"] = "头部外接圆", ["head_diameter"] = "头部直径",
                ["thickness"] = "厚度", ["height"] = "高度",
                ["inner_diameter"] = "内径", ["outer_diameter"] = "外径",
                ["chamfer_size"] = "倒角"
            };
            var unitMap = new Dictionary<string, string>
            {
                ["d"] = "mm", ["diameter"] = "mm", ["l_default"] = "mm", ["length"] = "mm",
                ["s"] = "mm", ["k"] = "mm", ["e"] = "mm", ["thickness"] = "mm",
                ["height"] = "mm", ["inner_diameter"] = "mm", ["outer_diameter"] = "mm",
                ["chamfer_size"] = "mm", ["head_height"] = "mm", ["head_diameter"] = "mm",
                ["width_across_flats"] = "mm"
            };

            foreach (var mapping in editableKeys)
            {
                if (part.Specs.TryGetValue(mapping.Key, out var value) && value != null)
                {
                    PartParamInputs.Items.Add(new ParamInput
                    {
                        Key = mapping.Key,
                        Label = mapping.Value,
                        Value = value.ToString() ?? "",
                        Unit = unitMap.TryGetValue(mapping.Key, out var unit) ? unit : "mm"
                    });
                }
            }
        }
        private void ShowExternalUI()
        {
            PartSpecs.Visibility = Visibility.Visible;
            ParamEditPanel.Visibility = Visibility.Collapsed;
            GeneratePartBtn.Visibility = Visibility.Collapsed;
            InsertBtn.Visibility = Visibility.Visible;
        }
        private void GeneratePartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPart == null) return;

            SyncParamInputValues();
            var parameters = CollectParamInputs();

            // Normalize key names
            if (parameters.ContainsKey("l_default")) parameters["length"] = parameters["l_default"];
            if (parameters.ContainsKey("d") && !parameters.ContainsKey("diameter")) parameters["diameter"] = parameters["d"];
            if (parameters.ContainsKey("s") && !parameters.ContainsKey("width_across_flats")) parameters["width_across_flats"] = parameters["s"];
            if (parameters.ContainsKey("k") && !parameters.ContainsKey("head_height")) parameters["head_height"] = parameters["k"];
            if (parameters.ContainsKey("e") && !parameters.ContainsKey("head_diameter")) parameters["head_diameter"] = parameters["e"];

            var builder = new ParametricBuilder(_connector);
            var (ok, msg) = _selectedPart.FeatureTemplate switch
            {
                "hex_bolt" => builder.BuildBolt(parameters),
                "hex_nut" => builder.BuildNut(parameters),
                "flat_washer" or "spring_washer" => builder.BuildWasher(parameters),
                "dowel_pin" => builder.BuildDowelPin(parameters),
                _ => (false, $"未知的建模模板: {_selectedPart.FeatureTemplate}")
            };

            GenerateResultText.Text = ok ? msg : $"错误: {msg}";
            GenerateResultText.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                : new SolidColorBrush(Color.FromRgb(198, 40, 40));
            GenerateResultText.Visibility = Visibility.Visible;
        }
        private void InsertBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPart == null) return;
            try
            {
                var swApp = (ISldWorks)_connector.GetSwApp();
                var foundFile = FindModelFile();

                if (foundFile != null)
                {
                    int err = 0, warn = 0;
                    int docType = foundFile.EndsWith(".sldprt") ? 1 :
                                  foundFile.EndsWith(".sldasm") ? 2 : 1;
                    swApp.OpenDoc6(foundFile, docType, 0, "", ref err, ref warn);
                    MessageBox.Show($"已加载: {_selectedPart.Name}\n来源: {foundFile}", "SW AI Plugin");
                }
                else
                {
                    ShowModelNotFoundMessage();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"插入失败: {ex.Message}", "SW AI Plugin");
            }
        }
        private string? FindModelFile()
        {
            if (string.IsNullOrEmpty(_modelLibPath) || !Directory.Exists(_modelLibPath))
                return null;

            var extensions = new[] { ".step", ".stp", ".iges", ".igs", ".x_t", ".sldprt" };
            foreach (var ext in extensions)
            {
                var candidate = Path.Combine(_modelLibPath, _selectedPart!.Id + ext);
                if (File.Exists(candidate)) return candidate;
            }

            foreach (var ext in extensions)
            {
                var matches = Directory.GetFiles(_modelLibPath, _selectedPart!.Id + ext, SearchOption.AllDirectories);
                if (matches.Length > 0) return matches[0];
            }

            return null;
        }
        private void ShowModelNotFoundMessage()
        {
            var source = _selectedPart!.Specs?.ContainsKey("download_source") == true
                ? _selectedPart.Specs["download_source"]?.ToString() : "";
            var message = $"零件 \"{_selectedPart.Name}\" 本地无模型文件。\n\n";
            if (!string.IsNullOrEmpty(_modelLibPath))
                message += $"模型库路径: {_modelLibPath}\n";
            else
                message += "未配置模型库路径，请在设置中指定。\n";
            message += $"\n请将 STEP 文件命名为 {_selectedPart.Id}.step 放入模型库目录。";
            if (!string.IsNullOrEmpty(source))
                message += $"\n\n下载来源: {source}";
            MessageBox.Show(message, "SW AI Plugin - 未找到模型", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}
