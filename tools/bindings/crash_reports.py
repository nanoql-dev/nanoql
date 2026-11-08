# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The crash reports a bindings leg's processes left on macOS, printed with the library's frames named (ci.yml).

Usage: python3 -I tools/bindings/crash_reports.py --since <file> [--wait <seconds>] [--symbols <dSYM>]...
       [--jvm-logs <folder>]...

A process that dies of a signal on macOS leaves a report in DiagnosticReports (a .ips file: a JSON header line, then
a JSON body), which ReportCrash writes some seconds after the process dies. Each report written since --since's file
was is printed: the process, its exception and the registers it stopped at, its crashed thread's frames, and each
other thread's first frames. libnanoql.dylib ships stripped (ILC's link strips its local symbols), so a frame in it is
named by atos from the dSYM among --symbols whose UUID is the frame's image's. With --wait, while no report is found,
one still being written is waited for, up to that many seconds. Each JVM fatal-error log (hs_err_pid*.log) written
since in a --jvm-logs folder is printed too. It exits 0: a check whose process died is red already.
"""
import argparse
import glob
import json
import os
import subprocess
import time

FOLDERS = [os.path.expanduser("~/Library/Logs/DiagnosticReports"), "/Library/Logs/DiagnosticReports"]
SHOWN = 30000
OTHER_FRAMES = 6


def reports(since):
    found = []
    for folder in FOLDERS:
        for path in glob.glob(os.path.join(folder, "*.ips")):
            if os.path.getmtime(path) > since:
                found.append(path)
    return sorted(found, key=os.path.getmtime)


def dsym_uuids(dsyms):
    """Each dSYM's DWARF file by the UUID dwarfdump reads in it, upper case."""
    by_uuid = {}
    for dsym in dsyms:
        dwarf = sorted(glob.glob(os.path.join(dsym, "Contents", "Resources", "DWARF", "*")))
        if not dwarf:
            print(f"no dSYM at {dsym}")
            continue
        try:
            out = subprocess.run(["dwarfdump", "--uuid", dwarf[0]], capture_output=True, text=True, check=True).stdout
        except (OSError, subprocess.CalledProcessError) as e:
            print(f"dwarfdump couldn't read {dwarf[0]}: {e}")
            continue
        for line in out.splitlines():
            if line.startswith("UUID: "):
                by_uuid[line.split()[1].upper()] = dwarf[0]
    return by_uuid


def symbolize(frames, images, by_uuid):
    """The names atos gives the frames in images a dSYM covers, by (image index, offset)."""
    names = {}
    wanted = {}
    for f in frames:
        i = f.get("imageIndex")
        if i is not None and 0 <= i < len(images) and images[i].get("uuid", "").upper() in by_uuid:
            wanted.setdefault(i, set()).add(f.get("imageOffset", 0))
    for i, offsets in wanted.items():
        base = images[i].get("base", 0)
        order = sorted(offsets)
        argv = ["atos", "-o", by_uuid[images[i]["uuid"].upper()], "-arch", "arm64", "-l", hex(base)]
        try:
            out = subprocess.run(argv + [hex(base + o) for o in order], capture_output=True, text=True, check=True)
        except (OSError, subprocess.CalledProcessError) as e:
            print(f"atos couldn't name {images[i].get('name')}'s frames: {e}")
            continue
        for o, name in zip(order, out.stdout.splitlines()):
            names[(i, o)] = name.strip()
    return names


def frame_line(k, f, images, names):
    i = f.get("imageIndex")
    image = images[i].get("name", "?") if i is not None and 0 <= i < len(images) else "?"
    offset = f.get("imageOffset", 0)
    name = names.get((i, offset))
    if name is None:
        name = f.get("symbol", "")
        if name and "symbolLocation" in f:
            name += f" + {f['symbolLocation']}"
    return f"  #{k:<3} {image} + {hex(offset)}  {name}".rstrip()


def show(path, by_uuid):
    text = open(path, encoding="utf-8", errors="replace").read()
    head, _, body = text.partition("\n")
    try:
        header, report = json.loads(head), json.loads(body)
    except ValueError:
        print(f"== {path}, not JSON; its first {SHOWN} characters:")
        print(text[:SHOWN])
        return
    exception = report.get("exception", {})
    print(f"== {path}: {report.get('procName', header.get('app_name', '?'))}, pid {report.get('pid', '?')}, "
          f"{exception.get('type', '?')} {exception.get('signal', '')} {exception.get('subtype', '')}".rstrip())
    for key in ("termination", "asi", "vmRegionInfo"):
        if key in report:
            print(f"  {key}: {json.dumps(report[key])[:2000]}")
    images = report.get("usedImages", [])
    threads = report.get("threads", [])
    crashed = report.get("faultingThread", 0)
    names = symbolize([f for t in threads for f in t.get("frames", [])], images, by_uuid)
    for n, thread in enumerate(threads):
        frames = thread.get("frames", [])
        state = thread.get("threadState", {}) if n == crashed else {}
        registers = " ".join(f"{r} {hex(state[r]['value'])}" for r in ("pc", "lr", "sp", "fp", "far")
                             if isinstance(state.get(r), dict) and "value" in state[r])
        print(f" thread {n}{' (crashed)' if n == crashed else ''} {thread.get('name', thread.get('queue', ''))}"
              f"{', ' + registers if registers else ''}".rstrip())
        for k, f in enumerate(frames if n == crashed else frames[:OTHER_FRAMES]):
            print(frame_line(k, f, images, names))


def main():
    p = argparse.ArgumentParser(description=__doc__.splitlines()[0])
    p.add_argument("--since", required=True, help="a file whose modification time is the job's start")
    p.add_argument("--wait", type=float, default=0, help="seconds to wait for a report while none is found")
    p.add_argument("--symbols", action="append", default=[], help="a dSYM bundle of a library the processes loaded")
    p.add_argument("--jvm-logs", action="append", default=[], help="a folder a JVM ran in")
    args = p.parse_args()
    since = os.path.getmtime(args.since)
    deadline = time.monotonic() + args.wait
    found = reports(since)
    while not found and time.monotonic() < deadline:
        time.sleep(2)
        found = reports(since)
    jvm = [path for folder in args.jvm_logs for path in sorted(glob.glob(os.path.join(folder, "hs_err_pid*.log")))
           if os.path.getmtime(path) > since]
    if not found and not jvm:
        print("no crash report")
        return 0
    by_uuid = dsym_uuids(args.symbols) if found else {}
    for path in found:
        show(path, by_uuid)
    for path in jvm:
        print(f"== {path}, its first {SHOWN} characters:")
        print(open(path, encoding="utf-8", errors="replace").read()[:SHOWN])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
