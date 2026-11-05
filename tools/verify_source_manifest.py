#!/usr/bin/env python3
"""Check bytes in SHA256SUMS.txt, without confusing hash integrity with runtime quality."""
from __future__ import annotations
from pathlib import Path
import hashlib
import argparse
import sys

ROOT=Path(__file__).resolve().parents[1]

def validate(*, complete: bool = False) -> tuple[int,list[str]]:
    file=ROOT/'SHA256SUMS.txt'
    if not file.is_file(): return 0,['SHA256SUMS.txt does not exist']
    issues=[]
    seen=set()
    entries=0
    for i,line in enumerate(file.read_text(encoding='utf-8').splitlines(),1):
        if not line.strip(): continue
        if '  ' not in line:
            issues.append(f'line {i}: malformed SHA256 entry');continue
        expected,relative=line.split('  ',1)
        if len(expected)!=64 or any(c not in '0123456789abcdefABCDEF' for c in expected):
            issues.append(f'line {i}: invalid SHA256');continue
        if not relative or relative in seen:
            issues.append(f'line {i}: blank or duplicate path');continue
        seen.add(relative)
        path=(ROOT/relative).resolve()
        if not path.is_relative_to(ROOT.resolve()) or path==file.resolve():
            issues.append(f'line {i}: unsafe path');continue
        if not path.is_file():
            issues.append(f'line {i}: missing {relative}');continue
        actual=hashlib.sha256(path.read_bytes()).hexdigest()
        if expected.lower()!=actual:
            issues.append(f'line {i}: SHA mismatch {relative}')
        entries+=1
    if entries==0: issues.append('manifest contains no files')
    if complete:
        for path in ROOT.rglob('*'):
            if not path.is_file(): continue
            rel=path.relative_to(ROOT).as_posix()
            if '.git' in path.relative_to(ROOT).parts: continue
            if (rel in ('SHA256SUMS.txt','artifacts/ci-static-report.json')
                or '__pycache__' in path.parts or path.suffix in ('.pyc', '.pyo')): continue
            if rel not in seen: issues.append(f'not in manifest: {rel}')
    return entries,issues

if __name__=='__main__':
    parser=argparse.ArgumentParser()
    parser.add_argument('--complete',action='store_true',help='Also check every source file is indexed')
    args=parser.parse_args()
    entries,issues=validate(complete=args.complete)
    for issue in issues:print('FAIL:',issue)
    print(f'Manifest: {entries} entries, {len(issues)} problems')
    raise SystemExit(1 if issues else 0)
