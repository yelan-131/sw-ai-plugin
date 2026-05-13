using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SwComAddin.Models;

namespace SwComAddin.Services
{
    /// <summary>
    /// Manages custom categories stored in Data/custom_library.json.
    /// Custom categories can be added, renamed, and deleted by the user.
    /// Default categories from standard_parts.json cannot be modified.
    /// </summary>
    public class CustomCategoryService
    {
        private readonly string _customLibPath;
        private CustomLibraryData _customData;

        /// <summary>
        /// Names of default categories loaded from standard_parts.json.
        /// These cannot be renamed or deleted.
        /// </summary>
        private readonly HashSet<string> _defaultCategoryNames = new();

        public CustomCategoryService(string baseDir)
        {
            _customLibPath = Path.Combine(baseDir, "Data", "custom_library.json");
            _customData = new CustomLibraryData();
        }

        public CustomLibraryData Data => _customData;

        /// <summary>
        /// Register a default category name (from standard_parts.json).
        /// These are protected from deletion/renaming.
        /// </summary>
        public void RegisterDefaultCategory(string name)
        {
            if (!string.IsNullOrWhiteSpace(name))
                _defaultCategoryNames.Add(name);
        }

        public bool IsDefaultCategory(string name)
        {
            return _defaultCategoryNames.Contains(name);
        }

        /// <summary>
        /// Load custom categories from JSON file.
        /// </summary>
        public void Load()
        {
            try
            {
                if (File.Exists(_customLibPath))
                {
                    var json = File.ReadAllText(_customLibPath);
                    _customData = JsonSerializer.Deserialize<CustomLibraryData>(json,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? new();
                }
            }
            catch
            {
                _customData = new CustomLibraryData();
            }
        }

        /// <summary>
        /// Save custom categories to JSON file.
        /// </summary>
        public void Save()
        {
            try
            {
                var dir = Path.GetDirectoryName(_customLibPath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
                var json = JsonSerializer.Serialize(_customData, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(_customLibPath, json);
            }
            catch { }
        }

        /// <summary>
        /// Add a new custom category. Returns null on success, or an error message.
        /// </summary>
        public string? AddCategory(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return "分类名称不能为空";

            name = name.Trim();

            // Check for duplicate
            if (_defaultCategoryNames.Contains(name))
                return "该分类名称已存在于默认分类中";

            if (_customData.Categories.Any(c => c.Name == name))
                return "该自定义分类已存在";

            _customData.Categories.Add(new CustomCategory { Name = name });
            Save();
            return null;
        }

        /// <summary>
        /// Rename a custom category. Returns null on success, or an error message.
        /// Cannot rename default categories.
        /// </summary>
        public string? RenameCategory(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(newName))
                return "新名称不能为空";

            newName = newName.Trim();

            if (_defaultCategoryNames.Contains(oldName))
                return "默认分类不可重命名";

            var cat = _customData.Categories.FirstOrDefault(c => c.Name == oldName);
            if (cat == null)
                return "未找到该自定义分类";

            if (_defaultCategoryNames.Contains(newName) || _customData.Categories.Any(c => c.Name == newName))
                return "该名称已被使用";

            cat.Name = newName;
            Save();
            return null;
        }

        /// <summary>
        /// Delete a custom category. Returns null on success, or an error message.
        /// Cannot delete default categories.
        /// </summary>
        public string? DeleteCategory(string name)
        {
            if (_defaultCategoryNames.Contains(name))
                return "默认分类不可删除";

            var cat = _customData.Categories.FirstOrDefault(c => c.Name == name);
            if (cat == null)
                return "未找到该自定义分类";

            _customData.Categories.Remove(cat);
            Save();
            return null;
        }

        /// <summary>
        /// Get all custom categories.
        /// </summary>
        public List<CustomCategory> GetCategories()
        {
            return _customData.Categories;
        }
    }
}
