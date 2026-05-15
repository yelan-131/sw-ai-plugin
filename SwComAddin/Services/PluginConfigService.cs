using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwComAddin.Services
{
    /// <summary>
    /// 配置职责拆分：
    ///   - user_config.json   用户私有运行时设置（被更新过程 preserve，不随包发布）
    ///   - plugin_meta.json   插件元数据（version / 发版仓库 / 通道，随发布包必覆盖）
    /// 同时提供从旧 plugin_config.json 的一次性迁移。
    /// </summary>
    public class PluginConfigService
    {
        private const string LegacyConfigFileName = "plugin_config.json";
        private const string UserConfigFileName = "user_config.json";
        private const string PluginMetaFileName = "plugin_meta.json";

        private static readonly JsonSerializerOptions WriteOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private readonly string _baseDir;

        public PluginConfigService(string baseDir)
        {
            _baseDir = baseDir;
        }

        public string UserConfigPath => Path.Combine(_baseDir, UserConfigFileName);
        public string PluginMetaPath => Path.Combine(_baseDir, PluginMetaFileName);
        public string LegacyConfigPath => Path.Combine(_baseDir, LegacyConfigFileName);

        // ─────────────────── User config ───────────────────

        public UserConfig LoadUserConfig()
        {
            MigrateFromLegacyIfNeeded();

            if (!File.Exists(UserConfigPath))
                return new UserConfig();

            try
            {
                var json = File.ReadAllText(UserConfigPath);
                var cfg = JsonSerializer.Deserialize<UserConfig>(json);
                return cfg ?? new UserConfig();
            }
            catch
            {
                return new UserConfig();
            }
        }

        public void SaveUserConfig(UserConfig config)
        {
            try
            {
                var json = JsonSerializer.Serialize(config, WriteOpts);
                File.WriteAllText(UserConfigPath, json);
            }
            catch
            {
                // 用户配置写盘失败不应导致插件崩溃，静默吞掉。
            }
        }

        // ─────────────────── Plugin meta ───────────────────

        public PluginMeta LoadPluginMeta()
        {
            MigrateFromLegacyIfNeeded();

            if (!File.Exists(PluginMetaPath))
                return new PluginMeta();

            try
            {
                var json = File.ReadAllText(PluginMetaPath);
                var meta = JsonSerializer.Deserialize<PluginMeta>(json);
                return meta ?? new PluginMeta();
            }
            catch
            {
                return new PluginMeta();
            }
        }

        /// <summary>仅 Updater 在更新成功后调用，主插件运行中不应该写元数据。</summary>
        public void WritePluginMeta(PluginMeta meta)
        {
            try
            {
                var json = JsonSerializer.Serialize(meta, WriteOpts);
                File.WriteAllText(PluginMetaPath, json);
            }
            catch
            {
                // ignore
            }
        }

        // ─────────────────── Legacy migration ───────────────────

        /// <summary>
        /// 兼容 v0.1.x：把旧的 plugin_config.json 拆为 user_config.json + plugin_meta.json。
        /// 迁移后旧文件改名为 plugin_config.json.legacy.bak。幂等。
        /// </summary>
        public void MigrateFromLegacyIfNeeded()
        {
            try
            {
                if (!File.Exists(LegacyConfigPath)) return;
                if (File.Exists(UserConfigPath) && File.Exists(PluginMetaPath)) return;

                var raw = File.ReadAllText(LegacyConfigPath);
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;

                var user = File.Exists(UserConfigPath) ? LoadUserConfig() : new UserConfig();
                var meta = File.Exists(PluginMetaPath) ? LoadPluginMeta() : new PluginMeta();

                if (root.TryGetProperty("backend_url", out var be))
                    user.BackendUrl = be.GetString() ?? user.BackendUrl;
                if (root.TryGetProperty("model_library_path", out var mlp))
                    user.ModelLibraryPath = mlp.GetString() ?? user.ModelLibraryPath;

                if (root.TryGetProperty("version", out var v))
                    meta.Version = v.GetString() ?? meta.Version;
                if (root.TryGetProperty("update_repo", out var ur))
                    meta.UpdateRepo = ur.GetString() ?? meta.UpdateRepo;
                if (root.TryGetProperty("gitee_repo", out var gr))
                    meta.GiteeRepo = gr.GetString() ?? meta.GiteeRepo;

                SaveUserConfig(user);
                WritePluginMeta(meta);

                var backup = LegacyConfigPath + ".legacy.bak";
                if (File.Exists(backup)) File.Delete(backup);
                File.Move(LegacyConfigPath, backup);
            }
            catch
            {
                // 迁移失败不致命，保留旧文件，让后续启动重试。
            }
        }
    }

    /// <summary>用户私有配置。更新流程 preserve，不随包发布。</summary>
    public class UserConfig
    {
        [JsonPropertyName("backend_url")]
        public string BackendUrl { get; set; } = "http://localhost:8765";

        [JsonPropertyName("model_library_path")]
        public string ModelLibraryPath { get; set; } = "";

        /// <summary>auto | gitee | github | mirror。</summary>
        [JsonPropertyName("update_source")]
        public string UpdateSource { get; set; } = "auto";

        /// <summary>用户在弹窗里"跳过此版本"标记过的版本号列表。</summary>
        [JsonPropertyName("skipped_versions")]
        public List<string> SkippedVersions { get; set; } = new List<string>();

        /// <summary>用户点击"稍后提醒"后，下一次提醒的最早时间（UTC ISO8601）。</summary>
        [JsonPropertyName("defer_until_utc")]
        public string? DeferUntilUtc { get; set; }

        /// <summary>更新检查周期（小时）。默认 4。</summary>
        [JsonPropertyName("check_interval_hours")]
        public int CheckIntervalHours { get; set; } = 4;

        /// <summary>是否接收预发布版（Beta 通道）。</summary>
        [JsonPropertyName("receive_prerelease")]
        public bool ReceivePrerelease { get; set; } = false;

        /// <summary>企业镜像 URL，非空时强制使用此源。</summary>
        [JsonPropertyName("mirror_url")]
        public string? MirrorUrl { get; set; }
    }

    /// <summary>随发布包发布的插件元数据。Updater 负责写入；主插件只读。</summary>
    public class PluginMeta
    {
        [JsonPropertyName("schema")]
        public string Schema { get; set; } = "1.0";

        [JsonPropertyName("version")]
        public string Version { get; set; } = "0.1.4";

        [JsonPropertyName("channel")]
        public string Channel { get; set; } = "stable";

        [JsonPropertyName("build_date")]
        public string BuildDate { get; set; } = "";

        [JsonPropertyName("update_repo")]
        public string UpdateRepo { get; set; } = "yelan-131/sw-ai-plugin";

        [JsonPropertyName("gitee_repo")]
        public string GiteeRepo { get; set; } = "yelan1387/sw-ai-plugin";
    }
}
