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
    ('helper', 'wrong display helper identity'),
    ('helper_null', '$.codeAssembly'),
    ('helper_empty', '$.codeAssembly'),
    ('single_line_type', '$.patterns[0].singleLine'),
    ('single_line_break', 'single-line pattern contains line breaks'),
    ('markup', 'markup'),
    ('missing_key', 'mismatched English/Russian key sets'),
    ('cllc_template', '$.cllc.genderedCreatureTranslations.m'),
    ('cllc_gender', 'CLLC gender without a nameplate template'),
    # Caught by tools/catalog.schema.json: before it, a misspelt section silently dropped its texts.
    ('unknown_section', "'rawText' was unexpected"),
    ('null_section', '$.texts'),
    ('empty_value', 'should be non-empty'),
])
def test_real_data_mutation_is_detected(tmp_path, mutation, diagnostic):
    root = tmp_path / 'subject'
    # .git and .cache hold release-kit state; its temporary directory, and so tmp_path,
    # can be inside .git, which would make the copy contain itself.
    shutil.copytree(ROOT, root, ignore=shutil.ignore_patterns('.git', '.cache', 'bin', 'obj', 'artifacts', 'evidence', '__pycache__', '.pytest_cache'))
    name = 'BetterArchery' if mutation == 'markup' else 'StructureTweaks'
    if mutation in ('missing_key', 'empty_value'):
        name = 'Warfare'
    elif mutation.startswith('cllc'):
        name = 'CreatureLevelControl'
    elif mutation.startswith('helper') or mutation.startswith('single_line'):
        name = 'ConditionalConfigSync'
    path = root / 'catalog' / ('tolmach-' + name + '.json')
    data = json.loads(path.read_text(encoding='utf-8'))
    if mutation == 'guid':
        data['guids'][0] = 'deliberately.wrong.guid'
    elif mutation == 'assembly':
        data['assembly'] = 'Deliberately.Wrong.Assembly'
    elif mutation == 'version':
        data['pluginVersion'] = '99.0.0'
    elif mutation == 'helper':
        data['codeAssembly'] = 'Deliberately.Wrong.Helper'
    elif mutation == 'helper_null':
        data['codeAssembly'] = None
    elif mutation == 'helper_empty':
        data['codeAssembly'] = ''
    elif mutation == 'single_line_type':
        data['patterns'][0]['singleLine'] = 'true'
    elif mutation == 'single_line_break':
        data['patterns'][0]['source'] += '\n'
    elif mutation == 'markup':
        pattern = next(p for p in data['patterns'] if '</size>' in p['target'])
        pattern['target'] = pattern['target'].replace('</size>', '', 1)
    elif mutation == 'missing_key':
        del data['words'][next(iter(data['words']))]
    elif mutation == 'cllc_template':
        data['cllc']['genderedCreatureTranslations']['m'] = '{name}[ ]'
    elif mutation == 'cllc_gender':
        data['cllc']['creatureGender']['Troll'] = 'x'
    elif mutation == 'unknown_section':
        data['rawText'] = {}
    elif mutation == 'null_section':
        data['texts'] = None
    elif mutation == 'empty_value':
        data['words'][next(iter(data['words']))] = ''
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    process, report = run_validator(root)
    assert process.returncode == 1, process.stderr + process.stdout
    assert any(diagnostic in error for error in report['errors']), report
