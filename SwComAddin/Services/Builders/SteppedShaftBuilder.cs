using System;
using System.Collections.Generic;
using System.Linq;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class SteppedShaftBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                var segs = ParametricBuilder.ParseSegs(GetS(p, "segments", "20x50,15x30"));
                if (segs.Count == 0) return (false, "无效轴段参数");

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                foreach (var (d, l) in segs)
                {
                    sketchMgr.InsertSketch(true);
                    sketchMgr.CreateCircle(0, 0, 0, d / 2.0, 0, 0);
                    sketchMgr.InsertSketch(true);
                    ParametricBuilder.ExtrudeBoss(featMgr, l / 1000.0);
                }

                part.SaveAs2($"shaft_{segs.Count}seg.sldprt", 0, false, false);
                return (true, $"阶梯轴: {segs.Count}段, 总长{segs.Sum(s => s.Item2)}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
        private static string GetS(Dictionary<string, object> p, string k, string d) => ParametricBuilder.GetS(p, k, d);
    }
}
