# Gemini / Codex — YangCAD 快捷键面板 + CAD 默认 Ribbon 实机验收

目标：AutoCAD 2024 + Civil 3D 2024；原始真实证据，不接受手写 PASS 汇总代替。

## 基线/加载

1. 检查当前候选 ZIP SHA 和内部  SHA256SUMS。
2. 原生 2024 build，记录 binlog、DLL SHA、部署 SHA、Loaded SHA、AutoCAD PID。
3. 若修编译错误，上传完整新候选 ZIP+SHA，先交 ChatGPT 审查；不可把旧静态包标为 Runtime GO。

## A. Ribbon 原生默认选项卡

1. 打开无 YangCAD 的 AutoCAD 2024，记录当前 Ribbon 标签是常用/Home。
2. 加载 YangCAD；开始未点击 YangCAD 标签时观察，当前 Ribbon 应仍是常用/Home。
3. 用户手动点击 YangCAD，重载/打开新 DWG 后不能强行切回原生或强行切回 YangCAD。
4. Civil 3D 用当前自己的选项卡选择（不能强行切换为 AutoCAD Home 造成 Civil 工作区失焦）。
5. 保存截图与运行时日志；若受用户 Workspace last-selected 状态影响，记录并单独说明。

## B. 快捷键面板

1. `YANGCAD` → 快捷键 → 打开，或者 `YGKEYS` 直接打开。
2. 记录 `HostApplicationServices.FindFile` 最终选中的真实 `acad.pgp` 路径及只读状态。
3. 设置 `YLX → YGLAYER`，保存前后 PGP SHA、备份路径和 YangCAD 标记区块。
4. 执行 REINIT，选择 PGP 文件，输入 `YLX`，证明真实调用图层工具。
5. 修改为 `YLT`，旧 `YLX` 不再触发该功能，新 `YLT` 生效。
6. 用系统原生 `C`（通常 CIRCLE）测试冲突检测；未勾覆盖时必须拒绝，勾覆盖后才允许替换（测试结束必须恢复）。
7. 取消绑定后 REINIT，确认系统默认 C 等非 YangCAD 别名完全未变。
8. 模拟 GBK/UTF8/UTF16 PGP（可用独立测试副本验证读写），保证原文件含中文注释时不会乱码。
9. `Ctrl+...` 不应被 PGP 输入框接受；按钮打开 CAD 原生 CUI，未更改原来的 Ctrl+S/Ctrl+C。
10. 模拟 PGP 文件只读、路径不存在、标记区块损坏，均要有可读错误而非覆盖。

## C. 回归与隔离

- YangCAD Ribbon 功能均存在：MCP、工具箱、比例转换、图层工具、提取大样、控制中心。
- `YGKEYS` 不改变 MCP stdio/live tool 数量，不修改 DWG 图元。
- AutoCAD 2021 如同时在运行不得影响其设置或进程。
- Existing user profile / other plugin alias remains intact unless explicitly overriding that alias.

## Evidence

上传真实 DWG/屏幕截图、PGP 前后文件（如有个人路径先脱敏）、差异、AutoCAD 命令行记录、DLL SHA、完整 build log；结论只能 `PASS EVIDENCE` / `FAIL EVIDENCE` / `NOT_TESTED`，交 ChatGPT 最终复核。
