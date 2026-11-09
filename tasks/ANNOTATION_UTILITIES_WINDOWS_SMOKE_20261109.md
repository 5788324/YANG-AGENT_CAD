# Gemini Windows AutoCAD 2024 实机验证：YGDIMTXT / YGSLOPE / YGTBOX
目标分支 feature/annotation-utilities-v1-20261109，HEAD 必须重新获取完整 40 位 SHA。
**所有 runtime G2/G3/G4 均 WINDOWS_NOT_TESTED**。先 clean build，再以真实 AutoCAD 2024 编译、NETLOAD、执行；勿继承 PR #16 的 DLL 或日志。

## 统一证据
- Windows Build 命令及退出码、binlog、DLL SHA-256、实际 NETLOAD 的 Assembly.Location/SHA/进程 PID；确认三条命令注册且 Toolbox 三按钮可执行。
- before.dwg、after.dwg、undo.dwg + before/after/undo_readback.json、操作脚本/Driver 源码/原生 CAD 命令日志、当前空间/图层/字体/INSUNITS。
- 真实输入一次 _.U 而不是 Transaction.Abort 或 dry-run 替代；按场景 PASS/FAIL/BLOCKED/NOT_TESTED，失败时最小修复并重编译。

## YGDIMTXT
1. 同一 DWG 有默认尺寸自动显示空 DimensionText、单独 <>、覆盖数字如 600、前后缀 <>±0、隐藏 "."、单空格、文字位置仅被挪动、文字颜色/高度样式变化等。
2. 验证只选真正覆盖内容的 Dimension；移动了位置但未覆盖文字内容的，应排除。精确 readback 每个 Handle、DimensionText、测量值，选中结果的 ObjectId/Handle。
3. All 与 Selection 范围、Model/Paper 独立、块定义与外参不进入、空结果及 20 万防护。
4. 证明原图 0 写入；CAD 选中高亮在命令结束后仍可用，原生 ESC 清除选择。

## YGSLOPE
5. Section 取 (0,0,0)→(10000,500,0)：5% / 1:20 / atan(0.05)=2.8624°。Plan3D 取 (0,0,100)→(10000,0,600)：同样 5%。
6. 倒序点、正负坡、零坡、过陡、run=0、三维 XY 投影重合，箭头指向低端。文字方向应正常（避免倒置）并保留源线。
7. 实测指示线偏移、文字 250mm 和多字体（中文、SHX、TrueType）、三种格式显示、单位从米/毫米转换、圆整精度与用户上次参数。
8. 当前 UCS 旋转/倾斜、布局空间、锁层/冻结/关闭层；原生 MTEXT 与 3 LINE 数量、保存重开。

## YGTBOX
9. 水平/30°/90°/180° 的 DBText，左中右对齐、不同文字高度/字宽，源文字位置及 Handle 原样。
10. MText 多行、宽编辑框内短文字、包含中文/特殊格式、实际 GetBoundingPoints/ActualWidth 的测量，防止生成巨大的空白外框。
11. Rectangle 的四边偏移给定距离（如 50mm），圆形外接圆覆盖全部可见文字并多出径向距离，当前层生成。
12. 一次批量选多个 TEXT/MTEXT，产生对应可编辑原生 Polyline/Circle，整个操作单次 Undo。遇非 XY OCS 或异常文字无部分提交。
13. 保存/重开/再次修改原文字后，旧框不会自动跟随（如实说明，不报假通过）。

## 防止旧功能干扰
- Gemini 正在进行 PR #16 的 YGDL 测试，该任务另分支、另 DLL、另 CAD 进程或彻底卸载后再切换，不混用同名命令。
- 不合并到 main；PR 维持 Draft。将 Build/真实运行证据上传 Drive，回来给 ChatGPT 做 G5。
最终报告：READY_FOR_CHATGPT_REVIEW — git=<full SHA> — BUILD=... — LOADED=... — UNDO=... — evidence=<URL>。
