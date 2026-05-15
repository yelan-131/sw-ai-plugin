using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class WasherBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double innerD = GetD(p, "inner_diameter", 6.4);
                double outerD = GetD(p, "outer_diameter", 12);
                double thickness = GetD(p, "thickness", 1.6);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, outerD / 2.0, 0, 0);
                sketchMgr.CreateCircle(0, 0, 0, innerD / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, thickness / 1000.0);

                part.SaveAs2($"washer_{(int)innerD}x{(int)outerD}.sldprt", 0, false, false);
                return (true, $"垫圈: 内径{innerD}mm, 外径{outerD}mm, 厚{thickness}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
