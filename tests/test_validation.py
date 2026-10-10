"""Fault controls for the DATA validator. No C# behavior or syntax is simulated."""
import importlib.util
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
    ('missing_english_key', 'mismatched English/Russian key sets'),
    ('named_placeholder', 'placeholder/markup mismatch'),
    ('duplicate_json_key', 'duplicate JSON key'),
    ('invalid_json', 'invalid JSON catalog'),
    ('cllc_template', '$.cllc.genderedCreatureTranslations.m'),
    ('cllc_gender', 'CLLC gender without a nameplate template'),
    # Caught by tools/catalog.schema.json: before it, a misspelt section silently dropped its texts.
    ('unknown_section', "'rawText' was unexpected"),
    ('null_section', '$.texts'),
    ('empty_value', 'should be non-empty'),
    ('idol_target', 'name alias must use its corresponding full-name key'),
    ('idol_source', '$.nameAliases'),
    ('idol_missing', 'name alias has no full-name words'),
    ('idol_recursive', 'name alias words must be plain full names'),
    ('idol_overlap', 'name alias duplicates raw text'),
    ('idol_ordinary_tokens', 'placeholder/markup mismatch'),
    ('idol_owner', 'name aliases belong to the Valheim game catalog'),
])
def test_real_data_mutation_is_detected(tmp_path, mutation, diagnostic):
    root = tmp_path / 'subject'
    # .git and .cache hold release-kit state; its temporary directory, and so tmp_path,
    # can be inside .git, which would make the copy contain itself.
    shutil.copytree(ROOT, root, ignore=shutil.ignore_patterns('.git', '.cache', '.private', 'tmp', 'bin', 'obj', 'artifacts', 'evidence', '__pycache__', '.pytest_cache'))
    name = 'BetterArchery' if mutation == 'markup' else 'StructureTweaks'
    if mutation in ('missing_key', 'missing_english_key', 'empty_value'):
        name = 'Warfare'
    elif mutation == 'named_placeholder':
        name = 'InventorySlots'
    elif mutation.startswith('cllc'):
        name = 'CreatureLevelControl'
    elif mutation.startswith('helper') or mutation.startswith('single_line'):
        name = 'ConditionalConfigSync'
    elif mutation.startswith('idol_'):
        name = 'Valheim'
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
    elif mutation == 'missing_english_key':
        del data['englishWords'][next(iter(data['englishWords']))]
    elif mutation == 'named_placeholder':
        key = next(k for k, value in data['englishWords'].items() if value == 'Delete {item}?')
        data['words'][key] = 'Удалить предмет?'
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
    elif mutation.startswith('idol_'):
        source = '$item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name'
        key = data['nameAliases'][source][1:]
        if mutation == 'idol_target':
            data['nameAliases'][source] = '$tolmach_idol_7_armor'
        elif mutation == 'idol_source':
            data['nameAliases'][source.replace('tier0', 'tier8')] = data['nameAliases'].pop(source)
        elif mutation == 'idol_missing':
            del data['words'][key]
            del data['englishWords'][key]
        elif mutation == 'idol_recursive':
            data['words'][key] = source
        elif mutation in ('idol_overlap', 'idol_ordinary_tokens'):
            data.setdefault('rawTexts', {})[source] = data['words'][key]
        elif mutation == 'idol_owner':
            data['id'] = 'Other'
    path.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding='utf-8')
    if mutation == 'duplicate_json_key':
        path.write_text(path.read_text(encoding='utf-8').replace('"id":', '"id": "duplicate", "id":', 1), encoding='utf-8')
    elif mutation == 'invalid_json':
        path.write_text('{"id":', encoding='utf-8')
    process, report = run_validator(root)
    assert process.returncode == 1, process.stderr + process.stdout
    assert any(diagnostic in error for error in report['errors']), report


@pytest.mark.parametrize('native_change', [None, 'wrong_value', 'missing_key', 'unexpected_key'])
def test_caption_resource_comparison_keeps_native_words_and_excludes_only_aliases(native_change):
    spec = importlib.util.spec_from_file_location('tolmach_validator', ROOT / 'tools/validate.py')
    validator = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(validator)
    resource = {'caption_groaning': 'groans', 'sfx_arrow_hit': 'Arrow hits'}
    module = {
        'englishWords': dict(resource, tolmach_idol_0_weapon='Wooden weapon idol'),
        'nameAliases': {
            '$item_upgrader_tier0 $item_upgrader_weapon $item_upgrader_name': '$tolmach_idol_0_weapon'
        }
    }
    if native_change == 'wrong_value':
        module['englishWords']['caption_groaning'] = 'incorrect'
    elif native_change == 'missing_key':
        del module['englishWords']['sfx_arrow_hit']
    elif native_change == 'unexpected_key':
        module['englishWords']['unexpected_caption'] = 'Unexpected caption'
    assert (validator.native_caption_words(module) == resource) is (native_change is None)
