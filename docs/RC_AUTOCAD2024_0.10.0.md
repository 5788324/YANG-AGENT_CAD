# YangCAD 0.10.0 RC1 — AutoCAD 2024 Quick 验收范围

更新：2026-11-05 · 状态：**G5_AUTOCAD2024_SCOPE_PASS / RC INTEGRATION REVIEW / ALL_HOSTS_NOT_APPROVED**。

## 1. 版本身份与范围（不可混用）

- RC 管理标签：**`0.10.0-rc.1 / AutoCAD 2024 Quick`**；不是经重新编译产生的 `0.10.0-rc.1` DLL。
- 产品二进制仍标识 `0.10.0` / `0.10.0-dev`；当前 RC 集成分支只动文档/元数据，不改变已验收运行文件。
- Windows 现场：Windows 10 x64 / AutoCAD 2024。`main` 的旧回退版本与这个 RC **不同**，勿按旧版加载成功推定 RC 已安装。
- 发行渠道：**Quick, unsigned, user-scope**；安装器不应该更改 `SECURELOAD`。未知的 Windows/AutoCAD 年份、Civil 3D live、Revit Native E2E **未本轮验收**。

## 2. 可信基线与哈希

| 对象 | 唯一标识 | 结论 |
|---|---|---|
| R00–R10 AutoCAD 2024 原生功能受测源码 | `b6ff1370b0ed48e5e50fec43d2a5a2e14ca5ec54` | G5 指定范围 PASS |
| Release 工具契约修复 | `d5200c57a75b1fd228713dc67b606126a5c04924` | GitHub CI + Windows PS5.1 正反向验收 |
| 已核验 0.10.0 Quick Release ZIP | `ACA88A9440968411A2D7A014800AC70FE8E60C314ECBE936BBBC4037C5C79692` | 419,586 字节 / 19 文件 SHA PASS |
| Release 内 AutoCAD DLL | `880329CF7E5C464078686FE56B0BB7070B4C2F7B7F482A91771D9C8` | 558,080 字节；与 G5 原生受测 DLL 一致 |
| stdio / AutoCAD / Civil Tool 合同 | `74 / 37 / 67` | stdio 74 与 AutoCAD 37 有证据；Civil 67 **未实机** |

发行 ZIP：[Drive 文件](https://drive.google.com/file/d/1fsPFpsQE3BiW_6_eTLCjTmeGNKnLuKyg/view)；G5 独立审核：[Issue #9 评论](https://github.com/5788324/YANG-AGENT_CAD/issues/9#issuecomment-6646760886)；原始 R00–R10：[Drive 根目录](https://drive.google.com/drive/folders/1AJI15SxQCXlPUXVXOMLg3-jN-wuOkMm0)；发布正反向：[Drive 12 目录](https://drive.google.com/drive/folders/1bUkqFnOBmG6j5vz5z0DogLN30xZcAYUs)。

## 3. 通过与边界

- R00–R10 AutoCAD 2024 指定测试阶段经独立审查；真实 GUI 与 MCP 对同一 11 实体 MAP 一致；图层、矩形大样、引线、比例、PGP、OLE/Bind、安装隔离均有原始材料。
- R06：Undo 是**受检实体的属性读回相等**，不是整 DWG 字节级恢复；标准 1:1 受检 INSERT 安全跳过（7/8），缩尺模式 8/8。
- R07：批量内嵌 **PARTIAL: 2 PASS / 1 SKIP / 1 FAIL**，不能宣布任意文件夹都成功；缺失/不安全 Xref 有 fail-closed 限制。
- R09：**CAD→Revit MAP 11/11 + ReaderVerifier 11/11**；**Revit 原生加载/模型对象及 ElementIds = NOT_TESTED**。
- Civil 3D live 67 Tools = **NOT_TESTED**，即使系统存在 Object Enabler DLL 也不代表安装并验证独立宿主。
- 未开发：任意闭合多段线边界、复杂对象选择裁切、删除边界外、Z 型折断线；不能包装成 0.10.0 已实现功能。

## 4. RC 集成与安装纪律

1. `release/autocad2024-0.10.0-rc1` 只作为集成审查入口。此分支从 `d5200c57...` 建立，包含上游 PR #4–#10 所有增量；仅文档、工作流和校验清单变化。**不在本轮更换运行 DLL**。
2. 单独开 Draft 集成 PR → `main`；先验证完整 diff、SHA/CI 与无二进制/用户 DWG。用户确认后再考虑合并；不要把旧堆叠 PR 按不正确的 base 逐个 squash。
3. 对个人电脑试用请**只取上面的已验收 ZIP**，先关闭目标 AutoCAD，备份原安装并记录原配置；安装/升级与实际加载路径按 `docs/BUILD_DEPLOY.md` 检查。切勿从 RC 分支自行推算出另一个“已验收二进制”。
4. 回滚：保留 `main` 旧提交和旧包；恢复旧版安装/配置，必须确认 AutoCAD 进程不再加载 RC DLL；版本与 Loaded SHA 不匹配即停止。
5. 将来修改 C# / Python MCP / 工人脚本或真正将编译版本号改为 `0.10.0-rc.1`，必须产生新的 Build/Deploy/Loaded SHA 并重审受影响范围，不能借用本次 G5。

## 5. 发布决策

**批准项：** AutoCAD 2024 Quick 的指定个人使用场景和集成分支进入 RC 审查。**未批准项：** 全宿主正式 Release / Civil 3D / Revit Native / 2021–2023、2025–2027 等全部兼容性声明。
