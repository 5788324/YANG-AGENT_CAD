# YGDETAIL 闭合多段线大样 V1（2026-11-05）

关联 [Issue #12](https://github.com/5788324/YANG-AGENT_CAD/issues/12)。

状态：**STATIC_CANDIDATE / WINDOWS_NOT_TESTED**。此分支仅是源码增量，不属于已验收的 AutoCAD 2024 RC1。不得据源代码、CI 或历史 DLL SHA 直接判断 CAD 2024 可运行。

## 使用方法

命令仍为 `YGDETAIL`，新增模式：
- Rectangle（默认）：走原来已经验收的矩形大样 `DetailExtractService.Extract`，代码保持原样。
- Select：拾取**当前模型空间/当前布局**内已闭合、零宽度、WCS XY 平面 **LWPOLYLINE**。
- Draw：逐点指定同一 WCS XY 平面的直边多边形，回车闭合（不在原位置增加边界对象）。

之后选择放置点和放大倍数。原图和原始边界对象只读；在同一事务内生成独立新对象与可编辑的闭合边界框。

## V1 支持和限制

- 直边闭合多边形，可凸可凹，可选择已有边界，也可逐点绘制。
- 几何：LINE、ARC、CIRCLE、非宽线的 LWPOLYLINE（先原生 Explode 为 LINE/ARC，再精确裁切）；非裁切对象被完整安全包含时克隆。
- 几何引擎 `PolygonBoundary2d` 验证 3–512 点、自交、相邻零长度、零面积；边界上的线段按包含处理；裁切只在**直线与弧线数学交点**处分段，不进行像素级或碎线近似。
- **不接受带 bulge 弧段的裁切边界**，报错并保持图纸不变。注意：源内容多段线的弧段可以通过 Explode→Arc 被裁切。此差异必须在 UX/测试中明确。
- 复杂跨界块、标注、文字、填充不强行裁切；属性块、动态块、外参、关联 HATCH、底图引用和受保护图层采用保守跳过，并输出警告。普通内部对象仅在其轴对齐包围框可证明完全位于边界内时克隆。
- 默认保持源实体的普通图元属性；新增图元的 Handle 与源图不同。尺寸标注放大时沿用既有的 Dimlfac 补偿策略。
- 本 V1 的边界坐标按**世界坐标 WCS XY**计算；旋转 UCS、非 XY 斜截面、多级嵌套块内容、非平面弧线、全闭合弧形边界不在本轮范围。
- 受控上限：当前空间 100000 个对象、单次输出 20000 个图元、边界顶点最多 512（手工绘制最多 128）。
- 源对象从不 Erase；实际 Undo 粒度仍以 Windows 原生 `_.U` 对象读回为准。
- 本轮不实现 `YGOUTSIDE`；后续应复用 `PolygonBoundary2d`，并加入删除预览、选择性裁切和高破坏操作门禁。

## 执行路径

`src/YangCAD.AutoCAD/Commands/DetailExtractCommands.cs` 负责中文提示和拾取。
`src/YangCAD.AutoCAD/Services/Cad/PolygonBoundary2d.cs` 负责几何、顶点、线与圆的交点。
`src/YangCAD.AutoCAD/Services/Cad/PolygonDetailExtractService.cs` 负责受控复制/缩放/事务输出。
`src/YangCAD.AutoCAD/Services/Cad/DetailExtractService.cs` 原矩形引擎未改。

仅有静态 policy 并不足以证明 C# 能编译、Autodesk API 正确、对象不会丢失或 Undo 真正有效。请执行 `tasks/DETAIL_POLYGON_WINDOWS_SMOKE_20261105.md` 的实机测试后再判断是否合并。
