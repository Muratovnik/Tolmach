#!/usr/bin/env python3
"""Validate translation data and snapshot metadata. Does NOT parse or execute C#.

Python 3.10+. No dependency for the base checks. With --evidence, PyYAML enables
additional comparisons against English/Russian resources in the supplied archive.
"""
from __future__ import annotations
import argparse
import collections
import csv
import hashlib
import json
import re
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
# Valheim itself has no BepInEx plugin record; game modules bind to this reserved GUID.
GAME_GUID = 'valheim'
GAME_ASSEMBLY = 'assembly_valheim'
HOLE = re.compile(r'\{(\d+)\}')
TOKEN = re.compile(r'\{\d+[^{}]*\}|\$\d+|\$[A-Za-z_]\w*|</?[^>\n]+>')
checks = 0
errors: list[str] = []
skipped: list[str] = []

def check(ok: bool, message: str) -> None:
    global checks
    checks += 1
    if not ok:
        errors.append(message)

def no_duplicates(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError(f'duplicate JSON key: {key}')
        result[key] = value
    return result

def load(path: Path) -> dict:
    return json.loads(path.read_text(encoding='utf-8-sig'), object_pairs_hook=no_duplicates)

def binding_checks(modules: list[dict], evidence: Path | None) -> None:
    """Check independent metadata extracted from the supplied ILSpy projects/attributes.
    This validates catalog wiring data, not a BepInEx bootstrap in a running game.
    """
    import xml.etree.ElementTree as ET
    snapshot = load(ROOT / 'tests/fixtures/snapshot-bindings.json')
    known = {row['id']: row for row in snapshot}
    check(set(known) == {m['id'] for m in modules}, 'Binding snapshot/module set mismatch')
    plugin_source = (ROOT / 'src/Plugin.cs').read_text()
    declared = set(re.findall(r'\[BepInDependency\("([^"\n]+)"', plugin_source))
    attribute = re.compile(r'\[BepInPlugin\("([^"\n]+)",\s*"([^"\n]+)",\s*"([^"\n]+)"\)\]')
    for m in modules:
        row = known.get(m['id'])
        if row is None:
            continue
        check(row['guid'] in m['guids'], m['id'] + ': wrong catalog GUID')
        check(set(m['guids']) - {GAME_GUID} <= declared, m['id'] + ': GUID has no declared BepInEx dependency')
        check(GAME_GUID not in m['guids'] or (m['guids'] == [GAME_GUID] and m['assembly'] == GAME_ASSEMBLY),
              m['id'] + ': game GUID must stand alone and bind the game assembly')
        check(m['assembly'] == row['assembly'], m['id'] + ': wrong assembly identity')
        check(m.get('pluginVersion') == row['pluginVersion'], m['id'] + ': wrong/missing BepInPlugin version')
        if evidence is not None:
            for source in row['sources']:
                path = evidence / source['path']
                check(path.is_file(), m['id'] + ': binding evidence missing')
                if not path.is_file():
                    continue
                check(hashlib.sha256(path.read_bytes()).hexdigest() == source['sha256'], m['id'] + ': binding evidence hash changed')
                if path.suffix == '.cs' and row['guid'] == GAME_GUID:
                    found = re.search(r'CurrentVersion \{ get; \} = new GameVersion\((\d+), (\d+), (\d+)\)', path.read_text(encoding='utf-8-sig'))
                    check(bool(found) and '.'.join(found.groups()) == row['pluginVersion'], m['id'] + ': game version contradicts snapshot')
                elif path.suffix == '.cs':
                    matches = [(g, v) for g, _, v in attribute.findall(path.read_text(encoding='utf-8-sig'))]
                    check((row['guid'], row['pluginVersion']) in matches, m['id'] + ': BepInPlugin metadata contradicts snapshot')
                elif path.suffix == '.csproj':
                    names = [node.text for node in ET.parse(path).getroot().iter() if node.tag.rsplit('}', 1)[-1] == 'AssemblyName']
                    check(row['assembly'] in names, m['id'] + ': AssemblyName contradicts snapshot')


def fixture_data_checks(modules: list[dict]) -> None:
    """Validate fixture references only. NUnit, not Python, executes their behavior."""
    ids = {m['id'] for m in modules}
    for case in load(ROOT / 'tests/fixtures/regressions.json'):
        check(case.get('module') in ids, 'Regression fixture references an unknown module')
        check(isinstance(case.get('source'), str) and isinstance(case.get('expected'), str), 'Invalid regression fixture strings')

def evidence_checks(evidence: Path, modules: list[dict]) -> None:
    coverage = load(evidence / 'localization/coverage.json')
    # The archived audit lists packages without Russian text; each needs a module. Packages
    # the audit saw with Russian (often from another pack) may have modules as well.
    gaps = {r['fullName'] for r in coverage['rows'] if r['status'] in ('missing', 'partial')}
    uncovered = gaps - {m['package'] + '-' + m['version'] for m in modules}
    check(not uncovered, 'Archive audit gaps without a module: ' + ', '.join(sorted(uncovered)))
    for m in modules:
        for relative in m['sourceFiles']:
            check((evidence / relative).is_file(), m['id'] + ': missing evidence ' + relative)
            if relative.endswith('localization_captions.csv') and (evidence / relative).is_file():
                with (evidence / relative).open(encoding='utf-8-sig', newline='') as stream:
                    rows = list(csv.reader(stream))
                column = rows[0].index('English')
                english = {r[0]: r[column] for r in rows[1:] if r and r[0] and not r[0].startswith('//') and len(r) > column}
                check(m['englishWords'] == english, m['id'] + ': game caption table not exactly represented')
    expert = next(m for m in modules if m['id'] == 'ExpertExplorer')
    expert_english = {}
    for path in (evidence / 'packages/MilkMediaProductions-ExpertExplorer/plugins').glob('*.English.json'):
        expert_english.update(load(path))
    check(expert['englishWords'] == expert_english, 'ExpertExplorer English resources are not fully represented')
    try:
        import yaml
    except ImportError:
        skipped.append('Extended YAML comparisons: PyYAML is not installed.')
        return
    talks = yaml.safe_load((evidence / 'profile/BepInEx/config/Norsemen/RandomTalks.yml').read_text(encoding='utf-8-sig'))
    dialog = [line for group in talks.values() for line in group]
    norse = next(m for m in modules if m['id'] == 'Norsemen')
    available = set(norse['texts']) | {p['source'] for p in norse['patterns']}
    check(len(dialog) == 63 and set(dialog) <= available, 'Not all 63 archived Norsemen dialogues are represented')
    for m in modules:
        for relative in m['sourceFiles']:
            path = evidence / relative
            if not path.name.endswith('English.yml'): continue
            source = yaml.safe_load(path.read_text(encoding='utf-8-sig'))
            # englishWords[k] == k marks a key the mod uses without any English text; keys that the
            # mod builds in code (PieceManager categories) are not in its resource.
            check(all(source[k] == v for k, v in m['englishWords'].items() if v != k and k in source), m['id'] + ': English resource/key mismatch')
            russian = path.with_name(path.name.replace('English.yml', 'Russian.yml'))
            if russian.is_file():
                existing = yaml.safe_load(russian.read_text(encoding='utf-8-sig')) or {}
                missing = {k for k in source if source[k] and not existing.get(k)}
                check(missing <= set(m['words']), m['id'] + ': existing Russian resource gaps are not covered')
            elif m['id'] != 'Norsemen':
                # A value made only of placeholders ("{label} x{count}") has nothing to translate.
                wordy = {k for k, v in source.items() if re.search(r'[A-Za-z]{2,}', re.sub(r'\{[^{}]*\}', '', str(v)))}
                check(wordy <= set(m['words']), m['id'] + ': English table not fully covered')
    # Check the exact explicit literal replacements against the selected decompilations.
    provenance = load(ROOT / 'PROVENANCE.json')
    roots = {m['id']: evidence / m['source_root'] for m in provenance['modules']}
    for m in modules:
        if not m.get('literals'):
            continue
        # IL dumps back literals that a decompiler shows as string interpolation.
        text = '\n'.join(p.read_text(encoding='utf-8-sig') for pattern in ('*.cs', '*.il') for p in roots[m['id']].rglob(pattern))
        for rule in m['literals']:
            for en in rule['values']:
                literal = json.dumps(en, ensure_ascii=False)
                check(literal in text, m['id'] + ': explicit literal not found in evidence: ' + repr(en))

def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path)
    parser.add_argument('--report', type=Path)
    args = parser.parse_args()
    paths = sorted((ROOT / 'catalog').glob('*.json'))
    modules = [load(p) for p in paths]
    check(bool(modules), 'No modules')
    # LocalizationManager in many mods reads every "<plugin name>.*" file under BepInEx as its own translation.
    for path, m in zip(paths, modules):
        check(path.name == 'tolmach-' + m['id'] + '.json', path.name + ': catalog file must be named tolmach-<id>.json')
    ids = [m['id'] for m in modules]
    check(len(set(ids)) == len(ids), 'Duplicate module ID')
    counts = collections.Counter(modules=len(modules))
    # One key or raw text, one translation: the plugin skips a key whose English or Russian
    # differs between modules, and the first module wins a conflicting raw text.
    seen_words, seen_raw = {}, {}
    for m in modules:
        for key, ru in m['words'].items():
            pair = (m['englishWords'].get(key), ru)
            check(seen_words.setdefault(key, pair) == pair, m['id'] + ': word key conflicts with another module: ' + key)
        for en, ru in m.get('rawTexts', {}).items():
            check(seen_raw.setdefault(en, ru) == ru, m['id'] + ': raw text conflicts with another module: ' + en)
    for m in modules:
        name = m['id']
        terms = m.get('terms', {})
        check(bool(re.fullmatch(r'\d+\.\d+(?:\.\d+){0,2}', m.get('pluginVersion', ''))), name + ': invalid pluginVersion')
        check(bool(re.fullmatch(r'[A-Za-z0-9_.]+', name)), name + ': invalid module ID')
        scoped = any(m.get(k) for k in ('texts', 'patterns', 'literals', 'returns'))
        check(bool(m['guids']) and (bool(m['namespaces']) or not scoped), name + ': missing GUID/namespace')
        check(set(m['words']) == set(m['englishWords']), name + ': mismatched English/Russian key sets')
        check(sum(len(m.get(k, [])) for k in ('words', 'texts', 'patterns', 'literals', 'rawTexts', 'rawPatterns')) > 0, name + ': empty module')
        counts.update({key: len(m.get(key, [])) for key in ('words', 'texts', 'patterns', 'rawTexts', 'rawPatterns')})
        for key in m['words']:
            check(bool(re.fullmatch(r'[A-Za-z0-9_]+', key)), name + ': unsafe token ' + key)
        for vocabulary, table in terms.items():
            check(bool(re.fullmatch(r'[A-Za-z0-9_]+', vocabulary)) and bool(table), name + ': invalid term vocabulary ' + vocabulary)
        pairs = [(m['englishWords'][k], ru) for k, ru in m['words'].items()] + list(m['texts'].items())
        pairs += list(m.get('mapLabels', {}).items()) + list(m.get('rawTexts', {}).items())
        pairs += [pair for table in terms.values() for pair in table.items()]
        pairs += [(p['source'], p['target']) for p in m['patterns'] + m.get('rawPatterns', [])]
        pairs += [p for rule in m.get('literals', []) for p in rule['values'].items()]
        counts['explicit_literals'] += sum(len(rule['values']) for rule in m.get('literals', []))
        for en, ru in pairs:
            check(isinstance(en, str) and isinstance(ru, str) and bool(en) and bool(ru), name + ': empty/non-string translation')
            check(collections.Counter(TOKEN.findall(en)) == collections.Counter(TOKEN.findall(ru)), name + ': placeholder/markup mismatch: ' + repr(en))
            check(re.findall(r'</?[^>\n]+>', en) == re.findall(r'</?[^>\n]+>', ru), name + ': markup order mismatch: ' + repr(en))
        for p, allowed in [(p, ('text', 'message', 'mapLabel')) for p in m['patterns']] + [(p, ('text',)) for p in m.get('rawPatterns', [])]:
            holes = set(HOLE.findall(p['source']))
            check(holes == set(HOLE.findall(p['target'])), name + ': pattern placeholders differ: ' + repr(p['source']))
            check(set(p.get('numeric', [])) <= {int(h) for h in holes}, name + ': invalid numeric placeholder')
            for key, semantic in p.get('arguments', {}).items():
                known = semantic in allowed or (semantic.startswith('term:') and semantic[5:] in terms)
                check(key in holes and known, name + ': invalid semantic argument')
                check(int(key) not in p.get('numeric', []), name + ': conflicting numeric/semantic argument')
        for p in m.get('rawPatterns', []):
            check(HOLE.sub('', p['source']).strip() != '', name + ': raw pattern without fixed text: ' + repr(p['source']))
        for en, ru in m.get('mapLabels', {}).items():
            check(m['texts'].get(en) == ru, name + ': map label differs from display translation')
    binding_checks(modules, args.evidence.resolve() if args.evidence else None)
    fixture_data_checks(modules)
    if args.evidence:
        evidence_checks(args.evidence.resolve(), modules)
    else:
        skipped.append('Archive cross-checks: no --evidence directory supplied.')
    report = {
        'status': 'passed' if not errors else 'failed', 'python_assertions': checks,
        'counts': dict(counts), 'errors': errors, 'skipped': skipped,
        'scope': 'Data only: JSON, placeholder/tag preservation, fixture references, independent snapshot binding metadata and optional source-resource comparisons. No C# parser or alternate translation engine.',
        # This script checks data only. Build.ps1 compiles and runs the NUnit/Harmony tests
        # and validates the Thunderstore package; the game itself is checked by hand.
        'not_performed_by_this_validator': ['csharp_compilation', 'csharp_execution', 'harmony_runtime', 'package_build', 'valheim_runtime']
    }
    rendered = json.dumps(report, ensure_ascii=False, indent=2) + '\n'
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(rendered, encoding='utf-8')
    print(rendered)
    return 0 if not errors else 1

if __name__ == '__main__':
    sys.exit(main())
