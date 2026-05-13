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
using SwComAddin.Models;
using SwComAddin.Services;

namespace SwComAddin.Views
{
    public partial class MainTaskPaneView : UserControl
    {
        private readonly SwConnector _connector;
        private HttpClient _httpClient;
        private StandardPart? _selectedPart;
        private string? _selectedTemplate;
        private string _backendUrl = "http://localhost:8765";
        private string _modelLibPath = "";  // local folder or http://server/models/
        private string _version = "0.1.0";
        private UpdateService _updateService;
        private string? _pendingUpdateUrl;
        private string? _pendingUpdateVersion;
        private bool _updatePanelOpen;

        private static readonly string BaseDir = Path.GetDirectoryName(
            typeof(MainTaskPaneView).Assembly.Location);

        private static readonly string ConfigPath = Path.Combine(BaseDir, "plugin_config.json");

        private static readonly string CustomLibPath = Path.Combine(BaseDir, "Data", "custom_library.json");

        private CustomLibraryData _customData = new();

        // --- Inner classes ---

        public class ParamInput : INotifyPropertyChanged
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public string Unit { get; set; } = "";
            private string _value = "";
            public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? n = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
        }

        public class ChatMsg
        {
            public string Text { get; set; } = "";
            public Brush Bg { get; set; } = Brushes.White;
            public Brush Fg { get; set; } = Brushes.Black;
        }

        // --- Standard parametric templates ---

        private static readonly Dictionary<string, List<ParamInput>> Templates = new()
        {
            ["法兰"] = new()
            {
                new(){ Key="outer_d", Label="外径", Value="100", Unit="mm" },
                new(){ Key="inner_d", Label="内径", Value="60", Unit="mm" },
                new(){ Key="bolt_circle_d", Label="螺栓圆直径", Value="80", Unit="mm" },
                new(){ Key="thickness", Label="厚度", Value="10", Unit="mm" },
                new(){ Key="bolt_count", Label="螺栓数量", Value="6", Unit="个" },
                new(){ Key="bolt_d", Label="螺栓孔径", Value="8", Unit="mm" },
            },
            ["阶梯轴"] = new()
            {
                new(){ Key="segments", Label="轴段(直径x长度)", Value="20x50,15x30", Unit="逗号分隔" },
            },
            ["连接板"] = new()
            {
                new(){ Key="width", Label="宽度", Value="100", Unit="mm" },
                new(){ Key="height", Label="高度", Value="80", Unit="mm" },
                new(){ Key="thickness", Label="厚度", Value="5", Unit="mm" },
                new(){ Key="hole_count", Label="孔数量", Value="4", Unit="个" },
                new(){ Key="hole_d", Label="孔径", Value="8", Unit="mm" },
            },
            ["支架"] = new()
            {
                new(){ Key="base_w", Label="底座宽度", Value="100", Unit="mm" },
                new(){ Key="base_h", Label="底座高度", Value="20", Unit="mm" },
                new(){ Key="base_t", Label="底座厚度", Value="10", Unit="mm" },
                new(){ Key="arm_h", Label="臂高", Value="60", Unit="mm" },
                new(){ Key="arm_t", Label="臂厚", Value="8", Unit="mm" },
                new(){ Key="hole_d", Label="孔径", Value="8", Unit="mm" },
            },
            ["轴承座"] = new()
            {
                new(){ Key="bore_d", Label="轴承孔径", Value="25", Unit="mm" },
                new(){ Key="outer_w", Label="外宽", Value="60", Unit="mm" },
                new(){ Key="base_h", Label="底座高度", Value="30", Unit="mm" },
                new(){ Key="bolt_spacing", Label="螺栓间距", Value="80", Unit="mm" },
            },
            ["六角螺栓"] = new()
            {
                new(){ Key="diameter", Label="螺纹直径", Value="6", Unit="mm" },
                new(){ Key="length", Label="螺栓长度", Value="50", Unit="mm" },
                new(){ Key="head_diameter", Label="头部外接圆直径", Value="11", Unit="mm" },
                new(){ Key="head_height", Label="头部高度", Value="4", Unit="mm" },
                new(){ Key="chamfer_size", Label="倒角大小", Value="1", Unit="mm" },
            },
            ["六角螺母"] = new()
            {
                new(){ Key="diameter", Label="螺纹孔径", Value="6", Unit="mm" },
                new(){ Key="width_across_flats", Label="对边宽度", Value="10", Unit="mm" },
                new(){ Key="height", Label="螺母厚度", Value="5.2", Unit="mm" },
            },
            ["平垫圈"] = new()
            {
                new(){ Key="inner_diameter", Label="内径", Value="6.4", Unit="mm" },
                new(){ Key="outer_diameter", Label="外径", Value="12", Unit="mm" },
                new(){ Key="thickness", Label="厚度", Value="1.6", Unit="mm" },
            },
            ["圆柱销"] = new()
            {
                new(){ Key="diameter", Label="直径", Value="6", Unit="mm" },
                new(){ Key="length", Label="长度", Value="30", Unit="mm" },
                new(){ Key="chamfer_size", Label="倒角大小", Value="2", Unit="mm" },
            },
        };

