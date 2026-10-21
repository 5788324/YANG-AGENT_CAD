# FEATURE-PROJECT-IMAGE-EMBED-001 — Windows Smoke

状态：**READY / WINDOWS PENDING**

## 目标

只验证 `project.image.embed` 的真实 OLE 内嵌批处理，不重跑历史 Toolbox 全矩阵。

## 1. Clean build

最低：
- AutoCAD 2024 clean build 0 error；
- BatchWorker 2024 0 error；
- `System.Text.Json.dll` external = 0；
- 既有 static/policy 全 PASS。

如新代码仅使用 framework/COM 通用能力，2021–2023 只做 compile；runtime 先以 2024 为 P0。

## 2. Bundle/runtime

正式 bundle 必须存在：

```text
Contents\Mcp\Start-YangCAD-Mcp.ps1
Contents\ImageEmbed\Start-YangCAD-ImageEmbed.ps1
```

同时确认 `YangCAD.AutoCAD.dll` 内嵌 runtime fallback 可用。

## 3. 受控样本

准备至少 6 个 DWG：

1. ModelSpace JPG 正常图片；
2. ModelSpace PNG 正常图片；
3. PaperSpace/Layout 图片；
4. 缺失 source image；
5. clipped image；
6. 无 RasterImage；
7. 可额外增加 2–3 张 DWG 验证连续批处理。

记录所有输入 DWG SHA before。

## 4. Preview

Toolbox → `批量图片内嵌`：

- 输入文件夹；
- Preview；
- 不创建输出 DWG；
- 显示 file_count / output_folder；
- source SHA 不变。

## 5. Real batch

默认：
- `require_all_images=true`
- `show_worker=false`

必须验证：
- 独立 AutoCAD COM process；
- `status.json` 按文件持续更新；
- 正常 JPG/PNG 输出 PASS；
- PaperSpace PASS，并证明 OLE 落在目标 Layout/PaperSpace，而不是浮动 viewport 的 ModelSpace；
- missing source = FAIL；
- clipped = FAIL；
- no image = SKIPPED，不生成重复 DWG；
- 单文件失败不阻止后续其它 DWG；
- 输入 SHA before == after。

## 6. OLE 真内嵌验证

对 PASS 输出：

1. 关闭 worker；
2. 临时移动/重命名原 JPG/PNG，使原路径不可访问；
3. 重新打开输出 DWG；
4. 图片仍正常可见；
5. 实体为 `AcDbOle2Frame`；
6. 原对应 `AcDbRasterImage` 已不存在；
7. insertion / width / height / rotation / layer 与源 Raster 基本一致。

这一步是 P0，不能只看 ObjectName。

## 7. No overwrite

再次对同一 output folder 执行：

- 已存在输出 = SKIPPED / output_exists；
- 不能覆盖现有 DWG；
- 可选 race smoke：worker 处理中人为创建同名 output，最终必须 FAIL/保留该外部 output，不能被 `-Force` 覆盖。

## 8. Logging/safety

- 0 unhandled exception；
- input SHA 全部不变；
- FAIL DWG 无 working 半成品残留；
- status PASS/PARTIAL/FAIL/SKIPPED 与实际一致；
- 不宣称 block nested / clipped 等 V1 未支持对象已转换。

## 9. 返回

回传：
- 完整 FINAL_SOURCE ZIP + SHA；
- build；
- 各样本 status；
- 一个 PASS 输出在移走源图片后的重开证据；
- input SHA before/after；
- production diff；
- runtime.log/error。
