# Git Workflow

Repository：

`https://github.com/5788324/YANG-AGENT_CAD`

## 唯一主线

**`main` 是唯一开发线、唯一事实源。**

用户明确：
- 旧 YANG-AGENT_CAD 历史没有保留价值；
- 不需要 archive/backup branch；
- 不需要旧项目 worklog/handoff；
- 后续所有开发只在当前 YangCAD 主线继续。

## 一次性主线重置

TOOLBOX-001 Round 0 开始时执行：

1. 用当前完整仓库包作为新根；
2. 建 orphan commit；
3. force push 到 `main`；
4. 删除所有 archive/backup 旧分支；
5. 从这一提交开始建立新的历史。

旧 Git 对象可能在 GitHub 后台 GC 前短期仍可通过旧 SHA 缓存访问，但它们不再属于任何项目 ref，也不再作为项目资产。

## 日常

个人项目不强制 PR。

推荐：
- 一轮任务 = 一个或少数清晰 commit；
- AI 开发完 → static → Windows smoke → commit；
- 重要稳定节点打 tag；
- 不逐文件碎提交；
- 不 force-push 正常新主线历史，除非用户明确要求再次重置。

## 禁止提交

- Autodesk DLL
- bin/obj
- *.dll / *.pdb / *.exe
- *.binlog
- __pycache__ / *.pyc
- release ZIP
- token / secret
- runtime logs
- 临时测试 DWG
