from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]

def text(path): return (ROOT/path).read_text(encoding='utf-8')
checks = [
    ('workflow', '.github/workflows/static-validation.yml', ['pull_request:', 'workflow_dispatch:', 'tools/verify_source_manifest.py --complete', 'tools/run_static_checks.py', 'permissions:', 'contents: read']),
    ('PR template', '.github/PULL_REQUEST_TEMPLATE.md', ['WINDOWS_BUILD_PASS', 'NOT_TESTED', 'Git Commit SHA']),
    ('Gemini issue template', '.github/ISSUE_TEMPLATE/windows_test.md', ['git_sha', 'Build', 'Loaded'] ),
    ('static runner', 'tools/run_static_checks.py', ['PYTHONDONTWRITEBYTECODE', 'STATIC ONLY', 'test_*_policy.py']),
    ('manifest verifier', 'tools/verify_source_manifest.py', ['SHA mismatch', 'not in manifest']),
    ('quality gates', 'docs/QUALITY_GATES.md', ['G0 基线', 'G1 静态', 'G2 Windows Build', 'G3 部署加载', 'G4 业务实测', 'G5 独立审查']),
    ('Gemini SOP', 'docs/GEMINI_EXECUTOR_SOP.md', ['git rev-parse HEAD', 'Undo', 'GUI', 'Revit', 'FINAL GO']),
    ('testing task', 'tasks/GEMINI_WINDOWS_INTEGRATED_SMOKE_20261103.md', ['Phase A', 'Phase B', 'Phase C', 'Phase D', 'R00', 'R10']),
    ('branch map', 'docs/BRANCH_DEPENDENCIES.md', ['PR #4', 'PR #5', 'PR #6', 'PR #7', 'PR #8']),
    ('handoff', 'docs/AI_START_HERE.md', ['feature/product-foundation-v010-20261103', 'docs/RUNTIME_TEST_MATRIX.md']),
]
for name, file, terms in checks:
    source = text(file)
    missing = [term for term in terms if term not in source]
    if missing:
        raise AssertionError(f'{name}: missing {missing}')
    print(f'PASS {name}')
matrix = text('docs/RUNTIME_TEST_MATRIX.md')
for number in range(11):
    assert f'R{number:02d}' in matrix, f'missing test R{number:02d}'
assert 'NOT_TESTED' in matrix and 'PARTIAL_OLD_EVIDENCE' in matrix
print('governance static policy: 11/11 PASS')
