# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The reproducibility gate: reruns generators into a temporary folder and compares each file they write with the
working tree's, byte for byte (docs/porting-playbook.md §8).

Usage: .venv\\Scripts\\python -I tools/vectors/reproduce.py [--jobs <n>] [<generator> ...]

A generator is named by its path under tools/vectors/gen/ without ".py" (for example time/date), and a folder by its
path ending in "/" (cxx/ reruns every harness generator); with none, every generator runs. A harness generator needs
NANOQL_HARNESS (docs/porting-playbook.md §7.2). The comparison reads the files themselves, outside git, so it sees
untracked files and line endings. Prints files V/V and exits 1 on any difference. --jobs runs n generators at once,
the processor count by default; each runs in its own process and folders, so the files they write don't depend on it.
"""
import concurrent.futures
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


def reproduce(name):
    """The generator's failure, or None when it writes only its file, equal to the committed one byte for byte."""
    script = os.path.join(GEN_ROOT, *name.split("/")) + ".py"
    if name.endswith("/"):
        return f"{name}: no generator under tools/vectors/gen/{name}"
    if not os.path.isfile(script):
        return f"{name}: no generator at {script}"
    with tempfile.TemporaryDirectory() as out, tempfile.TemporaryDirectory() as cache:
        # A fresh bytecode cache: a .pyc whose source changed within the same second, at the same size, would
        # otherwise be read as current, and the run would not see the change.
        run = subprocess.run([sys.executable, "-I", "-X", f"pycache_prefix={cache}", script, out],
                             capture_output=True, text=True)
        if run.returncode != 0:
            return f"{name}: the generator failed\n{run.stderr.strip()}"
        files = written(out)
        if files != [name + ".json"]:
            return f"{name}: wrote {files}, expected only {name}.json"
        committed = os.path.join(VECTORS, *name.split("/")) + ".json"
        fresh = os.path.join(out, *name.split("/")) + ".json"
        if not os.path.isfile(committed):
            return f"{name}: tests/vectors/{name}.json is missing"
        if not filecmp.cmp(committed, fresh, shallow=False):
            return f"{name}: tests/vectors/{name}.json differs from a fresh generation"
    return None


def main(args):
    jobs = os.cpu_count() or 1
    if "--jobs" in args:
        k = args.index("--jobs")
        if k + 1 >= len(args) or not args[k + 1].isdigit() or int(args[k + 1]) < 1:
            print(__doc__)
            return 2
        jobs, args = int(args[k + 1]), args[:k] + args[k + 2:]
    names = selected(args) or all_generators()
    with concurrent.futures.ThreadPoolExecutor(max_workers=jobs) as pool:
        failures = [f for f in pool.map(reproduce, names) if f is not None]
    for f in failures:
        print("DIFFERS " + f)
    same = len(names) - len(failures)
    print(f"files {same}/{len(names)} byte-identical")
    return 0 if same == len(names) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
