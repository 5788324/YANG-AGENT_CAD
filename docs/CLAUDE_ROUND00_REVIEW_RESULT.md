# Claude Round 0 Static Review Result

日期：2026-08-28
状态：**GO TO WINDOWS ROUND 1**

## 结论

Claude 已审查 Round 0 完整源码与 6 个 policy/static gates。

结论：

> **GO TO WINDOWS ROUND 1**

未发现已确认的静态 P0 blocker；D-023~D-030 均有代码落点；旧 `CadTransactionRunner.Read/Write(Func<Transaction,T>)` 保持不变；65/28/58 contract 未被 Round 0 触碰。

## AddVisual 复核

Claude 唯一无法本地确认的 P0 候选是：

```csharp
_palette.AddVisual("工具箱", _view, true);
```

ChatGPT 随后核对 Autodesk 官方 OARX ManagedRefGuide，官方明确存在：

```csharp
PaletteSet.AddVisual(string name, Visual control, bool bResizeContentToPaletteSize)
```

第三个 bool 用于控制内容是否随 Palette 尺寸调整。

因此该项从“潜在 P0”降为：

> **API 官方确认存在；仍由 Round 1 exact-year clean build 做最终实机编译确认。**

不需要为此改成两参数重载。

## Claude P1 — 记录但 Round 0 不改代码

1. `ToolboxPaletteManager.Shutdown()` 后续可考虑 `Dispose()`；当前只在 Terminate 使用，不阻塞 Round 1。
2. `CadExecutionContext internal` 只能限制到程序集级；当前全仓唯一构造点仍是 Runner。保持 D-025 文档约束，不为此增加复杂嵌套类型。
3. `system.mcp-status` Adapter 当前直接 `MessageBox.Show`；以后新增真实 Adapter 时不要把它当业务层模板照抄。
4. Toolbox Ribbon 暂时复用 ControlCenter 图标；作为 UI polish backlog。
5. `ToolboxRegistry` 当前仅 UI 线程访问；不加锁。若未来改变线程模型，再补同步。

原则：

> Round 0 已静态 GO，本轮不为 P1 再改生产代码，先用冻结源码建立 Windows baseline。

## Round 1 SOP 合并修改

采纳 Claude 两条建议：

- 2024：除了“面板能开”，必须确认内容确实渲染：最近/收藏/CAD/Civil 3D/项目/系统/规划七分类可见，`mcp-status` 卡片可点击执行。
- 2022/2023：Toolbox smoke 从可选提升为最低必做：`YANGCAD_TOOLBOX` 能打开且有内容；不要求 dock/float/三主题深测。

详细清单以：

`tasks/TOOLBOX_001_ROUND_01_WINDOWS_UI_SMOKE.md`

为唯一当前 Round 1 SOP。
