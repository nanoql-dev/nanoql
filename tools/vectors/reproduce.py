# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The reproducibility gate: reruns generators into a temporary folder and compares each file they write with the
working tree's, byte for byte (docs/porting-playbook.md §8).

Usage: .venv\\Scripts\\python -I tools/vectors/reproduce.py [<generator> ...]

A generator is named by its path under tools/vectors/gen/ without ".py" (for example time/date), and a folder by its
path ending in "/" (cxx/ reruns every harness generator); with none, every generator runs. A harness generator needs
NANOQL_HARNESS (docs/porting-playbook.md §7.2). The comparison reads the files themselves, outside git, so it sees
untracked files and line endings. Prints files V/V and exits 1 on any difference.
"""
import filecmp
import os
import subprocess
import sys
import tempfile

TOOLS_VECTORS = os.path.dirname(os.path.abspath(__file__))
GEN_ROOT = os.path.join(TOOLS_VECTORS, "gen")
REPO = os.path.dirname(os.path.dirname(TOOLS_VECTORS))
VECTORS = os.path.join(REPO, "tests", "vectors")


def all_generators(folder=""):
    root = os.path.join(GEN_ROOT, *folder.split("/"))
    return sorted(os.path.relpath(os.path.join(dp, fn), GEN_ROOT).replace(os.sep, "/")[:-3]
                  for dp, _, fns in os.walk(root) for fn in fns if fn.endswith(".py"))


def selected(args):
    names = []
    for a in args:
        a = a.replace("\\", "/")
        if a.endswith("/"):
            under = all_generators(a.rstrip("/"))
            names += under or [a]
        else:
            names.append(a.removesuffix(".py"))
    return names


def written(root):
    return sorted(os.path.relpath(os.path.join(dp, fn), root).replace(os.sep, "/")
                  for dp, _, fns in os.walk(root) for fn in fns)


def main(args):
    names = selected(args) or all_generators()
    same, failures = 0, []
    for name in names:
        script = os.path.join(GEN_ROOT, *name.split("/")) + ".py"
        if name.endswith("/"):
            failures.append(f"{name}: no generator under tools/vectors/gen/{name}")
            continue
        if not os.path.isfile(script):
            failures.append(f"{name}: no generator at {script}")
            continue
        with tempfile.TemporaryDirectory() as out, tempfile.TemporaryDirectory() as cache:
            # A fresh bytecode cache: a .pyc whose source changed within the same second, at the same size, would
            # otherwise be read as current, and the run would not see the change.
            run = subprocess.run([sys.executable, "-I", "-X", f"pycache_prefix={cache}", script, out],
                                 capture_output=True, text=True)
            if run.returncode != 0:
                failures.append(f"{name}: the generator failed\n{run.stderr.strip()}")
                continue
            files = written(out)
            if files != [name + ".json"]:
                failures.append(f"{name}: wrote {files}, expected only {name}.json")
                continue
            committed = os.path.join(VECTORS, *name.split("/")) + ".json"
            fresh = os.path.join(out, *name.split("/")) + ".json"
            if not os.path.isfile(committed):
                failures.append(f"{name}: tests/vectors/{name}.json is missing")
            elif not filecmp.cmp(committed, fresh, shallow=False):
                failures.append(f"{name}: tests/vectors/{name}.json differs from a fresh generation")
            else:
                same += 1
    for f in failures:
        print("DIFFERS " + f)
    print(f"files {same}/{len(names)} byte-identical")
    return 0 if same == len(names) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
