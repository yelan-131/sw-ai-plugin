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
using Microsoft.Win32;
using SolidWorks.Interop.sldworks;
using SwComAddin.Helpers;
using SwComAddin.Models;
using SwComAddin.Services;
namespace SwComAddin.Views
{
    public partial class MainTaskPaneView : UserControl
    {
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

            // 检查是否刚完成更新（版本号变更）
            CheckPostUpdateGreenBadge();
        }

        private void UpdateFooterStatus()
        {
            var okColor = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            var errorColor = new SolidColorBrush(Color.FromRgb(198, 40, 40));

            bool isAiOk = BackendStatus.Text == "已连接";
            StatusAIDot.Fill = isAiOk ? okColor : errorColor;

            bool isPartsOk = File.Exists(Path.Combine(BaseDir, "Data", "standard_parts.json"));
            StatusPartsDot.Fill = isPartsOk ? okColor : errorColor;

            bool isLibOk = !string.IsNullOrEmpty(_modelLibPath) && Directory.Exists(_modelLibPath);
            StatusLibDot.Fill = isLibOk ? okColor : errorColor;
        }

        // === Version Display State ===

        private enum VersionDisplayState { Normal, HasUpdate, Downloading, Ready, Updated, Error }

        private void SetVersionDisplay(VersionDisplayState state)
        {
            switch (state)
            {
                case VersionDisplayState.Normal:
                    FooterVersion.Text = $"v{_version}";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102));
                    FooterVersion.Cursor = Cursors.Arrow;
                    break;
                case VersionDisplayState.HasUpdate:
                    FooterVersion.Text = $"v{_version} → {_pendingUpdateVersion} ↗";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    FooterVersion.Cursor = Cursors.Hand;
                    break;
                case VersionDisplayState.Downloading:
                    // Text updated by progress callback
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    FooterVersion.Cursor = Cursors.Arrow;
                    break;
                case VersionDisplayState.Ready:
                    FooterVersion.Text = $"v{_version} ✓就绪";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(0, 120, 215));
                    FooterVersion.Cursor = Cursors.Hand;
                    break;
                case VersionDisplayState.Updated:
                    FooterVersion.Text = $"v{_version} ✓";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    FooterVersion.Cursor = Cursors.Arrow;
                    break;
                case VersionDisplayState.Error:
                    FooterVersion.Text = $"v{_version} ⚠";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(255, 143, 0));
                    FooterVersion.Cursor = Cursors.Hand;
                    break;
            }
        }

        // === Version Click (expand/collapse update panel) ===

        private void FooterVersion_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_updatePanelOpen)
            {
                ShowUpdatePanel(false);
            }
            else if (_pendingManifest != null)
            {
                ShowUpdatePanel(true);
            }
        }

        // === Update UI State Machine ===

        private enum UpdateUIState
        {
            Idle,           // 显示 release notes + [稍后提醒] [立即更新]
            Downloading,    // 显示进度条 + [取消]
            Ready,          // 显示安装确认 + [取消] [保存并安装]
            Installing,     // 正在安装，按钮禁用
            Error           // 显示错误 + [稍后提醒] [重试] [手动下载]
        }

        private void SetUpdateUIState(UpdateUIState state)
        {
            switch (state)
            {
                case UpdateUIState.Idle:
                    UpdateNowBtn.IsEnabled = true;
                    UpdateNowBtn.Content = "立即更新";
                    UpdateLaterBtn.IsEnabled = true;
                    UpdateLaterBtn.Content = "稍后提醒 ▾";
                    ManualDownloadRow.Visibility = Visibility.Collapsed;
                    break;
                case UpdateUIState.Downloading:
                    UpdateNowBtn.IsEnabled = false;
                    UpdateNowBtn.Content = "下载中...";
                    UpdateLaterBtn.IsEnabled = false;
                    ManualDownloadRow.Visibility = Visibility.Collapsed;
                    break;
                case UpdateUIState.Ready:
                    UpdateNowBtn.IsEnabled = true;
                    UpdateNowBtn.Content = "保存并安装";
                    UpdateLaterBtn.IsEnabled = true;
                    UpdateLaterBtn.Content = "取消";
                    ManualDownloadRow.Visibility = Visibility.Collapsed;
                    break;
                case UpdateUIState.Installing:
                    UpdateNowBtn.IsEnabled = false;
                    UpdateNowBtn.Content = "正在安装...";
                    UpdateLaterBtn.IsEnabled = false;
                    ManualDownloadRow.Visibility = Visibility.Collapsed;
                    break;
                case UpdateUIState.Error:
                    UpdateNowBtn.IsEnabled = true;
                    UpdateNowBtn.Content = "重试";
                    UpdateLaterBtn.IsEnabled = true;
                    UpdateLaterBtn.Content = "稍后提醒 ▾";
                    ManualDownloadRow.Visibility = Visibility.Visible;
                    _downloadedZipPath = null;
                    break;
            }
        }

        // === Update Check ===

        private async void CheckForUpdateAsync(bool isManual = false)
        {
            if (_isChecking)
            {
                if (isManual)
                {
                    CheckUpdateBtn.IsEnabled = true;
                    CheckUpdateBtn.Content = "检查更新";
                    UpdateCheckResultText.Text = "已有检查任务在进行中，请稍候...";
                    UpdateCheckResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8F00"));
                    UpdateCheckResult.Visibility = Visibility.Visible;
                }
                return;
            }

            _isChecking = true;
            try
            {
                if (isManual)
                {
                    // 手动检查：只在 Tab5 内显示状态，不操作底部 UpdatePanel
                    _downloadedZipPath = null;
                    CheckUpdateBtn.IsEnabled = false;
                    CheckUpdateBtn.Content = "检查中...";
                    UpdateCheckResult.Visibility = Visibility.Visible;
                    UpdateCheckResultText.Text = "正在检查更新...";
                    UpdateCheckResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF8F00"));
                }

                Log("Update: starting check...");
                var result = await _updateService.CheckForUpdateAsync();
                Log($"Update: done. Deferred={result.Deferred} Error={result.ErrorCode} HasUpdate={result.HasUpdate} Source={result.Source} Version={result.Manifest?.Version}");

                if (result.Deferred)
                {
                    Log("Update: deferred, skipping");
                    if (isManual)
                    {
                        ShowManualCheckResult(false, "已稍后提醒，窗口期内不再检查");
                    }
                    return;
                }

                if (result.ErrorCode != null && result.Manifest == null)
                {
                    Log($"Update: fail - {result.ErrorCode}: {result.ErrorMessage}");
                    if (isManual)
                    {
                        ShowManualCheckResult(false, $"✗ 检查失败：{result.ErrorMessage ?? "网络错误"}");
                    }
                    else
                    {
                        SetVersionDisplay(VersionDisplayState.Error);
                    }
                    return;
                }

                if (result.HasUpdate && result.Manifest != null)
                {
                    Log($"Update: showing update UI for {result.Manifest.Version}");
                    _pendingManifest = result.Manifest;
                    _pendingUpdateUrl = result.Manifest.Package?.PrimaryUrl;
                    _pendingUpdateVersion = result.Manifest.Version;

                    SetVersionDisplay(VersionDisplayState.HasUpdate);

                    if (isManual)
                    {
                        // Tab5 内显示结果
                        ShowManualCheckResult(true, $"发现新版本 v{result.Manifest.Version}");

                        // 准备底部 UpdatePanel 数据（但不展开，等用户点击版本号或选择版本后再展开）
                        UpdateTitle.Text = $"v{_version} → {result.Manifest.Version}";
                        UpdateMetaText.Text = BuildMetaLine(result.Manifest, result.Source);
                        RenderReleaseNotes(result.Manifest);
                        SetUpdateUIState(UpdateUIState.Idle);
                    }
                    else
                    {
                        // 自动检查：准备底部面板数据，等用户点击版本号展开
                        UpdateTitle.Text = $"v{_version} → {result.Manifest.Version}";
                        UpdateMetaText.Text = BuildMetaLine(result.Manifest, result.Source);
                        RenderReleaseNotes(result.Manifest);
                        SetUpdateUIState(UpdateUIState.Idle);
                    }
                }
                else if (isManual)
                {
                    ShowManualCheckResult(false, $"✓ 已是最新版本 v{_version}");
                }
            }
            catch (Exception ex)
            {
                Log($"Update: exception - {ex.Message}");
                if (isManual)
                {
                    ShowManualCheckResult(false, $"✗ 检查失败：{ex.Message}");
                }
                else
                {
                    SetVersionDisplay(VersionDisplayState.Error);
                }
            }
            finally
            {
                _isChecking = false;
                if (isManual)
                {
                    CheckUpdateBtn.IsEnabled = true;
                    CheckUpdateBtn.Content = "检查更新";
                }
            }
        }

        private void StartPeriodicUpdateCheck()
        {
            int hours = _userConfig.CheckIntervalHours > 0 ? _userConfig.CheckIntervalHours : DefaultUpdateCheckIntervalHours;
            _updateTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromHours(hours)
            };
            _updateTimer.Tick += (sender, args) => CheckForUpdateAsync(isManual: false);
            _updateTimer.Start();
        }

        // === Download & Install ===

        private async void UpdateNowBtn_Click(object sender, RoutedEventArgs e)
        {
            if (_pendingManifest == null) return;

            // Phase 2: already downloaded, install directly
            if (!string.IsNullOrEmpty(_downloadedZipPath))
            {
                DoInstall(_downloadedZipPath);
                return;
            }

            // Start download
            SetUpdateUIState(UpdateUIState.Downloading);
            SetVersionDisplay(VersionDisplayState.Downloading);
            ShowUpdateError(null, null);
            UpdateProgressArea.Visibility = Visibility.Visible;
            UpdateProgressBar.Value = 0;
            UpdateProgressText.Text = "正在准备下载...";

            try
            {
                var progress = new Progress<UpdateService.DownloadProgress>(p =>
                {
                    UpdateProgressBar.Value = p.Fraction * 100;
                    UpdateProgressText.Text = FormatProgress(p);
                    // 同时更新版本号显示下载进度
                    FooterVersion.Text = $"↓ {(int)(p.Fraction * 100)}%";
                });

                var zipPath = await _updateService.DownloadUpdateAsync(_pendingManifest, progress);

                UpdateProgressText.Text = "✓ 校验通过，准备就绪";
                _downloadedZipPath = zipPath;
                SetUpdateUIState(UpdateUIState.Ready);
                SetVersionDisplay(VersionDisplayState.Ready);
            }
            catch (OperationCanceledException)
            {
                ShowUpdateError("下载已取消", UpdateErrorCodes.DownloadCancelled);
                SetUpdateUIState(UpdateUIState.Error);
                SetVersionDisplay(VersionDisplayState.Error);
            }
            catch (System.IO.InvalidDataException ex)
            {
                ShowUpdateError(ex.Message, UpdateErrorCodes.VerifyHashMismatch);
                SetUpdateUIState(UpdateUIState.Error);
                SetVersionDisplay(VersionDisplayState.Error);
            }
            catch (Exception ex)
            {
                ShowUpdateError($"更新失败：{ex.Message}", UpdateErrorCodes.DownloadHttp);
                SetUpdateUIState(UpdateUIState.Error);
                SetVersionDisplay(VersionDisplayState.Error);
            }
        }

        private void InstallFromDownloadedZip(string zipPath)
        {
            if (_pendingManifest == null) return;
            SetUpdateUIState(UpdateUIState.Ready);
            SetVersionDisplay(VersionDisplayState.Ready);
        }

        private void DoInstall(string zipPath)
        {
            try
            {
                // 1. 自动保存所有脏文档
                SaveAllDirtyDocuments();

                // 2. 准备更新
                var batPath = _updateService.PrepareUpdate(zipPath, BaseDir, _pendingManifest);

                // 3. 启动 bat
                _updateService.ExecuteUpdate(batPath);

                // 4. 退出 SolidWorks
                SetUpdateUIState(UpdateUIState.Installing);
                var swApp = _connector.GetSwApp() as ISldWorks;
                swApp?.ExitApp();
            }
            catch (Exception ex)
            {
                ShowUpdateError($"安装失败：{ex.Message}", null);
                SetUpdateUIState(UpdateUIState.Error);
                SetVersionDisplay(VersionDisplayState.Error);
            }
        }

        /// <summary>
        /// 自动保存所有有未保存修改的 SolidWorks 文档。
        /// </summary>
        private void SaveAllDirtyDocuments()
        {
            try
            {
                var swApp = _connector.GetSwApp() as ISldWorks;
                if (swApp == null) return;
                var doc = swApp.GetFirstDocument() as IModelDoc2;
                while (doc != null)
                {
                    try { if (doc.GetSaveFlag()) doc.Save(); } catch { }
                    doc = doc.GetNext() as IModelDoc2;
                }
            }
            catch { }
        }

        // === Defer (稍后提醒) with popup ===

        private void UpdateLaterBtn_Click(object sender, RoutedEventArgs e)
        {
            // If in Ready state, "cancel" means go back to idle
            if (!string.IsNullOrEmpty(_downloadedZipPath))
            {
                _downloadedZipPath = null;
                SetUpdateUIState(UpdateUIState.Idle);
                UpdateProgressArea.Visibility = Visibility.Collapsed;
                SetVersionDisplay(VersionDisplayState.HasUpdate);
                return;
            }

            DeferPopup.IsOpen = true;
        }

        private void DeferOption_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string hoursStr && int.TryParse(hoursStr, out int hours))
            {
                _userConfig.DeferUntilUtc = DateTime.UtcNow.AddHours(hours).ToString("o");
                _configService.SaveUserConfig(_userConfig);
                DeferPopup.IsOpen = false;
                ShowUpdatePanel(false);
                SetVersionDisplay(VersionDisplayState.Normal);
            }
        }

        // === Keyboard ===

        private void UserControl_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Escape) return;

            if (_updatePanelOpen)
            {
                ShowUpdatePanel(false);
                e.Handled = true;
            }
        }

        // === Update UI helpers ===

        private void ShowUpdatePanel(bool show)
        {
            _updatePanelOpen = show;
            UpdatePanel.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            if (show)
            {
                double available = this.ActualHeight > 0 ? this.ActualHeight : 600;
                UpdateNotesScroller.MaxHeight = Math.Max(60, (available - 220) * 0.5);
            }
        }

        private void ShowUpdateError(string? message, string? code)
        {
            if (string.IsNullOrEmpty(message))
            {
                UpdateErrorArea.Visibility = Visibility.Collapsed;
                UpdateErrorText.Text = "";
                UpdateErrorCodeText.Text = "";
            }
            else
            {
                UpdateErrorArea.Visibility = Visibility.Visible;
                UpdateErrorText.Text = message;
                UpdateErrorCodeText.Text = code != null ? $"错误码：{code}" : "";
            }
        }

        private void RenderReleaseNotes(UpdateManifest? manifest)
        {
            if (manifest?.ReleaseNotes != null && manifest.ReleaseNotes.Count > 0)
            {
                UpdateNotesItems.Visibility = Visibility.Visible;
                UpdateNotes.Visibility = Visibility.Collapsed;
                var displaySections = new List<ReleaseNoteDisplaySection>();
                int sIdx = 0;
                foreach (var section in manifest.ReleaseNotes)
                {
                    sIdx++;
                    var items = new List<ReleaseNoteDisplayItem>();
                    for (int i = 0; i < section.Items.Count; i++)
                        items.Add(new ReleaseNoteDisplayItem
                        {
                            Index = $"{sIdx}.{i + 1}",
                            Text = section.Items[i]
                        });
                    displaySections.Add(new ReleaseNoteDisplaySection
                    {
                        Section = $"■ {section.Section}",
                        Items = items
                    });
                }
                UpdateNotesItems.ItemsSource = displaySections;
            }
            else if (manifest != null && !string.IsNullOrEmpty(manifest.ReleaseNotesSummary))
            {
                UpdateNotesItems.Visibility = Visibility.Collapsed;
                UpdateNotes.Visibility = Visibility.Visible;
                UpdateNotes.Text = manifest.ReleaseNotesSummary;
            }
            else
            {
                UpdateNotesItems.Visibility = Visibility.Collapsed;
                UpdateNotes.Visibility = Visibility.Visible;
                UpdateNotes.Text = "暂无更新说明";
            }
        }

        private static string FormatProgress(UpdateService.DownloadProgress p)
        {
            string size = p.TotalBytes.HasValue
                ? $"{FormatBytes(p.BytesReceived)} / {FormatBytes(p.TotalBytes.Value)}"
                : FormatBytes(p.BytesReceived);
            string speed = p.BytesPerSecond > 1 ? $"  {FormatBytes((long)p.BytesPerSecond)}/s" : "";
            return $"{(int)(p.Fraction * 100)}%  {size}{speed}";
        }

        private static string FormatBytes(long bytes)
        {
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / 1024.0 / 1024.0:F2} MB";
        }

        private static string BuildMetaLine(Models.UpdateManifest manifest, string source)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(manifest.ReleasedAt)) parts.Add(manifest.ReleasedAt);
            if (manifest.Package != null && manifest.Package.Size > 0)
                parts.Add(FormatBytes(manifest.Package.Size));
            if (!string.IsNullOrEmpty(source)) parts.Add($"源: {source}");
            return string.Join(" · ", parts);
        }

        /// <summary>
        /// 检查 SolidWorks 中是否有未保存的文档，避免安装前丢数据。
        /// </summary>
        private bool HasUnsavedSwDocuments(out string busyDocs)
        {
            busyDocs = "";
            try
            {
                var swApp = _connector.GetSwApp() as ISldWorks;
                if (swApp == null) return false;

                var dirty = new List<string>();
                var doc = swApp.GetFirstDocument() as IModelDoc2;
                while (doc != null)
                {
                    try
                    {
                        if (doc.GetSaveFlag())
                            dirty.Add(doc.GetTitle() ?? "(未命名)");
                    }
                    catch { }
                    doc = doc.GetNext() as IModelDoc2;
                }
                if (dirty.Count == 0) return false;
                busyDocs = string.Join("\n", dirty.Select(d => "  • " + d));
                return true;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// 检查是否刚完成更新（版本号变更），显示绿色确认标记。
        /// </summary>
        private void CheckPostUpdateGreenBadge()
        {
            try
            {
                var currentMeta = _configService.LoadPluginMeta();
                if (!string.IsNullOrEmpty(currentMeta.Version) && currentMeta.Version != _lastKnownVersion)
                {
                    FooterVersion.Text = $"v{currentMeta.Version} ✓";
                    FooterVersion.Foreground = new SolidColorBrush(Color.FromRgb(46, 125, 50));
                    _lastKnownVersion = currentMeta.Version;
                    _version = currentMeta.Version;

                    var timer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                    timer.Tick += (s, e) =>
                    {
                        timer.Stop();
                        if (_pendingManifest == null)
                            SetVersionDisplay(VersionDisplayState.Normal);
                    };
                    timer.Start();
                }
            }
            catch { }
        }

        // === Manual download handlers ===

        private void GitHubReleaseBtn_Click(object sender, RoutedEventArgs e)
        {
            string repo = _pluginMeta.UpdateRepo ?? "yelan-131/sw-ai-plugin";
            OpenUrl($"https://github.com/{repo}/releases");
        }

        private void GiteeReleaseBtn_Click(object sender, RoutedEventArgs e)
        {
            string repo = _pluginMeta.GiteeRepo ?? "yelan1387/sw-ai-plugin";
            OpenUrl($"https://gitee.com/{repo}/releases");
        }

        private void LocalZipBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Title = "选择更新包",
                Filter = "ZIP 文件 (*.zip)|*.zip",
                DefaultExt = ".zip"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                string hash = UpdateService.ComputeSha256(dlg.FileName);
                string? expected = _pendingManifest?.Package?.Sha256;
                if (!string.IsNullOrEmpty(expected) &&
                    !string.Equals(hash, expected, StringComparison.OrdinalIgnoreCase))
                {
                    ShowUpdateError($"SHA256 校验失败。\n期望: {expected.Substring(0, 16)}...\n实际: {hash.Substring(0, 16)}...",
                        UpdateErrorCodes.VerifyHashMismatch);
                    return;
                }

                _downloadedZipPath = dlg.FileName;
                ShowUpdateError(null, null);
                UpdateProgressArea.Visibility = Visibility.Collapsed;
                SetUpdateUIState(UpdateUIState.Ready);
                UpdateProgressText.Text = "✓ 本地包已就绪";
                UpdateProgressArea.Visibility = Visibility.Visible;
                UpdateProgressBar.Value = 100;
                SetVersionDisplay(VersionDisplayState.Ready);
            }
            catch (Exception ex)
            {
                ShowUpdateError($"读取文件失败：{ex.Message}", null);
            }
        }

        private static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
                { UseShellExecute = true });
            }
            catch { }
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
        // === Tab5: Update Settings ===

        private void CheckUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckUpdateBtn.IsEnabled = false;
            CheckUpdateBtn.Content = "检查中...";
            UpdateCheckResult.Visibility = Visibility.Visible;
            UpdateCheckResultText.Text = "正在检查更新...";

            CheckForUpdateAsync(isManual: true);
        }

        private void ShowManualCheckResult(bool hasUpdate, string message)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                CheckUpdateBtn.IsEnabled = true;
                CheckUpdateBtn.Content = "检查更新";

                UpdateCheckResult.Visibility = Visibility.Visible;
                if (hasUpdate)
                {
                    UpdateCheckResultText.Text = message;
                    UpdateCheckResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0078D7"));
                }
                else
                {
                    UpdateCheckResultText.Text = message;
                    UpdateCheckResultText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2E7D32"));
                }
            }));
        }

        private async void OfflineUpdateBtn_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "ZIP 文件|*.zip",
                Title = "选择更新包"
            };
            if (dlg.ShowDialog() != true) return;

            var zipPath = dlg.FileName;
            OfflineFileInfo.Text = $"已选择：{Path.GetFileName(zipPath)} ({new FileInfo(zipPath).Length / 1024.0 / 1024.0:F1}MB)";
            OfflineFileInfo.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#888888"));
            OfflineFileInfo.Visibility = Visibility.Visible;

            // 将 ZIP 路径设为已下载状态，走现有安装流程
            _downloadedZipPath = zipPath;
            OfflineFileInfo.Text += "\n✓ 已就绪，点击下方「保存并安装」完成更新";
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
