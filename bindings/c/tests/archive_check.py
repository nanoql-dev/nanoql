# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The C archive's check (docs/bindings.md, "C and C++"): one folder holding include/ (nanoql.h and nanoql.hpp), lib/
(the platform's library, its import library on Windows, and cmake/nanoql/'s config and version files, the version
file's version the archive's) and the five notices files, each not empty, .NET's the MIT licence, and nothing else: no
symbols, which ship apart. In a .tar.xz the libraries alone are executable.

Usage: python archive_check.py <archive>
"""

import pathlib
import sys
import tarfile
import zipfile


def entries(path: pathlib.Path) -> dict[str, int]:
    if path.suffix == ".zip":
        with zipfile.ZipFile(path) as z:
            return {i.filename: i.file_size for i in z.infolist() if not i.is_dir()}
    with tarfile.open(path, "r:xz") as t:
        return {m.name: m.size for m in t.getmembers() if m.isfile()}


def read(path: pathlib.Path, name: str) -> bytes:
    if path.suffix == ".zip":
        with zipfile.ZipFile(path) as z:
            return z.read(name)
    with tarfile.open(path, "r:xz") as t:
        return t.extractfile(name).read()  # type: ignore[union-attr]


def main() -> int:
    path = pathlib.Path(sys.argv[1])
    name = path.name.removesuffix(".zip").removesuffix(".tar.xz")
    assert name.startswith("nanoql-") and name.endswith("-c-api"), name
    rid = name.split("-")[-4] + "-" + name.split("-")[-3]
    libraries = {"win": ["nanoql.dll", "nanoql.lib"], "linux": ["libnanoql.so"], "osx": ["libnanoql.dylib"]}[rid.split("-")[0]]
    package = ["lib/cmake/nanoql/nanoqlConfig.cmake", "lib/cmake/nanoql/nanoqlConfigVersion.cmake"]
    expected = {"include/nanoql.h", "include/nanoql.hpp", "LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "dotnet/LICENSE.TXT",
                "dotnet/THIRD-PARTY-NOTICES.TXT", *package}
    expected |= {f"lib/{n}" for n in libraries}
    found = entries(path)
    folders = {p.split("/", 1)[0] for p in found}
    assert folders == {name}, f"one folder, {name}: {folders}"
    inside = {p.split("/", 1)[1]: size for p, size in found.items()}
    assert set(inside) == expected, f"missing {sorted(expected - set(inside))}, unexpected {sorted(set(inside) - expected)}"
    empty = [p for p, size in inside.items() if size == 0]
    assert not empty, f"empty: {empty}"
    dotnet = read(path, f"{name}/dotnet/LICENSE.TXT")
    assert dotnet.lstrip().startswith(b"The MIT License") and b".NET Foundation" in dotnet, "dotnet/LICENSE.TXT isn't .NET's MIT licence"
    version = name.removeprefix("nanoql-").removesuffix(f"-{rid}-c-api")
    declared = read(path, f"{name}/lib/cmake/nanoql/nanoqlConfigVersion.cmake")
    assert f'set(PACKAGE_VERSION "{version}")'.encode("ascii") in declared, f"the CMake package's version isn't {version}"
    if path.suffix != ".zip":
        with tarfile.open(path, "r:xz") as t:
            executable = sorted(m.name.split("/", 1)[1] for m in t.getmembers() if m.isfile() and m.mode & 0o111)
        assert executable == sorted(f"lib/{n}" for n in libraries), f"executable: {executable}"
    print(f"the archive {path.name}: {len(inside)}/{len(expected)} files (include/'s nanoql.h and nanoql.hpp, lib/ with the CMake package {version}, "
          f"and the five notices), none empty, .NET's the MIT licence; {path.stat().st_size:,} bytes")
    return 0


if __name__ == "__main__":
    sys.exit(main())
