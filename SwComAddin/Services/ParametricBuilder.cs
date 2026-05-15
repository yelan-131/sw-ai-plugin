using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;
using SwComAddin.Services.Builders;

namespace SwComAddin.Services
{
    public class ParametricBuilder
    {
        private readonly ISldWorks _swApp;

        private static readonly Dictionary<string, IPartBuilder> _builders = new()
        {
            ["flange"] = new FlangeBuilder(),
            ["stepped_shaft"] = new SteppedShaftBuilder(),
            ["connection_plate"] = new ConnectionPlateBuilder(),
            ["bracket"] = new BracketBuilder(),
            ["bearing_block"] = new BearingBlockBuilder(),
            ["hex_bolt"] = new BoltBuilder(),
            ["hex_nut"] = new NutBuilder(),
            ["flat_washer"] = new WasherBuilder(),
            ["spring_washer"] = new WasherBuilder(),
            ["dowel_pin"] = new DowelPinBuilder(),
        };

        public ParametricBuilder(SwConnector connector)
        {
            _swApp = (ISldWorks)connector.GetSwApp();
        }

        public (bool success, string message) Build(string templateKey, Dictionary<string, object> parameters)
        {
            if (_builders.TryGetValue(templateKey, out var builder))
                return builder.Build(parameters, _swApp);
            return (false, $"未知的建模模板: {templateKey}");
        }

        // Legacy methods kept for backward compatibility with existing callers
        public (bool success, string message) BuildFlange(Dictionary<string, object> p) => _builders["flange"].Build(p, _swApp);
        public (bool success, string message) BuildSteppedShaft(Dictionary<string, object> p) => _builders["stepped_shaft"].Build(p, _swApp);
        public (bool success, string message) BuildConnectionPlate(Dictionary<string, object> p) => _builders["connection_plate"].Build(p, _swApp);
        public (bool success, string message) BuildBracket(Dictionary<string, object> p) => _builders["bracket"].Build(p, _swApp);
        public (bool success, string message) BuildBearingBlock(Dictionary<string, object> p) => _builders["bearing_block"].Build(p, _swApp);
        public (bool success, string message) BuildBolt(Dictionary<string, object> p) => _builders["hex_bolt"].Build(p, _swApp);
        public (bool success, string message) BuildNut(Dictionary<string, object> p) => _builders["hex_nut"].Build(p, _swApp);
        public (bool success, string message) BuildWasher(Dictionary<string, object> p) => _builders["flat_washer"].Build(p, _swApp);
        public (bool success, string message) BuildDowelPin(Dictionary<string, object> p) => _builders["dowel_pin"].Build(p, _swApp);

        // === Public static helpers ===

        public static double GetD(Dictionary<string, object> p, string k, double d)
            => p.TryGetValue(k, out var v) ? (v is double dd ? dd : double.TryParse(v?.ToString(), out var r) ? r : d) : d;

        public static int GetI(Dictionary<string, object> p, string k, int d)
            => p.TryGetValue(k, out var v) ? (v is int i ? i : int.TryParse(v?.ToString(), out var r) ? r : d) : d;

        public static string GetS(Dictionary<string, object> p, string k, string d)
            => p.TryGetValue(k, out var v) ? v?.ToString() ?? d : d;

        public static List<(double, double)> ParseSegs(string s)
        {
            var r = new List<(double, double)>();
            foreach (var p in s.Split(','))
            {
                var d = p.Trim().Split('x', '×');
                if (d.Length == 2 && double.TryParse(d[0], out var dd) && double.TryParse(d[1], out var ll))
                    r.Add((dd, ll));
            }
            return r;
        }

        // === Public SW API wrappers ===

        public static void CreateRectangle(ISketchManager sm, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            sm.CreateCornerRectangle(x1, y1, z1, x2, y2, z2);
        }

        public static object ExtrudeBoss(IFeatureManager fm, double depth)
        {
            return fm.FeatureExtrusion3(
                true, false, false, 0, 0, depth, 0.0,
                false, false, false, false, 0.0175, 0.0175,
                false, false, false, false, true, true, true,
                0, 0.0, false);
        }

        public static object ExtrudeCut(IFeatureManager fm, double depth)
        {
            return fm.FeatureCut3(
                false, false, false, 0, 0, depth, 0.0,
                false, false, false, false, 0.0175, 0.0175,
                false, false, false, false, true, true, true, false, false, false,
                0, 0.0, false);
        }
    }
}
