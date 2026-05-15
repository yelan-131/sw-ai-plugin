using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class ConnectionPlateBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double w = GetD(p, "width", 100), h = GetD(p, "height", 80), t = GetD(p, "thickness", 5);
                int hc = GetI(p, "hole_count", 4); double hd = GetD(p, "hole_d", 8);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                sketchMgr.InsertSketch(true);
                ParametricBuilder.CreateRectangle(sketchMgr, -w / 2, -h / 2, 0, w / 2, h / 2, 0);
                sketchMgr.InsertSketch(true);

                var featMgr = (IFeatureManager)part.FeatureManager;
                ParametricBuilder.ExtrudeBoss(featMgr, t / 1000.0);

                part.SaveAs2($"plate_{(int)w}x{(int)h}.sldprt", 0, false, false);
                return (true, $"连接板: {w}x{h}x{t}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
        private static int GetI(Dictionary<string, object> p, string k, int d) => ParametricBuilder.GetI(p, k, d);
    }
}
