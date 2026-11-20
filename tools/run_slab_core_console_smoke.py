#!/usr/bin/env python3
"""Run the SLB fixture against a supplied AutoCAD 2024 build on Windows."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path
import re
import shutil
import subprocess
import tempfile


ROOT = Path(__file__).resolve().parents[1]
CASES = (
    ("01", 5000, 23000, 8, 0),
    ("02", 15000, 23000, 9, 0),
    ("03", 25000, 23000, 5, 0),
    ("04", 5000, 14000, 7, 0),
    ("05", 15000, 14000, 15, 0),
    ("06", 25000, 14000, 0, 0),
    ("07", 5000, 5000, 10, 0.5),
    ("08", 15000, 5000, 8, 0),
    ("09", 25000, 5000, 0, 0),
)
CHECKS = {
    "fixture", "symbol-count", "zero-support", "undo", "trim", "marker",
    *(f"case-{number}" for number, *_ in CASES),
}
REOPEN_CHECKS = {"reopen-input", "reopen-case", "nested-inner", "nested-outer", "nested-handles", "type-one", "type-two"}
LISP = """
(defun ys-check (name passed)
  (prompt (strcat "\\nYANGCAD_SLAB_CHECK " (if passed "PASS " "FAIL ") name "\\n"))
  (princ))

(defun ys-set (type layer)
  (ssget "_X" (list (cons 0 type) (cons 8 layer))))

(defun ys-count (items)
  (if items (sslength items) 0))

(defun ys-at (point / items i item found)
  (setq items (ys-set "INSERT" "SLB_SYMBOL") i 0)
  (repeat (ys-count items)
    (setq item (ssname items i) i (1+ i))
    (if (equal point (cdr (assoc 10 (entget item))) 0.00001)
      (setq found item)))
  found)

