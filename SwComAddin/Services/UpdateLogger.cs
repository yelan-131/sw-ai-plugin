using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.Json;

namespace SwComAddin.Services
{
    /// <summary>
    /// 更新流程结构化日志，写入 %LOCALAPPDATA%\SwAiPlugin\logs\update.log。
    /// 每行一条 JSON（JSON Lines），便于后续分析与远程支持。
    /// </summary>
    public static class UpdateLogger
    {
        private static readonly object _lock = new object();

        private static readonly string LogDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SwAiPlugin", "logs");

        private static readonly string LogPath = Path.Combine(LogDir, "update.log");

        private const long MaxBytes = 1 * 1024 * 1024; // 1 MB，超出则轮转一次

        private static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions
        {
            WriteIndented = false,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string LogFilePath => LogPath;

        public static void Info(string stage, IDictionary<string, object?>? fields = null)
            => Write("info", stage, null, fields);

        public static void Warn(string stage, string? errorCode = null, IDictionary<string, object?>? fields = null)
            => Write("warn", stage, errorCode, fields);

        public static void Error(string stage, string errorCode, IDictionary<string, object?>? fields = null)
            => Write("error", stage, errorCode, fields);

        private static void Write(string level, string stage, string? errorCode, IDictionary<string, object?>? fields)
        {
            try
            {
                var entry = new Dictionary<string, object?>
                {
                    ["ts"] = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                    ["level"] = level,
                    ["stage"] = stage
                };
                if (!string.IsNullOrEmpty(errorCode))
                    entry["code"] = errorCode;
                if (fields != null)
                {
                    foreach (var kv in fields)
                        entry[kv.Key] = kv.Value;
                }

                string line = JsonSerializer.Serialize(entry, JsonOpts);

                lock (_lock)
                {
                    Directory.CreateDirectory(LogDir);
                    RotateIfNeeded();
                    File.AppendAllText(LogPath, line + Environment.NewLine, Encoding.UTF8);
                }
            }
            catch
            {
                // 日志失败不应影响主流程
            }
        }

        private static void RotateIfNeeded()
        {
            try
            {
                if (!File.Exists(LogPath)) return;
                var fi = new FileInfo(LogPath);
                if (fi.Length < MaxBytes) return;

                string rotated = Path.Combine(LogDir, "update.log.1");
                if (File.Exists(rotated)) File.Delete(rotated);
                File.Move(LogPath, rotated);
            }
            catch
            {
                // 轮转失败时直接截断
                try { File.WriteAllText(LogPath, string.Empty); } catch { }
            }
        }
    }
}
