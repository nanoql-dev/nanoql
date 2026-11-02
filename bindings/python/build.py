# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Assembles the Python package: nanoql-bind's generated modules beside the hand-written runtime, the platform's
library from a native build, and the notices (docs/bindings.md, "Python"); then a py3-none-<platform> wheel (PEP 427),
its metadata from pyproject.toml (PEP 621, PEP 639's License-Expression and License-Files), or the staged package
alone for the tests.

Usage: python bindings/python/build.py --library <native build folder> (--out <dist folder> | --stage <folder>)
           [--rid <rid>] [--version <version>] [--test-exports]
The native build folder is nanoql-bind build's --out: lib/ holds the library.
"""

import argparse
import base64
import hashlib
import json
import os
import pathlib
import platform
import shutil
import subprocess
import sys
import tomllib
import zipfile

HERE = pathlib.Path(__file__).resolve().parent
REPO = HERE.parent.parent
LIBRARY = {"win": "nanoql.dll", "linux": "libnanoql.so", "osx": "libnanoql.dylib"}
TAGS = {
    "win-x64": "win_amd64",
    "win-arm64": "win_arm64",
    "linux-x64": "manylinux_2_28_x86_64",
    "linux-arm64": "manylinux_2_28_aarch64",
    "osx-arm64": "macosx_14_0_arm64",
}


def host_rid() -> str:
    os_ = {"win32": "win", "darwin": "osx"}.get(sys.platform, "linux")
    arch = "arm64" if platform.machine().lower() in ("arm64", "aarch64") else "x64"
    return f"{os_}-{arch}"


def generate(package: pathlib.Path, test_exports: bool) -> None:
    """The tool's modules, through dotnet run so the tool reads the nanoql it builds."""
    cmd = ["dotnet", "run", "--project", str(REPO / "src" / "NanoQL.Bind"), "-c", "Release", "--", "python", "--out", str(package)]
    if test_exports:
        cmd.append("--test-exports")
    subprocess.run(cmd, cwd=REPO, check=True)


def dotnet_notices(rid: str) -> dict[str, bytes]:
    """The .NET runtime's notices, which Native AOT compiles into the library: its THIRD-PARTY-NOTICES.TXT from the
    ILCompiler runtime pack the native build used, and its MIT LICENSE.TXT from the runtime pack of the same version,
    which the Native AOT publish downloads beside it (.NET 10's ILCompiler pack carries no LICENSE.TXT, and the SDK
    root's LICENSE.txt is, on Windows, the installer's Microsoft .NET Library terms)."""
    packages = pathlib.Path(os.environ.get("NUGET_PACKAGES") or pathlib.Path.home() / ".nuget" / "packages")
    pack = packages / f"runtime.{rid}.microsoft.dotnet.ilcompiler"
    version = None
    assets = REPO / "src" / "NanoQL.Native" / "obj" / "project.assets.json"
    if assets.exists():
        libs = json.loads(assets.read_text(encoding="utf-8")).get("libraries", {})
        for key in libs:
            name, _, v = key.partition("/")
            if name.lower() == f"runtime.{rid}.microsoft.dotnet.ilcompiler":
                version = v
    if version is None:
        versions = sorted((p.name for p in pack.iterdir() if p.is_dir()), key=lambda v: [int(x) if x.isdigit() else 0 for x in v.split(".")]) if pack.exists() else []
        if not versions:
            raise SystemExit(f"no ILCompiler runtime pack for {rid} under {packages}: run nanoql-bind build first")
        version = versions[-1]
    third = pack / version / "THIRD-PARTY-NOTICES.TXT"
    license_txt = packages / f"microsoft.netcore.app.runtime.{rid}" / version / "LICENSE.TXT"
    if not third.exists() or not license_txt.exists():
        raise SystemExit(f"the .NET notices weren't found ({third}, {license_txt}): run nanoql-bind build first")
    if not is_dotnet_mit(license_txt.read_bytes()):
        raise SystemExit(f"{license_txt} isn't .NET's MIT licence")
    return {"dotnet/THIRD-PARTY-NOTICES.TXT": third.read_bytes(), "dotnet/LICENSE.TXT": license_txt.read_bytes()}


def is_dotnet_mit(text: bytes) -> bool:
    """Whether a licence file is .NET's: the MIT licence with the .NET Foundation's copyright."""
    return text.lstrip().startswith(b"The MIT License") and b".NET Foundation" in text


def stage(folder: pathlib.Path, library: pathlib.Path, rid: str, test_exports: bool) -> pathlib.Path:
    package = folder / "nanoql"
    if folder.exists():
        shutil.rmtree(folder)
    (package / "lib").mkdir(parents=True)
    for f in (HERE / "nanoql").iterdir():
        if f.is_file() and f.suffix in (".py", ".typed"):
            shutil.copy2(f, package / f.name)
    lib = library / "lib" / LIBRARY[rid.split("-")[0]]
    if not lib.exists():
        raise SystemExit(f"no library at {lib}")
    shutil.copy2(lib, package / "lib" / lib.name)
    generate(package, test_exports)
    return package


def metadata(project: dict, version: str) -> str:
    lines = ["Metadata-Version: 2.4", f"Name: {project['name']}", f"Version: {version}", f"Summary: {project['description']}"]
    lines.append(f"License-Expression: {project['license']}")
    lines += [f"License-File: {f}" for f in project["license-files"]]
    lines.append(f"Requires-Python: {project['requires-python']}")
    lines += [f"Requires-Dist: {d}" for d in project["dependencies"]]
    lines += [f"Author: {a['name']}" for a in project.get("authors", [])]
    lines += [f"Classifier: {c}" for c in project.get("classifiers", [])]
    lines += [f"Project-URL: {k}, {v}" for k, v in project.get("urls", {}).items()]
    return "\n".join(lines) + "\n"


def wheel(package: pathlib.Path, out: pathlib.Path, rid: str, version: str, notices: dict[str, bytes]) -> pathlib.Path:
    project = tomllib.loads((HERE / "pyproject.toml").read_text(encoding="utf-8"))["project"]
    version = version or project["version"]
    if rid not in TAGS:
        raise SystemExit(f"no wheel for {rid}: nanoql's bindings are built for {', '.join(TAGS)}")
    tag = TAGS[rid]
    dist = f"nanoql-{version}"
    files: dict[str, bytes] = {}
    for f in sorted(package.rglob("*")):
        if f.is_file() and "__pycache__" not in f.parts:
            files["nanoql/" + f.relative_to(package).as_posix()] = f.read_bytes()
    for name in project["license-files"]:
        data = notices.get(name)
        if data is None:
            data = (REPO / name).read_bytes()
        files[f"{dist}.dist-info/licenses/{name}"] = data
    files[f"{dist}.dist-info/METADATA"] = metadata(project, version).encode("utf-8")
    files[f"{dist}.dist-info/WHEEL"] = f"Wheel-Version: 1.0\nGenerator: nanoql build.py\nRoot-Is-Purelib: false\nTag: py3-none-{tag}\n".encode("utf-8")
    record = []
    for path, data in files.items():
        digest = base64.urlsafe_b64encode(hashlib.sha256(data).digest()).rstrip(b"=").decode("ascii")
        record.append(f"{path},sha256={digest},{len(data)}")
    record.append(f"{dist}.dist-info/RECORD,,")
    files[f"{dist}.dist-info/RECORD"] = ("\n".join(record) + "\n").encode("utf-8")
    out.mkdir(parents=True, exist_ok=True)
    target = out / f"{dist}-py3-none-{tag}.whl"
    with zipfile.ZipFile(target, "w", zipfile.ZIP_DEFLATED, compresslevel=9) as z:
        for path, data in files.items():
            z.writestr(zipfile.ZipInfo(path, date_time=(2026, 1, 1, 0, 0, 0)), data, compress_type=zipfile.ZIP_DEFLATED)
    return target


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--library", required=True, type=pathlib.Path)
    a.add_argument("--out", type=pathlib.Path)
    a.add_argument("--stage", type=pathlib.Path)
    a.add_argument("--rid", default=host_rid(), choices=sorted(TAGS))
    a.add_argument("--version", default="")
    a.add_argument("--test-exports", action="store_true")
    args = a.parse_args()
    if (args.out is None) == (args.stage is None):
        a.error("give --out for a wheel or --stage for the staged package")
    folder = args.stage or (args.out / "build")
    package = stage(folder, args.library.resolve(), args.rid, args.test_exports)
    if args.stage:
        print(f"staged {package}")
        return 0
    target = wheel(package, args.out, args.rid, args.version, dotnet_notices(args.rid))
    shutil.rmtree(folder)
    print(f"{target} ({target.stat().st_size:,} bytes)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
