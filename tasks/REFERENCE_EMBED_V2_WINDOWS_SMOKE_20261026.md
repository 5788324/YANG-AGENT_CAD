# YangCAD 引用内嵌 V2 + YGSCALE 定点 Windows Smoke

执行器：Windows/AutoCAD 2024 实机，禁止自行 FINAL GO。

0. 下载唯一当前源码、核对 outer ZIP SHA 与 SHA256SUMS；记录 build command / exit code、deploy DLL SHA、loaded DLL SHA、AutoCAD PID。禁止改测试规则和在插件已加载时覆盖 DLL。
1. ToolBox 当前图纸一键：先保存 DWG（含 JPG 相对路径、PNG 绝对路径、PaperSpace 图片），点一次快捷按钮；不用再选输入文件。截图按钮/界面和 `status.json`；证明 GUI 只说“已启动”，而不是 Worker PASS。原 DWG 的 SHA 不变。
2. ToolBox 文件夹批量：两个可处理 DWG + 一个无图片、一个含丢失图片。递归子目录；每个文件必须出自己结果，一个 FAIL 不能停止其余文件。输出路径保留相对结构且不覆盖已有 DWG。
3. 源图片路径：原 DWG 与输出 `_Embedded` 在不同目录，图片路径是相对路径。验证图片找得到并最终 OLE 在正确位置。
4. Xref Bind：原 DWG 用相对路径参照一个外部 DWG，在副本中绑定。检查 `bound_xrefs`，输出打开后无对应 Xref 依赖。另测 Xref 缺失/未加载情况，不应谎称绑定成功。逐项检查嵌套 Xref、嵌套图片，剩余依赖应标 PARTIAL。
5. PDF/DGN/DWF Underlay：只报告不破坏，若只存在不可处理引用不得写全部成功。保留输出文件或未输出的状态需要真实记录。
6. OLE 真实性：处理完成→关闭输出 DWG → 移走源图片 → 在新 AutoCAD 2024 会话重新打开结果，ModelSpace/PaperSpace 都能看到 OLE，记录 `AcDbOle2Frame` 和无原 `AcDbRasterImage`、图层、宽高、旋转、坐标；实截图不能纯黑。没有这个证据只算 UNPROVEN。
7. COM 安全：测试时已有用户 AutoCAD 进程仍打开。如果 Worker 复用原 AutoCAD PID，必须拒绝处理、禁止 Quit 用户进程；验证启动前/后 PID。必要时用独立进程但不可关闭其他 AutoCAD。
8. YGSCALE：1:5→1:10 和反向、Standard1To1，补 LEADER/MULTILEADER、动态块应跳过和单次真实 Ctrl+Z readback。尺寸数值、字体、箭头、PatternScale、LinetypeScale 对账。
9. 上传完整 raw build log、runtime.log、worker.log、status.json、before/after SHA、输入/输出 DWG、真实截图、对象属性 readback。未测标 NOT_TESTED，不允许制造统计表代替原始证据。

任一源码修复需上传完整新源码 + diff + SHA，暂停原候选实机验收；统一由 ChatGPT 审查。
