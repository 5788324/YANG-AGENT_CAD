# 插件箱规范

插件箱用于沉淀用户和 AI 共同验证过的 CAD 能力，类似源泉工具箱，但要能被 AI/MCP 调用。

## 插件目录

```text
toolbox/
  plugins/
    <plugin_id>/
      plugin.json
      main.lsp
      README.md
      tests/
```

## 插件来源

| 类型 | 说明 | 是否默认启用 |
| --- | --- | --- |
| builtin | 项目内置稳定插件 | 是 |
| ai_temp | AI 临时生成脚本 | 否 |
| user_saved | 用户确认收藏插件 | 是 |
| experimental | 实验插件 | 否 |

## plugin.json

```json
{
  "id": "titleblock.update_attrs",
  "name": "批量修改图框属性",
  "version": "0.1.0",
  "category": "图框",
  "description": "修改指定块的属性值",
  "author": "AI",
  "source": "user_saved",
  "tracks": ["B", "C"],
  "accore_safe": true,
  "risk": "batch_modify",
  "requires_backup": true,
  "supports_dry_run": true,
  "params_schema": {
    "block_name": {"type": "string"},
    "attrs": {"type": "object"}
  },
  "entry": {
    "type": "lisp",
    "path": "main.lsp",
    "command": "yang_titleblock_update_attrs"
  }
}
```

## 入箱流程

1. AI 生成临时 LISP。
2. 静态校验通过。
3. 在测试图或当前任务中运行成功。
4. 用户明确说“保存到插件箱”或“以后还要用”。
5. AI 生成 manifest、README、参数说明。
6. 插件默认进入 `user_saved`。

## AI 调用流程

1. 根据用户需求搜索插件 manifest。
2. 如果参数齐全，优先调用插件。
3. 如果参数不齐，先查询图纸或向用户确认。
4. 如果插件不存在，再生成临时脚本。

