using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwComAddin.Models
{
    public class StandardPartsCatalog
    {
        public List<Category> Categories { get; set; } = new();
    }

    public class Category
    {
        public string Name { get; set; } = "";
        public string Icon { get; set; } = "";
        public List<SubCategory> SubCategories { get; set; } = new();
        public List<StandardPart> Parts { get; set; } = new();
    }

    public class SubCategory
    {
        public string Name { get; set; } = "";
        public List<StandardPart> Parts { get; set; } = new();
    }

    public class StandardPart
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Standard { get; set; } = "";
        public bool Geometric { get; set; }
        [JsonPropertyName("feature_template")]
        public string? FeatureTemplate { get; set; }
        public string ModelType { get; set; } = "";       // "parametric" | "external" | ""
        public string BuilderMethod { get; set; } = "";   // "BuildBolt" | "BuildNut" etc.
        public Dictionary<string, object> Specs { get; set; } = new();
        public string Description { get; set; } = "";
    }
}
