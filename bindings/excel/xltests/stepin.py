# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Steps into nanoql from a cell (docs/excel.md, "Debugging"; X5):
python -I bindings/excel/xltests/stepin.py [--netcoredbg <netcoredbg.exe>] [--at <file:line>]

Builds an add-in over nanoql's Debug build into obj/stepin/, nanoql's PDB left where its build wrote it, apart from the
.xll. Starts an Excel of its own through automation (pywin32), loads the add-in for the session alone, attaches
netcoredbg (3.2.0, from PATH unless --netcoredbg names it) with Just My Code off, breaks at FlatForward's discount
(FlatForward.cs:94 unless --at says otherwise), and makes a cell's discount call. It passes when the call stops there,
nanoql's module has its symbols loaded, and the generated worksheet function is a frame above. It then detaches and
quits that Excel alone; Excel's settings in the registry are compared as run.py compares them.
"""
import os
import queue
import re
import shutil
import subprocess
import sys
import threading
import time

import pythoncom
import win32com.client
import win32process

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import run as suite  # noqa: E402  (the suite's registry snapshot and Excel processes)

REPO = suite.REPO
OBJ = os.path.join(HERE, "obj", "stepin")
DEBUG = os.path.join(REPO, "src", "NanoQL.Extensions", "bin", "Debug", "net10.0")


def build():
    shutil.rmtree(OBJ, ignore_errors=True)
    suite.run(["dotnet", "build", os.path.join("src", "NanoQL.Extensions"), "-c", "Debug", "-nologo", "-v:q"])
    suite.run(["dotnet", "build", os.path.join("src", "NanoQL.Bind"), "-c", "Release", "-nologo", "-v:q"])
    suite.run(["dotnet", "pack", os.path.join("src", "NanoQL.Excel"), "-c", "Release", "-nologo", "-v:q", "-o", os.path.join(OBJ, "feed")])
    suite.run(["dotnet", suite.TOOL, "excel", "build", "--runtime-source", os.path.join(OBJ, "feed"),
               "--assembly", "nql=" + os.path.join(DEBUG, "NanoQL.dll"), "--assembly", "nql=" + os.path.join(DEBUG, "NanoQL.Extensions.dll"),
               "--out", os.path.join(OBJ, "addin")])
    return os.path.join(OBJ, "addin", "nanoql64.xll")


class Debugger:
    """netcoredbg over its machine interface, its output read on a thread."""

    def __init__(self, exe, pid):
        self.p = subprocess.Popen([exe, "--interpreter=mi", "--attach", str(pid)], stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                  stderr=subprocess.STDOUT, text=True, encoding="utf-8", bufsize=1)
        self.lines = queue.Queue()
        self.seen = []
        threading.Thread(target=lambda: [self.lines.put(line.rstrip()) for line in self.p.stdout], daemon=True).start()

    def send(self, command):
        self.p.stdin.write(command + "\n")
        self.p.stdin.flush()

    def until(self, seconds, prefix):
        """The lines read until one starting with prefix, or the time is up."""
        got, end = [], time.time() + seconds
        while time.time() < end:
            try:
                line = self.lines.get(timeout=0.2)
            except queue.Empty:
                continue
            got.append(line)
            self.seen.append(line)
            if line.startswith(prefix):
                break
        return got


def main():
    args = sys.argv[1:]
    exe = args[args.index("--netcoredbg") + 1] if "--netcoredbg" in args else shutil.which("netcoredbg") or "netcoredbg"
    at = args[args.index("--at") + 1] if "--at" in args else "FlatForward.cs:94"
    xll = build()
    before, snap, pos = suite.excel_pids(), suite.snapshot(), suite.window_position()
    checks = {}
    pythoncom.CoInitialize()
    app = win32com.client.DispatchEx("Excel.Application")
    _, pid = win32process.GetWindowThreadProcessId(app.Hwnd)
    assert pid not in before, "the Excel started isn't a new process"
    dbg = None
    try:
        app.DisplayAlerts = False
        assert app.RegisterXLL(xll), "RegisterXLL"
        ws = app.Workbooks.Add().Worksheets(1)
        ws.Range("A1").Formula = "=DATE(2015,9,15)"
        ws.Range("A2").Formula = '=nqlConventionSetDayCounter(nqlConventionSetQuantLib(),"Actual365Fixed")'
        ws.Range("A3").Formula = "=nqlFlatForward(A1,0.031,A2)"
        app.Calculate()
        dbg = Debugger(exe, pid)
        dbg.until(15, "^done")
        dbg.send("1-gdb-set just-my-code 0")
        dbg.until(5, "1^")
        dbg.send(f"2-break-insert -f {at}")
        print("stepin.py: break-insert " + " ".join(dbg.until(5, "2^")[-1:])[:200], flush=True)
        dbg.send("3-exec-continue")
        dbg.until(3, "3^")
        hit = {}

        def watch():
            stop = [line for line in dbg.until(60, "*stopped") if line.startswith("*stopped")]
            hit["stopped"] = stop[0] if stop else None
            if stop:
                dbg.send("4-stack-list-frames 0 8")
                hit["stack"] = " ".join(line for line in dbg.until(10, "4^") if line.startswith("4^"))
                dbg.send("5-exec-continue")
                dbg.until(5, "5^")

        watcher = threading.Thread(target=watch)
        watcher.start()
        # The cell's call; it blocks while the debugger holds Excel's calculation at the breakpoint.
        ws.Range("A4").Formula = "=nqlYieldTermStructureDiscount2(A3,5)"
        app.Calculate()
        watcher.join(75)
        stopped, stack = hit.get("stopped") or "", hit.get("stack") or ""
        file, line = at.rsplit(":", 1)
        checks["stopped at " + at] = bool(re.search(rf'file="{re.escape(file)}",.*line="{line}"', stopped))
        # nanoql's module, which Excel-DNA loads from bytes, by its name; the stopped frame is in it.
        module = next((x for x in dbg.seen if x.startswith("=library-loaded") and 'target-name="NanoQL"' in x), "")
        print(f"stepin.py: nanoql's module {module[:300] or 'not loaded'}", flush=True)
        ident = re.search(r'id="(\{[^"]+\})"', module)
        checks["nanoql's module, its symbols loaded"] = 'symbols-loaded="1"' in module
        checks["the stopped frame in nanoql's module"] = bool(ident) and f'module-id="{ident.group(1)}"' in stopped
        checks["the generated worksheet function a frame above"] = bool(re.search(r'file="XlFunctions\d+\.g\.cs"', stack))
        print(f"stepin.py: stopped {stopped[:300] or 'never'}", flush=True)
        print(f"stepin.py: the stack {stack[:1200]}", flush=True)
        print(f"stepin.py: the cell {ws.Range('A4').Value!r}", flush=True)
        dbg.send("6-target-detach")
        dbg.until(5, "6^")
        dbg.send("7-gdb-exit")
        dbg.p.wait(10)
    finally:
        if dbg and dbg.p.poll() is None:
            dbg.p.kill()
        for wb in list(app.Workbooks):
            wb.Close(False)
        app.Quit()
        del app
        pythoncom.CoUninitialize()
        deadline = time.time() + 60
        while pid in suite.excel_pids() and time.time() < deadline:
            time.sleep(1)
        if pid in suite.excel_pids():
            subprocess.run(["taskkill", "/PID", str(pid), "/F"], capture_output=True)
            checks["its Excel ended by itself"] = False
        if pos is not None and suite.window_position() != pos:
            import winreg
            with winreg.OpenKey(winreg.HKEY_CURRENT_USER, suite.OPTIONS, 0, winreg.KEY_SET_VALUE) as key:
                winreg.SetValueEx(key, "Pos", 0, winreg.REG_SZ, pos)
        checks["Excel's settings unchanged"] = not suite.diff(snap, suite.snapshot())
        checks["the other Excel processes untouched"] = before <= suite.excel_pids()
    for name, ok in checks.items():
        print(f"stepin.py: {'ok  ' if ok else 'FAIL'} {name}", flush=True)
    passed = all(checks.values()) and len(checks) >= 6
    print(f"stepin.py: X5 {1 if passed else 0}/1", flush=True)
    sys.exit(0 if passed else 1)


if __name__ == "__main__":
    main()