(defun ys-shafts (name / item data p q lines tips)
  (setq item (tblobjname "BLOCK" name) lines 0)
  (while (and item (setq item (entnext item)) (setq data (entget item))
              (/= "ENDBLK" (cdr (assoc 0 data))))
    (if (= "LINE" (cdr (assoc 0 data)))
      (progn
        (setq p (cdr (assoc 10 data)) q (cdr (assoc 11 data)) lines (1+ lines))
        (if (equal p '(0.0 0.0 0.0) 0.00001)
          (setq tips (cons q tips))
          (if (equal q '(0.0 0.0 0.0) 0.00001)
            (setq tips (cons p tips)))))))
  (list lines tips))

(defun ys-old-symbol (name point)
  (entmake (list '(0 . "BLOCK") (cons 2 name) '(70 . 0) '(10 0.0 0.0 0.0)))
  (entmake '((0 . "LINE") (10 0.0 0.0 0.0) (11 1.0 0.0 0.0)))
  (entmake '((0 . "ENDBLK")))
  (entmake (list '(0 . "INSERT") (cons 2 name) (cons 10 point)))
  (entlast))

(defun ys-symbol (point name rotation tips / item data geometry)
  (setq item (ys-at point))
  (if name
    (and item (setq data (entget item)) (= name (cdr (assoc 2 data)))
         (equal rotation (cond ((cdr (assoc 50 data))) (0.0)) 0.00001)
         (setq geometry (ys-shafts name))
         (= (car geometry) (* 3 (length tips)))
         (= (length (cadr geometry)) (length tips))
         (not (vl-some '(lambda (p) (not (member p (cadr geometry)))) tips)))
    (null item)))
""".strip()
TIPS = ("(0.5 0.0 0.0)", "(0.0 0.5 0.0)", "(-0.5 0.0 0.0)", "(0.0 -0.5 0.0)")


def symbol_check(name: str, x: int, y: int, mask: int, rotation: float) -> str:
    block = f'"SLB_DIR_{mask:02}"' if mask else 'nil'
    tips = ' '.join(tip for bit, tip in enumerate(TIPS) if mask & (1 << bit))
    return f'(ys-check "{name}" (ys-symbol \'({x} {y} 0) {block} {rotation} \'({tips})))'


def setup_lines(dll: Path) -> list[str]:
    return [
        f'(setvar "TRUSTEDPATHS" (strcat (getvar "TRUSTEDPATHS") ";{dll.parent.as_posix()}"))',
        f'(command "_.NETLOAD" "{dll.as_posix()}")',
        '(setvar "OSMODE" 0)',
        '(setvar "SNAPMODE" 0)',
        '(command "_.UCS" "_World")',
        *(' '.join(line.strip() for line in definition.splitlines()) for definition in LISP.split("\n\n")),
    ]


def build_script(dll: Path) -> str:
    lines = [
        *setup_lines(dll),
        '(setq ys-outlines (ys-set "LWPOLYLINE" "SLAB_OUTLINE"))',
        '(setq ys-markers (ys-set "INSERT" "TEST_MARKER"))',
        """(setq ys-south (ssname (ssget "_X" '((0 . "LINE") (8 . "BEAM") (10 2000.0 21000.0 0.0) (11 8000.0 21000.0 0.0))) 0))""",
        """(setq ys-east (ssname (ssget "_X" '((0 . "LINE") (8 . "BEAM") (10 18000.0 21000.0 0.0) (11 18000.0 25000.0 0.0))) 0))""",
        '(setq ys-marker (ssname ys-markers 0) ys-marker-before (entget ys-marker))',
        '(ys-check "fixture" (and (= 9 (ys-count ys-outlines)) (= 1 (ys-count ys-markers)) ys-south ys-east (= 0 (ys-count (ys-set "INSERT" "SLB_SYMBOL")))))',
        '(command "SLBLAYER" ys-south "" (ssname ys-outlines 0))',
        '(command "_.-LAYER" "_Rename" "BEAM" "BEAM_SMOKE" "")',
        '(command "_.QSAVE")',
        '(command "SLB" ' + ' '.join(f"'({x} {y} 0)" for _, x, y, _, _ in CASES) + ' "")',
        '(ys-check "symbol-count" (= 7 (ys-count (ys-set "INSERT" "SLB_SYMBOL"))))',
        *(symbol_check(f"case-{number}", x, y, mask, rotation) for number, x, y, mask, rotation in CASES),
        """(setq ys-first (ys-at '(5000 23000 0)))""",
        '(command "_.ERASE" ys-south "")',
        '(command "SLBFIX" ys-first ys-marker "" "Y")',
        '(ys-check "zero-support" (and ys-first (null (entget ys-first)) (null (entget ys-south))))',
        '(command "_.U")',
        '(ys-check "undo" (and ys-first (entget ys-first) (null (entget ys-south))))',
        """(setq ys-second (ys-at '(15000 23000 0)))""",
        '(command "_.ERASE" ys-east "")',
        '(command "SLBFIX" ys-second "" "Y")',
        symbol_check("trim", 15000, 23000, 8, 0),
        '(ys-check "marker" (and ys-marker-before (equal ys-marker-before (entget ys-marker))))',
        '(prompt "\\nYANGCAD_SLAB_SMOKE_DONE\\n")',
        '_.QUIT',
        '_No',
        '',
    ]
    return '\r\n'.join(lines) + '\r\n'


def build_reopen_script(dll: Path) -> str:
    lines = [
        *setup_lines(dll),
        '(ys-check "reopen-input" (and (= 21 (ys-count (ys-set "LINE" "BEAM_SMOKE"))) (= 0 (ys-count (ys-set "INSERT" "SLB_SYMBOL")))))',
        """(command "SLB" '(15000 23000 0) "")""",
        symbol_check("reopen-case", 15000, 23000, 9, 0),
        """(setq ys-outer (ys-at '(15000 23000 0)))""",
        """(entmake '((0 . "LWPOLYLINE") (100 . "AcDbEntity") (8 . "SLAB_OUTLINE") (100 . "AcDbPolyline") (90 . 4) (70 . 1) (10 13100.0 21800.0) (10 14500.0 21800.0) (10 14500.0 23400.0) (10 13100.0 23400.0)))""",
        """(entmake '((0 . "LINE") (8 . "BEAM_SMOKE") (10 13100.0 23400.0 0.0) (11 14500.0 23400.0 0.0)))""",
        """(command "SLB" '(13800 22600 0) "")""",
        """(setq ys-inner (ys-at '(13800 22600 0)))""",
        """(command "SLB" '(15000 23000 0) "")""",
        '(command "SLBFIX" ys-inner ys-outer "" "Y")',
        symbol_check("nested-inner", 13800, 22600, 2, 0),
        symbol_check("nested-outer", 15000, 23000, 9, 0),
        """(ys-check "nested-handles" (and ys-inner ys-outer (equal ys-inner (ys-at '(13800 22600 0))) (equal ys-outer (ys-at '(15000 23000 0))) (= 2 (ys-count (ys-set "INSERT" "SLB_SYMBOL")))))""",
        """(setq ys-one (ys-old-symbol "YB5_ARM_X" '(25000.0 23000.0 0.0)) ys-two (ys-old-symbol "YB5_ARM_B" '(5000.0 14000.0 0.0)))""",
        '(setq ys-two-before (entget ys-two))',
        '(command "SLBFIX" ys-one ys-two "" "One")',
        '(ys-check "type-one" (and (= "SLB_DIR_05" (cdr (assoc 2 (entget ys-one)))) (equal ys-two-before (entget ys-two))))',
        '(setq ys-one-after (entget ys-one))',
        '(command "SLBFIX" ys-one ys-two "" "Two")',
        '(ys-check "type-two" (and (= "SLB_DIR_07" (cdr (assoc 2 (entget ys-two)))) (equal ys-one-after (entget ys-one))))',
        '(prompt "\\nYANGCAD_SLAB_SMOKE_DONE\\n")',
        '_.QUIT',
        '_No',
        '',
    ]
    return '\r\n'.join(lines) + '\r\n'


def check_results(raw: bytes, exit_code: int, expected: set[str] = CHECKS) -> list[str]:
    # Accept plain-text or UTF-16 console output. Markers themselves are ASCII.
    lines = [line.strip() for line in raw.replace(b'\0', b'').splitlines()]
    matches = [match.groups() for line in lines if
               (match := re.fullmatch(rb'YANGCAD_SLAB_CHECK (PASS|FAIL) ([a-z0-9-]+)', line))]
    checks = [name.decode("ascii") for _, name in matches]
    failures = [name.decode("ascii") for status, name in matches if status == b"FAIL"]
    missing = sorted(expected - set(checks))
    if missing:
        failures.append("missing: " + ", ".join(missing))
    if len(checks) != len(set(checks)) or set(checks) - expected:
        failures.append("duplicate or unexpected check marker")
    if lines.count(b'YANGCAD_SLAB_SMOKE_DONE') != 1:
        failures.append("missing or duplicate completion marker")
    if exit_code:
        failures.append(f"Core Console exit code: {exit_code}")
    return failures


def build_plugin(core: Path, run_dir: Path) -> Path | None:
    dotnet = shutil.which("dotnet")
    if dotnet is None:
        print("FAIL: 找不到 dotnet SDK，无法编译 AutoCAD 2024 插件。")
        return None

    output = run_dir / "plugin"
    project = ROOT / "src" / "YangCAD.AutoCAD" / "YangCAD.AutoCAD.csproj"
    log = run_dir / "build.log"
    print(f"编译 AutoCAD 2024 插件，日志：{log}", flush=True)
    with log.open("wb") as stream:
        process = subprocess.run(
            [dotnet, "build", str(project), "-c", "Release", "--no-incremental",
             "-p:CadYear=2024", f"-p:AcadRoot={core.parent}", "-o", str(output)],
            cwd=ROOT, stdout=stream, stderr=subprocess.STDOUT, check=False,
        )

    dll = output / "YangCAD.AutoCAD.dll"
    if process.returncode or not dll.is_file():
        print(f"FAIL: 编译阶段未生成可测 DLL，请查看 {log}")
        return None
    return dll


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--core-console", type=Path, required=True, help="AutoCAD 2024 accoreconsole.exe")
    plugin = parser.add_mutually_exclusive_group(required=True)
    plugin.add_argument("--build", action="store_true", help="Build AutoCAD 2024 plugin from this checkout first")
    plugin.add_argument("--dll", type=Path, help="Built AutoCAD 2024 YangCAD.AutoCAD.dll")
    parser.add_argument("--dwg", type=Path, required=True, help="DWG saved from SLB_SUPPORT_DIRECTIONS.dxf")
    args = parser.parse_args()
    core = args.core_console.resolve()
    dwg = args.dwg.resolve()
    dll = args.dll.resolve() if args.dll else None
    inputs = [(core, ".exe"), (dwg, ".dwg")]
    if dll is not None:
        inputs.append((dll, ".dll"))
    for path, suffix in inputs:
        if not path.is_file() or path.suffix.lower() != suffix:
            parser.error(f"Expected an existing {suffix} file: {path}")

    artifacts = ROOT / "artifacts"
    artifacts.mkdir(exist_ok=True)
    run_dir = Path(tempfile.mkdtemp(prefix="slab-smoke-", dir=artifacts))
    if dll is None:
        dll = build_plugin(core, run_dir)
        if dll is None:
            return 1

    profile = run_dir / "profile"
    profile.mkdir()
    drawing = run_dir / "fixture-copy.dwg"
    shutil.copyfile(dwg, drawing)
    description = (
        f"DLL: {dll}\nDLL SHA-256: {hashlib.sha256(dll.read_bytes()).hexdigest()}\n"
        f"Core Console: {core}\nInput DWG: {dwg}\n"
        f"Input DWG SHA-256: {hashlib.sha256(drawing.read_bytes()).hexdigest()}\n"
    )
    (run_dir / "inputs.txt").write_text(description, encoding="utf-8")
    print(description + f"Output: {run_dir}", flush=True)
    stages = (
        ("smoke", build_script(dll), CHECKS),
        ("reopen", build_reopen_script(dll), REOPEN_CHECKS),
    )
    for stage, text, checks in stages:
        script = run_dir / f"{stage}.scr"
        # SCR is saved in the workstation ANSI code page used by AutoCAD scripts.
        script.write_bytes(text.encode("mbcs"))
        log = run_dir / f"{stage}.log"
        try:
            process = subprocess.run(
                [str(core), "/i", str(drawing), "/s", str(script), "/isolate", "YangCADSlabSmoke", str(profile)],
                cwd=run_dir, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, timeout=120, check=False,
            )
        except subprocess.TimeoutExpired as exc:
            log.write_bytes(exc.output or b"")
            print(f"FAIL: {stage} timed out; see {log.name}.")
            return 1

        log.write_bytes(process.stdout)
        failures = check_results(process.stdout, process.returncode, checks)
        if failures:
            print(f"FAIL ({stage}): " + "; ".join(failures))
            return 1
        print(f"PASS ({stage}): {len(checks)} Core Console drawing checks.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
