# RELEASE-002 Phase B1 — Civil Runtime Result

状态：**CLOSED / PASS**  
日期：2026-08-28

## 实机范围
Civil 3D 2022 / 2023 / 2024。

## 结果
| Year | /health | PVI | Helper path | FeatureLine | Corridor | Batch | Runtime exceptions |
|---|---:|---|---|---|---|---|---:|
| 2022 | 58 | PASS / 100 PVIs | legacy `Station` fallback | SAMPLE_MISSING | SAMPLE_MISSING | available | 0 |
| 2023 | 58 | PASS / 100 PVIs | legacy `Station` fallback | SAMPLE_MISSING | SAMPLE_MISSING | available | 0 |
| 2024 | 58 | PASS / 100 PVIs | native `RawStation` | SAMPLE_MISSING | SAMPLE_MISSING | available | 0 |

共同：
- `product="Civil 3D"`
- `extension.*` live 泄漏 = 0
- `civil.status.available=true`
- PVI station/elevation finite，station 单调递增且范围合理
- `cad.status` x3 PASS
- token leak = 0

## 限制
FeatureLine / Corridor 三年份样本均为 `count=0`，因此只能记录 `SAMPLE_MISSING`，不能声称对象级 get/diagnose 已有样本证明。该限制**不阻塞 Phase B1 的 PVI compatibility 目标关闭**，但后续遇到真实 FeatureLine/Corridor 项目时可顺手补证据。
