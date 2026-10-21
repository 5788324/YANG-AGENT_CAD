# YangCAD Image Embed v1

状态：**STATIC HARDENED / WINDOWS PENDING**  
Tool：`project.image.embed` / **批量图片内嵌**

## 1. 业务目标

把 DWG 中顶层外链 `RasterImage` 转成 DWG 内嵌 OLE，使外发图纸不再依赖原 JPG/PNG 路径。

重点是**大量 DWG 批处理安全性**，不是单张图片编辑体验。

## 2. 架构

```text
Toolbox
  ↓
ImageEmbedBatchService
  ↓
job.json
  ↓
独立 PowerShell / COM Worker
  ↓
独立 AutoCAD.Application.<year>
  ↓
工作副本 DWG
  ↓
Clipboard + PASTECLIP → AcDbOle2Frame
  ↓
校正尺寸/旋转/图层
  ↓
确认新 OLE 后删除对应 RasterImage
  ↓
输出 DWG
```

Live YangCAD `acad.exe` 不负责循环打开几百张图。

## 3. 输入保护

- 输入 DWG 永远不原位保存；
- Worker 先复制到 `.__yangcad_work_<guid>.dwg`；
- 转换成功且 source SHA 仍与 before 一致，才发布 working file 为最终输出；
- 最终发布不使用强制覆盖；已存在或并发出现的输出文件都 fail/skip，不覆盖；
- 每个输入记录 SHA-256 before / after，并在 output 发布前后复核；
- SHA 变化视为 FAIL，若本轮刚发布 output 则删除本轮拥有的 output；
- 失败工作副本删除，不留下“看似成功”的半成品。

## 4. V1 支持范围

处理：
- ModelSpace 顶层 RasterImage；
- PaperSpace / Layout 顶层 RasterImage；
- JPG / PNG / BMP 等可由 `System.Drawing.Image` 读取的常规位图。

转换前显式切换目标空间：ModelSpace 使用 model；PaperSpace/Layout 使用 paper space 且退出浮动 viewport (`MSpace=false`)。

转换后尝试保持：
- insertion/origin；
- drawing-unit width / height；
- rotation；
- layer。

## 5. V1 明确拒绝/降级

为避免视觉失真，V1 不假装完整支持：

- `ClippingEnabled=true`；
- brightness != 50；
- contrast != 50；
- fade != 0；
- raster transparency=true；
- 普通 BlockDefinition 内嵌套 RasterImage。

默认 `require_all_images=true`：

> 任一顶层图片转换失败，或发现 block 内嵌套图片，则整个 DWG = FAIL，不输出半成品。

关闭该选项时可以产生 `PARTIAL`，但绝不标 PASS。

Xref 单独统计；V1 不修改 Xref 文件。

## 6. 无图片 DWG

如果 DWG 没有顶层/嵌套 RasterImage：

- 状态 = `SKIPPED / no_top_level_raster_images`；
- 不生成一份无意义的重复输出。

## 7. 批处理 Job

Job 位于：

```text
%LOCALAPPDATA%\YangCADAgent\jobs\image-embed\<job-id>\
```

包含：
- `job.json`
- `status.json`
- `worker.log`

`status.json` 每处理完一个 DWG 通过同目录临时文件 + `File.Replace` 更新，避免 delete→move 的可见空窗。

V1 单批上限：5000 DWG。

## 8. Clipboard 边界

AutoCAD 官方 `PASTECLIP` 会将非 AutoCAD Clipboard 内容作为 embedded/linked OLE 处理。

因此 V1 必须使用 Windows Clipboard。

批处理运行期间：
- 不建议用户同时使用系统复制/粘贴；
- Worker 每张图片粘贴后清空 Clipboard；
- 后续若实际使用反馈需要更强隔离，再单独评估其他 OLE 创建路径。

## 9. PASS 标准

Windows 2024 必须证明：

```text
source RasterImage exists
→ Preview: 0 DWG write
→ Real batch
→ output OLE exists
→ source RasterImage removed only in output
→ source DWG SHA unchanged
→ reopen output without source image path still shows embedded image
```

并至少验证：
- ModelSpace JPG/PNG；
- PaperSpace 图片；
- missing source path FAIL；
- clipped image FAIL；
- output exists no overwrite；
- multi-DWG batch continues after one file FAIL；
- 0 unhandled exception。
