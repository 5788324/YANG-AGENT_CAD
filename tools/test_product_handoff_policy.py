from pathlib import Path
import json, re, xml.etree.ElementTree as ET

root=Path(__file__).resolve().parents[1]
checks=[]
def txt(rel): return (root/rel).read_text(encoding="utf-8-sig")
def check(title, ok): checks.append((title,bool(ok)))

check("version product 0.10.0", "<Version>0.10.0</Version>" in txt("Directory.Build.props"))
check("version info explicitly dev", "<InformationalVersion>0.10.0-dev</InformationalVersion>" in txt("Directory.Build.props"))
for name,token in [
 ("mcp/pyproject.toml",'version = "0.10.0"'),
 ("mcp/yangcad_mcp/__init__.py",'__version__ = "0.10.0"'),
 ("bundle/PackageContents.template.xml",'AppVersion="0.10.0"'),
 ("tools/Package-Release.ps1","0.10.0"),
 ("tools/verify_release_source.py","0.10.0")]:
 check("aligned "+name,token in txt(name))
for rel in ["README.md","PROJECT.md","AGENTS.md","CLAUDE.md","docs/CURRENT_STATE.md",
 "docs/HANDOFF.md","docs/FEATURE_CATALOG.md","docs/PRODUCT_STRUCTURE.md","docs/PRODUCT_UI.md",
 "docs/VERSIONING.md","docs/GIT_WORKFLOW.md","references/README.md",
 "references/yqarch/README.md","references/jianren-toolbox/README.md",
 "references/manifest.json","references/FEATURE_COMPARISON.md","tasks/CURRENT_TASK.md"]:
 check("present "+rel,(root/rel).is_file())
ref=json.loads(txt("references/manifest.json"))
check("both references and no binaries",len(ref.get("entries",[]))==2 and all(x.get("binary_in_repository") is False for x in ref["entries"]))
check("historical vs current status explicit","STATIC" in txt("docs/CURRENT_STATE.md") and "WINDOWS" in txt("docs/CURRENT_STATE.md"))
xaml=txt("src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml")
code=txt("src/YangCAD.AutoCAD/UI/ControlCenterWindow.xaml.cs")
ET.fromstring(xaml)
check("about copy actual host and dll", all(x in code for x in ['assembly.Location','CadHostInfo.Product','UserSettings.SettingsPath']))
check("about link action",'OpenProjectClick' in xaml and 'https://github.com/5788324/YANG-AGENT_CAD' in code)
check("about displays preview not release",'开发预览版' in xaml)
tool=txt("src/YangCAD.AutoCAD/UI/ToolboxView.xaml")
back=txt("src/YangCAD.AutoCAD/UI/ToolboxView.xaml.cs")
check("quick tool buttons",all(f'Tag="{cmd}"' in tool for cmd in ['YGLAYER','YGDETAIL','YGSCALE','YGLEADERFIX','YGKEYS']))
check("quick command handler present",'QuickNativeCommandClick' in back)
ribbon=txt("src/YangCAD.AutoCAD/UI/RibbonBootstrapper.cs")
check("ribbon separated and native focus preserved",all(x in ribbon for x in ['常用工具','连接与设置','previousTab','ribbon.ActiveTab']))
for suffix in ['*.vlx','*.fas','*.arx','*.crx','*.dvb','*.lsp','*.dll','*.exe']:
 check("no third party compiled source "+suffix,not list((root/'references').rglob(suffix)))
failed=[a for a,b in checks if not b]
for a,b in checks: print(('PASS' if b else 'FAIL')+': '+a)
print(f'product handoff policy: {len(checks)-len(failed)}/{len(checks)} PASS')
raise SystemExit(1 if failed else 0)
