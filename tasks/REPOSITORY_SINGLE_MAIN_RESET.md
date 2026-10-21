# GitHub 单主线彻底重置

仓库：
`https://github.com/5788324/YANG-AGENT_CAD`

用户明确：
- 不要旧历史；
- 不要 archive；
- 不要 backup；
- 新 YangCAD 是唯一主线。

## 结果目标

远端只保留：

```text
main
```

main 的第一个新历史节点就是当前 YangCAD Round 0。

## Windows / Codex 执行

先准备一个空目录并解压：
`YangCAD_FULL_REPO_MAINLINE_ROUND00_20260828.zip`

然后：

```powershell
cd <解压后的仓库根目录>

git init
git checkout -b main
git add -A
git commit -m "Initialize YangCAD Toolbox mainline"
git remote add origin https://github.com/5788324/YANG-AGENT_CAD.git

git push --force origin main
git push origin --delete archive/pre-yangcad-toolbox-20260828
```

如果还有其他旧 branch：

```powershell
git ls-remote --heads origin
```

逐个删除，只保留 main：

```powershell
git push origin --delete <old-branch>
```

## 注意

这是用户明确批准的破坏性历史重置。

不需要：
- merge 旧 main；
- cherry-pick 旧 commit；
- 保留 archive；
- 旧 project-state；
- 旧 handoff；
- 旧 worklog。

GitHub 旧对象可能在后台 GC 前短暂通过旧 SHA 可访问，但只要不再有 refs 指向它们，它们就不属于当前项目历史。

## Reset 后门禁

```powershell
python tools/verify_source.py
python tools/verify_release_source.py
python tools/test_extension_policy.py
python tools/test_quick_install_policy.py
python tools/test_civil_api_compat_policy.py
python tools/test_toolbox_foundation_policy.py
git status --short
git branch -a
```

预期：
- verify_source errors=0
- policy 全 PASS
- git status clean
- remote 只保留 main
