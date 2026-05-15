using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwComAddin.Models
{
    /// <summary>
    /// 远端发布元数据。每次发版上传到 Release 资产中：
    ///   - manifest.json     （本类反序列化目标）
    ///   - SwAiPlugin_vX.Y.Z.zip
    ///   - manifest.sig      （可选，Ed25519 签名，Iteration 2 启用）
    /// 检查更新阶段只需下载 manifest.json（&lt; 5KB），即可判定是否升级、是否兼容。
    /// </summary>
    public class UpdateManifest
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = "1.0";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = "stable"; // stable | beta | dev

        [JsonPropertyName("released_at")]
        public string ReleasedAt { get; set; } = "";

        /// <summary>低于此插件版本无法增量升级，必须重装。</summary>
        [JsonPropertyName("min_plugin_version")]
        public string? MinPluginVersion { get; set; }

        /// <summary>所需 SolidWorks 最低主版本号（如 2020）。</summary>
        [JsonPropertyName("min_sw_version")]
        public int? MinSwVersion { get; set; }

        /// <summary>所需 .NET Framework 版本。</summary>
        [JsonPropertyName("min_dotnet")]
        public string? MinDotNet { get; set; }

        /// <summary>关键安全更新时设为 true，UI 不提供"稍后"。</summary>
        [JsonPropertyName("force_update")]
        public bool ForceUpdate { get; set; }

        [JsonPropertyName("package")]
        public UpdatePackage Package { get; set; } = new UpdatePackage();

        /// <summary>逐文件清单。action=replace 覆盖，action=delete 在新版本中删除该旧文件。</summary>
        [JsonPropertyName("files")]
        public List<UpdateFileEntry> Files { get; set; } = new List<UpdateFileEntry>();

        /// <summary>升级时绝不覆盖的用户文件（相对 installDir）。</summary>
        [JsonPropertyName("preserve")]
        public List<string> Preserve { get; set; } = new List<string>();

        [JsonPropertyName("backend")]
        public BackendInfo? Backend { get; set; }

        [JsonPropertyName("release_notes_url")]
        public string? ReleaseNotesUrl { get; set; }

        [JsonPropertyName("release_notes_summary")]
        public string? ReleaseNotesSummary { get; set; }

        /// <summary>Structured release notes (v2). Falls back to ReleaseNotesSummary when null.</summary>
        [JsonPropertyName("release_notes")]
        public List<ReleaseNoteSection>? ReleaseNotes { get; set; }
    }

    /// <summary>Structured release note section with numbered items.</summary>
    public class ReleaseNoteSection
    {
        [JsonPropertyName("section")]
        public string Section { get; set; } = "";

        [JsonPropertyName("items")]
        public List<string> Items { get; set; } = new List<string>();
    }

    public class UpdatePackage
    {
        [JsonPropertyName("name")]
        public string Name { get; set; } = "";

        [JsonPropertyName("size")]
        public long Size { get; set; }

        [JsonPropertyName("sha256")]
        public string Sha256 { get; set; } = "";

        [JsonPropertyName("primary_url")]
        public string PrimaryUrl { get; set; } = "";

        [JsonPropertyName("mirrors")]
        public List<string> Mirrors { get; set; } = new List<string>();
    }

    public class UpdateFileEntry
    {
        [JsonPropertyName("path")]
        public string Path { get; set; } = "";

        [JsonPropertyName("sha256")]
        public string? Sha256 { get; set; }

        [JsonPropertyName("action")]
        public string Action { get; set; } = "replace"; // replace | delete
    }

    public class BackendInfo
    {
        [JsonPropertyName("version")]
        public string Version { get; set; } = "";

        [JsonPropertyName("requirements_changed")]
        public bool RequirementsChanged { get; set; }
    }

    /// <summary>更新流程结构化错误码。日志与 UI 共用。</summary>
    public static class UpdateErrorCodes
    {
        // 检查阶段
        public const string CheckNetwork = "UPD-CHECK-NETWORK";
        public const string CheckParse = "UPD-CHECK-PARSE";
        public const string CheckIncompatible = "UPD-CHECK-INCOMPATIBLE";

        // 下载阶段
        public const string DownloadTimeout = "UPD-DL-TIMEOUT";
        public const string DownloadHttp = "UPD-DL-HTTP";
        public const string DownloadCancelled = "UPD-DL-CANCELLED";

        // 校验阶段
        public const string VerifyHashMismatch = "UPD-VERIFY-HASH";
        public const string VerifySignature = "UPD-VERIFY-SIG";

        // 安装阶段
        public const string InstallExtract = "UPD-INSTALL-EXTRACT";
        public const string InstallCopy = "UPD-INSTALL-COPY";
        public const string InstallRegAsm = "UPD-INSTALL-REGASM";
        public const string InstallSwBusy = "UPD-INSTALL-SW-BUSY";

        // 回滚
        public const string RollbackOk = "UPD-ROLLBACK-OK";
        public const string RollbackFail = "UPD-ROLLBACK-FAIL";
    }

}
