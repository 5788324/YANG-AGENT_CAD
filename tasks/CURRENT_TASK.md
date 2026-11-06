# 当前任务 — YangCAD 双线 / 管道工具 V1 + 对称改宽
日期 2026-11-06
主线：main@2f0980d4acd08b087d924b5c1f5a5bb18e8668a8 (AutoCAD 2024 RC1)
当前独立源码分支：feature/dual-line-width-v1-20261106

## 需求锁定
1. YGDL 统一源泉 XD 连续双线与贱人工具箱的管道中心线概念，不修改两者插件。
2. 默认双线宽 1000 mm，后续自动使用上次成功值（重启仍记忆）。
3. Double 仅双侧，Pipe 加中心线；中心/左/右对齐，斜接、倒角、圆弧。
4. YGDLW 保持中线原位对称改宽（600→1000 左右各200）；自动识别新绘制的左右侧或中心线 XData；旧 LINE 允许人工选两个平行 LINE 配对。
5. 单事务写入、可编辑原生 CAD Polyline、无法安全识别时拒绝。

## G0–G5
- [ ] G1 GitHub static policy + complete SHA256SUMS manifest
- [ ] G2 Windows AutoCAD 2024 .NET48 clean Build (Gemini)
- [ ] G3 NETLOAD Loaded DLL 路径、PID 和编译 SHA 对账
- [ ] G4 真实对象读回、单位、标高、转角、宽度600→1000→600、保存重开
- [ ] 原生 _.U 用户操作撤销读回，不能用 Transaction.Abort 冒充
- [ ] G5 ChatGPT 独立审核后再合并

Windows 测试：tasks/DUAL_LINE_WINDOWS_SMOKE_20261106.md
详细技术说明：docs/DUAL_LINE_V1_20261106.md
注意：PR #13/#14/#15 仍独立 Draft，不由此任务合并或变更。

