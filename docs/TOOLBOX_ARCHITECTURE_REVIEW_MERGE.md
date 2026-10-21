# YangCAD Toolbox — ChatGPT + Claude 合并结论

日期：2026-08-28
状态：**架构方向已收敛，可落正式决策**

## 用户最高优先级

YangCAD 是个人工作插件箱。

优先：
1. AI 快速开发；
2. 快速迭代；
3. 快速维护；
4. 功能尽快上线干活；
5. 架构简单、好理解、好定位问题。

安全只保留防崩溃、误改图纸、破坏输入文件所必需的部分。

## 双方一致

- WPF + PaletteSet 做高频 Toolbox；ControlCenterWindow 保留。
- Descriptor / RuntimeAdapter / Service 三层。
- GUI/MCP 共享 Service，不互相调用。
- 参数系统第一版保持 8 种基础类型。
- ParameterShape 与强类型 Request 分离。
- 搜索第一版 `SearchKeywords + Contains`。
- GUI/MCP metadata 第一阶段不大统一。
- TASK-007 动态代码安全不扩散到固定 Toolbox。
- Toolbox 三档安全。
- dry_run 按需，不是所有 GUI 写工具义务。
- 不引入 Mediator/CQRS/EventBus/DI 等平台化复杂度。
- 先跑 RELEASE-002 最小 Phase B，再改 Toolbox 生产代码。

## 代码层最终纠偏

### CadExecutionContext
采用，但：
- 只含 Document / Database / Editor / Transaction；
- 只能 CadTransactionRunner 构造；
- Service/Command 不允许直接 new。

### 不使用同名 Runner overload
现有：

```csharp
Read<T>(Document, Func<Transaction,T>)
Write<T>(Document, bool, Func<Transaction,T>)
```

保持冻结。

新入口使用明确命名：

```text
ReadContext
WriteContext
```

原因：如果新增同名 `Func<CadExecutionContext,T>` overload，现有大量 lambda 可能出现 overload ambiguity，违背“65 个 Tool 不动”的目标。

## 正式决策

写入：
- D-023
- D-024
- D-025
- D-026
- D-027
- D-028
- D-029
- D-030

不继续为了完整性新增更多决策号。

## 下一顺序

1. RELEASE-002 Phase B MINIMAL runtime 2021–2024
2. 冻结 0.8.x runtime baseline
3. 0.9.0 TOOLBOX-001
4. 0.9.x 按需填功能
5. 稳定后再补完整兼容/正式签名
