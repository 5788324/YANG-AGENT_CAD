# TOOLBOX-001 Round 2 — Windows Interaction Smoke

状态：**READY FOR WINDOWS**
开发轨：`0.9.0-dev`

## 0. 原则

本轮只验 Round 2 新交互框架。

不要：
- 重跑 TASK-001~007 全套；
- 重跑 Phase B1 PVI 深测；
- 顺手开发 CAD/Civil 新功能；
- 因 UI 小问题大重构 Toolbox；
- 改 65/28/58 contract。

若发现 blocker：
1. 保存 build/runtime evidence；
2. 做最小源码修复；
3. 重跑本 SOP 受影响项；
4. 回传完整 FINAL_SOURCE + Outer SHA。

---

# 1. 输入核验

记录：
- candidate ZIP 名；
- Outer SHA；
- internal `SHA256SUMS.txt` matched/mismatch；
- exact source root。

先跑：

```text
verify_source.py
verify_release_source.py
test_extension_policy.py
test_quick_install_policy.py
test_civil_api_compat_policy.py
test_toolbox_foundation_policy.py
test_toolbox_round2_interaction_policy.py
```

预期 Round 2 新 policy：`16/16 PASS`。

---

# 2. Clean Build

必须：

| Year | AutoCAD | Civil | Batch |
|---|---|---|---|
| 2021 | PASS | N/A | PASS |
| 2022 | PASS | PASS | PASS |
| 2023 | PASS | PASS | PASS |
| 2024 | PASS | PASS | PASS |

并确认 2021–2024：
- 0 compile error；
- external `System.Text.Json.dll = 0`。

Round 2 新增 WPF/XAML，所以 2021–2024 exact-year compile 是硬门禁。

---

# 3. 测试设置隔离

Round 2 会修改 Favorites / Recent。

测试前备份：

```text
%LOCALAPPDATA%\YangCADAgent\settings.json
```

记录至少：
- Theme
- AllowWrites
- AllowErase
- AllowCommandExecution
- AllowExtensionInvoke
- ToolboxFavorites
- ToolboxRecent

测试结束后可恢复原文件。

要求 Round 2 Favorites/Recent 操作**不得意外改变其它 allow/theme 字段**。

---

# 4. Runtime 2024 — FULL P0

优先 Civil 3D 2024 做 full smoke，以便同时看到 `/health=58`。

## 4.1 基础回归

- Start Page 不主动创建/弹出 Toolbox；
- DWG 激活后 stdio=65；
- AutoCAD live=28；
- Civil live=58；
- `YANGCAD_TOOLBOX` open/reopen；
- 三主题；
- Control Center MCP / 系统设置 / 关于。

## 4.2 Round 2 Tool Detail

系统分类应至少看到：
- MCP 状态
- 当前文档（Experimental）
- 参数面板自检（Experimental）

点击 Tool Card：
- 列表切到详情页；
- 返回按钮正常；
- name/category/description/status 正确；
- Planned Tool 可查看但 Execute disabled。

## 4.3 8 ParameterShape

打开 `参数面板自检`，必须实际看到：
- Text
- Number
- Bool
- Choice
- Layer
- ObjectSelection
- Point
- File

专项：
- Layer 下拉至少能看到当前 DWG 的 `0` 图层；
- File 的“浏览…”能打开文件选择器；
- Point 接受 `1,2` 和 `1,2,3`；
- ObjectSelection 接受如 `ABCD, EF01` 并按 Handle 列表回显。

注意：Round 2 通用 ObjectSelection **不要求屏幕点选**；Handle-first 是当前设计。

## 4.4 Validation

至少验证：

### Required
把“文本”清空 → Execute：
- visible validation error；
- Tool 不执行；
- Recent 不新增。

### Number range
数值填 `101` → Execute：
- visible `不能大于 100` 类错误；
- Tool 不执行；
- Palette 不 crash。

### Valid
恢复合法值并执行：
- success result 在 Palette 内显示；
- result 包含参数回显；
- runtime.log 无 ERROR。

## 4.5 Favorites

对 `参数面板自检`：
1. 收藏；
2. 返回 → 收藏分类中可见；
3. 取消收藏 → 收藏分类移除；
4. 再收藏，为 persistence test 准备。

要求：
- 只存 Tool ID；
- settings.json 中其它 allow/theme 字段保持原值。

## 4.6 Recent

合法执行：
- `参数面板自检` 进入 Recent；
- 重复执行不重复出现；
- newest first。

非法 validation / Planned / failed execution：
- 不进入 Recent。

10 项上限由 static policy 固定，本轮无需为了凑 10 个 Tool 造假工具。

## 4.7 Persistence — 必做真实进程重启

仅 close/reopen Palette 不足以证明磁盘持久化。

必须：
1. 收藏并成功执行 `参数面板自检`；
2. 正常关闭 AutoCAD/Civil 3D；
3. 重新启动同年份宿主；
4. 打开 DWG → Toolbox；
5. Favorites 仍包含 Tool；
6. Recent 仍包含 Tool。

PASS 才能判 persistence runtime PASS。

## 4.8 Runtime unavailable

1. 有 DWG 时打开“当前文档” → Execute enabled，返回当前 DWG 名；
2. 保持 Palette 打开，关闭所有 DWG 回到 Start Page；
3. 切一次“系统”分类/重新选择卡片触发刷新；
4. “当前文档”应显示 `当前不可用`；
5. Execute disabled；
6. 不 crash。

若 AutoCAD 在无文档时限制某个 UI 操作，记录实际 host 行为，不要伪造 PASS；只做最小适配。

## 4.9 SearchKeywords

搜索至少：
- `round2` → 参数面板自检；
- `document` → 当前文档；
- `gateway` → MCP 状态。

大小写不敏感。

---

# 5. 2021 / 2022 / 2023 Minimum Runtime

Round 2 新增 WPF UserControl / ComboBox / FileDialog / dynamic controls，因此做最低 UI smoke。

### 2021 AutoCAD
- Toolbox open；
- 系统分类；
- 参数面板自检详情可打开；
- 8 参数控件实际渲染；
- 合法 Execute PASS；
- close/reopen；
- 0 unhandled exception。

### 2022
AutoCAD 或 Civil 3D 任一 exact-year host：执行同样 minimum smoke。

### 2023
AutoCAD 或 Civil 3D 任一 exact-year host：执行同样 minimum smoke。

不重复 Round 1 dock/float/multi-doc 全套。

---

# 6. Logs / Contract

最终：
- runtime.log `[ERROR] = 0`，或每个 ERROR 有明确解释且本轮修复；
- token quick scan = 0 leak；
- stdio tools = 65；
- plain AutoCAD health = 28；
- Civil health = 58；
- source/mcp schema tool count 不变。

---

# 7. 判定

## GO / PASS

必须：
- static/policy 全 PASS；
- exact-year build PASS；
- 2024 full interaction P0 PASS；
- real process restart persistence PASS；
- 2021–2023 minimum UI PASS；
- 65/28/58 无回归；
- no unhandled exception；
- token 0 leak。

## 回传

必须回传：
- 完整 FINAL_SOURCE ZIP；
- Outer SHA；
- internal SHA result；
- build matrix；
- 2024 interaction assertions；
- 2021–2023 minimum assertions；
- settings before/after relevant fields；
- production diff；
- runtime errors/token summary。
