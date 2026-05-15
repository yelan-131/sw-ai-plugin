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
        // === Constants ===

        private const string DefaultBackendUrl = "http://localhost:8765";
        private const string PlaceholderSearchText = "搜索零件...";
        private const int HttpTimeoutSeconds = 60;
        /// <summary>默认更新检查周期，会被 UserConfig.CheckIntervalHours 覆盖。</summary>
        private const int DefaultUpdateCheckIntervalHours = 4;
        private static readonly string BaseDir = Path.GetDirectoryName(
            typeof(MainTaskPaneView).Assembly.Location);
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
        private string _version = "0.1.3";
        private readonly PluginConfigService _configService;
        private UserConfig _userConfig = new UserConfig();
        private PluginMeta _pluginMeta = new PluginMeta();
        private readonly UpdateService _updateService;
        private string? _pendingUpdateUrl;
        private string? _pendingUpdateVersion;
        private UpdateManifest? _pendingManifest;
        private bool _updatePanelOpen;
        private string? _lastKnownVersion;
        private CustomLibraryData _customData = new();
        private readonly PartsSearchService _searchService = new();
        private readonly CustomCategoryService _categoryService;
        private List<Category> _allCategories = new();
        private System.Windows.Threading.DispatcherTimer? _updateTimer;
        private bool _isChecking;
        private string? _downloadedZipPath;
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

        public class ReleaseNoteDisplaySection
        {
            public string Section { get; set; } = "";
            public List<ReleaseNoteDisplayItem> Items { get; set; } = new List<ReleaseNoteDisplayItem>();
        }

        public class ReleaseNoteDisplayItem
        {
            public string Index { get; set; } = "";   // "1.", "2.", ...
            public string Text { get; set; } = "";
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
            _configService = new PluginConfigService(BaseDir);
            LoadConfig();
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(HttpTimeoutSeconds) };
            _updateService = new UpdateService(_pluginMeta, _userConfig);
            _lastKnownVersion = _version;
            BackendUrlInput.Text = _backendUrl;
            ModelLibPathInput.Text = _modelLibPath;
            FooterVersion.Text = $"v{_version}";
            SetupTreeViewTemplates();
            LoadPartsLibrary();
            LoadCustomLibrary();
            RefreshTemplateCombo();
            AddWelcomeMessage();
            CheckAllStatus();
            // 延迟检查更新（等 SW 加载完毕后再检查）
            var delayedCheck = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
            delayedCheck.Tick += (s, args) =>
            {
                delayedCheck.Stop();
                CheckForUpdateAsync(isManual: false);
            };
            delayedCheck.Start();
            StartPeriodicUpdateCheck();
        }
        // === Config ===

        private void LoadConfig()
        {
            try
            {
                _userConfig = _configService.LoadUserConfig();
                _pluginMeta = _configService.LoadPluginMeta();

                _backendUrl = string.IsNullOrEmpty(_userConfig.BackendUrl) ? DefaultBackendUrl : _userConfig.BackendUrl;
                _modelLibPath = _userConfig.ModelLibraryPath ?? "";
                _version = string.IsNullOrEmpty(_pluginMeta.Version) ? "0.1.3" : _pluginMeta.Version;
            }
            catch { }
        }

        /// <summary>
        /// 仅保存用户配置。版本元数据由 Updater 在更新成功后写入，主插件不会触发。
        /// </summary>
        private void SaveConfig()
        {
            try
            {
                _userConfig.BackendUrl = _backendUrl;
                _userConfig.ModelLibraryPath = _modelLibPath;
                _configService.SaveUserConfig(_userConfig);
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
    }
}
