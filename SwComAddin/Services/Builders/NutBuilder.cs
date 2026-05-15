using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class NutBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double diameter = GetD(p, "diameter", 6);
                double widthAcrossFlats = GetD(p, "width_across_flats", 10);
                double height = GetD(p, "height", 5.2);

                var part = (IModelDoc2)swApp.NewPart();
                if (part == null) return (false, "无法创建零件");

                var sketchMgr = (ISketchManager)part.SketchManager;
                var featMgr = (IFeatureManager)part.FeatureManager;

                double circumRadius = widthAcrossFlats / Math.Sqrt(3);

                sketchMgr.InsertSketch(true);
                sketchMgr.CreatePolygon(0, 0, 0, circumRadius, 0, 0, 6, true);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeBoss(featMgr, height / 1000.0);

                sketchMgr.InsertSketch(true);
                sketchMgr.CreateCircle(0, 0, 0, diameter / 2.0, 0, 0);
                sketchMgr.InsertSketch(true);
                ParametricBuilder.ExtrudeCut(featMgr, height / 1000.0 + 0.001);

                part.SaveAs2($"nut_M{(int)diameter}.sldprt", 0, false, false);
                return (true, $"螺母: M{(int)diameter}, 对边{widthAcrossFlats}mm");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
    }
}
