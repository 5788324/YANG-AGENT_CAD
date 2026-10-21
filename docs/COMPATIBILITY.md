# Compatibility

更新时间：**2026-08-28**

## Target / Evidence Matrix

| Year | Series | Framework | AutoCAD Build | Batch Build | Civil Build | Runtime evidence |
|---|---|---|---|---|---|---|
| 2021 | R24.0 | net48 | PASS | PASS | NOT_INSTALLED | AutoCAD/Batch Round 1 PASS |
| 2022 | R24.1 | net48 | PASS | PASS | PASS | Civil Phase B1 PASS; Toolbox UI Round 1 PASS |
| 2023 | R24.2 | net48 | PASS | PASS | PASS | Civil Phase B1 PASS; Toolbox UI Round 1 PASS |
| 2024 | R24.3 | net48 | PASS | PASS | PASS | historical core PASS + Civil Phase B1 PASS; Toolbox UI Round 1 PASS |
| 2025 | R25.0 | net8.0-windows | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED |
| 2026 | R25.1 | net8.0-windows | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED |
| 2027 | R26.0 | net10.0-windows | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED | NOT_INSTALLED |

## Build 已证明

### 2021–2024
- AutoCAD clean build PASS。
- BatchWorker clean build PASS。
- Civil 3D 2022–2024 clean build PASS。
- 2021–2024 external System.Text.Json.dll = 0。

## RELEASE-002 Phase B1 Civil Runtime — CLOSED / PASS

### Civil 3D 2022
- `/health=58`
- `civil.status available=true`
- PVI runtime PASS（100 samples）
- helper = legacy `Station`
- `cad.status` regression PASS
- `batch.status available=true`
- runtime exception = 0

### Civil 3D 2023
- `/health=58`
- `civil.status available=true`
- PVI runtime PASS（100 samples）
- helper = legacy `Station`
- `cad.status` regression PASS
- `batch.status available=true`
- runtime exception = 0

### Civil 3D 2024
- `/health=58`
- `civil.status available=true`
- PVI runtime PASS（100 samples）
- helper = native `RawStation`
- `cad.status` regression PASS
- `batch.status available=true`
- runtime exception = 0

### Limitation
三年份：
- FeatureLine = `SAMPLE_MISSING`
- Corridor = `SAMPLE_MISSING`

因此不能声称 FeatureLine/Corridor 对象级 get/diagnose 已有样本证明。  
该 limitation 不阻塞 Phase B1；对应 Toolbox 功能真正进入开发时补对象级 smoke。

## 2024 historical runtime

TASK-001~007 / RELEASE-001 已证明：
- stdio 65
- AutoCAD live 28
- Civil live 58
- Quick Install
- Batch
- Extension security / true Undo

Phase B1 又完成了 compat helper 的 2024 regression，因此不再标 “post-compat regression pending”。

## 当前兼容验证：TOOLBOX-001 Round 1

当前不是重测 Phase B1，而是验证新 Toolbox UI/Host：

### 2021
- AutoCAD read
- one write/native Undo
- Batch
- Toolbox minimum content

### 2022 / 2023
- exact-year build
- Toolbox 能打开、有内容、能重开

### 2024
- full Toolbox lifecycle/content/theme/Control Center
- 65 / 28 / 58 contract 不回归

## 证据规则

- Build PASS ≠ Runtime PASS。
- Civil Runtime PASS ≠ Toolbox UI Runtime PASS。
- `SAMPLE_MISSING` ≠ 对象级 PASS。
- NOT_INSTALLED ≠ PASS。
- 2025–2027 只有真实安装与实机证据后才能升级。
- 不跨年份复制 DLL。


## TOOLBOX-001 Round 1 Runtime — CLOSED / PASS

- 2021 AutoCAD Toolbox + read/write/native Undo/Batch PASS
- 2022 AutoCAD/Civil Toolbox minimum UI PASS
- 2023 AutoCAD/Civil Toolbox minimum UI PASS
- 2024 AutoCAD/Civil full Toolbox UI P0 PASS
- AddVisual 3-param exact-year build 2021–2024 PASS
- external System.Text.Json.dll = 0
- token leak = 0
- unhandled exception = 0
