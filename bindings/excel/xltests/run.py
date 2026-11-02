# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The in-Excel suite's wrapper (docs/excel.md, XLD18): python -I bindings/excel/xltests/run.py [dotnet test arguments]

Builds what the suite loads into obj/: the runtime package, then with nanoql-bind the two shipped add-ins, the test
build (S-bind's test types and nqlxlStats) and a user's add-in over Acme.Models. Then runs the suite from this folder,
which carries its own global.json. Around the run it records the machine's Excel processes and Excel's settings in the
registry: the suite's instances must end by themselves, any left is killed by its identifier and reds the run, and the
others are never touched; a key changed reds the run, except Excel's window position, which Excel rewrites when a test
closes a second workbook, restored and reported.
"""
import os
import shutil
import subprocess
import sys
import time
import winreg

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OBJ = os.path.join(HERE, "obj")
FEED = os.path.join(OBJ, "feed")
ADDINS = os.path.join(OBJ, "addins")
SCRATCH = os.path.join(OBJ, "scratch")
TOOL = os.path.join(REPO, "src", "NanoQL.Bind", "bin", "Release", "net10.0", "NanoQL.Bind.dll")
ACME = os.path.join(HERE, "Acme", "bin", "Release", "net10.0")
OPTIONS = r"Software\Microsoft\Office\16.0\Excel\Options"
KEYS = [
    OPTIONS,
    r"Software\Microsoft\Office\16.0\Excel\Add-in Manager",
    r"Software\Microsoft\Office\16.0\Excel\Resiliency",
    r"Software\Microsoft\Office\Excel\Addins",
    r"Software\Microsoft\Office\16.0\Excel\Addins",
    r"Software\Microsoft\Office\16.0\Excel\File MRU",
    r"Software\Microsoft\Office\16.0\Excel\Security",
]
# The COM classes Excel-DNA registers for a session's RTD server and removes at its end: their names alone.
NAMES = [r"Software\Classes\CLSID", r"Software\Classes"]


def run(argv, cwd=REPO):
    print("run.py> " + " ".join(argv), flush=True)
    rc = subprocess.run(argv, cwd=cwd).returncode
    if rc != 0:
        sys.exit(f"run.py: exit {rc} from {' '.join(argv)}")


def build():
    shutil.rmtree(FEED, ignore_errors=True)
    shutil.rmtree(ADDINS, ignore_errors=True)
    shutil.rmtree(SCRATCH, ignore_errors=True)
    run(["dotnet", "build", os.path.join("src", "NanoQL.Bind"), "-c", "Release", "-nologo", "-v:q"])
    run(["dotnet", "pack", os.path.join("src", "NanoQL.Excel"), "-c", "Release", "-nologo", "-v:q", "-o", FEED])
    run(["dotnet", "build", os.path.join(HERE, "Acme"), "-c", "Release", "-nologo", "-v:q"])
    # The tool finds the repository by the working directory, so it runs from the root.
    tool = ["dotnet", TOOL, "excel", "build", "--runtime-source", FEED]
    run(tool + ["--experimental", "--out", os.path.join(ADDINS, "experimental")])
    run(tool + ["--out", os.path.join(ADDINS, "stable")])
    run(tool + ["--experimental", "--test-exports", "--out", os.path.join(ADDINS, "test")])
    run(tool + ["--assembly", "nql=" + os.path.join(ACME, "NanoQL.dll"), "--assembly", "nql=" + os.path.join(ACME, "NanoQL.Extensions.dll"),
                "--assembly", "acme=" + os.path.join(ACME, "Acme.Models.dll"), "--out", os.path.join(ADDINS, "acme")])


def excel_pids():
    out = subprocess.run(["tasklist", "/FI", "IMAGENAME eq EXCEL.EXE", "/FO", "CSV", "/NH"], capture_output=True, text=True).stdout
    pids = set()
    for line in out.splitlines():
        cells = [c.strip('"') for c in line.split('","')]
        if len(cells) > 1 and cells[0].strip('"').upper() == "EXCEL.EXE":
            pids.add(int(cells[1]))
    return pids


def dump(path, depth=0):
    try:
        key = winreg.OpenKey(winreg.HKEY_CURRENT_USER, path)
    except OSError:
        return None
    out = {}
    with key:
        i = 0
        while True:
            try:
                name, value, _ = winreg.EnumValue(key, i)
            except OSError:
                break
            out["=" + name] = repr(value)
            i += 1
        i = 0
        while True:
            try:
                sub = winreg.EnumKey(key, i)
            except OSError:
                break
            if depth < 6:
                out[sub] = dump(path + "\\" + sub, depth + 1)
            i += 1
    return out


def names(path):
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, path) as key:
            out, i = [], 0
            while True:
                try:
                    out.append(winreg.EnumKey(key, i))
                except OSError:
                    return sorted(out)
                i += 1
    except OSError:
        return None


def snapshot():
    snap = {p: dump(p) for p in KEYS}
    snap.update({"names:" + p: names(p) for p in NAMES})
    return snap


def diff(a, b, prefix=""):
    if isinstance(a, dict) and isinstance(b, dict):
        out = []
        for k in sorted(set(a) | set(b)):
            if k not in a:
                out.append(f"+ {prefix}{k}")
            elif k not in b:
                out.append(f"- {prefix}{k}")
            else:
                out += diff(a[k], b[k], prefix + k + "\\")
        return out
    if isinstance(a, list) and isinstance(b, list):
        return [f"+ {prefix}{x}" for x in sorted(set(b) - set(a))] + [f"- {prefix}{x}" for x in sorted(set(a) - set(b))]
    return [] if a == b else [f"~ {prefix}: {a!r} -> {b!r}"]


def window_position():
    try:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, OPTIONS) as key:
            return winreg.QueryValueEx(key, "Pos")[0]
    except OSError:
        return None


def main():
    build()
    before, snap, pos = excel_pids(), snapshot(), window_position()
    print(f"run.py: Excel processes before the run: {sorted(before) or 'none'}", flush=True)
    t0 = time.perf_counter()
    rc = subprocess.run(["dotnet", "test", "-c", "Release", "-nologo", *sys.argv[1:]], cwd=HERE).returncode
    print(f"run.py: dotnet test exit {rc} ({time.perf_counter() - t0:.0f} s)", flush=True)
    reds = [] if rc == 0 else [f"dotnet test exit {rc}"]

    # The runner's instance ends once its last reference is released; any of the run's left is killed, by its identifier.
    deadline = time.time() + 60
    while excel_pids() - before and time.time() < deadline:
        time.sleep(1)
    for pid in sorted(excel_pids() - before):
        subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)
        reds.append(f"the run's Excel process {pid} didn't end by itself; killed")
    if not before <= excel_pids():
        reds.append(f"Excel processes the run didn't start ended: {sorted(before - excel_pids())}")

    now = window_position()
    if now != pos and pos is not None:
        with winreg.OpenKey(winreg.HKEY_CURRENT_USER, OPTIONS, 0, winreg.KEY_SET_VALUE) as key:
            winreg.SetValueEx(key, "Pos", 0, winreg.REG_SZ, pos)
        print(f"run.py: Excel's window position changed ({now!r}); restored to {pos!r}", flush=True)
    changed = diff(snap, snapshot())
    if changed:
        reds.append("Excel's settings changed:\n  " + "\n  ".join(changed[:40]))
    else:
        print("run.py: Excel's settings unchanged", flush=True)

    for red in reds:
        print("run.py: RED: " + red, flush=True)
    sys.exit(1 if reds else 0)


if __name__ == "__main__":
    main()
