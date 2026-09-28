"""Fault controls for the DATA validator. No C# behavior or syntax is simulated."""
import json
import shutil
import subprocess
import sys
from pathlib import Path

import pytest

ROOT = Path(__file__).resolve().parents[1]


def run_validator(root: Path):
    result = subprocess.run([sys.executable, str(root / 'tools/validate.py')],
                            capture_output=True, text=True, timeout=30)
    return result, json.loads(result.stdout)


def test_unmodified_data_passes():
    process, report = run_validator(ROOT)
    assert process.returncode == 0, process.stderr + process.stdout
    assert report['status'] == 'passed'
    assert 'csharp_execution' in report['not_performed_by_this_validator']
    assert report['errors'] == []


@pytest.mark.parametrize('mutation,diagnostic', [
    ('guid', 'wrong catalog GUID'),
    ('assembly', 'wrong assembly identity'),
    ('version', 'wrong/missing BepInPlugin version'),
    ('markup', 'markup'),
    ('missing_key', 'mismatched English/Russian key sets'),
])
def test_real_data_mutation_is_detected(tmp_path, mutation, diagnostic):
    root = tmp_path / 'subject'
    shutil.copytree(ROOT, root, ignore=shutil.ignore_patterns('bin', 'obj', 'artifacts', 'evidence', '__pycache__', '.pytest_cache'))
    name = 'BetterArchery' if mutation == 'markup' else 'StructureTweaks'
    if mutation == 'missing_key':
        name = 'Warfare'
    path = root / 'catalog' / (name + '.json')
    data = json.loads(path.read_text(encoding='utf-8'))
    if mutation == 'guid':
        data['guids'][0] = 'deliberately.wrong.guid'
    elif mutation == 'assembly':
        data['assembly'] = 'Deliberately.Wrong.Assembly'
    elif mutation == 'version':
        data['pluginVersion'] = '99.0.0'
    elif mutation == 'markup':
        pattern = next(p for p in data['patterns'] if '</size>' in p['target'])
        pattern['target'] = pattern['target'].replace('</size>', '', 1)
    elif mutation == 'missing_key':
        del data['words'][next(iter(data['words']))]
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    process, report = run_validator(root)
    assert process.returncode == 1, process.stderr + process.stdout
    assert any(diagnostic in error for error in report['errors']), report
