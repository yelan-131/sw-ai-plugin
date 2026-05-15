using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class DowelPinBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double length = GetD(p, "length", 30);
                double chamferSize = GetD(p, "chamfer_size", 2.0);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;
                var ext = (IModelDocExtension)part.Extension;

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, length / 1000.0);

                if (chamferSize > 0)
                {
                    part.ClearSelection2(true);
                    ext.SelectByID2("", "EDGE", diameter / 2.0 / 1000.0, 0, length / 1000.0, false, 0, null, 0);
                    featMgr.InsertFeatureChamfer(4, 1, chamferSize / 1000.0, 0.7854, 0, 0, 0, 0);
                    part.ClearSelection2(true);
                    ext.SelectByID2("", "EDGE", diameter / 2.0 / 1000.0, 0, 0, false, 0, null, 0);
                    featMgr.InsertFeatureChamfer(4, 1, chamferSize / 1000.0, 0.7854, 0, 0, 0, 0);
                }

                part.SaveAs2($"dowel_pin_{(int)diameter}x{(int)length}.sldprt", 0, false, false);
                return (true, $"圆柱销: 直径{diameter}mm, 长{length}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
