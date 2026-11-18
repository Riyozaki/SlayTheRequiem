#!/usr/bin/env python3
"""Verify the recorded B2.1 source/log identities offline; does NOT compile or run the game."""
from pathlib import Path
import argparse
import hashlib
import json
import subprocess


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--workspace', action='store_true', help='Also require current workspace inputs to match the historical B2.1 source')
    args = parser.parse_args()
    folder = Path(__file__).resolve().parent
    root = folder.parents[3]
    checks = json.loads((folder / 'checks.json').read_text())
    ci = json.loads((folder / 'ci.json').read_text())
    native = json.loads((folder / 'native-adapters.json').read_text())
    commit = checks['code_source_commit']
    sha = lambda b: hashlib.sha256(b).hexdigest()
    assert ci['headSha'] == ci['code_source_commit'] == native['code_source_commit'] == commit
    assert ci['status'] == 'completed' and ci['conclusion'] == 'success'
    assert len(ci['jobs']) == 4 and all(j['conclusion'] == 'success' for j in ci['jobs'])
    counters = ci['core_counters']
    assert counters['total'] == counters['executed'] == counters['passed'] == 658
    assert all(counters[k] == 0 for k in ('failed', 'error', 'timeout', 'aborted', 'notExecuted', 'notRunnable'))
    assert not ci['raw_trx_downloaded_locally'] and not ci['full_mod_build'] and not ci['game_acceptance']
    for item in checks['checks']:
        assert item['exit_code'] == 0, item['name']
        assert sha((folder / item['log']).read_bytes()) == item['sha256'], item['log']
    for path, digest in checks['inputs_sha256'].items():
        recorded = subprocess.check_output(['git', 'show', f'{commit}:{path}'], cwd=root)
        assert sha(recorded) == digest, f'pinned source mismatch: {path}'
        if args.workspace:
            assert sha((root / path).read_bytes()) == digest, f'workspace changed since B2.1: {path}'
    assert native['source_cases'] == len(native['cases']) == 77
    assert len({c['name'] for c in native['cases']}) == 77
    assert not native['compiled'] and not native['executed'] and not any(c['executed'] for c in native['cases'])
    for path, digest in native['source_files'].items():
        assert sha(subprocess.check_output(['git', 'show', f'{commit}:{path}'], cwd=root)) == digest
    print(f"PASS: {len(checks['checks'])} logs, {len(checks['inputs_sha256'])} pinned inputs, CI 658/658, 77 unexecuted native cases; workspace={args.workspace}")


if __name__ == '__main__':
    main()
