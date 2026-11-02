# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Fills bindings/java/target/ for Maven (docs/bindings.md, "Java"): nanoql's idiomatic layer, which nanoql-bind
writes, and jextract's layer over nanoql.h (run here, or copied from a folder CI's one jextract run made); for the test
build, the test types' layers as a user's package beside nanoql's, compiled with the tests (nqlt's classes over
jextract's nqlt.ffi.nqlt_h, its own functions alone); the replayer's dispatch; the notices every jar carries (LICENSE,
NOTICE, THIRD_PARTY_NOTICES.md and the .NET runtime's); and the platform's library for its natives jar. Prints the
natives jar's classifier.

Usage: python bindings/java/stage.py --library <native build folder> (--jextract <jextract> | --ffi <layers folder>)
           [--rid <rid>] [--test-exports] [--dispatch]
       python bindings/java/stage.py --jextract <jextract> --headers <folder> --layers <out>
           (jextract's layer over each header there, into <out>/<header's name>/, which --ffi takes: CI's one run over
           nanoql-bind headers's output)

A user's package is made the same way: nanoql-bind java --prefix <prefix> for its classes, and jextract_user's flags
over its header for its jextract layer, <prefix>.ffi.<prefix>_h.
"""

import argparse
import importlib.util
import pathlib
import shutil
import subprocess
import sys
import tempfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
TARGET = HERE / "target"
PLATFORMS = {"win-x64": "windows-x64", "win-arm64": "windows-arm64", "linux-x64": "linux-x64", "linux-arm64": "linux-arm64",
             "osx-arm64": "macos-arm64"}
LIBRARY = {"win": "nanoql.dll", "linux": "libnanoql.so", "osx": "libnanoql.dylib"}


def wheel_builder():
    """The Python wheel's builder, for the host's runtime identifier and the .NET notices it finds."""
    spec = importlib.util.spec_from_file_location("nanoql_build", REPO / "bindings" / "python" / "build.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)  # type: ignore[union-attr]
    return module


def tool(*args: str) -> None:
    subprocess.run(["dotnet", "run", "--project", str(REPO / "src" / "NanoQL.Bind"), "-c", "Release", "--", *args], cwd=REPO, check=True)


def run(binary: str, *args: str) -> None:
    subprocess.run([binary, *args], check=True, shell=sys.platform == "win32" and binary.lower().endswith((".bat", ".cmd")))


def jextract(binary: str, header: pathlib.Path, out: pathlib.Path) -> None:
    """jextract's layer: org.nanoql.ffi.nanoql_h over nanoql.h, the experimental API included (BIR7, BIR13)."""
    run(binary, "--output", str(out), "-t", "org.nanoql.ffi", "--header-class-name", "nanoql_h", "-D", "NANOQL_EXPERIMENTAL",
        "-I", str(header.parent), str(header))


def jextract_user(binary: str, header: pathlib.Path, out: pathlib.Path) -> None:
    """A user prefix's jextract layer (BIR9): <prefix>.ffi.<prefix>_h over its header, which includes nanoql.h, holding
    the prefix's own functions, the structs and typedefs any may take, and its header's constants; nanoql's functions
    stay in nanoql's jar."""
    prefix = header.stem
    with tempfile.TemporaryDirectory() as tmp:
        dump = pathlib.Path(tmp) / "all.txt"
        run(binary, "--dump-includes", str(dump), "-D", "NANOQL_EXPERIMENTAL", "-I", str(header.parent), str(header))
        keep = []
        for line in dump.read_text(encoding="utf-8").splitlines():
            parts = line.split("#", 1)[0].split()
            if len(parts) != 2 or not parts[0].startswith("--include-"):
                continue
            kind, name = parts[0], parts[1]
            source = line.split("# header:", 1)[1].strip() if "# header:" in line else ""
            if (kind == "--include-function" and name.startswith(prefix + "_")) or kind in ("--include-struct", "--include-typedef", "--include-union") \
                    or (kind == "--include-constant" and pathlib.Path(source).name == header.name):
                keep.append(f"{kind} {name}")
        args = pathlib.Path(tmp) / "args.txt"
        args.write_text("\n".join(keep) + "\n", encoding="utf-8")
        run(binary, f"@{args}", "--output", str(out), "-t", f"{prefix}.ffi", "--header-class-name", f"{prefix}_h", "-D", "NANOQL_EXPERIMENTAL",
            "-I", str(header.parent), str(header))


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--library", type=pathlib.Path)
    a.add_argument("--headers", type=pathlib.Path)
    a.add_argument("--layers", type=pathlib.Path)
    a.add_argument("--jextract")
    a.add_argument("--ffi", type=pathlib.Path)
    a.add_argument("--rid", default="")
    a.add_argument("--test-exports", action="store_true")
    a.add_argument("--dispatch", action="store_true")
    args = a.parse_args()
    if args.layers:
        if not args.jextract or not args.headers:
            a.error("--layers takes --jextract and --headers")
        for header in sorted(args.headers.glob("*.h")):
            (jextract if header.stem == "nanoql" else jextract_user)(args.jextract, header, args.layers / header.stem)
            print(f"jextract's layer over {header.name} in {args.layers / header.stem}")
        return 0
    if args.library is None:
        a.error("give --library, the native build's folder")
    if (args.jextract is None) == (args.ffi is None):
        a.error("give --jextract to run jextract, or --ffi for a layer it made")
    builder = wheel_builder()
    rid = args.rid or builder.host_rid()
    if rid not in PLATFORMS:
        raise SystemExit(f"no natives jar for {rid}: nanoql's bindings are built for {', '.join(PLATFORMS)}")
    library = args.library.resolve()
    for d in ("generated", "ffi", "generated-test", "ffi-test", "dispatch", "notices", "natives"):
        shutil.rmtree(TARGET / d, ignore_errors=True)
    tool("java", "--out", str(TARGET / "generated"), "--prefix", "nql")
    if args.test_exports:
        tool("java", "--out", str(TARGET / "generated-test"), "--prefix", "nqlt", "--test-exports")
    if args.dispatch:
        tool("replay-java", "--out", str(TARGET / "dispatch"))
    if args.ffi:
        shutil.copytree(args.ffi / "nanoql", TARGET / "ffi")
        if args.test_exports:
            shutil.copytree(args.ffi / "nqlt", TARGET / "ffi-test")
    else:
        jextract(args.jextract, library / "include" / "nanoql.h", TARGET / "ffi")
        if args.test_exports:
            jextract_user(args.jextract, library / "include" / "nqlt.h", TARGET / "ffi-test")
    notices = {name: (REPO / name).read_bytes() for name in ("LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md")}
    notices.update(builder.dotnet_notices(rid))
    for root in (TARGET / "notices", TARGET / "natives" / "META-INF"):
        for name, data in notices.items():
            (root / name).parent.mkdir(parents=True, exist_ok=True)
            (root / name).write_bytes(data)
    name = LIBRARY[rid.split("-")[0]]
    natives = TARGET / "natives" / "org" / "nanoql" / "natives" / PLATFORMS[rid]
    natives.mkdir(parents=True, exist_ok=True)
    shutil.copy2(library / "lib" / name, natives / name)
    print(f"natives-{PLATFORMS[rid]}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
