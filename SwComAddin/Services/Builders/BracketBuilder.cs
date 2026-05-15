using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class BracketBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double bw = GetD(p, "base_w", 100), bh = GetD(p, "base_h", 20), bt = GetD(p, "base_t", 10);
                double ah = GetD(p, "arm_h", 60), at = GetD(p, "arm_t", 8), hd = GetD(p, "hole_d", 8);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                sketchMgr.InsertSketch(true);
                ParametricBuilder.CreateRectangle(sketchMgr, -bw / 2, 0, 0, bw / 2, bh, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, bt / 1000.0);

                sketchMgr.InsertSketch(true);
                ParametricBuilder.CreateRectangle(sketchMgr, -at / 2, bh, 0, at / 2, bh + ah, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, bt / 1000.0);

                part.SaveAs2($"bracket_{(int)bw}.sldprt", 0, false, false);
                return (true, $"支架: 底座{bw}x{bh}mm, 臂高{ah}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
