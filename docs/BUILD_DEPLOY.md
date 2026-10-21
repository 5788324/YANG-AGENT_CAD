# Build & Deploy

## Framework / Series

| Year | Series | Framework |
|---|---|---|
| 2021 | R24.0 | net48 |
| 2022 | R24.1 | net48 |
| 2023 | R24.2 | net48 |
| 2024 | R24.3 | net48 |
| 2025 | R25.0 | net8.0-windows |
| 2026 | R25.1 | net8.0-windows |
| 2027 | R26.0 | net10.0-windows |

2021–2024 若输出 external `System.Text.Json.dll`，直接视为失败。

## Build

```powershell
python .\tools\verify_source.py
python .\tools\verify_release_source.py
.\tools\Build.ps1 -Year 2024
.\tools\Test-CompatibilityMatrix.ps1 -Years 2021,2022,2023,2024,2025,2026,2027
```

每个年份必须使用真实对应 AutoCAD/Civil API assemblies；禁止跨年份复制 DLL 代替编译。

## 当前快速安装

迭代阶段默认：

`YangCADAgent_Setup.exe`

- User-scope
- `%APPDATA%\Autodesk\ApplicationPlugins\YangCADAgent.bundle`
- target-year isolation
- YangCAD 专用 TRUSTEDPATHS
- APPAUTOLOAD bit 2/8
- SECURELOAD 不修改
- NETLOAD 仅作为 runtime 隔离测试/救急手段

RELEASE-001 已在 Windows 2024、SECURELOAD=1 下证明无 NETLOAD Autoloader 28/58。

## 正式签名

签名工具链保留，但稳定版本后再作为默认流程：
- Authenticode
- TrustedPublisher
- RFC3161 timestamp

## BatchWorker

BatchWorker 不注册 startup ComponentEntry。  
由 `batch.*` 独立启动对应年份 AcCoreConsole 并显式加载。

## Runtime compatibility

RELEASE-002 runtime 测试：
- 一次只启动/绑定一个目标年份测试宿主；
- 精确记录 PID、product、version、document；
- 2021 现在允许参与测试；
- 2025–2027 未安装，保持 NOT_INSTALLED。
