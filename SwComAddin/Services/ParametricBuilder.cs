using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services
{
    public class ParametricBuilder
    {
        private readonly SwConnector _connector;

        public ParametricBuilder(SwConnector connector)
        {
            _connector = connector;
        }

        private ISldWorks SwApp => (ISldWorks)_connector.GetSwApp();

        /// <summary>
        /// Draw a rectangle using CreateCornerRectangle from ISketchManager.
        /// CreateCornerRectangle(x1, y1, z1, x2, y2, z2)
        /// </summary>
        private static void CreateRectangle(ISketchManager sm, double x1, double y1, double z1, double x2, double y2, double z2)
        {
            sm.CreateCornerRectangle(x1, y1, z1, x2, y2, z2);
        }

        /// <summary>
        /// Wrapper for FeatureExtrusion3 matching the strongly-typed interop signature (23 params):
        /// FeatureExtrusion3(bool, bool, bool, int, int, double, double,
        ///   bool, bool, bool, bool, double, double,
        ///   bool, bool, bool, bool, bool, bool, bool, int, double, bool)
        /// </summary>
        private static object? ExtrudeBoss(IFeatureManager fm, double depth)
        {
            return fm.FeatureExtrusion3(
                true,   // sd - single direction
                false,  // flip
                false,  // dir
                0,      // t1Type (int: 0=blind)
                0,      // t2Type (int: 0=blind)
                depth,  // t1Depth
                0.0,    // t2Depth
                false,  // t1ReverseDir
                false,  // t2ReverseDir
                false,  // t1Translate
                false,  // t2Translate
                0.0175, // t1Angle
                0.0175, // t2Angle
                false,  // t1Align
                false,  // t2Align
                false,  // t1Translate2
                false,  // t2Translate2
                true,   // merge
                true,   // useFeatScope
                true,   // useAutoSelect
                0,      // swStartCondition (int)
                0.0,    // swStartOffset (double)
                false); // swStartAlign
        }

        /// <summary>
        /// Wrapper for FeatureCut3 matching the strongly-typed interop signature (26 params):
        /// FeatureCut3(bool, bool, bool, int, int, double, double,
        ///   bool, bool, bool, bool, double, double,
        ///   bool, bool, bool, bool, bool, bool, bool, bool, bool, bool,
        ///   int, double, bool)
        /// </summary>
        private static object? ExtrudeCut(IFeatureManager fm, double depth)
        {
            return fm.FeatureCut3(
                false,  // sd
                false,  // flip
                false,  // dir
                0,      // t1Type (int: 0=blind)
                0,      // t2Type (int: 0=blind)
                depth,  // t1Depth
                0.0,    // t2Depth
                false,  // t1ReverseDir
                false,  // t2ReverseDir
                false,  // t1Translate
                false,  // t2Translate
                0.0175, // t1Angle
                0.0175, // t2Angle
                false,  // t1Align
                false,  // t2Align
                false,  // t1Translate2
                false,  // t2Translate2
                true,   // t1Cap
                true,   // t2Cap
                true,   // t1BodyOnly
                false,  // t2BodyOnly
                false,  // t1Offset
                false,  // t2Offset
                0,      // cutOption (int)
                0.0,    // cutAngle (double)
                false); // assemblyCutScope
        }

        public (bool success, string message) BuildFlange(Dictionary<string, object> p)
        {
            try
            {
                double outerD = GetD(p, "outer_d", 100);
                double innerD = GetD(p, "inner_d", 60);
                double boltCD = GetD(p, "bolt_circle_d", 80);
                double thick = GetD(p, "thickness", 10);
                int bolts = GetI(p, "bolt_count", 6);
                double boltD = GetD(p, "bolt_d", 8);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, outerD / 2.0, 0, 0);
                sketchMgr.CreateCircle(0, 0, 0, innerD / 2.0, 0, 0);
                for (int i = 0; i < bolts; i++)
                {
                    double a = 2.0 * Math.PI * i / bolts;
                    sketchMgr.CreateCircle(boltCD / 2.0 * Math.Cos(a), boltCD / 2.0 * Math.Sin(a), 0, boltD / 2.0, 0, 0);
                }
                sketchMgr.InsertSketch(true);

                var featMgr = (IFeatureManager)part.FeatureManager;
                ExtrudeBoss(featMgr, thick / 1000.0);

                part.SaveAs2($"flange_{(int)outerD}_{(int)innerD}.sldprt", 0, false, false);
                return (true, $"法兰: 外径{outerD}mm, 内径{innerD}mm, {bolts}孔");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildSteppedShaft(Dictionary<string, object> p)
        {
            try
            {
                var segs = ParseSegs(GetS(p, "segments", "20x50,15x30"));
                if (segs.Count == 0) return (false, "无效轴段参数");

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                foreach (var (d, l) in segs)
                {
                    sketchMgr.InsertSketch(true);
                    sketchMgr.CreateCircle(0, 0, 0, d / 2.0, 0, 0);
                    sketchMgr.InsertSketch(true);
                    ExtrudeBoss(featMgr, l / 1000.0);
                }

                part.SaveAs2($"shaft_{segs.Count}seg.sldprt", 0, false, false);
                return (true, $"阶梯轴: {segs.Count}段, 总长{segs.Sum(s => s.Item2)}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildConnectionPlate(Dictionary<string, object> p)
        {
            try
            {
                double w = GetD(p, "width", 100), h = GetD(p, "height", 80), t = GetD(p, "thickness", 5);
                int hc = GetI(p, "hole_count", 4); double hd = GetD(p, "hole_d", 8);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                sketchMgr.InsertSketch(true);
                CreateRectangle(sketchMgr, -w / 2, -h / 2, 0, w / 2, h / 2, 0);
                sketchMgr.InsertSketch(true);

                var featMgr = (IFeatureManager)part.FeatureManager;
                ExtrudeBoss(featMgr, t / 1000.0);

                part.SaveAs2($"plate_{(int)w}x{(int)h}.sldprt", 0, false, false);
                return (true, $"连接板: {w}x{h}x{t}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildBracket(Dictionary<string, object> p)
        {
            try
            {
                double bw = GetD(p, "base_w", 100), bh = GetD(p, "base_h", 20), bt = GetD(p, "base_t", 10);
                double ah = GetD(p, "arm_h", 60), at = GetD(p, "arm_t", 8), hd = GetD(p, "hole_d", 8);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                // Base
                sketchMgr.InsertSketch(true);
                CreateRectangle(sketchMgr, -bw / 2, 0, 0, bw / 2, bh, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, bt / 1000.0);

                // Arm
                sketchMgr.InsertSketch(true);
                CreateRectangle(sketchMgr, -at / 2, bh, 0, at / 2, bh + ah, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, bt / 1000.0);

                part.SaveAs2($"bracket_{(int)bw}.sldprt", 0, false, false);
                return (true, $"支架: 底座{bw}x{bh}mm, 臂高{ah}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildBearingBlock(Dictionary<string, object> p)
        {
            try
            {
                double bd = GetD(p, "bore_d", 25), ow = GetD(p, "outer_w", 60);
                double bsh = GetD(p, "base_h", 30), bs = GetD(p, "bolt_spacing", 80);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                // Body
                sketchMgr.InsertSketch(true);
                CreateRectangle(sketchMgr, -ow / 2, 0, 0, ow / 2, bsh + ow / 4, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, ow / 1000.0);

                // Bore hole
                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, bsh + ow / 4, 0, bd / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeCut(featMgr, ow / 1000.0 + 0.001);

                part.SaveAs2($"bearing_block_{(int)bd}.sldprt", 0, false, false);
                return (true, $"轴承座: 孔径{bd}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildBolt(Dictionary<string, object> p)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double length = GetD(p, "length", 50);
                double headDiameter = GetD(p, "head_diameter", 10);
                double headHeight = GetD(p, "head_height", 4);
                double chamferSize = GetD(p, "chamfer_size", 1.0);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;
                var ext = (IModelDocExtension)part.Extension;

                // 1. Hex head
                sketchMgr.InsertSketch(true);
                sketchMgr.CreatePolygon(0, 0, 0, headDiameter / 2.0, 0, 0, 6, true);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, headHeight / 1000.0);

                // 2. Select top face of head using SelectByID2 with coordinates
                part.ClearSelection2(true);
                ext.SelectByID2("", "FACE", 0, 0, headHeight / 1000.0, false, 0, null, 0);

                // 3. Shank on head top face
                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, length / 1000.0);

                // 4. Chamfer on head top edges
                if (chamferSize > 0)
                {
                    part.ClearSelection2(true);
                    ext.SelectByID2("", "FACE", 0, 0, headHeight / 1000.0, false, 0, null, 0);
                    featMgr.InsertFeatureChamfer(4, 1, chamferSize / 1000.0, 0.5236, 0, 0, 0, 0);
                }

                part.SaveAs2($"bolt_M{(int)diameter}x{(int)length}.sldprt", 0, false, false);
                return (true, $"螺栓: M{(int)diameter}x{(int)length}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildNut(Dictionary<string, object> p)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double widthAcrossFlats = GetD(p, "width_across_flats", 10);
                double height = GetD(p, "height", 5.2);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                double circumRadius = widthAcrossFlats / Math.Sqrt(3);

                // Hex body
                sketchMgr.InsertSketch(true);
                sketchMgr.CreatePolygon(0, 0, 0, circumRadius, 0, 0, 6, true);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, height / 1000.0);

                // Through hole
                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeCut(featMgr, height / 1000.0 + 0.001);

                part.SaveAs2($"nut_M{(int)diameter}.sldprt", 0, false, false);
                return (true, $"螺母: M{(int)diameter}, 对边{widthAcrossFlats}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildWasher(Dictionary<string, object> p)
        {
            try
            {
                double innerD = GetD(p, "inner_diameter", 6.4);
                double outerD = GetD(p, "outer_diameter", 12);
                double thickness = GetD(p, "thickness", 1.6);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, outerD / 2.0, 0, 0);
                sketchMgr.CreateCircle(0, 0, 0, innerD / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, thickness / 1000.0);

                part.SaveAs2($"washer_{(int)innerD}x{(int)outerD}.sldprt", 0, false, false);
                return (true, $"垫圈: 内径{innerD}mm, 外径{outerD}mm, 厚{thickness}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool success, string message) BuildDowelPin(Dictionary<string, object> p)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double length = GetD(p, "length", 30);
                double chamferSize = GetD(p, "chamfer_size", 2.0);

                var part = (IModelDoc2)SwApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;
                var ext = (IModelDocExtension)part.Extension;

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ExtrudeBoss(featMgr, length / 1000.0);

                if (chamferSize > 0)
                {
                    // Top chamfer - select edge at top
                    part.ClearSelection2(true);
                    ext.SelectByID2("", "EDGE", diameter / 2.0 / 1000.0, 0, length / 1000.0, false, 0, null, 0);
                    featMgr.InsertFeatureChamfer(4, 1, chamferSize / 1000.0, 0.7854, 0, 0, 0, 0);
                    // Bottom chamfer
                    part.ClearSelection2(true);
                    ext.SelectByID2("", "EDGE", diameter / 2.0 / 1000.0, 0, 0, false, 0, null, 0);
                    featMgr.InsertFeatureChamfer(4, 1, chamferSize / 1000.0, 0.7854, 0, 0, 0, 0);
                }

                part.SaveAs2($"dowel_pin_{(int)diameter}x{(int)length}.sldprt", 0, false, false);
                return (true, $"圆柱销: 直径{diameter}mm, 长{length}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d)
            => p.TryGetValue(k, out var v) ? (v is double dd ? dd : double.TryParse(v?.ToString(), out var r) ? r : d) : d;
        private static int GetI(Dictionary<string, object> p, string k, int d)
            => p.TryGetValue(k, out var v) ? (v is int i ? i : int.TryParse(v?.ToString(), out var r) ? r : d) : d;
        private static string GetS(Dictionary<string, object> p, string k, string d)
            => p.TryGetValue(k, out var v) ? v?.ToString() ?? d : d;

        private static List<(double, double)> ParseSegs(string s)
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
    }
}
