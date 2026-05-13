using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace SwComAddin.Services
{
    /// <summary>
    /// Handles version checking, update downloading, and update execution
    /// for the SW AI Plugin SolidWorks COM Add-in.
    /// </summary>
    public class UpdateService
    {
        private const string DefaultRepo = "yelan-131/sw-ai-plugin";
        private const string UpdateSubDir = "SwAiPlugin_update";
        private const string ConfigFileName = "plugin_config.json";

        private static readonly HttpClient _http = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(5)
        };

        /// <summary>
        /// Reads the current plugin version from plugin_config.json.
        /// Returns "1.0.0" if the file or key is not found.
        /// </summary>
        /// <returns>The current version string.</returns>
        public string GetCurrentVersion()
        {
            try
            {
                string dllDir = GetDllDirectory();
                string configPath = Path.Combine(dllDir, ConfigFileName);

                if (!File.Exists(configPath))
                    return "1.0.0";

                string json = File.ReadAllText(configPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("version", out JsonElement versionElem))
                    {
                        string ver = versionElem.GetString();
                        return !string.IsNullOrEmpty(ver) ? ver : "1.0.0";
                    }
                }
            }
            catch
            {
                // Never crash -- return default version
            }

            return "1.0.0";
        }

        /// <summary>
        /// Checks GitHub Releases API for the latest release and determines
        /// whether an update is available.
        /// </summary>
        /// <returns>
        /// A tuple containing: whether an update is available, the latest version tag,
        /// the download URL for the ZIP asset, and the release notes body.
        /// </returns>
        public async Task<(bool hasUpdate, string latestVersion, string downloadUrl, string releaseNotes)> CheckForUpdateAsync()
        {
            try
            {
                string repo = GetUpdateRepo();
                string url = $"https://api.github.com/repos/{repo}/releases/latest";

                var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.UserAgent.ParseAdd("SwAiPlugin");

                HttpResponseMessage response = await _http.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                    return (false, null, null, null);

                string json = await response.Content.ReadAsStringAsync();
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    JsonElement root = doc.RootElement;

                    // Tag name is the version (e.g. "v1.2.0" or "1.2.0")
                    string tagName = root.TryGetProperty("tag_name", out JsonElement tagElem)
                        ? tagElem.GetString() ?? ""
                        : "";

                    string releaseNotes = root.TryGetProperty("body", out JsonElement bodyElem)
                        ? bodyElem.GetString() ?? ""
                        : "";

                    string htmlUrl = root.TryGetProperty("html_url", out JsonElement htmlElem)
                        ? htmlElem.GetString() ?? ""
                        : "";

                    // Find the first .zip asset's browser_download_url
                    string downloadUrl = null;
                    if (root.TryGetProperty("assets", out JsonElement assetsElem))
                    {
                        foreach (JsonElement asset in assetsElem.EnumerateArray())
                        {
                            if (asset.TryGetProperty("name", out JsonElement nameElem))
                            {
                                string name = nameElem.GetString() ?? "";
                                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                                {
                                    if (asset.TryGetProperty("browser_download_url", out JsonElement dlElem))
                                    {
                                        downloadUrl = dlElem.GetString();
                                    }
                                    break;
                                }
                            }
                        }
                    }

                    // Compare versions
                    string currentVersion = GetCurrentVersion();
                    bool hasUpdate = IsNewerVersion(tagName, currentVersion);

                    return (hasUpdate, tagName, downloadUrl, releaseNotes);
                }
            }
            catch
            {
                // Network errors, JSON parse errors, etc. -- never crash
                return (false, null, null, null);
            }
        }

        /// <summary>
        /// Downloads the update ZIP file from the given URL to a temp directory.
        /// Reports download progress via the provided IProgress handle.
        /// </summary>
        /// <param name="downloadUrl">The URL of the ZIP file to download.</param>
        /// <param name="progress">Progress reporter (0.0 to 1.0).</param>
        /// <returns>The local file path of the downloaded ZIP.</returns>
        public async Task<string> DownloadUpdateAsync(string downloadUrl, IProgress<double> progress)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), UpdateSubDir);
            Directory.CreateDirectory(tempDir);

            string zipPath = Path.Combine(tempDir, "update.zip");

            using (var response = await _http.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead))
            {
                response.EnsureSuccessStatusCode();

                long? totalBytes = response.Content.Headers.ContentLength;

                using (Stream contentStream = await response.Content.ReadAsStreamAsync())
                using (var fileStream = new FileStream(zipPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    byte[] buffer = new byte[81920]; // 80 KB buffer
                    long bytesRead = 0;
                    int read;

                    while ((read = await contentStream.ReadAsync(buffer, 0, buffer.Length)) > 0)
                    {
                        await fileStream.WriteAsync(buffer, 0, read);
                        bytesRead += read;

                        if (totalBytes.HasValue && totalBytes.Value > 0)
                        {
                            progress?.Report((double)bytesRead / totalBytes.Value);
                        }
                    }
                }
            }

            progress?.Report(1.0);
            return zipPath;
        }

        /// <summary>
        /// Extracts the downloaded ZIP and generates a batch script that waits for
        /// SolidWorks to exit, copies files, re-registers the DLL, and restarts SolidWorks.
        /// </summary>
        /// <param name="zipPath">Path to the downloaded ZIP file.</param>
        /// <param name="installDir">The target installation directory.</param>
        /// <returns>The path to the generated update.bat script.</returns>
        public string PrepareUpdate(string zipPath, string installDir)
        {
            string tempDir = Path.Combine(Path.GetTempPath(), UpdateSubDir);
            string extractDir = Path.Combine(tempDir, "files");

            // Clean up previous extraction if it exists
            if (Directory.Exists(extractDir))
                Directory.Delete(extractDir, true);

            Directory.CreateDirectory(extractDir);

            // Extract the ZIP
            System.IO.Compression.ZipFile.ExtractToDirectory(zipPath, extractDir);

            // Find SolidWorks executable path
            string swPath = GetSolidWorksPath();

            // Generate update batch script
            string batContent = string.Format(
@"@echo off
echo 正在更新 SW AI Plugin...
:wait
tasklist /FI ""IMAGENAME eq SLDWORKS.exe"" 2>NUL | find /I /N ""SLDWORKS.exe"">NUL
if ""%ERRORLEVEL%""==""0"" (
    timeout /t 2 /nobreak >NUL
    goto wait
)
echo 正在复制文件...
xcopy /E /Y ""{0}\*"" ""{1}\""
echo 正在重新注册...
""%WINDIR%\Microsoft.NET\Framework64\v4.0.30319\RegAsm.exe"" ""{1}\SwComAddin.dll"" /codebase /tlb
echo 更新完成，正在启动 SolidWorks...
start """" ""{2}""
rd /s /q ""{3}""
exit
",
                extractDir,    // {0} - temp extract path
                installDir,    // {1} - install directory
                swPath,        // {2} - SolidWorks executable path
                tempDir        // {3} - temp directory to clean up
            );

            string batPath = Path.Combine(tempDir, "update.bat");
            File.WriteAllText(batPath, batContent);

            return batPath;
        }

        /// <summary>
        /// Launches the update batch script and shuts down the current application.
        /// The batch script waits for SolidWorks to exit before applying the update.
        /// </summary>
        /// <param name="batPath">Path to the update.bat script generated by <see cref="PrepareUpdate"/>.</param>
        public void ExecuteUpdate(string batPath)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = batPath,
                UseShellExecute = true,
                WindowStyle = ProcessWindowStyle.Normal
            });

            // Shut down the WPF application
            try
            {
                System.Windows.Application.Current.Shutdown();
            }
            catch
            {
                // If WPF Application.Current is not available (e.g. during testing),
                // fall back to exiting the process
                Environment.Exit(0);
            }
        }

        // ────────────────────────── Private Helpers ──────────────────────────

        /// <summary>
        /// Reads the update_repo value from plugin_config.json.
        /// Falls back to <see cref="DefaultRepo"/> if not configured.
        /// </summary>
        private string GetUpdateRepo()
        {
            try
            {
                string dllDir = GetDllDirectory();
                string configPath = Path.Combine(dllDir, ConfigFileName);

                if (!File.Exists(configPath))
                    return DefaultRepo;

                string json = File.ReadAllText(configPath);
                using (JsonDocument doc = JsonDocument.Parse(json))
                {
                    if (doc.RootElement.TryGetProperty("update_repo", out JsonElement repoElem))
                    {
                        string repo = repoElem.GetString();
                        return !string.IsNullOrEmpty(repo) ? repo : DefaultRepo;
                    }
                }
            }
            catch
            {
                // Fall back to default
            }

            return DefaultRepo;
        }

        /// <summary>
        /// Returns the directory containing the currently executing assembly (DLL).
        /// </summary>
        private static string GetDllDirectory()
        {
            string location = Assembly.GetExecutingAssembly().Location;
            return !string.IsNullOrEmpty(location)
                ? Path.GetDirectoryName(location)
                : AppDomain.CurrentDomain.BaseDirectory;
        }

        /// <summary>
        /// Compares two version strings, handling optional "v" prefix.
        /// Returns true if <paramref name="newVersion"/> is newer than <paramref name="currentVersion"/>.
        /// </summary>
        private static bool IsNewerVersion(string newVersion, string currentVersion)
        {
            try
            {
                // Strip leading 'v' or 'V' if present
                string newClean = newVersion.TrimStart('v', 'V');
                string curClean = currentVersion.TrimStart('v', 'V');

                if (Version.TryParse(newClean, out Version newVer) &&
                    Version.TryParse(curClean, out Version curVer))
                {
                    return newVer > curVer;
                }
            }
            catch
            {
                // If parsing fails, assume no update available
            }

            return false;
        }

        /// <summary>
        /// Attempts to locate the SolidWorks executable path from running processes
        /// or common installation directories.
        /// </summary>
        private static string GetSolidWorksPath()
        {
            try
            {
                // Try to find a running SolidWorks process and get its path
                foreach (var proc in Process.GetProcessesByName("SLDWORKS"))
                {
                    try
                    {
                        return proc.MainModule.FileName;
                    }
                    catch
                    {
                        // Access denied or process exited -- continue
                    }
                }

                // Try common installation paths
                string[] programDirs = new[]
                {
                    @"D:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                    @"C:\Program Files\SOLIDWORKS Corp\SOLIDWORKS",
                    @"C:\Program Files (x86)\SOLIDWORKS Corp\SOLIDWORKS"
                };

                foreach (string dir in programDirs)
                {
                    string swExe = Path.Combine(dir, "SLDWORKS.exe");
                    if (File.Exists(swExe))
                        return swExe;
                }
            }
            catch
            {
                // Fall through to default
            }

            // Default fallback -- rely on PATH or file association
            return "SLDWORKS.exe";
        }
    }
}
