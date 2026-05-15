using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;
using Microsoft.Win32;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SwComAddin.Models;

namespace SwComAddin.Services
{
    /// <summary>
    /// 更新流程：检查 → 下载 manifest → 决策 → 下载 ZIP → SHA256 校验 →
    /// 生成 update.bat（Iteration 1 暂保留 .bat 路线，Iteration 2 再切 Updater.exe）→ 执行。
    /// 数据契约：远端 Release 必须同时包含 manifest.json 与 ZIP 包，
    /// 旧版（仅 ZIP，无 manifest）会被识别为「无法升级」并提示需先重装新版。
    /// </summary>
    public class UpdateService
    {
        private const string UpdateSubDir = "SwAiPlugin_update";
        private const string ManifestAssetName = "manifest.json";

        /// <summary>下载 ZIP 包的 buffer。</summary>
        private const int DownloadBufferBytes = 81920;

        /// <summary>HttpClient 通用超时（仅 manifest，下载 ZIP 用单独 client）。</summary>
        private static readonly TimeSpan MetaTimeout = TimeSpan.FromSeconds(15);

        /// <summary>下载 ZIP 的超时，可被取消令牌中断。</summary>
        private static readonly TimeSpan PackageTimeout = TimeSpan.FromMinutes(30);

        private readonly PluginMeta _meta;
        private readonly UserConfig _userCfg;
        private readonly HttpClient _metaClient;

        public UpdateService(PluginMeta meta, UserConfig userCfg)
        {
            _meta = meta ?? new PluginMeta();
            _userCfg = userCfg ?? new UserConfig();
            _metaClient = CreateHttpClient(MetaTimeout);
        }

        // ────────────────────────── Public API ──────────────────────────

        public string GetCurrentVersion() => string.IsNullOrEmpty(_meta.Version) ? "0.1.1" : _meta.Version;

        /// <summary>
        /// 完整的检查结果，封装给 UI 层。失败时 <see cref="ErrorCode"/> 不为空。
        /// </summary>
        public class CheckResult
        {
            public bool HasUpdate { get; set; }
            public bool Skipped { get; set; }            // 用户主动 skip 过此版本
            public bool Deferred { get; set; }           // 在「稍后提醒」窗口内
            public string Source { get; set; } = "";    // gitee | github | mirror
            public UpdateManifest? Manifest { get; set; }
            public string? ErrorCode { get; set; }
            public string? ErrorMessage { get; set; }
        }

        public async Task<CheckResult> CheckForUpdateAsync(CancellationToken cancel = default)
        {
            UpdateLogger.Info("check", new Dictionary<string, object?> { ["phase"] = "started", ["current"] = GetCurrentVersion() });
            // 1. 「稍后提醒」窗口内则跳过
            if (TryParseUtc(_userCfg.DeferUntilUtc, out var deferUntil) && DateTime.UtcNow < deferUntil)
            {
                UpdateLogger.Info("check", new Dictionary<string, object?>
                {
                    ["result"] = "deferred",
                    ["defer_until"] = _userCfg.DeferUntilUtc
                });
                return new CheckResult { Deferred = true };
            }

            // 2. 按 UpdateSource 决定查询顺序
            var sources = ResolveSources();

            CheckResult? bestResult = null;
            foreach (var source in sources)
            {
                cancel.ThrowIfCancellationRequested();
                var r = await CheckSourceAsync(source, cancel);
                if (r.Manifest != null)
                {
                    if (bestResult == null || bestResult.Manifest == null) bestResult = r;
                    else
                    {
                        if (SemanticVersion.TryParse(r.Manifest.Version, out var vNew) &&
                            SemanticVersion.TryParse(bestResult.Manifest.Version, out var vOld) &&
                            vNew! > vOld!)
                        {
                            bestResult = r;
                        }
                    }
                }
                else if (bestResult == null)
                {
                    bestResult = r; // 保留错误信息
                }
            }

            if (bestResult?.Manifest == null)
            {
                return bestResult ?? new CheckResult { ErrorCode = UpdateErrorCodes.CheckNetwork, ErrorMessage = "未能联系任何更新源" };
            }

            var manifest = bestResult.Manifest;

            // 3. 通道过滤：用户未开 Beta 时拒绝 prerelease
            if (!_userCfg.ReceivePrerelease && IsPreRelease(manifest))
            {
                UpdateLogger.Info("check", new Dictionary<string, object?>
                {
                    ["result"] = "channel-mismatch",
                    ["version"] = manifest.Version,
                    ["channel"] = manifest.Channel
                });
                return new CheckResult { Source = bestResult.Source, Manifest = manifest, HasUpdate = false };
            }

            // 4. 版本比较
            if (!SemanticVersion.TryParse(manifest.Version, out var latest) ||
                !SemanticVersion.TryParse(GetCurrentVersion(), out var current))
            {
                return new CheckResult
                {
                    Source = bestResult.Source,
                    Manifest = manifest,
                    ErrorCode = UpdateErrorCodes.CheckParse,
                    ErrorMessage = "无法解析版本号"
                };
            }

            bool hasUpdate = latest! > current!;

            // 5. 跳过列表
            bool skipped = _userCfg.SkippedVersions != null &&
                           _userCfg.SkippedVersions.Contains(manifest.Version);

            UpdateLogger.Info("check", new Dictionary<string, object?>
            {
                ["result"] = hasUpdate ? (skipped ? "skipped" : "available") : "uptodate",
                ["current"] = current!.ToString(),
                ["latest"] = latest!.ToString(),
                ["source"] = bestResult.Source
            });

            return new CheckResult
            {
                HasUpdate = hasUpdate && !skipped,
                Skipped = skipped,
                Source = bestResult.Source,
                Manifest = manifest
            };
        }

