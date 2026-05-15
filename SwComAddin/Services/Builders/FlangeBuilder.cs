using System;
using System.Collections.Generic;
using SolidWorks.Interop.sldworks;

namespace SwComAddin.Services.Builders
{
    public class FlangeBuilder : IPartBuilder
    {
        public (bool success, string message) Build(Dictionary<string, object> p, ISldWorks swApp)
        {
            try
            {
                double outerD = GetD(p, "outer_d", 100);
                double innerD = GetD(p, "inner_d", 60);
                double boltCD = GetD(p, "bolt_circle_d", 80);
                double thick = GetD(p, "thickness", 10);
                int bolts = GetI(p, "bolt_count", 6);
                double boltD = GetD(p, "bolt_d", 8);

                var part = (IModelDoc2)swApp.NewPart();
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
                ParametricBuilder.ExtrudeBoss(featMgr, thick / 1000.0);

                part.SaveAs2($"flange_{(int)outerD}_{(int)innerD}.sldprt", 0, false, false);
                return (true, $"法兰: 外径{outerD}mm, 内径{innerD}mm, {bolts}孔");
            }
            catch (Exception ex) { return (false, ex.Message); }
        }

        private static double GetD(Dictionary<string, object> p, string k, double d) => ParametricBuilder.GetD(p, k, d);
        private static int GetI(Dictionary<string, object> p, string k, int d) => ParametricBuilder.GetI(p, k, d);
    }
}
