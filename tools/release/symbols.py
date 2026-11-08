# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The native library's symbols archive for a release (docs/releasing.md): the debugger's file for one platform's
library, beside the C archive, in its form: one folder, nanoql-<version>-<rid>-symbols, zipped on Windows and a .tar.xz
elsewhere, holding nanoql.pdb, libnanoql.so.dbg or libnanoql.dylib.dSYM/ with LICENSE, NOTICE and
THIRD_PARTY_NOTICES.md. A release build's symbols map nanoql's sources to its commit on GitHub (Source Link on
Windows); the C archive carries none.

Usage: python -I tools/release/symbols.py --library <native build folder> --out <folder> --rid <rid>
       [--version <version>]
The native build folder is nanoql-bind build's --out: the symbols in lib/, or the macOS bundle in publish/.
"""

import argparse
import io
import json
import pathlib
import sys
import tarfile
import zipfile

REPO = pathlib.Path(__file__).resolve().parents[2]
SYMBOLS = {"win": "nanoql.pdb", "linux": "libnanoql.so.dbg", "osx": "libnanoql.dylib.dSYM"}
NOTICES = ["LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"]


def files(library: pathlib.Path, rid: str) -> dict[str, bytes]:
    """The archive's files by their path inside its folder: the symbols, a file or the dSYM bundle's tree, and the
    notices."""
    name = SYMBOLS[rid.split("-")[0]]
    found = [p for p in (library / "lib" / name, library / "publish" / name) if p.exists()]
    if not found:
        raise SystemExit(f"no {name} in {library}'s lib/ or publish/")
    source = found[0]
    out: dict[str, bytes] = {}
    if source.is_dir():
        for f in sorted(source.rglob("*")):
            if f.is_file():
                out[f"{name}/{f.relative_to(source).as_posix()}"] = f.read_bytes()
    else:
        out[name] = source.read_bytes()
    if not any(out.values()):
        raise SystemExit(f"{source} is empty")
    for n in NOTICES:
        out[n] = (REPO / n).read_bytes()
    return out


def write(out: pathlib.Path, rid: str, version: str, content: dict[str, bytes]) -> pathlib.Path:
    folder = f"nanoql-{version}-{rid}-symbols"
    out.mkdir(parents=True, exist_ok=True)
    if rid.startswith("win"):
        target = out / f"{folder}.zip"
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for path in sorted(content):
                z.writestr(zipfile.ZipInfo(f"{folder}/{path}", date_time=(2026, 1, 1, 0, 0, 0)), content[path],
                           compress_type=zipfile.ZIP_DEFLATED)
        return target
    target = out / f"{folder}.tar.xz"
    with tarfile.open(target, "w:xz") as t:
        for path in sorted(content):
            info = tarfile.TarInfo(f"{folder}/{path}")
            info.size = len(content[path])
            info.mtime = 1767225600  # 2026-01-01
            info.mode = 0o644
            t.addfile(info, io.BytesIO(content[path]))
    return target


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--library", required=True, type=pathlib.Path)
    a.add_argument("--out", required=True, type=pathlib.Path)
    a.add_argument("--rid", required=True)
    a.add_argument("--version", default="")
    args = a.parse_args()
    version = args.version or json.loads((REPO / "bindings" / "surface.json").read_text(encoding="utf-8"))["version"]
    content = files(args.library.resolve(), args.rid)
    target = write(args.out, args.rid, version, content)
    print(f"{target} ({target.stat().st_size:,} bytes): {len(content)} files")
    return 0


if __name__ == "__main__":
    sys.exit(main())
