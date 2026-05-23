import json
import tempfile
import unittest
from pathlib import Path

from yang_cad_agent.toolbox import list_plugins, validate_plugin_manifest


class ToolboxTests(unittest.TestCase):
    def test_validate_plugin_manifest(self):
        with tempfile.TemporaryDirectory() as tmp:
            root = Path(tmp)
            plugin_dir = root / "toolbox" / "plugins" / "demo"
            plugin_dir.mkdir(parents=True)
            (plugin_dir / "main.lsp").write_text("(princ)", encoding="utf-8")
            manifest = {
                "id": "demo.plugin",
                "name": "Demo Plugin",
                "version": "0.1.0",
                "category": "demo",
                "tracks": ["B", "C"],
                "risk": "read",
                "entry": {"type": "lisp", "path": "main.lsp"},
            }
            manifest_path = plugin_dir / "plugin.json"
            manifest_path.write_text(json.dumps(manifest), encoding="utf-8")

            result = validate_plugin_manifest(manifest_path)
            listing = list_plugins(root)

            self.assertTrue(result["ok"])
            self.assertTrue(listing["ok"])
            self.assertEqual(listing["plugins"][0]["id"], "demo.plugin")


if __name__ == "__main__":
    unittest.main()

