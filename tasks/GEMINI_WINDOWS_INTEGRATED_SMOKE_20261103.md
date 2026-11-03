# GEMINI — YangCAD 0.10.0-dev 合并候选 Windows 定点验收

日期：2026-11-03；执行角色：Gemini；最终审核：ChatGPT。

**唯一源码入口**：GitHub `5788324/YANG-AGENT_CAD`，分支 `feature/product-foundation-v010-20261103` 的**执行时 HEAD**。不要测试 `main`、旧 0.9 ZIP、旧的 `POST_FIX`。固定开始测试时的 `git rev-parse HEAD`，保存到所有 JSON 的 `git_sha` 字段；任何修复改 commit 必须重新建新批次。

**必须先读**：`docs/GEMINI_EXECUTOR_SOP.md`、`docs/QUALITY_GATES.md`、`docs/RUNTIME_TEST_MATRIX.md`。

## Phase A — 构建与身份：阻断型（R00）

1. 下载/checkout 唯一 Git SHA，`git status --porcelain` 必须空；记录源码和执行环境。
2. Windows 安装有对应 Autodesk SDK 时，使用项目原生 `tools/Build.ps1 -Year 2024` 与必要的 `dotnet build`；保存完整 stdout/stderr/binlog、退出码。
3. 生成 Build、Deploy、**实际进程 Loaded DLL** 三组 SHA-256、DLL 绝对路径、Host 产品/版本、acad.exe PID、Plugin 版本。未得到 Loaded 证据不能跳过并标 PASS。
4. 保留正在运行的非测试宿主，特别是 AutoCAD 2021，不更改/关闭它。
5. G2/G3 失败应停止依赖该 DLL 的业务测试，转为失败报告；可继续独立的静态/文档检查。

## Phase B — 0.10.0 产品回归（R01/R02）

- Ribbon：原生 Home/常用不被 YangCAD 抢焦点；YangCAD 正确出现两个分组；按 Ribbon 真实按钮弹出工具箱/控制中心，记录原始 GUI 路径。
- Palette：能浮动/停靠/关闭再开；常用快捷按钮实际执行原生命令，不只是 UI 渲染。
- About：版本 `0.10.0-dev`、Host、PID、Loaded DLL 路径与 Phase A 对上，复制信息无 Token，GitHub 链接和打开日志可用。
- `YGKEYS`：真实 PGP 存储、冲突和覆盖、备份、REINIT 生效、重开仍生效。旧 CAD 快捷键不能悄悄覆盖；CUI 组合键不假装由 PGP 自动管理。

## Phase C — CAD 工具（R03/R04/R05/R06）

- 图层：普通图元与块插入层/本地块定义内部图元分别验证；合并后对象属性、块属性不丢失；Layer 0、当前层及 Xref 依赖层边界正确；On/Off、Freeze、Lock、恢复和真实一次 Undo。
- 提取大样：矩形内外线/圆弧/圆/多段线边界实际 readback；设置比例并指定位置；原图 SHA/对象不变，输出不遗漏被支持对象；复杂块和标注按首版限制跳过+报告。
- 引线重建：连续 LINE/Polyline/Leader → 原生 MLeader；独立中文 TEXT/MTEXT 正确关联；箭头方向、文字位置、属性、同空间、拒绝断链/分支，真实 Undo。
- 比例转换：`standard_1to1` 和 `scaled_drawing` 1:5 ↔ 1:10；实体级 DIMLFAC/文字/填充/块；Leader/MLeader 真实覆盖；动态块跳过；每次真实一次 `U` 后属性完全恢复。

## Phase D — 文件/引用业务（R07/R08/R09）

- 图片内嵌：当前图纸一键/文件夹多 DWG；Model/Paper 各有实际 Raster→OLE；删除/移走图片源文件、关掉输出 DWG、重新打开仍可见（真实屏幕截图）；源 DWG SHA 前后相同。
- DWG Xref：在输出副本进行 Bind，保持原图和源 Xref 不改，报告剩余未处理引用；PDF/DGN/DWF 要正确标为不支持无损内嵌，不得冒充成功。
- MCP：Python stdio `tools/list` 74、普通 AutoCAD 37、Civil 3D 67（没有 Civil 则 NOT_TESTED）；PID/Document binding + 自愈 raw evidence。真实 GUI 与 MCP 路径须有独立可区分的轨迹。
- RVT Exchange：CAD controlled fixture 11/11 MAP；真实 GUI 与 MCP 各导出一次独立对账。只有机器装有 Revit 及 YangTools 3.1.36 并提供 Revit API/ElementId/Loaded DLL 时才可标 Revit native E2E PASS；否则保留 NOT_TESTED。

## Phase E — 安装隔离和汇总（R10）

- 验证目标 2024 的用户范围加载、修复、卸载和输出 DLL 一致，不碰其他 AutoCAD 年份正在运行的进程；旧版本加载与新版本不混合。
- 所有 R00–R10 各自独立输出 PASS/FAIL/BLOCKED/NOT_TESTED。区分**执行未测**与**测试失败**。

## 原始证据目录（推荐）

```text
20261103_YANGCAD_0100_WINDOWS_GEMINI/
  INDEX.md
  00_SOURCE_AND_ENV/
  01_BUILD_DEPLOY_LOADED/
  02_RIBBON_PALETTE_ABOUT/
  03_SHORTCUT_PGP/
  04_LAYERS_DETAIL/
  05_LEADER_SCALE_UNDO/
  06_IMAGE_XREF/
  07_MCP_SELFHEAL/
  08_RVT_EXCHANGE/
  09_INSTALL_ISOLATION/
  10_SCREENSHOTS_VIDEO/
  11_FAILURES/
```

每目录放原始 `*.log`、受控 `*.dwg`、读取属性 JSON 和适用截图。截图黑屏/空白不能算 PASS。额外说明测试过程中的自修复行为、主机 PID/端口变更，不隐瞒。

## 交付口令

交付 GitHub Issue 评论 + Google Drive 证据目录 URL。完成后留言：

`READY_FOR_CHATGPT_REVIEW — git=<完整 SHA> — PASS=<项数> FAIL=<项数> BLOCKED=<项数> NOT_TESTED=<项数> — evidence=<URL>`

**不要自行合并 PR 或标记 FINAL GO。**
