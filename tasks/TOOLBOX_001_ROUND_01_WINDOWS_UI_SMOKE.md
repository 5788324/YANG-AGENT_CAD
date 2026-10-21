# TOOLBOX-001 Round 1 — Windows UI / Host Smoke

状态：**READY TO RUN**  
输入：Round 0 Claude-GO 完整源码基线。

## 目标

只验证 Toolbox Round 0 新 UI/Host 框架在真实 Autodesk 年份里站得住。

不开发新功能，不重跑 TASK-001~007 全套。

---

## 1. Build / Static

依次：

- `verify_source.py`
- `verify_release_source.py`
- `test_extension_policy.py`
- `test_quick_install_policy.py`
- `test_civil_api_compat_policy.py`
- `test_toolbox_foundation_policy.py`
- AutoCAD 2021/2022/2023/2024 clean build
- Civil 2022/2023/2024 clean build
- 2021–2024 external `System.Text.Json.dll` = 0

### AddVisual 特别记录

Round 0 使用：

```csharp
_palette.AddVisual("工具箱", _view, true);
```

Autodesk 官方 ManagedRefGuide 已确认三参数 `AddVisual(string, Visual, bool)` 存在。

本阶段只需通过 exact-year clean build 证明实际目标 assemblies 同样接受；若出现真实编译错误，再回传完整 error/binlog，不要预先改两参数版。

---

## 2. Runtime 2024 — UI P0

### Start / lifecycle

- Start Page：不得自动创建/弹出 Toolbox
- 激活 DWG 后：stdio 65 / AutoCAD live 28
- Civil 3D 2024：live 58
- `YANGCAD_TOOLBOX`：打开 → 关闭 → 再次打开
- dock left / dock right / float 基础 smoke
- Document A → B → close A：不崩、不抛未处理异常

### 内容渲染 — 必做

Toolbox 打开后必须实际看到：

- 最近
- 收藏
- CAD
- Civil 3D
- 项目
- 系统
- 规划

七个分类。

并确认：

- 工具卡片不是空白壳；
- `mcp-status` / MCP 状态工具卡片可点击；
- 点击后有正常可见结果/提示；
- 搜索框输入现有工具名时列表可收窄；
- Planned 工具不伪装成可执行真实工具。

### Theme / Control Center

- 墨曜 / 月瓷 / 雾蓝切换
- Control Center 三页均可进入：
  - MCP
  - 系统设置
  - 关于
- About：版本/开发声明/版权内容可见
- “复制版本信息”可用
- “打开日志目录”不抛异常

### Log

- runtime.log 无新增 unhandled exception
- token leak = 0

---

## 3. Runtime 2021 — AutoCAD baseline

### Contract / read

- AutoCAD health = 28
- `cad.status`
- `cad.document.info`
- `cad.entity.query`

### One write / native Undo

选一个既有、最简单、已验证的 AutoCAD MCP 写 Tool：

1. baseline
2. real
3. 确认一次真实变化
4. real 后不插入额外 YangCAD read RPC
5. AutoCAD 原生 `_.U`
6. 等待 quiescent
7. 最终状态回 baseline

要求：
- one real write = one transaction
- one native Undo 恢复

不重跑 TASK-007 Extension Undo。

### Batch

- `batch.status(2021)=true`
- 一份 `batch.dwg.audit`
- source DWG SHA before = after

### UI minimum

- `YANGCAD_TOOLBOX` 能打开
- 至少能看到分类/工具卡片，不是空 Palette

### Log

- token leak = 0
- 0 unhandled exception

---

## 4. Runtime 2022 — 最低 UI smoke（必做）

Civil runtime compatibility 已由 RELEASE-002 Phase B1 关闭；本轮不重跑 PVI/FeatureLine/Corridor。

要求：

- exact-year AutoCAD/Civil clean build PASS
- 启动 AutoCAD 2022（或 Civil 3D 2022 的 AutoCAD host）
- `YANGCAD_TOOLBOX` 能打开
- Palette 内实际有内容
- 至少看到分类 + 一个 Tool Card
- 关闭后可再次打开
- 0 unhandled exception

不要求：
- dock/float 全测
- 三主题全测
- Civil 对象功能回归

---

## 5. Runtime 2023 — 最低 UI smoke（必做）

与 2022 相同：

- exact-year build PASS
- `YANGCAD_TOOLBOX` 能打开
- Palette 有内容
- 至少看到分类 + 一个 Tool Card
- 关闭后可再次打开
- 0 unhandled exception

不重复 Phase B1 Civil 深测。

---

## 6. Round 1 PASS 判定

只有以下全部满足才可写：

> **TOOLBOX-001 Round 0 = CLOSED / GO / PASS**

必须：

- 6 个 static/policy gates PASS
- AutoCAD 2021–2024 clean build PASS
- Civil 2022–2024 clean build PASS
- 2024 Palette lifecycle/content/theme/Control Center PASS
- 2021 AutoCAD read + one write/native Undo + Batch PASS
- 2021 Toolbox minimum open/content PASS
- 2022 Toolbox minimum open/content PASS
- 2023 Toolbox minimum open/content PASS
- 65 / 28 / 58 contract 不回归
- 0 token leak
- 0 unhandled exception

若失败：

- 只修真实 blocker；
- 不顺手做 Round 2 功能；
- 返回完整 FINAL_SOURCE + Outer SHA（若源码发生修改）。
