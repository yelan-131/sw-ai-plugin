# SwComAddin 参数化建模模块

> Services/ParametricBuilder、Services/Builders/（IPartBuilder + 9 个构建器）、PartFeatureTemplates

---

## Services/ParametricBuilder.cs — 参数化建模

通过 SW API 直接创建零件，支持 5 种模板：

| 方法 | 零件 | 关键参数 |
|------|------|----------|
| `BuildFlange()` | 法兰 | outer_d, inner_d, bolt_circle_d, thickness, bolt_count, bolt_d |
| `BuildSteppedShaft()` | 阶梯轴 | segments (格式: "直径x长度,直径x长度") |
| `BuildConnectionPlate()` | 连接板 | width, height, thickness, hole_count, hole_d |
| `BuildBracket()` | 支架 | base_w, base_h, base_t, arm_h, arm_t, hole_d |
| `BuildBearingBlock()` | 轴承座 | bore_d, outer_w, base_h, bolt_spacing |

**建模流程**：`NewPart` → `InsertSketch` → `CreateCircle/CreateCornerRectangle` → `InsertSketch` → `FeatureExtrusion3/FeatureCut3` → `SaveAs2`

**注意**：尺寸单位为 mm，传入 SW API 时深度除以 1000（米制）。

## Services/Builders/ — Builder 模式零件构建器

`IPartBuilder` 接口定义统一的零件构建契约：

```csharp
public interface IPartBuilder
{
    (bool success, string message) Build(Dictionary<string, object> parameters, ISldWorks swApp);
}
```

**9 个构建器实现**：

| 构建器 | 零件 |
|--------|------|
| `BoltBuilder` | 六角螺栓 |
| `NutBuilder` | 六角螺母 |
| `WasherBuilder` | 平垫圈 |
| `DowelPinBuilder` | 圆柱销 |
| `FlangeBuilder` | 法兰 |
| `SteppedShaftBuilder` | 阶梯轴 |
| `ConnectionPlateBuilder` | 连接板 |
| `BracketBuilder` | 支架 |
| `BearingBlockBuilder` | 轴承座 |

**设计意图**：统一接口使每种零件拥有独立的构建类，便于扩展新零件类型，调用方只需面向 `IPartBuilder` 编程。

## PartFeatureTemplates — 14 种特征模板

将标准件 JSON 数据转换为 `FeatureList`：

| 模板名 | 零件 | 标准号 |
|--------|------|--------|
| `hex_bolt` | 六角头螺栓 | GB/T 5782 |
| `socket_screw` | 内六角螺钉 | GB/T 70.1 |
| `hex_nut` | 六角螺母 | GB/T 6170 |
| `flat_washer` | 平垫圈 | GB/T 97.1 |
| `spring_washer` | 弹簧垫圈 | GB/T 93 |
| `dowel_pin` | 圆柱销 | GB/T 119.1 |
| `ball_bearing` | 深沟球轴承 | GB/T 276 |
| `thrust_bearing` | 推力轴承 | GB/T 301 |
| `linear_bearing` | 直线轴承 | ISO 10285 |
| `pneumatic_cylinder` | 气缸 | SMC |
| `stepper_motor` | 步进电机 | NEMA |
| `servo_motor` | 伺服电机 | IEC 60034 |
| `ac_motor` | 交流电机 | IEC 60034 |
| `linear_guide` | 直线导轨 | HIWIN |

---

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| 项目配置与 COM 入口 | [SwComAddin 核心](SwComAddin-核心.md) |
| 主界面 Tab1-Tab6 详解 | [SwComAddin 主界面](SwComAddin-主界面.md) |
| 自动更新系统 | [SwComAddin 更新系统](SwComAddin-更新系统.md) |
| Python AI 后端 | [SwAiBackend](SwAiBackend.md) |
| C++ 原生 COM 垫片 | [SwNativeShim](SwNativeShim.md) |
| VBA 宏启动器与旧版 | [SwMacroPlugin](SwMacroPlugin.md) |
