# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Assembles the C archive (docs/bindings.md, "C and C++"): include/ (nanoql.h, and nanoql.hpp, the C++ layer over it),
lib/ (the library, its import library on Windows, and cmake/nanoql/, the CMake package, from bindings/cpp/cmake/ with
the version written in) and the notices (LICENSE, NOTICE, THIRD_PARTY_NOTICES.md, and the .NET runtime's LICENSE.TXT
and THIRD-PARTY-NOTICES.TXT, which Native AOT compiles into the library), in one folder, nanoql-<version>-<rid>-c-api,
zipped on Windows and a .tar.xz elsewhere, where the libraries alone are executable. The symbols ship apart.

Usage: python bindings/c/archive.py --library <native build folder> --out <folder> [--rid <rid>] [--version <version>]
The native build folder is nanoql-bind build's --out: include/ and lib/.
"""

import argparse
import importlib.util
import io
import json
import pathlib
import sys
import tarfile
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
LIBRARIES = {"win": ["nanoql.dll", "nanoql.lib"], "linux": ["libnanoql.so"], "osx": ["libnanoql.dylib"]}
HEADERS = ["nanoql.h", "nanoql.hpp"]
NOTICES = ["LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md"]
# The CMake package: the config as it is, and the version file with the archive's version for @VERSION@.
CMAKE = REPO / "bindings" / "cpp" / "cmake"
PACKAGE = "lib/cmake/nanoql/"


def wheel_builder():
    """The Python wheel's builder, for the host's runtime identifier and the .NET notices it finds."""
    spec = importlib.util.spec_from_file_location("nanoql_build", REPO / "bindings" / "python" / "build.py")
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)  # type: ignore[union-attr]
    return module


def files(library: pathlib.Path, rid: str, version: str) -> dict[str, bytes]:
    """The archive's files by their path inside its folder."""
    out: dict[str, bytes] = {}
    for h in HEADERS:
        out[f"include/{h}"] = (library / "include" / h).read_bytes()
    for name in LIBRARIES[rid.split("-")[0]]:
        out[f"lib/{name}"] = (library / "lib" / name).read_bytes()
    out[PACKAGE + "nanoqlConfig.cmake"] = (CMAKE / "nanoqlConfig.cmake").read_bytes()
    template = (CMAKE / "nanoqlConfigVersion.cmake.in").read_bytes()
    assert template.count(b"@VERSION@") == 1, "the version file's template names @VERSION@ once"
    out[PACKAGE + "nanoqlConfigVersion.cmake"] = template.replace(b"@VERSION@", version.encode("ascii"))
    for name in NOTICES:
        out[name] = (REPO / name).read_bytes()
    out.update(wheel_builder().dotnet_notices(rid))
    return out


def write(out: pathlib.Path, rid: str, version: str, content: dict[str, bytes]) -> pathlib.Path:
    folder = f"nanoql-{version}-{rid}-c-api"
    out.mkdir(parents=True, exist_ok=True)
    if rid.startswith("win"):
        target = out / f"{folder}.zip"
        with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
            for path in sorted(content):
                z.writestr(zipfile.ZipInfo(f"{folder}/{path}", date_time=(2026, 1, 1, 0, 0, 0)), content[path], compress_type=zipfile.ZIP_DEFLATED)
        return target
    target = out / f"{folder}.tar.xz"
    libraries = {f"lib/{name}" for name in LIBRARIES[rid.split("-")[0]]}
    with tarfile.open(target, "w:xz") as t:
        for path in sorted(content):
            info = tarfile.TarInfo(f"{folder}/{path}")
            info.size = len(content[path])
            info.mtime = 1767225600  # 2026-01-01
            info.mode = 0o755 if path in libraries else 0o644
            t.addfile(info, io.BytesIO(content[path]))
    return target


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--library", required=True, type=pathlib.Path)
    a.add_argument("--out", required=True, type=pathlib.Path)
    a.add_argument("--rid", default="")
    a.add_argument("--version", default="")
    args = a.parse_args()
    rid = args.rid or wheel_builder().host_rid()
    version = args.version or json.loads((REPO / "bindings" / "surface.json").read_text(encoding="utf-8"))["version"]
    target = write(args.out, rid, version, files(args.library.resolve(), rid, version))
    print(f"{target} ({target.stat().st_size:,} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
