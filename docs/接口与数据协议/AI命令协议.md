# AI 命令协议


Claude 返回的 `commands` 数组中每个元素格式为：

```json
{ "action": "<命令名>", "<参数1>": <值1>, "<参数2>": <值2> }
```

### 基础操作（4 种）

| action | 参数 | 说明 |
|--------|------|------|
| `new_part` | 无 | 新建零件文档 |
| `new_assembly` | 无 | 新建装配体文档 |
| `open_file` | `filename: string` | 打开文件 |
| `save_file` | `filename?: string` | 保存文件（可选指定名称） |

### 草图操作（7 种）

| action | 参数 | 说明 |
|--------|------|------|
| `select_plane` | `plane: "top"/"front"/"right"` | 选择草图基准面 |
| `sketch_start` | 无 | 进入草图模式 |
| `sketch_end` | 无 | 退出草图模式 |
| `sketch_circle` | `center_x, center_y, radius: double` | 绘制圆 |
| `sketch_rectangle` | `x1, y1, x2, y2: double` | 绘制矩形（两个对角点） |
| `sketch_line` | `x1, y1, x2, y2: double` | 绘制直线 |
| `sketch_arc` | `center_x, center_y, radius, start_angle, end_angle: double` | 绘制圆弧 |

### 特征操作（5 种）

| action | 参数 | 说明 |
|--------|------|------|
| `extrude` | `depth: double`, `direction?: "forward"/"backward"` | 拉伸凸台 |
| `revolve` | `axis: "x"/"y"/"z"`, `angle?: double` | 旋转体（默认 360°） |
| `cut_extrude` | `depth: double` | 拉伸切除 |
| `add_fillet` | `radius: double`, `edges: int[]` | 添加圆角 |
| `add_chamfer` | `distance: double`, `edges: int[]` | 添加倒角 |

### 装配体操作（2 种）

| action | 参数 | 说明 |
|--------|------|------|
| `insert_component` | `file_path: string`, `x, y, z: double` | 插入零部件到指定位置 |
| `add_mate` | `type: string`, `selection1, selection2: string` | 添加配合关系 |

**单位约定**：尺寸 mm，角度 degree。

---

**单位约定**：尺寸 mm，角度 degree。

## 相关文档

| 想了解... | 请看 |
|-----------|------|
| HTTP API | [HTTP-API](HTTP-API.md) |
| AI 命令 | [AI命令协议](AI命令协议.md) |
| 数据结构 | [数据结构](数据结构.md) |
| 配置文件 | [配置文件](配置文件.md) |
| manifest | [manifest](manifest.md) |
| 错误码 | [错误码](错误码.md) |
