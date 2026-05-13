using System.Collections.Generic;

namespace SwComAddin.Models
{
    public class CustomLibraryData
    {
        public List<CustomCategory> Categories { get; set; } = new();
        public List<CustomPart> Parts { get; set; } = new();
        public List<CustomTemplate> Templates { get; set; } = new();
    }

    public class CustomCategory
    {
        public string Name { get; set; } = "";
        public List<SubCategory> SubCategories { get; set; } = new();
        public List<StandardPart> Parts { get; set; } = new();
    }

    public class CustomPart
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name { get; set; } = "";
        public string Category { get; set; } = "";
        public string Standard { get; set; } = "";
        public Dictionary<string, string> Specs { get; set; } = new();
        public string Notes { get; set; } = "";
    }

    public class CustomTemplate
    {
        public string Id { get; set; } = System.Guid.NewGuid().ToString("N").Substring(0, 8);
        public string Name { get; set; } = "";
        public List<TemplateParam> Parameters { get; set; } = new();
    }

    public class TemplateParam
    {
        public string Key { get; set; } = "";
        public string Label { get; set; } = "";
        public string DefaultValue { get; set; } = "";
        public string Unit { get; set; } = "mm";
    }
}
