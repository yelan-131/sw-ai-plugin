using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class BearingBlockBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double bd = GetD(p, "bore_d", 25), ow = GetD(p, "outer_w", 60);
                double bsh = GetD(p, "base_h", 30), bs = GetD(p, "bolt_spacing", 80);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                sketchMgr.InsertSketch(true);
                ParametricBuilder.CreateRectangle(sketchMgr, -ow / 2, 0, 0, ow / 2, bsh + ow / 4, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, ow / 1000.0);

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, bsh + ow / 4, 0, bd / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeCut(featMgr, ow / 1000.0 + 0.001);

                part.SaveAs2($"bearing_block_{(int)bd}.sldprt", 0, false, false);
                return (true, $"轴承座: 孔径{bd}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