        /// <summary>
        /// 下载 ZIP，按 manifest.primary_url → mirrors[] 顺序尝试。校验 SHA256，校验失败抛 IOException。
        /// </summary>
        public async Task<string> DownloadUpdateAsync(
            UpdateManifest manifest,
            IProgress<DownloadProgress>? progress,
            CancellationToken cancel = default)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), UpdateSubDir);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");

            var candidates = new List<string>();
            if (!string.IsNullOrEmpty(manifest.Package.PrimaryUrl))
                candidates.Add(manifest.Package.PrimaryUrl);
            if (manifest.Package.Mirrors != null)
                candidates.AddRange(manifest.Package.Mirrors.Where(u => !string.IsNullOrWhiteSpace(u)));

            if (candidates.Count == 0)
                throw new InvalidOperationException("Manifest 未提供任何下载 URL");

            Exception? lastError = null;
            foreach (var url in candidates)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    UpdateLogger.Info("download", new Dictionary<string, object?>
                    {
                        ["url"] = url,
                        ["size"] = manifest.Package.Size
                    });

                    await DownloadToFileAsync(url, zipPath, progress, cancel);

                    // SHA256 校验
                    var actual = ComputeSha256(zipPath);
                    var expected = (manifest.Package.Sha256 ?? string.Empty).Trim().ToLowerInvariant();
                    if (!string.IsNullOrEmpty(expected) && !string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
                    {
                        UpdateLogger.Error("verify", UpdateErrorCodes.VerifyHashMismatch, new Dictionary<string, object?>
                        {
                            ["expected"] = expected,
                            ["actual"] = actual,
                            ["url"] = url
                        });
                        try { File.Delete(zipPath); } catch { }
                        lastError = new InvalidDataException(
                            "SHA256 校验失败：期望 " +
                            expected.Substring(0, Math.Min(16, expected.Length)) + "…，实际 " +
                            actual.Substring(0, Math.Min(16, actual.Length)) + "…");
                        continue; // 尝试下一个镜像
                    }

                    UpdateLogger.Info("verify", new Dictionary<string, object?>
                    {
                        ["result"] = "ok",
                        ["sha256"] = actual
                    });
                    return zipPath;
                }
                catch (OperationCanceledException)
                {
                    UpdateLogger.Warn("download", UpdateErrorCodes.DownloadCancelled);
                    throw;
                }
                catch (Exception ex)
                {
                    UpdateLogger.Warn("download", UpdateErrorCodes.DownloadHttp, new Dictionary<string, object?>
                    {
                        ["url"] = url,
                        ["error"] = ex.Message
                    });
                    lastError = ex;
                }
            }

            throw lastError ?? new IOException("所有下载源均失败");
        }

        /// <summary>
        /// 生成 update.bat 接力脚本（含备份+回滚）。
        /// 注意：版本号不再由主插件写盘，而由 update.bat 在 RegAsm 成功后写入 plugin_meta.json。
        /// </summary>
        public string PrepareUpdate(string zipPath, string installDir, UpdateManifest manifest)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), UpdateSubDir);
            string extractDir = Path.Combine(tempDir, "files");
            string backupDir = Path.Combine(tempDir, "backup");

            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);
            Directory.CreateDirectory(extractDir);

            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

            // 防 Zip Slip：检查解压后所有文件都在 extractDir 之内
            string extractFull = Path.GetFullPath(extractDir);
            foreach (var f in Directory.EnumerateFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                if (!Path.GetFullPath(f).StartsWith(extractFull, StringComparison.OrdinalIgnoreCase))
                    throw new SecurityException("更新包中检测到非法路径，已中止");
            }

            string swPath = GetSolidWorksPath();
            string regAsm = @"%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe";

            // preserve 清单
            var preserve = new List<string>
            {
                "user_config.json",
                "Data/custom_library.json",
                "install.bat",
                "uninstall.bat"
            };
            if (manifest.Preserve != null)
                preserve.AddRange(manifest.Preserve);

            string excludeFile = Path.Combine(tempDir, "exclude.txt");
            File.WriteAllLines(excludeFile, preserve.Distinct(StringComparer.OrdinalIgnoreCase), Encoding.Default);

            // plugin_meta.json 内容：以远端 manifest 为准，由 .bat 写入
            string metaJson = JsonSerializer.Serialize(new
            {
                schema = "1.0",
                version = manifest.Version,
                channel = manifest.Channel,
                build_date = manifest.ReleasedAt,
                update_repo = _meta.UpdateRepo,
                gitee_repo = _meta.GiteeRepo
            });
            string metaPath = Path.Combine(tempDir, "plugin_meta.json");
            File.WriteAllText(metaPath, metaJson, new UTF8Encoding(false));

            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal ENABLEEXTENSIONS");
            sb.AppendLine();
            sb.AppendLine("set LOGFILE=" + EscapeBat(tempDir + "\\update.log"));
            sb.AppendLine("set BACKUP_DIR=" + EscapeBat(backupDir));
            sb.AppendLine("set INSTALL_DIR=" + EscapeBat(installDir));
            sb.AppendLine("set EXTRACT_DIR=" + EscapeBat(extractDir));
            sb.AppendLine("set EXCLUDE_FILE=" + EscapeBat(excludeFile));
            sb.AppendLine("set META_FILE=" + EscapeBat(metaPath));
            sb.AppendLine("set REGASM=" + regAsm);
            sb.AppendLine("set SW_PATH=" + EscapeBat(swPath));
            sb.AppendLine();
            sb.AppendLine("echo [%date% %time%] update.bat started > \"%LOGFILE%\"");
            sb.AppendLine("echo Updating SW AI Plugin...");
            sb.AppendLine();
            sb.AppendLine("echo Waiting for SolidWorks to exit...");
            sb.AppendLine(":wait");
            sb.AppendLine("tasklist /FI \"IMAGENAME eq SLDWORKS.exe\" 2>NUL | find /I /N \"SLDWORKS.exe\">NUL");
            sb.AppendLine("if \"%ERRORLEVEL%\"==\"0\" (");
            sb.AppendLine("    timeout /t 2 /nobreak >NUL");
            sb.AppendLine("    goto wait");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("echo Creating backup... >> \"%LOGFILE%\"");
            sb.AppendLine("if not exist \"%BACKUP_DIR%\" mkdir \"%BACKUP_DIR%\"");
            sb.AppendLine("xcopy /E /Y /Q /EXCLUDE:\"%EXCLUDE_FILE%\" \"%INSTALL_DIR%\\*\" \"%BACKUP_DIR%\\\" >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("if errorlevel 4 (");
            sb.AppendLine("    echo [ERROR] Backup failed, aborting update >> \"%LOGFILE%\"");
            sb.AppendLine("    goto :abort");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("echo Copying files... >> \"%LOGFILE%\"");
            sb.AppendLine("xcopy /E /Y /EXCLUDE:\"%EXCLUDE_FILE%\" \"%EXTRACT_DIR%\\*\" \"%INSTALL_DIR%\\\" >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("if errorlevel 1 (");
            sb.AppendLine("    echo [ERROR] xcopy failed >> \"%LOGFILE%\"");
            sb.AppendLine("    goto :rollback");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("echo Re-registering DLL... >> \"%LOGFILE%\"");
            sb.AppendLine("\"%REGASM%\" \"%INSTALL_DIR%\\SwComAddin.dll\" /codebase /tlb >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("if errorlevel 1 (");
            sb.AppendLine("    echo [ERROR] RegAsm failed >> \"%LOGFILE%\"");
            sb.AppendLine("    goto :rollback");
            sb.AppendLine(")");
            sb.AppendLine();
            sb.AppendLine("echo Writing plugin_meta.json... >> \"%LOGFILE%\"");
            sb.AppendLine("copy /Y \"%META_FILE%\" \"%INSTALL_DIR%\\plugin_meta.json\" >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("if errorlevel 1 (");
            sb.AppendLine("    echo [WARN] plugin_meta.json copy failed, retrying... >> \"%LOGFILE%\"");
            sb.AppendLine("    timeout /t 2 /nobreak >NUL");
            sb.AppendLine("    copy /Y \"%META_FILE%\" \"%INSTALL_DIR%\\plugin_meta.json\" >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine(")");
            sb.AppendLine("goto :success");
            sb.AppendLine();
            sb.AppendLine(":rollback");
            sb.AppendLine("echo Rolling back update... >> \"%LOGFILE%\"");
            sb.AppendLine("xcopy /E /Y /Q \"%BACKUP_DIR%\\*\" \"%INSTALL_DIR%\\\" >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("echo Re-registering original DLL... >> \"%LOGFILE%\"");
            sb.AppendLine("\"%REGASM%\" \"%INSTALL_DIR%\\SwComAddin.dll\" /codebase /tlb >> \"%LOGFILE%\" 2>&1");
            sb.AppendLine("echo [ROLLBACK] Update failed and was rolled back. Please restart SolidWorks. >> \"%LOGFILE%\"");
            sb.AppendLine("goto :cleanup");
            sb.AppendLine();
            sb.AppendLine(":abort");
            sb.AppendLine("echo [ABORT] Backup failed, update not applied. >> \"%LOGFILE%\"");
            sb.AppendLine("goto :cleanup");
            sb.AppendLine();
            sb.AppendLine(":success");
            sb.AppendLine("echo Update complete. Starting SolidWorks... >> \"%LOGFILE%\"");
            sb.AppendLine("rd /s /q \"%BACKUP_DIR%\" 2>NUL");
            sb.AppendLine("start \"\" \"%SW_PATH%\"");
            sb.AppendLine();
            sb.AppendLine(":cleanup");
            sb.AppendLine("echo Cleaning up... >> \"%LOGFILE%\"");
            sb.AppendLine("rd /s /q \"" + EscapeBat(tempDir) + "\"");
            sb.AppendLine("exit /b 0");

            string batPath = Path.Combine(tempDir, "update.bat");
            File.WriteAllText(batPath, sb.ToString(), Encoding.Default);

            UpdateLogger.Info("staging", new Dictionary<string, object?>
            {
                ["bat"] = batPath,
                ["install_dir"] = installDir,
                ["preserve_count"] = preserve.Count
            });

            return batPath;
        }

        private static string EscapeBat(string path) => path.Replace("&", "^&");

        public void ExecuteUpdate(string batPath)
        {
            UpdateLogger.Info("execute", new Dictionary<string, object?> { ["bat"] = batPath });

            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Hidden,
                CreateNoWindow = true
            });
        }

        // ────────────────────────── Public API: multi-release ──────────────────────────

        /// <summary>
        /// 单个 Release 的摘要信息，供 UI 版本选择器展示。
        /// </summary>
        public class ReleaseInfo
        {
            public string Version { get; set; } = "";
            public string ReleasedAt { get; set; } = "";
            public string Channel { get; set; } = "stable";
            public UpdateManifest? Manifest { get; set; }
            public string Source { get; set; } = "";
        }

        /// <summary>
        /// 获取最近 maxCount 个 Release 的 manifest 信息，供 Tab5 手动检查时的版本选择器使用。
        /// 汇总所有更新源，按版本号降序排列并去重（取最高版本对应的源）。
        /// </summary>
        public async Task<List<ReleaseInfo>> FetchAllReleasesAsync(int maxCount = 5, CancellationToken cancel = default)
        {
            UpdateLogger.Info("fetch-releases", new Dictionary<string, object?>
            {
                ["phase"] = "started",
                ["maxCount"] = maxCount
            });

            var sources = ResolveSources();
            var allReleases = new List<ReleaseInfo>();

            foreach (var source in sources)
            {
                cancel.ThrowIfCancellationRequested();
                try
                {
                    var releases = await FetchReleasesFromSourceAsync(source, maxCount, cancel);
                    allReleases.AddRange(releases);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    UpdateLogger.Warn("fetch-releases", UpdateErrorCodes.CheckNetwork, new Dictionary<string, object?>
                    {
                        ["source"] = source.Name,
                        ["error"] = ex.Message
                    });
                }
            }

            // 去重：同一版本号取最高版本对应的源（实际同一版本号取第一个出现的即可）
            var deduped = new Dictionary<string, ReleaseInfo>();
            foreach (var r in allReleases)
            {
                if (string.IsNullOrEmpty(r.Version)) continue;
                if (!deduped.ContainsKey(r.Version))
                {
                    deduped[r.Version] = r;
                }
            }

            // 按版本号降序排列
            var sorted = deduped.Values
                .OrderByDescending(r =>
                {
                    if (SemanticVersion.TryParse(r.Version, out var sv)) return sv;
                    return null;
                }, Comparer<SemanticVersion?>.Create((a, b) =>
                {
                    if (a == null && b == null) return 0;
                    if (a == null) return 1;
                    if (b == null) return -1;
                    return a.CompareTo(b);
                }))
                .Take(maxCount)
                .ToList();

            UpdateLogger.Info("fetch-releases", new Dictionary<string, object?>
            {
                ["result"] = "ok",
                ["total"] = allReleases.Count,
                ["deduped"] = deduped.Count,
                ["returned"] = sorted.Count
            });

            return sorted;
        }

        /// <summary>
        /// 从单个更新源获取多个 Release 信息。
        /// GitHub/Gitee：调用 /releases 列表 API，逐个解析 assets 中的 manifest.json。
        /// Mirror：尝试 releases.json 或返回空（Mirror 可能不支持多版本）。
        /// </summary>
        private async Task<List<ReleaseInfo>> FetchReleasesFromSourceAsync(SourceSpec source, int maxCount, CancellationToken cancel)
        {
            var results = new List<ReleaseInfo>();

            switch (source.Name)
            {
                case "gitee":
                    results = await FetchReleasesFromGitHostAsync(
                        "gitee",
                        string.IsNullOrEmpty(_meta.GiteeRepo) ? "yelan1387/sw-ai-plugin" : _meta.GiteeRepo,
                        "https://gitee.com/api/v5/repos",
                        maxCount, cancel);
                    break;

                case "github":
                    results = await FetchReleasesFromGitHostAsync(
                        "github",
                        string.IsNullOrEmpty(_meta.UpdateRepo) ? "yelan-131/sw-ai-plugin" : _meta.UpdateRepo,
                        "https://api.github.com/repos",
                        maxCount, cancel);
                    break;

                case "mirror":
                    // Mirror 可能不支持多版本列表，返回空即可
                    break;
            }

            // 标记来源
            foreach (var r in results)
            {
                r.Source = source.Name;
            }

            return results;
        }

        /// <summary>
        /// 从 GitHub/Gitee 的 /releases 列表 API 获取多个 Release，解析每个 Release 的 assets 中的 manifest.json。
        /// </summary>
        private async Task<List<ReleaseInfo>> FetchReleasesFromGitHostAsync(
            string sourceName, string repo, string apiBase, int maxCount, CancellationToken cancel)
        {
            var results = new List<ReleaseInfo>();

            string url = $"{apiBase}/{repo}/releases";
            var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.UserAgent.ParseAdd("SwAiPlugin");

            HttpResponseMessage resp;
            try
            {
                resp = await _metaClient.SendAsync(req, cancel);
                if (!resp.IsSuccessStatusCode)
                {
                    UpdateLogger.Warn("fetch-releases", UpdateErrorCodes.CheckNetwork, new Dictionary<string, object?>
                    {
                        ["source"] = sourceName,
                        ["status"] = (int)resp.StatusCode
                    });
                    return results;
                }
            }
            catch (Exception ex)
            {
                UpdateLogger.Warn("fetch-releases", UpdateErrorCodes.CheckNetwork, new Dictionary<string, object?>
                {
                    ["source"] = sourceName,
                    ["error"] = ex.Message
                });
                return results;
            }

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.ValueKind != JsonValueKind.Array) return results;

            int count = 0;
            foreach (var releaseEl in doc.RootElement.EnumerateArray())
            {
                if (count >= maxCount) break;
                cancel.ThrowIfCancellationRequested();

                // 解析 assets 中的 manifest.json 和 manifest.sig URL
                var urls = ParseAssetUrlsFromRelease(releaseEl);
                if (urls?.ManifestUrl == null) continue;

                // 下载并验证 manifest（逻辑与 FetchAndVerifyManifestAsync 一致）
                var manifest = await FetchAndVerifyManifestAsync(urls, cancel);
                if (manifest == null) continue;

                // 提取 release 元数据
                string tagName = "";
                if (releaseEl.TryGetProperty("tag_name", out var tagProp))
                    tagName = tagProp.GetString() ?? "";

                string publishedAt = "";
                if (releaseEl.TryGetProperty("published_at", out var pubProp))
                    publishedAt = pubProp.GetString() ?? "";

                results.Add(new ReleaseInfo
                {
                    Version = string.IsNullOrEmpty(manifest.Version) ? tagName.TrimStart('v') : manifest.Version,
                    ReleasedAt = string.IsNullOrEmpty(manifest.ReleasedAt) ? publishedAt : manifest.ReleasedAt,
                    Channel = manifest.Channel,
                    Manifest = manifest,
                    Source = sourceName
                });

                count++;
            }

            return results;
        }

        /// <summary>
        /// 从单个 Release 的 JSON 元素中解析 manifest.json 和 manifest.sig 的下载 URL。
        /// </summary>
        private static AssetUrls? ParseAssetUrlsFromRelease(JsonElement releaseEl)
        {
            if (!releaseEl.TryGetProperty("assets", out var assets)) return null;

            var urls = new AssetUrls();
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var n) || !a.TryGetProperty("browser_download_url", out var dl))
                    continue;
                string name = n.GetString() ?? "";
                if (string.Equals(name, ManifestAssetName, StringComparison.OrdinalIgnoreCase))
                    urls.ManifestUrl = dl.GetString();
                else if (string.Equals(name, "manifest.sig", StringComparison.OrdinalIgnoreCase))
                    urls.SigUrl = dl.GetString();
            }
            return urls.ManifestUrl != null ? urls : null;
        }

        // ────────────────────────── Internal: check ──────────────────────────

        private class SourceSpec
        {
            public string Name { get; }
            public Func<CancellationToken, Task<UpdateManifest?>> Fetch { get; }
            public SourceSpec(string name, Func<CancellationToken, Task<UpdateManifest?>> fetch)
            {
                Name = name;
                Fetch = fetch;
            }
        }

        private class AssetUrls
        {
            public string? ManifestUrl { get; set; }
            public string? SigUrl { get; set; }
        }

        private IEnumerable<SourceSpec> ResolveSources()
        {
            var list = new List<SourceSpec>();
            string src = (_userCfg.UpdateSource ?? "auto").Trim().ToLowerInvariant();

            switch (src)
            {
                case "gitee":
                    list.Add(new SourceSpec("gitee", ct => FetchGiteeManifestAsync(ct)));
                    break;
                case "github":
                    list.Add(new SourceSpec("github", ct => FetchGitHubManifestAsync(ct)));
                    break;
                case "mirror" when !string.IsNullOrEmpty(_userCfg.MirrorUrl):
                    list.Add(new SourceSpec("mirror", ct => FetchMirrorManifestAsync(_userCfg.MirrorUrl!, ct)));
                    break;
                default:
                    // auto：GitHub 主、Gitee 备（取最高版本号）
                    list.Add(new SourceSpec("github", ct => FetchGitHubManifestAsync(ct)));
                    list.Add(new SourceSpec("gitee", ct => FetchGiteeManifestAsync(ct)));
                    if (!string.IsNullOrEmpty(_userCfg.MirrorUrl))
                        list.Add(new SourceSpec("mirror", ct => FetchMirrorManifestAsync(_userCfg.MirrorUrl!, ct)));
                    break;
            }
            return list;
        }

        private async Task<CheckResult> CheckSourceAsync(SourceSpec source, CancellationToken cancel)
        {
            try
            {
                var manifest = await source.Fetch(cancel);
                if (manifest == null)
                    return new CheckResult { Source = source.Name, ErrorCode = UpdateErrorCodes.CheckNetwork };
                return new CheckResult { Source = source.Name, Manifest = manifest };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                UpdateLogger.Warn("check", UpdateErrorCodes.CheckNetwork, new Dictionary<string, object?>
                {
                    ["source"] = source.Name,
                    ["error"] = ex.Message
                });
                return new CheckResult { Source = source.Name, ErrorCode = UpdateErrorCodes.CheckNetwork, ErrorMessage = ex.Message };
            }
        }

        private async Task<UpdateManifest?> FetchGiteeManifestAsync(CancellationToken cancel)
        {
            string repo = string.IsNullOrEmpty(_meta.GiteeRepo) ? "yelan1387/sw-ai-plugin" : _meta.GiteeRepo;
            string url = $"https://gitee.com/api/v5/repos/{repo}/releases/latest";
            var urls = await ResolveAssetUrlsAsync(url, cancel);
            if (urls?.ManifestUrl == null) return null;
            return await FetchAndVerifyManifestAsync(urls, cancel);
        }

        private async Task<UpdateManifest?> FetchGitHubManifestAsync(CancellationToken cancel)
        {
            string repo = string.IsNullOrEmpty(_meta.UpdateRepo) ? "yelan-131/sw-ai-plugin" : _meta.UpdateRepo;
            string url = $"https://api.github.com/repos/{repo}/releases/latest";
            var urls = await ResolveAssetUrlsAsync(url, cancel);
            if (urls?.ManifestUrl == null) return null;
            return await FetchAndVerifyManifestAsync(urls, cancel);
        }

        private async Task<UpdateManifest?> FetchMirrorManifestAsync(string mirrorBase, CancellationToken cancel)
        {
            string base_ = mirrorBase.TrimEnd('/');
            var urls = new AssetUrls
            {
                ManifestUrl = base_ + "/manifest.json",
                SigUrl = base_ + "/manifest.sig"
            };
            return await FetchAndVerifyManifestAsync(urls, cancel);
        }

        /// <summary>
        /// 下载 manifest 原始字节 + 可选签名 → 验证 → 反序列化。
        /// 无签名时记录警告但继续（向后兼容无签名旧版本）。
        /// </summary>
        private async Task<UpdateManifest?> FetchAndVerifyManifestAsync(AssetUrls urls, CancellationToken cancel)
        {
            // 下载 manifest 原始字节
            var manifestBytes = await DownloadBytesAsync(urls.ManifestUrl!, cancel);
            if (manifestBytes == null) return null;

            // 下载签名（可选）
            byte[]? sigBytes = null;
            if (!string.IsNullOrEmpty(urls.SigUrl))
            {
                sigBytes = await DownloadBytesAsync(urls.SigUrl, cancel);
            }

            // 签名验证
            if (sigBytes != null)
            {
                if (!ManifestVerifier.Verify(manifestBytes, sigBytes))
                {
                    UpdateLogger.Error("verify", UpdateErrorCodes.VerifySignature, new Dictionary<string, object?>
                    {
                        ["url"] = urls.ManifestUrl
                    });
                    return null;
                }
                UpdateLogger.Info("verify", new Dictionary<string, object?> { ["result"] = "signature-ok" });
            }
            else
            {
                UpdateLogger.Warn("verify", null, new Dictionary<string, object?>
                {
                    ["result"] = "no-signature",
                    ["url"] = urls.ManifestUrl
                });
            }

            return JsonSerializer.Deserialize<UpdateManifest>(manifestBytes);
        }

        private async Task<AssetUrls?> ResolveAssetUrlsAsync(string releaseApiUrl, CancellationToken cancel)
        {
            var req = new HttpRequestMessage(HttpMethod.Get, releaseApiUrl);
            req.Headers.UserAgent.ParseAdd("SwAiPlugin");

            var resp = await _metaClient.SendAsync(req, cancel);
            if (!resp.IsSuccessStatusCode) return null;

            string json = await resp.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("assets", out var assets)) return null;

            var urls = new AssetUrls();
            foreach (var a in assets.EnumerateArray())
            {
                if (!a.TryGetProperty("name", out var n) || !a.TryGetProperty("browser_download_url", out var dl))
                    continue;
                string name = n.GetString() ?? "";
                if (string.Equals(name, ManifestAssetName, StringComparison.OrdinalIgnoreCase))
                    urls.ManifestUrl = dl.GetString();
                else if (string.Equals(name, "manifest.sig", StringComparison.OrdinalIgnoreCase))
                    urls.SigUrl = dl.GetString();
            }
            return urls.ManifestUrl != null ? urls : null;
        }

        private async Task<byte[]?> DownloadBytesAsync(string url, CancellationToken cancel)
        {
            try
            {
                var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.UserAgent.ParseAdd("SwAiPlugin");
                var resp = await _metaClient.SendAsync(req, cancel);
                if (!resp.IsSuccessStatusCode) return null;
                return await resp.Content.ReadAsByteArrayAsync();
            }
            catch
            {
                return null;
            }
        }

        // ────────────────────────── Internal: download ──────────────────────────

        public class DownloadProgress
        {
            public long BytesReceived { get; set; }
            public long? TotalBytes { get; set; }
            public double BytesPerSecond { get; set; }
            public double Fraction => TotalBytes.HasValue && TotalBytes.Value > 0
                ? (double)BytesReceived / TotalBytes.Value
                : 0;
        }

        private async Task DownloadToFileAsync(string url, string targetPath, IProgress<DownloadProgress>? progress, CancellationToken cancel)
        {
            using var client = CreateHttpClient(PackageTimeout);
            using var resp = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancel);
            resp.EnsureSuccessStatusCode();

            long? total = resp.Content.Headers.ContentLength;

            using var src = await resp.Content.ReadAsStreamAsync();
            using var dst = new FileStream(targetPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[DownloadBufferBytes];
            long received = 0;
            int read;
            var sw = Stopwatch.StartNew();
            long lastBytes = 0;
            DateTime lastReport = DateTime.UtcNow;

            while ((read = await src.ReadAsync(buffer, 0, buffer.Length, cancel)) > 0)
            {
                await dst.WriteAsync(buffer, 0, read, cancel);
                received += read;

                // 节流：~10 次/秒上报
                var now = DateTime.UtcNow;
                if ((now - lastReport).TotalMilliseconds >= 100 && progress != null)
                {
                    var elapsedSec = (now - lastReport).TotalSeconds;
                    double bps = elapsedSec > 0 ? (received - lastBytes) / elapsedSec : 0;
                    progress.Report(new DownloadProgress { BytesReceived = received, TotalBytes = total, BytesPerSecond = bps });
                    lastReport = now;
                    lastBytes = received;
                }
            }

            progress?.Report(new DownloadProgress { BytesReceived = received, TotalBytes = total ?? received, BytesPerSecond = 0 });
        }

        // ────────────────────────── Internal: helpers ──────────────────────────

        private static HttpClient CreateHttpClient(TimeSpan timeout)
        {
            var handler = new HttpClientHandler
            {
                UseProxy = false,                           // net48 在 SW 进程内系统代理可能挂起，直连
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            };
            try
            {
                // 显式启用 TLS1.2，避免 net48 默认协议带来的 GitHub 握手失败
                ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            }
            catch { }
            return new HttpClient(handler) { Timeout = timeout };
        }

        private static bool TryParseUtc(string? iso, out DateTime utc)
        {
            utc = default;
            if (string.IsNullOrWhiteSpace(iso)) return false;
            return DateTime.TryParse(iso, null,
                System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal,
                out utc);
        }

        private static bool IsPreRelease(UpdateManifest manifest)
        {
            if (!string.IsNullOrEmpty(manifest.Channel) &&
                !string.Equals(manifest.Channel, "stable", StringComparison.OrdinalIgnoreCase))
                return true;
            if (SemanticVersion.TryParse(manifest.Version, out var v) && v!.IsPreRelease)
                return true;
            return false;
        }

        public static string ComputeSha256(string filePath)
        {
            using var sha = SHA256.Create();
            using var fs = File.OpenRead(filePath);
            var hash = sha.ComputeHash(fs);
            var sb = new StringBuilder(hash.Length * 2);
            for (int i = 0; i < hash.Length; i++) sb.Append(hash[i].ToString("x2"));
            return sb.ToString();
        }

        private static string GetSolidWorksPath()
        {
            // 1. Running process (plugin runs inside SLDWORKS.exe)
            try
            {
                foreach (var proc in Process.GetProcessesByName("SLDWORKS"))
                {
                    try { return proc.MainModule.FileName; } catch { }
                }
            }
            catch { }

            // 2. Registry — find latest installed SolidWorks version
            try
            {
                string latestPath = null;
                int latestYear = 0;

                foreach (var root in new[]
                {
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\SolidWorks"),
                    Registry.LocalMachine.OpenSubKey(@"SOFTWARE\WOW6432Node\SolidWorks")
                })
                {
                    if (root == null) continue;
                    using (root)
                    {
                        foreach (var subKeyName in root.GetSubKeyNames())
                        {
                            if (!subKeyName.StartsWith("SolidWorks 20")) continue;
                            var yearStr = subKeyName.Substring("SolidWorks ".Length);
                            if (!int.TryParse(yearStr, out int year) || year <= latestYear) continue;

                            using var subKey = root.OpenSubKey(subKeyName);
                            var folder = subKey?.GetValue("SolidWorks Folder") as string;
                            if (string.IsNullOrEmpty(folder)) continue;
                            var exe = Path.Combine(folder, "SLDWORKS.exe");
                            if (File.Exists(exe))
                            {
                                latestYear = year;
                                latestPath = exe;
                            }
                        }
                    }
                }
                if (latestPath != null) return latestPath;
            }
            catch { }

            // 3. Hardcoded fallback
            string[] dirs =
            {
                @"D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                @"C:\Program Files (x86)\SOLIDWORKS Corp\SOLIDWORKS"
            };
            foreach (var d in dirs)
            {
                string exe = Path.Combine(d, "SLDWORKS.exe");
                if (File.Exists(exe)) return exe;
            }

            return "SLDWORKS.exe";
        }

        // 抛出非法路径用的简易异常
        private class SecurityException : Exception
        {
            public SecurityException(string msg) : base(msg) { }
        }
    }
}
