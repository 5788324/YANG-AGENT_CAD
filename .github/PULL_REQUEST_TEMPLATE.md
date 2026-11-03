## 本轮目标与基线

- 关联 Issue：
- 父 PR / 依赖：
- 输入 Git Commit SHA：
- 输出 Git Commit SHA：

## 变更范围

- 修改的功能和文件：
- 用户操作步骤是否变多：
- 未触及的模块：

## 验证层级（不可混写）

- [ ] 源码/静态检查（附 CI URL + Commit SHA）
- [ ] Windows clean build（如有 Autodesk API 增量）
- [ ] Build == Deploy == Loaded SHA（如有运行时增量）
- [ ] AutoCAD/Civil 3D 原生对象 readback（如有业务增量）
- [ ] GUI 真入口与 MCP 独立调用（如要求两者一致）
- [ ] 一次真实 CAD Undo（如有 DWG 写入）
- [ ] 剩余项已标记 NOT_TESTED/BLOCKED，而非 PASS

## 证据与状态

- 证据目录：
- 测试环境/宿主 PID：
- 当前结论：`STATIC_ONLY` / `WINDOWS_BUILD_PASS` / `RUNTIME_PARTIAL` / `READY_FOR_AUDIT` / `FAIL`
- 不支持/已知限制：

> 本 PR 不应因 GitHub 绿色静态检查就自动视为 Windows 2024 实机 PASS 或可以发布。