        // === Constructor ===

        public MainTaskPaneView(SwConnector connector)
        {
            InitializeComponent();
            _connector = connector;
            LoadConfig();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            _updateService = new UpdateService();
            BackendUrlInput.Text = _backendUrl;
            ModelLibPathInput.Text = _modelLibPath;
            FooterVersion.Text = $"v{_version}";
            SetupTreeViewTemplates();
            LoadPartsLibrary();
            LoadCustomLibrary();
            RefreshTemplateCombo();
            AddWelcomeMessage();
            CheckAllStatus();
            CheckForUpdateAsync();
            StartPeriodicUpdateCheck();
        }

        // === Config ===

        private void LoadConfig()
        {
            try
            {
                if (File.Exists(ConfigPath))
                {
                    var json = File.ReadAllText(ConfigPath);
                    var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.TryGetProperty("backend_url", out var urlEl))
                        _backendUrl = urlEl.GetString() ?? "http://localhost:8765";
                    if (doc.RootElement.TryGetProperty("model_library_path", out var mlEl))
                        _modelLibPath = mlEl.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("version", out var vEl))
                        _version = vEl.GetString() ?? "1.0.0";
                }
            }
            catch { }
        }

        private void SaveConfig()
        {
            try
            {
                var json = JsonSerializer.Serialize(new { backend_url = _backendUrl, model_library_path = _modelLibPath, version = _version });
                File.WriteAllText(ConfigPath, json);
            }
            catch { }
        }

        // === Navigation ===

        private void TabParts_Checked(object sender, RoutedEventArgs e) => ShowPage(PageParts);
        private void TabCustom_Checked(object sender, RoutedEventArgs e) => ShowPage(PageCustom);
        private void TabParam_Checked(object sender, RoutedEventArgs e) => ShowPage(PageParam);
        private void TabAI_Checked(object sender, RoutedEventArgs e) => ShowPage(PageAI);
        private void TabSettings_Checked(object sender, RoutedEventArgs e) => ShowPage(PageSettings);

        private void FooterSettingsBtn_Click(object sender, RoutedEventArgs e)
        {
            TabSettings.IsChecked = true;
            ShowPage(PageSettings);
        }

        private void ShowPage(UIElement page)
        {
            if (PageParts == null) return;
            PageParts.Visibility = page == PageParts ? Visibility.Visible : Visibility.Collapsed;
            PageCustom.Visibility = page == PageCustom ? Visibility.Visible : Visibility.Collapsed;
            PageParam.Visibility = page == PageParam ? Visibility.Visible : Visibility.Collapsed;
            PageAI.Visibility = page == PageAI ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = page == PageSettings ? Visibility.Visible : Visibility.Collapsed;
        }

        // === Keyboard Focus Fix (critical for WPF inside ElementHost) ===

        private void ParamInput_Focus(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox tb)
            {
                tb.Focus();
                Keyboard.Focus(tb);
                e.Handled = true;
            }
        }

        // === Search Box Placeholder ===

        private bool _searchFocused = false;

        private void PartsSearch_GotFocus(object sender, RoutedEventArgs e)
        {
            _searchFocused = true;
            if (PartsSearch.Text == "搜索零件...")
                PartsSearch.Text = "";
            PartsSearch.Foreground = new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33));
        }

        private void PartsSearch_LostFocus(object sender, RoutedEventArgs e)
        {
            _searchFocused = false;
            if (string.IsNullOrWhiteSpace(PartsSearch.Text))
            {
                PartsSearch.Text = "搜索零件...";
                PartsSearch.Foreground = new SolidColorBrush(Color.FromRgb(0x99, 0x99, 0x99));
            }
        }

        private void PartsSearch_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                var keyword = PartsSearch.Text.Trim();
                if (keyword == "搜索零件..." || string.IsNullOrEmpty(keyword))
                    LoadPartsLibrary();
            }
        }

        // === Tab 1: Standard Parts Tree ===

        /// <summary>
        /// Build 3-level HierarchicalDataTemplates in code so they correctly
        /// resolve the model types from SwComAddin.Models namespace.
        /// Level 1: Category -> bind SubCategories (if any) or fallback to Parts
        /// Level 2: SubCategory -> bind Parts
        /// Level 3: StandardPart -> display Name, handle click
        /// </summary>
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
            // Prefix name with a dash separator
            var dashRun = new FrameworkElementFactory(typeof(Run));
            dashRun.SetValue(Run.TextProperty, "– ");
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
            subCatIcon.SetValue(TextBlock.TextProperty, "");
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
            catIcon.SetValue(TextBlock.TextProperty, "");
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

            // For categories that only have flat Parts (no SubCategories),
            // we need a second template that binds Parts directly.
            // We handle this by ensuring the data always has SubCategories populated.
            // If a category has SubCategories.Count==0 but Parts.Count>0, we wrap Parts
            // into pseudo-subcategories in LoadPartsLibrary().

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

                if (catalog != null)
                {
                    foreach (var cat in catalog.Categories)
                    {
                        if ((cat.SubCategories == null || cat.SubCategories.Count == 0)
                            && cat.Parts != null && cat.Parts.Count > 0)
                        {
                            cat.SubCategories = new List<SubCategory>
                            {
                                new() { Name = cat.Name, Parts = cat.Parts }
                            };
                        }
                    }
                    Log($"Parts loaded: {catalog.Categories.Count} categories");
                    PartsTree.ItemsSource = catalog.Categories;
                }
            }
            catch (Exception ex)
            {
                Log($"LoadPartsLibrary FAILED: {ex.Message}");
            }
        }

        private void PartsTree_Expanded(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is TreeViewItem tvi)
                tvi.IsExpanded = true;
        }

        private void Part_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // The DataContext comes from the TextBlock inside the DataTemplate
            var fe = sender as FrameworkElement;
            if (fe?.DataContext is StandardPart part)
            {
                _selectedPart = part;
                PartDetailBorder.Visibility = Visibility.Visible;
                PartName.Text = part.Name;
                PartStandard.Text = part.Standard;
                PartDesc.Text = part.Description;

                // Show specs (skip non-display fields)
                PartSpecs.Items.Clear();
                if (part.Specs != null)
                {
                    var skipKeys = new HashSet<string> { "l_options", "lg_min" };
                    foreach (var kv in part.Specs)
                    {
                        if (!skipKeys.Contains(kv.Key))
                            PartSpecs.Items.Add(new { Key = kv.Key, Value = kv.Value?.ToString() ?? "" });
                    }
                }

                // Show schematic preview with dimensions
                try { PartPreview.ShowPart(part.Name, part.Standard, part.Specs, part.FeatureTemplate); }
                catch (Exception ex) { Log($"PartPreview failed: {ex.Message}"); }

                // Toggle parametric vs external UI
                bool isParametric = part.Geometric;
                if (isParametric)
                {
                    // Hide read-only specs table, show editable params instead
                    PartSpecs.Visibility = Visibility.Collapsed;
                    ParamEditPanel.Visibility = Visibility.Visible;
                    GeneratePartBtn.Visibility = Visibility.Visible;
                    InsertBtn.Visibility = Visibility.Collapsed;

                    // Build editable parameter inputs from specs
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
                    var units = new Dictionary<string, string>
                    {
                        ["d"] = "mm", ["diameter"] = "mm", ["l_default"] = "mm", ["length"] = "mm",
                        ["s"] = "mm", ["k"] = "mm", ["e"] = "mm", ["thickness"] = "mm",
                        ["height"] = "mm", ["inner_diameter"] = "mm", ["outer_diameter"] = "mm",
                        ["chamfer_size"] = "mm", ["head_height"] = "mm", ["head_diameter"] = "mm",
                        ["width_across_flats"] = "mm"
                    };
                    foreach (var kv in editableKeys)
                    {
                        if (part.Specs.TryGetValue(kv.Key, out var val) && val != null)
                        {
                            PartParamInputs.Items.Add(new ParamInput
                            {
                                Key = kv.Key,
                                Label = kv.Value,
                                Value = val.ToString() ?? "",
                                Unit = units.TryGetValue(kv.Key, out var u) ? u : "mm"
                            });
                        }
                    }
                }
                else
                {
                    PartSpecs.Visibility = Visibility.Visible;
                    ParamEditPanel.Visibility = Visibility.Collapsed;
                    GeneratePartBtn.Visibility = Visibility.Collapsed;
                    InsertBtn.Visibility = Visibility.Visible;
                }

                GenerateResultText.Visibility = Visibility.Collapsed;
                e.Handled = true;
            }
        }

        private void GeneratePartBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedPart == null) return;

            // Sync TextBox values
            foreach (var container in PartParamInputs.Items)
            {
                if (container is ParamInput pi)
                {
                    var element = PartParamInputs.ItemContainerGenerator.ContainerFromItem(container);
                    if (element is FrameworkElement fe)
                    {
                        var tb = FindVisualChild<System.Windows.Controls.TextBox>(fe);
                        if (tb != null)
                        {
                            var binding = tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                            binding?.UpdateSource();
                        }
                    }
                }
            }

            var parameters = new Dictionary<string, object>();
            foreach (var item in PartParamInputs.Items)
            {
                if (item is ParamInput pi)
                {
                    if (double.TryParse(pi.Value, out var d)) parameters[pi.Key] = d;
                    else if (int.TryParse(pi.Value, out var i)) parameters[pi.Key] = i;
                    else parameters[pi.Key] = pi.Value;
                }
            }

            // Normalize key names: l_default -> length, d -> diameter, s -> width_across_flats, k -> head_height, e -> head_diameter
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

                // Try to find model file in local library
                string? foundFile = null;
                if (!string.IsNullOrEmpty(_modelLibPath) && Directory.Exists(_modelLibPath))
                {
                    var extensions = new[] { ".step", ".stp", ".iges", ".igs", ".x_t", ".sldprt" };
                    foreach (var ext in extensions)
                    {
                        var candidate = Path.Combine(_modelLibPath, _selectedPart.Id + ext);
                        if (File.Exists(candidate))
                        {
                            foundFile = candidate;
                            break;
                        }
                    }
                    // Also search subdirectories by category-like naming
                    if (foundFile == null)
                    {
                        foreach (var ext in extensions)
                        {
                            var matches = Directory.GetFiles(_modelLibPath, _selectedPart.Id + ext, SearchOption.AllDirectories);
                            if (matches.Length > 0)
                            {
                                foundFile = matches[0];
                                break;
                            }
                        }
                    }
                }

                if (foundFile != null)
                {
                    int err = 0, warn = 0;
                    int docType = foundFile.EndsWith(".sldprt") ? 1 :
                                  foundFile.EndsWith(".sldasm") ? 2 : 1; // STEP/IGES open as part
                    swApp.OpenDoc6(foundFile, docType, 0, "", ref err, ref warn);
                    MessageBox.Show($"已加载: {_selectedPart.Name}\n来源: {foundFile}", "SW AI Plugin");
                }
                else
                {
                    var source = _selectedPart.Specs?.ContainsKey("download_source") == true
                        ? _selectedPart.Specs["download_source"]?.ToString() : "";
                    var msg = $"零件 \"{_selectedPart.Name}\" 本地无模型文件。\n\n";
                    if (!string.IsNullOrEmpty(_modelLibPath))
                        msg += $"模型库路径: {_modelLibPath}\n";
                    else
                        msg += "未配置模型库路径，请在设置中指定。\n";
                    msg += $"\n请将 STEP 文件命名为 {_selectedPart.Id}.step 放入模型库目录。";
                    if (!string.IsNullOrEmpty(source))
                        msg += $"\n\n下载来源: {source}";
                    MessageBox.Show(msg, "SW AI Plugin - 未找到模型", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"插入失败: {ex.Message}", "SW AI Plugin");
            }
        }

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
            // Simple input dialog: ask for part name
            var input = ShowInputDialog("添加零件", "请输入零件名称:");
            if (string.IsNullOrWhiteSpace(input)) return;

            var cat = ShowInputDialog("分类", "请输入分类 (可选):") ?? "";
            var part = new CustomPart
            {
                Name = input.Trim(),
                Category = cat.Trim(),
                Notes = ""
            };
            _customData.Parts.Add(part);
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
            var name = ShowInputDialog("添加模板", "请输入模板名称:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var tmpl = new CustomTemplate { Name = name.Trim() };

            // Ask for parameters in a loop
            while (true)
            {
                var paramStr = ShowInputDialog("添加参数",
                    $"模板: {name}\n输入参数 (格式: 键,标签,默认值,单位)\n留空结束");
                if (string.IsNullOrWhiteSpace(paramStr)) break;

                var parts = paramStr.Split(',');
                if (parts.Length >= 2)
                {
                    tmpl.Parameters.Add(new TemplateParam
                    {
                        Key = parts[0].Trim(),
                        Label = parts[1].Trim(),
                        DefaultValue = parts.Length > 2 ? parts[2].Trim() : "",
                        Unit = parts.Length > 3 ? parts[3].Trim() : "mm"
                    });
                }
            }

            _customData.Templates.Add(tmpl);
            SaveCustomLibrary();
            RefreshCustomLists();
            RefreshTemplateCombo();
        }

        private void DeleteCustomTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (CustomTemplatesList.SelectedItem is CustomTemplate tmpl)
            {
                _customData.Templates.Remove(tmpl);
                SaveCustomLibrary();
                RefreshCustomLists();
                RefreshTemplateCombo();
            }
            else
            {
                MessageBox.Show("请先选择要删除的模板", "提示");
            }
        }

        /// <summary>
        /// Simple input dialog using a WPF Window.
        /// </summary>
        private static string? ShowInputDialog(string title, string prompt)
        {
            var dlg = new Window
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

            string? result = null;
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            var okBtn = new Button { Content = "确定", Width = 60, Height = 26, Margin = new Thickness(0, 0, 8, 0) };
            var cancelBtn = new Button { Content = "取消", Width = 60, Height = 26 };
            okBtn.Click += (_, _) => { result = input.Text; dlg.Close(); };
            cancelBtn.Click += (_, _) => { dlg.Close(); };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            stack.Children.Add(btnPanel);
            dlg.Content = stack;
            input.Focus();
            dlg.ShowDialog();
            return result;
        }

        // === Tab 3: Parametric Modeling ===

        private void RefreshTemplateCombo()
        {
            var prevSelection = TemplateCombo.SelectedItem;
            TemplateCombo.Items.Clear();
            foreach (var name in Templates.Keys)
                TemplateCombo.Items.Add(new ComboBoxItem { Content = name });
            foreach (var tmpl in _customData.Templates)
                TemplateCombo.Items.Add(new ComboBoxItem { Content = tmpl.Name });
            if (prevSelection != null)
                TemplateCombo.SelectedItem = prevSelection;
            Log($"TemplateCombo: {TemplateCombo.Items.Count} items");
        }

        private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (TemplateCombo.SelectedItem == null) return;

            string selected;
            if (TemplateCombo.SelectedItem is ComboBoxItem cbi)
                selected = cbi.Content?.ToString() ?? "";
            else
                selected = TemplateCombo.SelectedItem.ToString() ?? "";
            if (string.IsNullOrEmpty(selected)) return;

            _selectedTemplate = selected;
            ParamInputs.Items.Clear();

            // Check standard templates first
            if (Templates.TryGetValue(selected, out var ps))
            {
                foreach (var p in ps)
                    ParamInputs.Items.Add(p);
                GenerateBtn.IsEnabled = true;
            }
            // Check custom templates
            else
            {
                var custom = _customData.Templates.FirstOrDefault(t => t.Name == selected);
                if (custom != null)
                {
                    foreach (var tp in custom.Parameters)
                    {
                        ParamInputs.Items.Add(new ParamInput
                        {
                            Key = tp.Key,
                            Label = tp.Label,
                            Value = tp.DefaultValue,
                            Unit = tp.Unit
                        });
                    }
                    GenerateBtn.IsEnabled = true;
                }
            }
        }

        private void GenerateBtn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_selectedTemplate)) return;

            // Sync TextBox values to ParamInput (binding uses LostFocus)
            foreach (var container in ParamInputs.Items)
            {
                if (container is ParamInput pi)
                {
                    var element = ParamInputs.ItemContainerGenerator.ContainerFromItem(container);
                    if (element is FrameworkElement fe)
                    {
                        var tb = FindVisualChild<System.Windows.Controls.TextBox>(fe);
                        if (tb != null)
                        {
                            var binding = tb.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                            binding?.UpdateSource();
                        }
                    }
                }
            }

            var parameters = new Dictionary<string, object>();
            foreach (var item in ParamInputs.Items)
            {
                if (item is ParamInput pi)
                {
                    if (double.TryParse(pi.Value, out var d)) parameters[pi.Key] = d;
                    else if (int.TryParse(pi.Value, out var i)) parameters[pi.Key] = i;
                    else parameters[pi.Key] = pi.Value;
                }
            }

            var builder = new ParametricBuilder(_connector);
            bool ok;
            string msg;

            if (Templates.ContainsKey(_selectedTemplate))
            {
                // Standard template
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
                // Custom template - for now just show parameters
                ok = false;
                msg = $"自定义模板 \"{_selectedTemplate}\" 参数已收集，建模功能开发中";
            }

            ResultText.Text = msg;
            ResultText.Foreground = ok
                ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                : new SolidColorBrush(Color.FromRgb(198, 40, 40));
        }

        // === Tab 4: AI Assistant ===

        private void AddWelcomeMessage()
        {
            ChatList.Items.Add(new ChatMsg
            {
                Text = "SW AI 助手已就绪。输入中文描述即可建模。\n例如: 创建M10螺栓",
                Bg = new SolidColorBrush(Color.FromRgb(232, 238, 254)),
                Fg = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            });
        }

        private void AiInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AiSendBtn_Click(sender, e);
        }

        private async void AiSendBtn_Click(object sender, RoutedEventArgs e)
        {
            var msg = AiInput.Text.Trim();
            if (string.IsNullOrEmpty(msg)) return;

            ChatList.Items.Add(new ChatMsg
            {
                Text = msg,
                Bg = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                Fg = new SolidColorBrush(Colors.White)
            });
            AiInput.Text = "";
            AiSendBtn.IsEnabled = false;

            try
            {
                var body = JsonSerializer.Serialize(new { message = msg });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync($"{_backendUrl}/api/chat", content);

                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var reply = doc.RootElement.GetProperty("reply").GetString() ?? "无回复";

                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = reply,
                        Bg = new SolidColorBrush(Colors.White),
                        Fg = new SolidColorBrush(Color.FromRgb(33, 33, 33))
                    });
                }
                else
                {
                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = $"服务错误 ({(int)resp.StatusCode})",
                        Bg = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                        Fg = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                    });
                }
            }
            catch (Exception ex)
            {
                ChatList.Items.Add(new ChatMsg
                {
                    Text = $"连接失败: {ex.Message}",
                    Bg = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                    Fg = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                });
            }

            ChatScroll.ScrollToEnd();
            AiSendBtn.IsEnabled = true;
        }

        // === Tab 5: Settings ===

        private async void SaveApiKey_Click(object sender, RoutedEventArgs e)
        {
            var key = ApiKeyInput.Password.Trim();
            if (string.IsNullOrEmpty(key))
            {
                ApiKeyStatus.Text = "请输入 API Key";
                ApiKeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                return;
            }

            try
            {
                var body = JsonSerializer.Serialize(new { anthropic_api_key = key });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var resp = await _httpClient.PostAsync($"{_backendUrl}/api/config", content);
                if (resp.IsSuccessStatusCode)
                {
                    ApiKeyStatus.Text = "API Key 已保存";
                    ApiKeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    ApiKeyInput.Password = "";
                    ApiKeyConfigStatus.Text = "已配置";
                    ApiKeyConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                }
                else
                {
                    ApiKeyStatus.Text = "保存失败";
                    ApiKeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                }
            }
            catch (Exception ex)
            {
                ApiKeyStatus.Text = $"保存失败: {ex.Message}";
                ApiKeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
        }

        private async void TestApiKey_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var resp = await _httpClient.GetAsync($"{_backendUrl}/api/config");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var keySet = doc.RootElement.GetProperty("anthropic_api_key_set").GetBoolean();
                    ApiKeyStatus.Text = keySet ? "API Key 已配置" : "API Key 未配置";
                    ApiKeyStatus.Foreground = keySet
                        ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                        : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                    ApiKeyConfigStatus.Text = keySet ? "已配置" : "未配置";
                    ApiKeyConfigStatus.Foreground = keySet
                        ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                        : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                }
            }
            catch
            {
                ApiKeyStatus.Text = "无法连接后端";
                ApiKeyStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
        }

        private void SaveBackendUrl_Click(object sender, RoutedEventArgs e)
        {
            var url = BackendUrlInput.Text.Trim();
            if (string.IsNullOrEmpty(url))
            {
                BackendUrlStatus.Text = "请输入后端地址";
                BackendUrlStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                return;
            }
            if (!url.StartsWith("http"))
            {
                url = "http://" + url;
                BackendUrlInput.Text = url;
            }

            _backendUrl = url.TrimEnd('/');
            SaveConfig();
            _httpClient.Dispose();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            BackendUrlStatus.Text = "已保存，正在重新连接...";
            BackendUrlStatus.Foreground = new SolidColorBrush(Color.FromRgb(255, 143, 0));
            CheckAllStatus();
        }

        private void RefreshStatus_Click(object sender, RoutedEventArgs e)
        {
            CheckAllStatus();
        }

        private void SaveModelLibPath_Click(object sender, RoutedEventArgs e)
        {
            var path = ModelLibPathInput.Text.Trim();
            if (!string.IsNullOrEmpty(path) && !Directory.Exists(path))
            {
                try { Directory.CreateDirectory(path); }
                catch (Exception ex)
                {
                    ModelLibStatus.Text = $"无法创建目录: {ex.Message}";
                    ModelLibStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                    return;
                }
            }
            _modelLibPath = path;
            SaveConfig();
            ModelLibStatus.Text = string.IsNullOrEmpty(path) ? "未设置" : "已保存";
            ModelLibStatus.Foreground = new SolidColorBrush(
                string.IsNullOrEmpty(path) ? Color.FromRgb(158, 158, 158) : Color.FromRgb(46, 125, 50));
        }

        private void OpenModelLibFolder_Click(object sender, RoutedEventArgs e)
        {
            var path = ModelLibPathInput.Text.Trim();
            if (string.IsNullOrEmpty(path))
            {
                ModelLibStatus.Text = "请先设置模型库路径";
                ModelLibStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                return;
            }
            if (!Directory.Exists(path))
            {
                ModelLibStatus.Text = "文件夹不存在";
                ModelLibStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                return;
            }
            try { System.Diagnostics.Process.Start("explorer.exe", path); }
            catch (Exception ex)
            {
                ModelLibStatus.Text = $"无法打开: {ex.Message}";
                ModelLibStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
        }

        private async void CheckAllStatus()
        {
            // SolidWorks status
            try
            {
                bool swOk = _connector.IsConnected;
                SwStatus.Text = swOk ? "已连接" : "未连接";
                SwStatus.Foreground = swOk
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                SwDot.Fill = swOk
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }
            catch
            {
                SwStatus.Text = "未知";
                SwStatus.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }

            // Backend health
            try
            {
                var resp = await _httpClient.GetAsync($"{_backendUrl}/api/health");
                bool ok = resp.IsSuccessStatusCode;
                BackendStatus.Text = ok ? "已连接" : "连接失败";
                BackendStatus.Foreground = ok
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                BackendDot.Fill = ok
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));

                if (ok)
                {
                    BackendUrlStatus.Text = "连接正常";
                    BackendUrlStatus.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                }
            }
            catch
            {
                BackendStatus.Text = "未连接";
                BackendStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                BackendDot.Fill = new SolidColorBrush(Color.FromRgb(198, 40, 40));
                BackendUrlStatus.Text = "无法连接";
                BackendUrlStatus.Foreground = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            }

            // API Key status
            try
            {
                var resp = await _httpClient.GetAsync($"{_backendUrl}/api/config");
                if (resp.IsSuccessStatusCode)
                {
                    var json = await resp.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var keySet = doc.RootElement.GetProperty("anthropic_api_key_set").GetBoolean();
                    ApiKeyConfigStatus.Text = keySet ? "已配置" : "未配置";
                    ApiKeyConfigStatus.Foreground = keySet
                        ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                        : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                }
            }
            catch
            {
                ApiKeyConfigStatus.Text = "未知";
                ApiKeyConfigStatus.Foreground = new SolidColorBrush(Color.FromRgb(158, 158, 158));
            }

            // Footer status bar
            UpdateFooterStatus();
        }

        private void UpdateFooterStatus()
        {
            // AI status
            bool aiOk = BackendStatus.Text == "已连接";
            var okColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            var errColor = new SolidColorBrush(Color.FromRgb(198, 40, 40));
            StatusAI.Text = aiOk ? "AI ✓" : "AI ✗";
            StatusAI.Foreground = aiOk ? okColor : errColor;
            StatusAI2.Text = StatusAI.Text;
            StatusAI2.Foreground = StatusAI.Foreground;

            // 标准件库 status
            bool partsOk = File.Exists(Path.Combine(BaseDir, "Data", "standard_parts.json"));
            StatusParts.Text = partsOk ? "标准件 ✓" : "标准件 ✗";
            StatusParts.Foreground = partsOk ? okColor : errColor;
            StatusParts2.Text = StatusParts.Text;
            StatusParts2.Foreground = StatusParts.Foreground;

            // 模型库 status
            bool libOk = !string.IsNullOrEmpty(_modelLibPath) && Directory.Exists(_modelLibPath);
            StatusLib.Text = libOk ? "模型库 ✓" : "模型库 ✗";
            StatusLib.Foreground = libOk ? okColor : errColor;
            StatusLib2.Text = StatusLib.Text;
            StatusLib2.Foreground = StatusLib.Foreground;
        }

        private static T FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = System.Windows.Media.VisualTreeHelper.GetChild(parent, i);
                if (child is T result) return result;
                var found = FindVisualChild<T>(child);
                if (found != null) return found;
            }
            return null;
        }

        // === Update ===

        private async void CheckForUpdateAsync()
        {
            try
            {
                var (hasUpdate, latest, url, notes) = await _updateService.CheckForUpdateAsync();
                if (hasUpdate)
                {
                    _pendingUpdateUrl = url;
                    _pendingUpdateVersion = latest;
                    UpdateVersionText.Text = $"新版本 {latest} 可用";
                    UpdateBar.Visibility = Visibility.Visible;
                    StatusBar.Visibility = Visibility.Collapsed;
                    UpdateTitle.Text = $"v{_version} → {latest}";
                    UpdateNotes.Text = notes ?? "暂无更新说明";
                }
            }
            catch { }
        }

        private System.Windows.Threading.DispatcherTimer? _updateTimer;

        private void StartPeriodicUpdateCheck()
        {
            _updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromHours(4)
            };
            _updateTimer.Tick += (s, e) => CheckForUpdateAsync();
            _updateTimer.Start();
        }

        private void FooterVersion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            CheckForUpdateAsync();
        }

        private void UpdateActionLink_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _updatePanelOpen = !_updatePanelOpen;
            UpdatePanel.Visibility = _updatePanelOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void UpdateNowBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingUpdateUrl == null) return;

            UpdateNowBtn.IsEnabled = false;
            UpdateNowBtn.Content = "下载中...";

            try
            {
                var zipPath = await _updateService.DownloadUpdateAsync(_pendingUpdateUrl, null);
                var installDir = BaseDir;
                var batPath = _updateService.PrepareUpdate(zipPath, installDir);

                var result = MessageBox.Show(
                    "更新已下载完成，需要重启 SolidWorks 完成安装。\n是否现在重启？",
                    "更新确认",
                    MessageBoxButton.YesNo, MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    _updateService.ExecuteUpdate(batPath);
                    try { ((ISldWorks)_connector.GetSwApp())?.ExitApp(); } catch { }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"更新失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateNowBtn.IsEnabled = true;
            UpdateNowBtn.Content = "立即更新";
        }

        private void UpdateLaterBtn_Click(object sender, RoutedEventArgs e)
        {
            _updatePanelOpen = false;
            UpdatePanel.Visibility = Visibility.Collapsed;
        }

        private static readonly string LogFilePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "SwAddin.log");

        private static void Log(string msg)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {msg}\n");
            }
            catch { }
        }
    }
}
