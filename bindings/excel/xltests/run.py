# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The in-Excel suite's wrapper (docs/excel.md): python -I bindings/excel/xltests/run.py [--python <interpreter>]
[dotnet test arguments] python -I bindings/excel/xltests/run.py --self-test

Builds what the suite loads into obj/: the runtime package, then with nanoql-bind the two shipped add-ins, the test
build (the bindings' test types, nqlxlStats and nqlxlLog), a user's add-in over Acme.Models, the experimental tests'
build and the replayer the repro tests replay through, and the package, whose stable add-in a test loads. --python
names an interpreter with nanoql's Python package and NumPy, which runs the swap's Python repro; without it that test
is left out, and the run says so. When every failure of a finished run is a test Excel rejected as busy
(RPC_E_SERVERCALL_RETRYLATER) on ExcelDna.Testing's own call, before the test's first line (no frame of the suite's code
in its stack), those tests run once more, by name; the run reports both runs' counts, and --self-test checks that rule
on sample runs. Then runs the suite from this folder, which carries its own global.json. Around the run it records the
machine's Excel processes and Excel's settings in the registry: the suite's instances must end by themselves, any left
is killed by its identifier and reds the run, and the others are never touched; a key changed reds the run, except
Excel's window position, which Excel rewrites when a test closes a second workbook, restored and reported.
"""
import os
import re
import shutil
import subprocess
import sys
import time
import winreg
import zipfile

HERE = os.path.dirname(os.path.abspath(__file__))
REPO = os.path.normpath(os.path.join(HERE, "..", "..", ".."))
OBJ = os.path.join(HERE, "obj")
FEED = os.path.join(OBJ, "feed")
ADDINS = os.path.join(OBJ, "addins")
SCRATCH = os.path.join(OBJ, "scratch")
TOOL = os.path.join(REPO, "src", "NanoQL.Bind", "bin", "Release", "net10.0", "NanoQL.Bind.dll")
ACME = os.path.join(HERE, "Acme", "bin", "Release", "net10.0")
PACKAGE = os.path.join(OBJ, "package")
# Excel's COM rejection while it is busy, which ExcelDna.Testing's own call before a test's first line doesn't retry.
BUSY = "RPC_E_SERVERCALL_RETRYLATER"
# dotnet test's summary of a finished run: its failed and total counts.
SUMMARY = re.compile(r"^(?:Passed|Failed)!\s+- Failed:\s+(\d+), Passed:\s+\d+, Skipped:\s+\d+, Total:\s+(\d+)", re.M)
PYTHON_FILE = os.path.join(OBJ, "python.txt")
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
    shutil.rmtree(PACKAGE, ignore_errors=True)
    run(["dotnet", "build", os.path.join("src", "NanoQL.Bind"), "-c", "Release", "-nologo", "-v:q"])
    run(["dotnet", "build", os.path.join("bindings", "excel", "replay"), "-c", "Release", "-nologo", "-v:q"])
    run(["dotnet", "pack", os.path.join("src", "NanoQL.Excel"), "-c", "Release", "-nologo", "-v:q", "-o", FEED])
    run(["dotnet", "build", os.path.join(HERE, "Acme"), "-c", "Release", "-nologo", "-v:q"])
    # The tool finds the repository by the working directory, so it runs from the root.
    tool = ["dotnet", TOOL, "excel", "build", "--runtime-source", FEED]
    run(tool + ["--experimental", "--out", os.path.join(ADDINS, "experimental")])
    run(tool + ["--out", os.path.join(ADDINS, "stable")])
    run(tool + ["--experimental", "--test-exports", "--out", os.path.join(ADDINS, "test")])
    run(tool + ["--assembly", "nql=" + os.path.join(ACME, "NanoQL.dll"), "--assembly", "nql=" + os.path.join(ACME, "NanoQL.Extensions.dll"),
                "--assembly", "acme=" + os.path.join(ACME, "Acme.Models.dll"), "--out", os.path.join(ADDINS, "acme")])
    run(tool + ["--experimental", "--list", "nql=none", "--no-pack", "--out", os.path.join(ADDINS, "texp")])
    run(["dotnet", TOOL, "excel", "package", "--runtime-source", FEED, "--out", PACKAGE])
    # The stable add-in as a user unzips it.
    zips = [n for n in os.listdir(PACKAGE) if n.endswith("-win-x64.zip")]
    assert len(zips) == 1, zips
    with zipfile.ZipFile(os.path.join(PACKAGE, zips[0])) as z:
        os.makedirs(os.path.join(ADDINS, "package"))
        with open(os.path.join(ADDINS, "package", "nanoql64.xll"), "wb") as f:
            f.write(z.read(zips[0][:-4] + "/nanoql64.xll"))


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


def test(argv):
    """dotnet test in the suite's folder, its output shown as it comes and kept."""
    p = subprocess.Popen(["dotnet", "test", "-c", "Release", "-nologo", *argv], cwd=HERE, stdout=subprocess.PIPE,
                         stderr=subprocess.STDOUT, text=True, encoding="utf-8", errors="replace")
    out = []
    for line in p.stdout:
        print(line, end="", flush=True)
        out.append(line)
    return p.wait(), "".join(out)


def summary(output):
    """A finished run's counts, (failed, total), from dotnet test's summary line; none for a run that didn't finish."""
    found = SUMMARY.findall(output)
    if len(found) != 1 or "Test Run Aborted" in output:
        return None
    return int(found[0][0]), int(found[0][1])


def passed(counts):
    """A run's passed tests of its total, as its lines report them."""
    return "it didn't finish" if counts is None else f"{counts[1] - counts[0]}/{counts[1]} passed"


def busy(output):
    """
    The failed tests to run once more: each a busy rejection of ExcelDna.Testing's own call, with no frame of the
    suite's code in its stack (before the test's first line, not a test whose own retries ran out), and together every
    failure of a run that finished. Otherwise none.
    """
    counts = summary(output)
    if counts is None or counts[0] == 0:
        return []
    names = []
    for block in re.split(r"(?m)^(?=[ \t]*Failed \S+ \[)", output)[1:]:
        name = re.match(r"[ \t]*Failed (\S+) \[", block).group(1)
        message = block.split("Stack Trace:", 1)[0]
        if BUSY not in message or re.search(r"^\s+at NanoQL\.Excel\.XlTests\.", block, re.M):
            return []
        names.append(name)
    return names if len(names) == counts[0] else []


def self_test():
    """busy() over runs in dotnet test's form: the rejections it reruns, and the runs it leaves red."""
    head = "[xUnit.net 00:01:00.00]     NanoQL.Excel.XlTests.{0} [FAIL]\n  Failed NanoQL.Excel.XlTests.{0} [{1}]\n  Error Message:\n   {2}\n"
    rejection = "System.Runtime.InteropServices.COMException : The message filter indicated that the application is busy. (0x8001010A (RPC_E_SERVERCALL_RETRYLATER))"
    frames = "  Stack Trace:\n     at NanoQL.Excel.XlTests.Xl.Retry[T](Func`1 f) in Xl.cs:line 119\n"
    total = "{0}!  - Failed:     {1}, Passed:    {2}, Skipped:     0, Total:    53, Duration: 3 m - NanoQL.Excel.XlTests.dll (net10.0)\n"
    cases = [
        ("a rejection before the first line", head.format("FormsTests.AComplexNumber", "76 ms", rejection) + "\n" + total.format("Failed", 1, 52),
         ["NanoQL.Excel.XlTests.FormsTests.AComplexNumber"]),
        ("two of them", head.format("FormsTests.AComplexNumber", "76 ms", rejection) + head.format("TaskTests.AQuantoCall", "77 ms", rejection) + total.format("Failed", 2, 51),
         ["NanoQL.Excel.XlTests.FormsTests.AComplexNumber", "NanoQL.Excel.XlTests.TaskTests.AQuantoCall"]),
        ("a rejection in a test's body, its retries run out", head.format("ReleaseTests.ALoopedCellsObjectsAreReleased", "10 s", rejection) + frames + total.format("Failed", 1, 52), []),
        ("a run aborted after a rejection", head.format("FormsTests.AComplexNumber", "76 ms", rejection) + "Test Run Aborted.\n", []),
        ("a rejection beside another failure", head.format("FormsTests.AComplexNumber", "76 ms", rejection)
         + head.format("ReleaseTests.ALoopedCellsObjectsAreReleased", "10 s", "Assert.Equal() Failure: Values differ") + frames + total.format("Failed", 2, 51), []),
        ("a summary counting more failures than were printed", head.format("FormsTests.AComplexNumber", "76 ms", rejection) + total.format("Failed", 2, 51), []),
        ("a green run", total.format("Passed", 0, 53), []),
    ]
    bad = [what for what, output, want in cases if busy(output) != want]
    for what in bad:
        print(f"run.py --self-test: FAIL {what}", flush=True)
    print(f"run.py --self-test: {len(cases) - len(bad)}/{len(cases)}", flush=True)
    return 1 if bad else 0


def main():
    argv = sys.argv[1:]
    if argv == ["--self-test"]:
        sys.exit(self_test())
    valued = ("--python", "--filter")
    if any(a in valued and (k + 1 == len(argv) or argv[k + 1].startswith("--")) for k, a in enumerate(argv)):
        print(__doc__)
        sys.exit(2)
    python = None
    if "--python" in argv:
        i = argv.index("--python")
        python = argv[i + 1]
        del argv[i:i + 2]
    build()
    if python:
        with open(PYTHON_FILE, "w", encoding="utf-8") as f:
            f.write(python + "\n")
    else:
        if os.path.exists(PYTHON_FILE):
            os.remove(PYTHON_FILE)
        if "--filter" in argv:
            i = argv.index("--filter") + 1
            argv[i] = f"({argv[i]})&FullyQualifiedName!~TheSwapsPythonRepro"
        else:
            argv += ["--filter", "FullyQualifiedName!~TheSwapsPythonRepro"]
        print("run.py: no --python: the swap's Python repro (ReproTests.TheSwapsPythonRepro) is left out", flush=True)
    before, snap, pos = excel_pids(), snapshot(), window_position()
    print(f"run.py: Excel processes before the run: {sorted(before) or 'none'}", flush=True)
    t0 = time.perf_counter()
    rc, output = test(argv)
    print(f"run.py: dotnet test exit {rc} ({time.perf_counter() - t0:.0f} s)", flush=True)

    # A test whose only failure is Excel rejecting ExcelDna.Testing's own call as busy, made before the test's first line
    # and never retried, runs once more; only when that is every failure, and a second rejection stays red.
    counts = summary(output)
    print(f"run.py: the run: {passed(counts)}", flush=True)
    rejected = busy(output) if rc != 0 else []
    if rejected:
        print(f"run.py: {len(rejected)} test(s) rejected by a busy Excel ({BUSY}) before their first line, run once more: {', '.join(rejected)}", flush=True)
        rest = [a for i, a in enumerate(argv) if a != "--filter" and (i == 0 or argv[i - 1] != "--filter")]
        rc, again = test([*rest, "--filter", "|".join(f"FullyQualifiedName={n}" for n in rejected)])
        rerun = summary(again)
        print(f"run.py: run again: {passed(rerun)}, dotnet test exit {rc}", flush=True)
        if rerun is None or rerun != (0, len(rejected)):
            rc = rc or 1
    reds = [] if rc == 0 else [f"dotnet test exit {rc}"]

    # The runner's instance ends once its last reference is released; any this run left is killed, by its identifier.
    deadline = time.time() + 60
    while excel_pids() - before and time.time() < deadline:
        time.sleep(1)
    for pid in sorted(excel_pids() - before):
        subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)
        reds.append(f"this run's Excel process {pid} didn't end by itself; killed")
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
