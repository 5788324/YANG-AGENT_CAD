# YangCAD — 正式开工前最终体检

日期：2026-08-28

## 结论

> **GO / 正式进入 TOOLBOX-001 Round 1**

## 代码检查

- 6 个 static/policy gates 全 PASS
- `verify_source`: cs=50 xaml=6 python=11 themes=3 tools=65 xml=12 errors=0
- MCP contract 仍 65 / 28 / 58
- AutoCAD project 已引用 `AdWindows.dll`
- SDK-style csproj 会自动纳入 Round 0 新 `.cs/.xaml`
- `Read/Write(Func<Transaction,T>)` 旧接口未改
- `ReadContext/WriteContext` 为明确新名字
- CadExecutionContext 仍四字段
- Planned 工具不会伪装可执行
- Start Page 不主动调用 Toolbox Show

## 官方 API 复核

Autodesk ManagedRefGuide：
`PaletteSet.AddVisual(string, Visual, bool)` 正式存在。

因此 Claude Round 0 唯一待验证 P0 已排除为静态 blocker。

Round 1 仍需 exact-year build 作为目标程序集最终证据。

## 文档遗漏已修

1. D-012：branch/PR → main-only
2. AGENT_RULES：GUI Toolbox write 与 MCP typed write 分开
3. TOOL_DEVELOPMENT_GUIDE：增加 GUI Tool 最小开发流程
4. RELEASE_CHECKLIST：Phase B1 CLOSED，Current=Round 1
5. TEST_PLAN：改成 Round 1 实际范围
6. 旧历史文档规则：改成不恢复旧 YANG-AGENT_CAD 历史

## 非阻塞 Backlog

- PaletteSet Dispose
- Toolbox 专用图标
- RuntimeAdapter 未来不要照抄 MessageBox 展示
- CadExecutionContext internal 只提供 assembly-level 限制
- Registry UI-thread-only 假设注释

不为这些小项打乱 Windows baseline。

## 下一动作

直接执行：
`tasks/TOOLBOX_001_ROUND_01_WINDOWS_UI_SMOKE.md`
