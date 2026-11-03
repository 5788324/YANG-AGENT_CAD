# YangCAD 0.9 — 图层工具 + 切割提取大样（2026-11-03）

状态：`IMPLEMENTED / STATIC REVIEW / AUTOCAD 2024 BUILD + RUNTIME + UNDO REQUIRED`，**非正式 Release GO**。

## 用户使用方式（尽量少步骤）

### 图层工具

Ribbon → **图层工具** (`YGLAYER`) → 选择图层 → 直接点按钮：

- **关闭图层 / 显示图层**
- **冻结图层 / 解冻图层**
- **锁定图层 / 解锁图层**
- **合并到其他图层**（先选择目标图层）

图层包括 `XREF|Layer`。隐藏/冻结之后仍可从下拉列表恢复。按“图中选取”可点普通对象、块内对象或外部参照内容。遇到嵌套选择，输入 `I` 选择内部对象图层，输入 `B` 选择**块 / 外参插入图层**。

想直接点图层操作，也可输入 `YGLMERGE`、`YGLONOFF`、`YGLFREEZE`、`YGLLOCK`。三个开关命令会根据图层当前状态自动切换。

**图层合并**会将当前 DWG 本地实体的 LayerId 改成目标图层，包括模型空间、所有布局、普通本地块定义及块属性中的实体；原图元不删除、不炸开。实体上的 ByLayer 颜色/线型会随目标图层变化，这是 AutoCAD 标准行为。源图层即使变空也暂时保留，可按需另行 PURGE，不冒充已删除。

**外参边界**：对 `XREF|Layer` 只能在宿主图纸进行显示/冻结/锁定控制；拒绝直接把 xref 依赖层合并到本地图层，也不会打开/覆盖 xref 的 DWG 文件。若要保留宿主图层 override，注意 `VISRETAIN` 设置；在 REFEDIT 中更改的状态不保证保留。

### 切割提取大样

Ribbon → **提取大样** (`YGDETAIL`) → 点两个对角点 → 点目标放置位置 → 输入倍率（默认 `1`，直接回车）。原位置的图纸保持不动。新大样默认有边框、保留完整位于框内的标注和引线。

- **精确裁切**：`LINE`、`ARC`、`CIRCLE`、直线/圆弧段 `LWPOLYLINE`，包括多段线 bulge 弧段。
- **完整位于框内复制**：常规几何、文字、独立普通块（无属性）、非关联 Hatch、完整标注/引线等。
- **不会强行处理**：跨边界的复杂块、Hatch、引线和尺寸标注；动态块、带属性块、外部参照、底图、关联 Hatch；宽多段线/特殊曲面不精确裁切。此类明确跳过并报告，不 silently explode/destroy。
- **倍率不为 1**：完整复制的原生 Dimension 会尝试按倍率反向修正对象级 `Dimlfac`，不能安全修正的拒绝；**不改全局 DimStyle**。这仍需要 AutoCAD 2024 验证。
- 此 V1 是**安全的矩形裁切**，还不是对源泉 QG 全部版本/全部对象的像素级复刻；圆形/多边形边界、跨界关联填充/标注修复需另行开发。

对应产品 MCP：

```text
cad.layer.merge
cad.layer.visibility
cad.layer.freeze
cad.layer.lock
cad.detail.extract
```

GUI 与 MCP 共用两个业务 Service：`LayerQuickService`、`DetailExtractService`。MCP 默认 `dry_run=true`，且使用现有 PID+Document binding。新增五个工具，静态目标合同 `74 / 37 / 67`（stdio / plain AutoCAD / Civil 3D）。

## 本轮设计取舍

- 用户个人工作优先：一窗口四操作 + 一个大样命令，不额外建复杂属性设置页。
- 同一操作由 `CadTransactionRunner.WriteContext` 包裹；目标一条命令一次 Ctrl+Z，**需要真机证明**。
- 受保护图层不偷偷变更；当前层拒绝隐藏/冻结；源图层 `0 / Defpoints` 不作为合并来源。
- QG 不修改任何源实体，避免依赖不安全的边界选择或 EXPLODE 原图。
- 任何有限度裁切无法保真时跳过，而不是把不可裁切类型“全复制”造成大样越界。
- 源泉插件命令参考仅用于功能用途分析；本项目 C# 独立实现，没有使用或修改源泉代码。

## 对实机执行器的事实口径

**静态检查只能证明源码结构，不能证明 Windows 2024 真运行。**

参见：`tasks/LAYER_TOOLS_QG_WINDOWS_SMOKE_20261103.md`，需要真实 Build/Deploy/Loaded SHA、场景前后属性、一次 Undo 证据。之前的 V2 图片内嵌、比例转换、引线修复仍保持待实机状态，本轮不得写成 GO。
