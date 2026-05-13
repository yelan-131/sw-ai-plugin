using System.Collections.Generic;

namespace SwComAddin.Models
{
    public enum ExtrudeDirection { Forward, Backward, Symmetric }

    // === Profiles (sketch shapes) ===

    public abstract class Profile { }

    public class CircleProfile : Profile
    {
        public double Diameter { get; set; }
    }

    public class RectangleProfile : Profile
    {
        public double Width { get; set; }
        public double Height { get; set; }
    }

    public class PolygonProfile : Profile
    {
        public int Sides { get; set; }
        public double Diameter { get; set; }
    }

    // === Features (modeling operations) ===

    public abstract class ModelFeature { }

    public class ExtrudeFeature : ModelFeature
    {
        public Profile Profile { get; set; }
        public double Depth { get; set; }
        public ExtrudeDirection Direction { get; set; } = ExtrudeDirection.Forward;
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public string Plane { get; set; } = "top";
        public string MergeRule { get; set; } = "join";
    }

    public class ExtrudeCutFeature : ModelFeature
    {
        public Profile Profile { get; set; }
        public double Depth { get; set; }
        public double OffsetX { get; set; }
        public double OffsetY { get; set; }
        public string Plane { get; set; } = "top";
    }

    public class RevolveFeature : ModelFeature
    {
        public Profile Profile { get; set; }
        public double Angle { get; set; } = 360;
        public string Plane { get; set; } = "front";
    }

    public class ChamferFeature : ModelFeature
    {
        public double Size { get; set; }
        public string Edge { get; set; } = "top";
    }

    public class FilletFeature : ModelFeature
    {
        public double Radius { get; set; }
        public string Edge { get; set; } = "all";
    }

    public class HoleFeature : ModelFeature
    {
        public double Diameter { get; set; }
        public double Depth { get; set; }
        public double PositionX { get; set; }
        public double PositionY { get; set; }
        public string Plane { get; set; } = "top";
    }

    // === Feature list (the modeling script) ===

    public class FeatureList
    {
        public List<ModelFeature> Features { get; set; } = new List<ModelFeature>();

        public void AddExtrude(Profile profile, double depth, string plane = "top",
            string merge = "join", double offsetX = 0, double offsetY = 0)
        {
            Features.Add(new ExtrudeFeature
            {
                Profile = profile,
                Depth = depth,
                Plane = plane,
                MergeRule = merge,
                OffsetX = offsetX,
                OffsetY = offsetY
            });
        }

        public void AddExtrudeCut(Profile profile, double depth, string plane = "top",
            double offsetX = 0, double offsetY = 0)
        {
            Features.Add(new ExtrudeCutFeature
            {
                Profile = profile,
                Depth = depth,
                Plane = plane,
                OffsetX = offsetX,
                OffsetY = offsetY
            });
        }

        public void AddRevolve(Profile profile, double angle = 360, string plane = "front")
        {
            Features.Add(new RevolveFeature
            {
                Profile = profile,
                Angle = angle,
                Plane = plane
            });
        }

        public void AddChamfer(double size, string edge = "top")
        {
            Features.Add(new ChamferFeature { Size = size, Edge = edge });
        }

        public void AddFillet(double radius, string edge = "all")
        {
            Features.Add(new FilletFeature { Radius = radius, Edge = edge });
        }

        public void AddHole(double diameter, double depth, double posX = 0,
            double posY = 0, string plane = "top")
        {
            Features.Add(new HoleFeature
            {
                Diameter = diameter,
                Depth = depth,
                PositionX = posX,
                PositionY = posY,
                Plane = plane
            });
        }
    }
}
