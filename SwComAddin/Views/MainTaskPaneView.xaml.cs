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
        // === Constants ===

        private const string DefaultBackendUrl = "http://localhost:8765";
        private const string PlaceholderSearchText = "搜索零件...";
        private const int HttpTimeoutSeconds = 60;
        private const int UpdateCheckIntervalHours = 4;
        private static readonly string BaseDir = Path.GetDirectoryName(
            typeof(MainTaskPaneView).Assembly.Location);
        private static readonly string ConfigPath = Path.Combine(BaseDir, "plugin_config.json");
        private static readonly string CustomLibPath = Path.Combine(BaseDir, "Data", "custom_library.json");
        private static readonly string LogFilePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop), "SwAddin.log");

        // === Private fields ===

        private readonly SwConnector _connector;
        private HttpClient _httpClient;
        private StandardPart? _selectedPart;
        private string? _selectedTemplate;
        private string _backendUrl = DefaultBackendUrl;
        private string _modelLibPath = "";
        private string _version = "0.1.1";
        private readonly UpdateService _updateService;
        private string? _pendingUpdateUrl;
        private string? _pendingUpdateVersion;
        private bool _updatePanelOpen;
        private bool _versionPopupOpen;
        private CustomLibraryData _customData = new();
        private readonly PartsSearchService _searchService = new();
        private readonly CustomCategoryService _categoryService;
        private List<Category> _allCategories = new();
        private System.Windows.Threading.DispatcherTimer? _updateTimer;

        // === Inner classes ===

        public class ParamInput : INotifyPropertyChanged
        {
            public string Key { get; set; } = "";
            public string Label { get; set; } = "";
            public string Unit { get; set; } = "";
            private string _value = "";
            public string Value { get => _value; set { _value = value; OnPropertyChanged(); } }
            public event PropertyChangedEventHandler? PropertyChanged;
            private void OnPropertyChanged([CallerMemberName] string? name = null)
                => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public class ChatMsg
        {
            public string Text { get; set; } = "";
            public Brush BackgroundBrush { get; set; } = Brushes.White;
            public Brush ForegroundBrush { get; set; } = Brushes.Black;
            public Brush Bg => BackgroundBrush;
            public Brush Fg => ForegroundBrush;
        }

        // === Standard parametric templates ===

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
            _categoryService = new CustomCategoryService(BaseDir);
            LoadConfig();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
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
                    if (doc.RootElement.TryGetProperty("backend_url", out var urlElement))
                        _backendUrl = urlElement.GetString() ?? DefaultBackendUrl;
                    if (doc.RootElement.TryGetProperty("model_library_path", out var modelElement))
                        _modelLibPath = modelElement.GetString() ?? "";
                    if (doc.RootElement.TryGetProperty("version", out var versionElement))
                        _version = versionElement.GetString() ?? "1.0.0";
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

        private void HelpBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowPage(PageHelp);
        }

        private void ShowPage(UIElement page)
        {
            if (PageParts == null) return;
            PageParts.Visibility = page == PageParts ? Visibility.Visible : Visibility.Collapsed;
            PageCustom.Visibility = page == PageCustom ? Visibility.Visible : Visibility.Collapsed;
            PageParam.Visibility = page == PageParam ? Visibility.Visible : Visibility.Collapsed;
            PageAI.Visibility = page == PageAI ? Visibility.Visible : Visibility.Collapsed;
            PageSettings.Visibility = page == PageSettings ? Visibility.Visible : Visibility.Collapsed;
            PageHelp.Visibility = page == PageHelp ? Visibility.Visible : Visibility.Collapsed;
        }

        // === Keyboard Focus Fix (critical for WPF inside ElementHost) ===

        private void ParamInput_Focus(object sender, MouseButtonEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                textBox.Focus();
                Keyboard.Focus(textBox);
                e.Handled = true;
            }
        }

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
                var treeViewItem = FindAncestor<TreeViewItem>(element);
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
            var name = ShowInputDialog("添加自定义分类", "请输入分类名称:");
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
            var newName = ShowInputDialog("重命名分类", $"将 \"{oldName}\" 重命名为:");
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
            var input = ShowInputDialog("添加零件", "请输入零件名称:");
            if (string.IsNullOrWhiteSpace(input)) return;

            var category = ShowInputDialog("分类", "请输入分类 (可选):") ?? "";
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
            var name = ShowInputDialog("添加模板", "请输入模板名称:");
            if (string.IsNullOrWhiteSpace(name)) return;

            var template = new CustomTemplate { Name = name.Trim() };
            while (true)
            {
                var paramStr = ShowInputDialog("添加参数",
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

        // === Tab 4: AI Assistant ===

        private void AddWelcomeMessage()
        {
            ChatList.Items.Add(new ChatMsg
            {
                Text = "SW AI 助手已就绪。输入中文描述即可建模。\n例如: 创建M10螺栓",
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(232, 238, 254)),
                ForegroundBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33))
            });
        }

        private void AiInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
                AiSendBtn_Click(sender, e);
        }

        private async void AiSendBtn_Click(object sender, RoutedEventArgs e)
        {
            var message = AiInput.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            ChatList.Items.Add(new ChatMsg
            {
                Text = message,
                BackgroundBrush = new SolidColorBrush(Color.FromRgb(21, 101, 192)),
                ForegroundBrush = new SolidColorBrush(Colors.White)
            });
            AiInput.Text = "";
            AiSendBtn.IsEnabled = false;

            try
            {
                var body = JsonSerializer.Serialize(new { message = message });
                var content = new StringContent(body, Encoding.UTF8, "application/json");
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/chat", content);

                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    var doc = JsonDocument.Parse(json);
                    var reply = doc.RootElement.GetProperty("reply").GetString() ?? "无回复";

                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = reply,
                        BackgroundBrush = new SolidColorBrush(Colors.White),
                        ForegroundBrush = new SolidColorBrush(Color.FromRgb(33, 33, 33))
                    });
                }
                else
                {
                    ChatList.Items.Add(new ChatMsg
                    {
                        Text = $"服务错误 ({(int)response.StatusCode})",
                        BackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                        ForegroundBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
                    });
                }
            }
            catch (Exception ex)
            {
                ChatList.Items.Add(new ChatMsg
                {
                    Text = $"连接失败: {ex.Message}",
                    BackgroundBrush = new SolidColorBrush(Color.FromRgb(255, 235, 238)),
                    ForegroundBrush = new SolidColorBrush(Color.FromRgb(198, 40, 40))
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
                var response = await _httpClient.PostAsync($"{_backendUrl}/api/config", content);
                if (response.IsSuccessStatusCode)
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
                var response = await _httpClient.GetAsync($"{_backendUrl}/api/config");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
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
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
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
                bool isConnected = _connector.IsConnected;
                SwStatus.Text = isConnected ? "已连接" : "未连接";
                SwStatus.Foreground = isConnected
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                SwDot.Fill = isConnected
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
                var response = await _httpClient.GetAsync($"{_backendUrl}/api/health");
                bool isOk = response.IsSuccessStatusCode;
                BackendStatus.Text = isOk ? "已连接" : "连接失败";
                BackendStatus.Foreground = isOk
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));
                BackendDot.Fill = isOk
                    ? new SolidColorBrush(Color.FromRgb(46, 125, 50))
                    : new SolidColorBrush(Color.FromRgb(198, 40, 40));

                if (isOk)
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
                var response = await _httpClient.GetAsync($"{_backendUrl}/api/config");
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
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

            UpdateFooterStatus();
        }

        private void UpdateFooterStatus()
        {
            bool isAiOk = BackendStatus.Text == "已连接";
            var okColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            var errorColor = new SolidColorBrush(Color.FromRgb(198, 40, 40));

            StatusAI.Text = isAiOk ? "AI OK" : "AI X";
            StatusAI.Foreground = isAiOk ? okColor : errorColor;
            StatusAI2.Text = StatusAI.Text;
            StatusAI2.Foreground = StatusAI.Foreground;

            bool isPartsOk = File.Exists(Path.Combine(BaseDir, "Data", "standard_parts.json"));
            StatusParts.Text = isPartsOk ? "标准件 OK" : "标准件 X";
            StatusParts.Foreground = isPartsOk ? okColor : errorColor;
            StatusParts2.Text = StatusParts.Text;
            StatusParts2.Foreground = StatusParts.Foreground;

            bool isLibOk = !string.IsNullOrEmpty(_modelLibPath) && Directory.Exists(_modelLibPath);
            StatusLib.Text = isLibOk ? "模型库 OK" : "模型库 X";
            StatusLib.Foreground = isLibOk ? okColor : errorColor;
            StatusLib2.Text = StatusLib.Text;
            StatusLib2.Foreground = StatusLib.Foreground;
        }

        // === Version Info Popup ===

        private void FooterVersion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            _versionPopupOpen = !_versionPopupOpen;
            VersionPopupVersion.Text = $"v{_version}";
            VersionPopup.Visibility = _versionPopupOpen ? Visibility.Visible : Visibility.Collapsed;
        }

        private void VersionCloseBtn_Click(object sender, RoutedEventArgs e)
        {
            _versionPopupOpen = false;
            VersionPopup.Visibility = Visibility.Collapsed;
        }

        private void VersionCheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            _versionPopupOpen = false;
            VersionPopup.Visibility = Visibility.Collapsed;
            CheckForUpdateAsync();
        }

        private void GitHubLink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
            catch { }
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
                    UpdateTitle.Text = $"v{_version} -> {latest}";
                    UpdateNotes.Text = notes ?? "暂无更新说明";
                }
            }
            catch { }
        }

        private void StartPeriodicUpdateCheck()
        {
            _updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromHours(UpdateCheckIntervalHours)
            };
            _updateTimer.Tick += (sender, args) => CheckForUpdateAsync();
            _updateTimer.Start();
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

                if (_pendingUpdateVersion != null)
                {
                    _version = _pendingUpdateVersion.TrimStart('v', 'V');
                    SaveConfig();
                }

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

        // === Helper Methods ===

        /// <summary>
        /// Simple input dialog using a WPF Window.
        /// </summary>
        private static string? ShowInputDialog(string title, string prompt)
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

            string? result = null;
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

        private void SyncParamInputValues()
        {
            foreach (var container in PartParamInputs.Items)
            {
                if (container is ParamInput paramInput)
                {
                    var element = PartParamInputs.ItemContainerGenerator.ContainerFromItem(container);
                    if (element is FrameworkElement fe)
                    {
                        var textBox = FindVisualChild<System.Windows.Controls.TextBox>(fe);
                        if (textBox != null)
                        {
                            var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                            binding?.UpdateSource();
                        }
                    }
                }
            }
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
                        var textBox = FindVisualChild<System.Windows.Controls.TextBox>(fe);
                        if (textBox != null)
                        {
                            var binding = textBox.GetBindingExpression(System.Windows.Controls.TextBox.TextProperty);
                            binding?.UpdateSource();
                        }
                    }
                }
            }
        }

        private Dictionary<string, object> CollectParamInputs()
        {
            var parameters = new Dictionary<string, object>();
            foreach (var item in PartParamInputs.Items)
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

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current != null)
            {
                if (current is T result) return result;
                current = System.Windows.Media.VisualTreeHelper.GetParent(current);
            }
            return null;
        }

        private static void Log(string message)
        {
            try
            {
                File.AppendAllText(LogFilePath, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
            }
            catch { }
        }
    }
}
