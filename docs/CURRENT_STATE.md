# YangCAD 当前状态（2026-11-20）

[PR #20](https://github.com/5788324/YANG-AGENT_CAD/pull/20) 汇总当前整合源码。DLL、MCP 与安装包使用 0.10.0-dev 对应的版本体系。验收以 PR HEAD 完整 SHA 为准。

| 领域 | 状态 |
|---|---|
| 源码、入口和测试契约 | STATIC，以 PR 最新 CI 为准 |
| AutoCAD 2024 构建、加载、Core Console | Gemini 已测试整合基线，修复提交待 WINDOWS 复验 |
| GUI、Civil 3D GUI、Revit 和安装实装 | Gemini 的测试会话无交互桌面，待验 |
| 发布 | 尚未发布 |

Gemini 实测基线为 `4b5e2466910a8e7414fe654088f4111c0390114b`。AutoCAD、真实 Civil 3D 2024、BatchWorker 和安装器均构建成功，AutoCAD 实测加载的 DLL SHA-256 为 `C814E5B51CE150C72012BF7316F5B7671063DDCBF8FAEFF8F1BAEDB44CC0BEAC`。基线验证覆盖命令注册、图层、大样、折断、边界外裁切、双线、DWG 交换、批处理与封包等。板符号在诊断脚本修正退出应答后使用该 DLL 通过 22/22 项。仓库现已修正板符号脚本、YGHSPLIT 选项和 YGINC 连字符编号，复验使用 PR 最新 HEAD 重新构建。

整合范围见 [功能目录](FEATURE_CATALOG.md)。折断、多边形大样、边界外裁切纳入本候选，多边形使用 YGDETAILPOLY，YGDETAIL 和原 MCP 矩形入口继续可用。

[当前验收任务](../tasks/CURRENT_TASK.md) · [RC1 历史记录](RC_AUTOCAD2024_0.10.0.md) · [历史实机矩阵](RUNTIME_TEST_MATRIX.md) · [项目日志](../WORKLOG.md)
