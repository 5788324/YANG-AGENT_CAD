---
name: Windows 实机测试报告
about: Gemini/Codex 执行原始证据交付；不能只粘 PASS 汇总
labels: []
---

## 必填身份
- 测试任务：
- Git 分支 / 完整 SHA（`git_sha`）：
- 测试开始时间、Windows/CAD/Civil/Revit 版本：
- acad.exe / Revit.exe PID 与 CAD active DWG：

## 原始证据
- 编译命令/日志/binlog：
- Build、Deploy、Loaded DLL 的各自 SHA 和路径：
- GUI 操作录屏/日志（如果有 GUI 测试）：
- MCP 原始请求/响应（如有）：
- 实测 DWG 与对象属性前后 JSON：
- 原生一次 Ctrl+Z 记录：
- 图片重开、Xref 绑定、布局空间证据：
- 证据目录 URL：

## 逐项状态
- [ ] PASS（仅引用实际证据）
- [ ] FAIL（有复现材料）
- [ ] BLOCKED（环境/样本缺失）
- [ ] NOT_TESTED（明确没有测）

不得仅用自写 summary JSON 冒充实体 readback、真实 GUI 或 Revit API 证据。
