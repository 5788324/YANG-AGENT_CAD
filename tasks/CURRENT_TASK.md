# Gemini 实机任务 — YangCAD 整合候选

## 取代码与构建

从 [PR #20](https://github.com/5788324/YANG-AGENT_CAD/pull/20) 取 feature/ribbon-layout-20261119，记录 PR HEAD、git rev-parse HEAD 和 git status --short。在独立测试目录和 AutoCAD 2024 会话使用受控图纸。保留生产插件、配置和工程文件。

```powershell
$acad = 'C:/Program Files/Autodesk/AutoCAD 2024' # 换为实际路径
./tools/Build.ps1 -Year 2024 -AcadRoot $acad -SkipCivil
./tools/Build-Setup.ps1
Get-FileHash artifacts/bin/2024/AutoCAD/YangCAD.AutoCAD.dll
```

记录 AutoCAD 完整版本、PID、Build 日志和实际 NETLOAD 的完整路径与哈希。全程同一份 DLL。只有真实 Civil 3D 2024 时，在开跑 AutoCAD 测试前构建 Civil 版本，使用 -AcadRoot 和 -CivilRoot 并去掉 -SkipCivil。记录 Revit、Civil 或安装验收缺少的实际依赖。

## 实测失败项复验

以下三项优先使用本次构建的同一份 DLL 执行。原报告基线为 `4b5e2466910a8e7414fe654088f4111c0390114b`。

| 项目 | 用例与预期 |
|---|---|
| 板符号 | 运行下方仓库脚本，两阶段共 22/22 通过。第一阶段保留 QSAVE 时的图层配置和测试图元，之后的删线与符号变更在退出时放弃。 |
| YGHSPLIT | 用原失败样本 A3b_Hatch_Fixture.dwg，选中带洞 ANSI31 后出现首点提示。用两点切分，再用 E 选现有直线或多段线切分，分别核对结果区域、洞口、图案属性和一次原生撤销。另测 Esc 取消与多孤岛拆分。 |
| YGINC | 步长 +1，验证 B-01 → B-02、B-009 → B-010、B-99 → B-100，并复测独立负数 -01 → 00。检查源文字不变，原生撤销移除副本。 |

原报告中的 Ribbon、工具箱、REINIT、YGSCALE、SLBP 预览、YGEMBED 与 Revit 重建、Live MCP、Civil 3D GUI 和安装实装需要可交互的 Windows 桌面。

## 一次跑完的验收范围

| 模块 | 用例与预期 |
|---|---|
| 构建、加载、GUI | 启动 CAD 页签保持，YangCAD 六组和双列工具箱，主题、停靠、显示比例、帮助和输出目录；运行 YANGCAD、YANGCAD_TOOLBOX、YGLAYER、YGDETAIL、BMK 等跨命令类入口，核对均能进入。 |
| 图层、快捷键 | 测锁定／冻结／开关／合并及 Undo，PGP 创建和冲突提示，REINIT 后快捷命令可用。对工作图纸和原配置分别使用副本。 |
| 大样与折断 | YGDETAIL 旧矩形交互；YGDETAILPOLY 的 Select / Draw、凹形、LINE／ARC／CIRCLE、弧段边界拒绝；YGBREAKLINE 保留端点和属性，2D／3D 与单次 Undo。详见 [多边形](DETAIL_POLYGON_WINDOWS_SMOKE_20261105.md) 与 [折断线](GEOMETRY_EDIT_V2_WINDOWS_SMOKE_20261105.md)。 |
| 边界外裁切 | YGOUTSIDE 分别取消和确认；核对跨界 LINE／ARC、完全外部与锁层／外参保护、原边界、一次 Undo。使用 [边界外案例](OUTSIDE_WINDOWS_SMOKE_20261105.md)。 |
| 比例、双线 | YGSCALE 1:1 和缩尺前后尺寸，锁层与 Undo；YGDL 直／弧段，YGDLW 保存中心线并改宽。 |
| 文字与标注 | YGTL、YGINC、YGLEADERFIX、YGDIMTXT、YGSLOPE、YGTBOX；检查原文字、标注数值、引线生成、撤销。 |
| 填充 | YGHCLIP、YGHCLIPSPLIT／YGHSPLIT、YGHUNION；用洞环、原纹理位置、整块覆盖、确认失败路径和 Undo。 |
| 梁柱板 | BMK／CMK 检查受控尺寸；板符号跑下方 22 项 Core Console，并手测选点、预览、混选、嵌套板。 |
| 内嵌、DWG 交换 | 未保存图纸提示；引用内嵌输出副本、原 DWG 哈希、移除原引用后重开；切到其他面板再返回可查进度；YGREVIT 的 MAP/JSON 与原图保持。Revit 原生重建单独记录。 |
| MCP、Batch、Civil | 验证 GUI 与 MCP 启动、选中图纸会话、读操作与受控写权限；Batch audit/hygiene 确认输入哈希和输出副本。Civil 测试仅在真实 Civil 3D 2024 上执行。 |
| 封包与安装 | 用本次构建产物执行 Package-Release.ps1 -Years 2024 -SkipBuild，再对产物目录执行 Verify-Release.ps1；记录每个宿主产物、MCP 合同和哈希。安装仅在测试环境验证。 |

一个用例失败仍收完其他模块。输出 INDEX.md，每项标记 PASS / FAIL / BLOCKED / NOT_TESTED，附原始日志、图元前后、必要截图与 Undo 证据。回传现象及最小复现步骤，保持当前测试提交便于集中修复。

## 矩形板方向箭头

打开 [SLB_SUPPORT_DIRECTIONS.dxf](../tools/fixtures/SLB_SUPPORT_DIRECTIONS.dxf)，切到世界坐标系和俯视图，图形单位为毫米。需要保存时另存为测试 DWG。青线是 SLAB_OUTLINE，红线是 BEAM。在 SLBLAYER 中分别用 01 的下边梁线与青色轮廓设置图层，保存 DWG 后，重开可直接使用。用 SLBP 与 SLB 点相同板内位置，核对下表。

| 编号 | 箭头预期 |
|---|---|
| 01 | 1 个，向下。板内另有普通块供框选测试。 |
| 02 | 2 个，向下与向右。 |
| 03 | 2 个，向左与向右。 |
| 04 | 3 个，向右、向上、向左。 |
| 05 | 4 个，四侧各一个。 |
| 06 | 0 个，预览只显示轮廓，布置不插入块。 |
| 07 | 2 个，对着旋转板的两条红色相对边。轮廓为顺时针。 |
| 08 | 1 个，向下。两段梁线重叠，总覆盖正好半边。 |
| 09 | 0 个。下边有横穿线和相距 10 mm 的平行线，上边梁线高 100 mm，左边重叠短线合计不足半边。 |

1. 在 01 放好方向符号，删除唯一的红色梁线，再框选符号和普通块执行 SLBFIX。方向符号应移除，普通块保留。原生撤销一次应恢复符号，红色梁线仍保持删除状态。再用 SLBP 预览，确认只显示轮廓。
2. 02 删除一侧梁线并执行 SLBFIX，只去掉对应方向的箭头。取消图层配置后再修正，方向符号应保留。
3. 另存测试 DWG，关闭并重开，直接用 SLB 点击 02，确认仍为下、右两向。再重命名梁线图层和轮廓图层，确认仍可识别。另开一张从未配置过的图纸，应沿用原闭合板格识别。
4. 梁线的原判定为距轮廓边不超过 1 个图纸单位且覆盖至少半边。未指定板轮廓时，复测旧闭合梁线识别和单向／双向板符号。

SLBFIX 有四个选项：Y 处理全部、传统模式保留原类型；N 处理全部并按长短跨重判传统类型；One 只选原单向；Two 只选原双向。One/Two 按更新前的已知块定义筛选，方向块用 Y 或 N 修正。有已配置的板轮廓时，更新结果按实际支承侧出箭头。

## Core Console 自动检查

在 Windows AutoCAD 2024 打开上面的 DXF，原样另存成测试 DWG。在仓库目录执行下面一条命令。安装路径按本机调整。用前面构建的 DLL 连跑两阶段实测。只测板符号时可用 --build 独立编译。

```powershell
python tools/run_slab_core_console_smoke.py --core-console "$acad/accoreconsole.exe" --dll "artifacts/bin/2024/AutoCAD/YangCAD.AutoCAD.dll" --dwg "D:/CAD-Test/SLB_SUPPORT_DIRECTIONS.dwg"
```

脚本检查九个板格、输入图元、符号总数、零支承移除、一次撤销、减一侧箭头和普通块保留。第一阶段在布置前重命名梁线图层并保存测试副本，第二阶段用独立 Core Console 进程重开并直接布置。在重开阶段还会创建 02 内板，先布置内板，再重布外板并批量修正，核对两块可同时保留、方向正确且 Handle 不变。同阶段还会混选原单向、原双向块分别运行 One 和 Two，核对筛选外块的属性没有变化。退出码 0 需要两阶段共 22 项全部通过。运行目录在 artifacts/slab-smoke-*，包含编译日志、候选 DLL、输入文件哈希、测试图副本、实际 SCR 和原始输出。若使用自定义板跨设置，须允许测试图的 4000–6000 mm 板跨。

同一批验收再用该目录下的 DLL 完成顶部的 GUI 清单，集中记录 Ribbon、命令入口、填充、内嵌及板符号结果，失败项收齐后统一修正。

旧版 RC1 和 PR #16/#17 的记录保留为历史证据。
