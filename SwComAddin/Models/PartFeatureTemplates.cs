using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace SwComAddin.Models
{
    public static class PartFeatureTemplates
    {
        public static FeatureList Build(string templateName, Dictionary<string, object> specs)
        {
            if (specs == null) return null;

            switch (templateName)
            {
                case "hex_bolt": return BuildHexBolt(specs);
                case "socket_screw":
                case "socket_head_cap_screw": return BuildSocketScrew(specs);
                case "hex_nut": return BuildHexNut(specs);
                case "flat_washer": return BuildFlatWasher(specs);
                case "spring_washer": return BuildSpringWasher(specs);
                case "dowel_pin": return BuildDowelPin(specs);
                case "ball_bearing":
                case "deep_groove_ball_bearing": return BuildBallBearing(specs);
                case "thrust_bearing":
                case "thrust_ball_bearing": return BuildThrustBearing(specs);
                case "linear_bearing":
                case "linear_ball_bearing": return BuildLinearBearing(specs);
                case "pneumatic_cylinder": return BuildCylinder(specs);
                case "stepper_motor": return BuildStepperMotor(specs);
                case "servo_motor": return BuildServoMotor(specs);
                case "ac_motor":
                case "ac_induction_motor": return BuildAcMotor(specs);
                case "linear_guide":
                case "linear_ball_guide": return BuildLinearGuide(specs);
                case "linear_roller_guide": return BuildLinearGuide(specs);
                default: return null;
            }
        }

        // === 紧固件 ===

        // 六角头螺栓 GB/T 5782
        // 需要: d(螺纹直径), l(长度), s(对边宽度), k(头部高度), r(圆角)
        // 特征: 螺柱(圆→拉伸) + 头部(六边形→拉伸) + 倒角 + 圆角
        private static FeatureList BuildHexBolt(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 6);
            double l = GetDouble(specs, "l_default", 50);
            double s = GetDouble(specs, "s", 10);
            double k = GetDouble(specs, "k", 4);
            double r = GetDouble(specs, "r", 0.25);

            var fl = new FeatureList();

            // 1. 螺柱: 圆截面拉伸
            fl.AddExtrude(
                new CircleProfile { Diameter = d },
                depth: l,
                plane: "top",
                merge: "new_body");

            // 2. 六角头: 六边形截面拉伸，接合到螺柱顶部
            fl.AddExtrude(
                new PolygonProfile { Sides = 6, Diameter = s },
                depth: k,
                plane: "previous_top",
                merge: "join");

            // 3. 顶部倒角
            fl.AddChamfer(size: 1.0, edge: "top");

            // 4. 底部圆角
            fl.AddFillet(radius: r, edge: "bottom");

            return fl;
        }

        // 内六角螺钉 GB/T 70.1
        // 需要: d, l, dk(头部直径), k(头部高度), hex(内六角对边), t(深度), r
        // 特征: 螺柱 + 圆柱头 + 内六角凹槽 + 倒角
        private static FeatureList BuildSocketScrew(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 6);
            double l = GetDouble(specs, "l_default", 30);
            double dk = GetDouble(specs, "dk", 10);
            double k = GetDouble(specs, "k", 6);
            double hex = GetDouble(specs, "hex", 5);
            double t = GetDouble(specs, "t", 3.3);
            double r = GetDouble(specs, "r", 0.5);

            var fl = new FeatureList();

            // 1. 螺柱
            fl.AddExtrude(
                new CircleProfile { Diameter = d },
                depth: l,
                plane: "top",
                merge: "new_body");

            // 2. 圆柱头
            fl.AddExtrude(
                new CircleProfile { Diameter = dk },
                depth: k,
                plane: "previous_top",
                merge: "join");

            // 3. 内六角凹槽（切除）
            fl.AddExtrudeCut(
                new PolygonProfile { Sides = 6, Diameter = hex },
                depth: t,
                plane: "top");

            // 4. 圆角
            fl.AddFillet(radius: r, edge: "all");

            return fl;
        }

        // 六角螺母 GB/T 6170
        // 需要: d(螺纹直径), s(对边宽度), m(螺母高度)
        // 特征: 六棱柱 + 螺纹孔
        private static FeatureList BuildHexNut(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 6);
            double s = GetDouble(specs, "s", 10);
            double m = GetDouble(specs, "m", 5.2);
            double c = GetDouble(specs, "c", 0.5);

            var fl = new FeatureList();

            // 1. 六棱柱
            fl.AddExtrude(
                new PolygonProfile { Sides = 6, Diameter = s },
                depth: m,
                plane: "top",
                merge: "new_body");

            // 2. 螺纹孔（贯穿）
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d },
                depth: m,
                plane: "top");

            // 3. 倒角
            fl.AddChamfer(size: c, edge: "all");

            return fl;
        }

        // 平垫圈 GB/T 97.1
        // 需要: d1(内径), d2(外径), h(厚度)
        // 特征: 圆盘拉伸 + 中心孔切除
        private static FeatureList BuildFlatWasher(Dictionary<string, object> specs)
        {
            double d1 = GetDouble(specs, "d1", 6.4);
            double d2 = GetDouble(specs, "d2", 12);
            double h = GetDouble(specs, "h", 1.6);

            var fl = new FeatureList();

            // 1. 外圆盘
            fl.AddExtrude(
                new CircleProfile { Diameter = d2 },
                depth: h,
                plane: "top",
                merge: "new_body");

            // 2. 中心孔
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d1 },
                depth: h,
                plane: "top");

            return fl;
        }

        // 弹簧垫圈 GB/T 93
        // 需要: d1, d2, s(厚度), b(宽度)
        private static FeatureList BuildSpringWasher(Dictionary<string, object> specs)
        {
            double d1 = GetDouble(specs, "d1", 6.1);
            double d2 = GetDouble(specs, "d2", 10.2);
            double s = GetDouble(specs, "s", 1.6);

            var fl = new FeatureList();

            // 1. 外圆盘（弹簧结构简化为环形）
            fl.AddExtrude(
                new CircleProfile { Diameter = d2 },
                depth: s,
                plane: "top",
                merge: "new_body");

            // 2. 中心孔
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d1 },
                depth: s,
                plane: "top");

            return fl;
        }

        // 圆柱销 GB/T 119.1
        // 需要: d(直径), l(长度), c(倒角)
        // 特征: 圆柱拉伸 + 两端倒角
        private static FeatureList BuildDowelPin(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 6);
            double l = GetDouble(specs, "l", 30);
            double c = GetDouble(specs, "c", 2.0);

            var fl = new FeatureList();

            // 1. 圆柱体
            fl.AddExtrude(
                new CircleProfile { Diameter = d },
                depth: l,
                plane: "top",
                merge: "new_body");

            // 2. 两端倒角
            fl.AddChamfer(size: c, edge: "all");

            return fl;
        }

        // === 轴承 ===

        // 深沟球轴承 GB/T 276
        // 需要: d(内径), D(外径), B(宽度), r(倒角)
        // 特征: 外环(旋转体) + 内环(旋转体) + 倒角
        private static FeatureList BuildBallBearing(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 10);
            double D = GetDouble(specs, "D", 26);
            double B = GetDouble(specs, "B", 8);
            double rMin = GetDouble(specs, "r_min", 0.3);

            var fl = new FeatureList();

            // 1. 外圆盘
            fl.AddExtrude(
                new CircleProfile { Diameter = D },
                depth: B,
                plane: "top",
                merge: "new_body");

            // 2. 内孔（简化模型，不画滚珠）
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d },
                depth: B,
                plane: "top");

            // 3. 倒角
            fl.AddChamfer(size: rMin, edge: "all");

            return fl;
        }

        // 推力轴承 GB/T 301
        // 需要: d, D, T(高度), d1, D1
        private static FeatureList BuildThrustBearing(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 10);
            double D = GetDouble(specs, "D", 24);
            double T = GetDouble(specs, "T", 9);

            var fl = new FeatureList();

            // 1. 外圆盘
            fl.AddExtrude(
                new CircleProfile { Diameter = D },
                depth: T,
                plane: "top",
                merge: "new_body");

            // 2. 内孔
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d },
                depth: T,
                plane: "top");

            return fl;
        }

        // 直线轴承 ISO 10285
        // 需要: d(内径), D(外径), L(长度)
        private static FeatureList BuildLinearBearing(Dictionary<string, object> specs)
        {
            double d = GetDouble(specs, "d", 8);
            double D = GetDouble(specs, "D", 15);
            double L = GetDouble(specs, "L", 24);

            var fl = new FeatureList();

            // 1. 外圆筒
            fl.AddExtrude(
                new CircleProfile { Diameter = D },
                depth: L,
                plane: "top",
                merge: "new_body");

            // 2. 内孔
            fl.AddExtrudeCut(
                new CircleProfile { Diameter = d },
                depth: L,
                plane: "top");

            return fl;
        }

        // === 气缸 ===

        // SMC 气缸
        // 需要: bore_diameter(缸径), stroke(行程), rod_diameter(杆径), mounting_length(总长)
        // 特征: 缸体圆柱 + 活塞杆 + 端盖
        private static FeatureList BuildCylinder(Dictionary<string, object> specs)
        {
            double bore = GetDouble(specs, "bore", 16);
            double stroke = GetDouble(specs, "stroke", 25);
            double rodDia = GetDouble(specs, "rod_diameter", 6);
            double overallLen = GetDouble(specs, "overall_length", 73.5);
            double endCapDia = GetDouble(specs, "end_cap_diameter", 22);
            double endCapH = GetDouble(specs, "end_cap_height", 14);

            var fl = new FeatureList();

            // 1. 缸体（外径约为缸径的1.5倍）
            double tubeDia = bore * 1.5;
            fl.AddExtrude(
                new CircleProfile { Diameter = tubeDia },
                depth: overallLen,
                plane: "top",
                merge: "new_body");

            // 2. 活塞杆（从前面伸出）
            fl.AddExtrude(
                new CircleProfile { Diameter = rodDia },
                depth: stroke,
                plane: "front",
                merge: "join");

            // 3. 后端盖
            fl.AddExtrude(
                new CircleProfile { Diameter = endCapDia },
                depth: endCapH,
                plane: "back",
                merge: "join");

            return fl;
        }

        // === 电机 ===

        // 步进电机 (简化模型: 方体+轴)
        // 需要: face_width, face_height, body_length, shaft_diameter
        private static FeatureList BuildStepperMotor(Dictionary<string, object> specs)
        {
            double faceW = GetDouble(specs, "face_width", 42.3);
            double bodyLen = GetDouble(specs, "body_length", 40);
            double shaftDia = GetDouble(specs, "shaft_diameter", 5);
            double shaftLen = GetDouble(specs, "shaft_length", 24);

            var fl = new FeatureList();

            // 1. 方体主体
            fl.AddExtrude(
                new RectangleProfile { Width = faceW, Height = faceW },
                depth: bodyLen,
                plane: "top",
                merge: "new_body");

            // 2. 输出轴
            fl.AddExtrude(
                new CircleProfile { Diameter = shaftDia },
                depth: shaftLen,
                plane: "front",
                merge: "join");

            return fl;
        }

        // 伺服电机 (简化模型: 圆柱体+轴)
        // 需要: shaft_diameter, body dimensions
        private static FeatureList BuildServoMotor(Dictionary<string, object> specs)
        {
            double bodyDia = GetDouble(specs, "body_diameter", 38);
            double bodyLen = GetDouble(specs, "body_length", 85);
            double shaftDia = GetDouble(specs, "shaft_diameter", 8);
            double shaftLen = GetDouble(specs, "shaft_length", 21);

            // 如果JSON中没有body_diameter/body_length，用轴径估算
            if (bodyDia <= 0) bodyDia = shaftDia * 8;
            if (bodyLen <= 0) bodyLen = shaftDia * 12;

            var fl = new FeatureList();

            // 1. 主体（圆柱体）
            fl.AddExtrude(
                new CircleProfile { Diameter = bodyDia },
                depth: bodyLen,
                plane: "top",
                merge: "new_body");

            // 2. 输出轴
            fl.AddExtrude(
                new CircleProfile { Diameter = shaftDia },
                depth: shaftLen,
                plane: "front",
                merge: "join");

            return fl;
        }

        // 交流电机 (简化模型)
        private static FeatureList BuildAcMotor(Dictionary<string, object> specs)
        {
            double bodyDia = GetDouble(specs, "body_diameter", 56);
            double bodyLen = GetDouble(specs, "body_length", 120);
            double shaftDia = GetDouble(specs, "shaft_diameter", 9);
            double shaftLen = GetDouble(specs, "shaft_length", 20);

            // 如果JSON中没有body_diameter/body_length，用轴径估算
            if (bodyDia <= 0) bodyDia = shaftDia * 8;
            if (bodyLen <= 0) bodyLen = shaftDia * 12;

            var fl = new FeatureList();

            // 1. 主体（圆柱体）
            fl.AddExtrude(
                new CircleProfile { Diameter = bodyDia },
                depth: bodyLen,
                plane: "top",
                merge: "new_body");

            // 2. 输出轴
            fl.AddExtrude(
                new CircleProfile { Diameter = shaftDia },
                depth: shaftLen,
                plane: "front",
                merge: "join");

            return fl;
        }

        // === 导轨 ===

        // 直线导轨 (简化截面模型)
        // 需要: rail_width, rail_height
        private static FeatureList BuildLinearGuide(Dictionary<string, object> specs)
        {
            double railW = GetDouble(specs, "rail_width", 12);
            double railH = GetDouble(specs, "rail_height", 8);
            double carriageLen = GetDouble(specs, "carriage_length", 30);
            double carriageW = GetDouble(specs, "carriage_width", 20);
            double carriageH = GetDouble(specs, "carriage_height", 13);

            var fl = new FeatureList();

            // 1. 导轨（固定100mm展示长度）
            fl.AddExtrude(
                new RectangleProfile { Width = railW, Height = railH },
                depth: 100,
                plane: "top",
                merge: "new_body");

            // 2. 滑块
            fl.AddExtrude(
                new RectangleProfile { Width = carriageW, Height = carriageH },
                depth: carriageLen,
                plane: "top",
                merge: "join");

            return fl;
        }

        // === 辅助方法 ===

        private static double GetDouble(Dictionary<string, object> d, string key, double defaultVal = 0)
        {
            if (d == null || !d.ContainsKey(key) || d[key] == null) return defaultVal;
            try
            {
                var v = d[key];
                if (v is double d2) return d2;
                if (v is int i) return i;
                if (v is float f) return f;
                if (v is long l) return l;
                if (v is decimal dec) return (double)dec;
                if (v is JsonElement je)
                {
                    if (je.ValueKind == JsonValueKind.Number) return je.GetDouble();
                    if (je.ValueKind == JsonValueKind.String)
                        return double.TryParse(je.GetString(), NumberStyles.Float,
                            CultureInfo.InvariantCulture, out var jp) ? jp : defaultVal;
                }
                if (v is string s && double.TryParse(s, NumberStyles.Float,
                    CultureInfo.InvariantCulture, out var parsed))
                    return parsed;
                return defaultVal;
            }
            catch { return defaultVal; }
        }

        private static int GetInt(Dictionary<string, object> d, string key, int defaultVal = 0)
        {
            return (int)Math.Round(GetDouble(d, key, defaultVal));
        }
    }
}
