using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class BoltBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double length = GetD(p, "length", 50);
                double headDiameter = GetD(p, "head_diameter", 10);
                double headHeight = GetD(p, "head_height", 4);
                double chamferSize = GetD(p, "chamfer_size", 1.0);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;
                var ext = (IModelDocExtension)part.Extension;

                sketchMgr.InsertSketch(true);
                sketchMgr.CreatePolygon(0, 0, 0, headDiameter / 2.0, 0, 0, 6, true);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, headHeight / 1000.0);

                part.ClearSelection2(true);
                ext.SelectByID2("", "FACE", 0, 0, headHeight / 1000.0, false, 0, null, 0);

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, length / 1000.0);

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

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
