# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Sets up the C++ harness (docs/porting-playbook.md §7.1): Boost 1.90.0, QuantLib built from its pinned sources,
and the harness program, all outside the repository under one root.

Usage: .venv\\Scripts\\python -I tools/cxx/build.py <root> --quantlib <QuantLib worktree> [--clean]

Under <root>:
    downloads/boost_1_90_0.zip   Boost's release zip, refused unless its SHA-256 is the published one
    boost_1_90_0/                the Boost tree (also the tree tools/check-upstream.py reads for Boost ports)
    quantlib-1.44/               a git archive copy of the pinned QuantLib commit
    quantlib-build/, quantlib-install/
    harness-build/               cxx-harness.exe, which generators find through NANOQL_HARNESS

Each step runs only when its output is missing, prints its wall-clock and the tools' versions, and stops on any
mismatch. QuantLib is rebuilt from empty folders whenever its configure command or the toolchain's versions differ
from the stamp its last build left; the harness builds incrementally on every run. --clean empties the build and
install folders first: the run for a pin move or a toolchain change.
"""
import argparse
import hashlib
import json
import os
import re
import shutil
import subprocess
import sys
import tarfile
import time
import urllib.request
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
HARNESS_SOURCE = os.path.join(REPO, "tools", "cxx")

BOOST_URL = "https://archives.boost.io/release/1.90.0/source/boost_1_90_0.zip"
BOOST_SHA256 = "bdc79f179d1a4a60c10fe764172946d0eeafad65e576a8703c4d89d49949973c"
BOOST_DIR = "boost_1_90_0"
BOOST_VERSION = "109000"

QUANTLIB_VERSION = "1.44"
QUANTLIB_COMMIT = "ab4cb476d35f7275834bc05b47d1324403d1823b"

# The wheel's toolchain (VS 2022, MSVC 14.44, Windows SDK 10.0.26100.0).
VS_RANGE = "[17.0,18.0)"
WINDOWS_SDK = "10.0.26100.0"
VC_TOOLSET = "14.44"
CL_VERSION = "19.44."

STAMP = "nanoql-stamp.json"


class Refused(Exception):
    pass


def step(title):
    print(f"== {title}", flush=True)
    return time.perf_counter()


def done(start, what="done"):
    print(f"   {what} in {time.perf_counter() - start:.1f} s", flush=True)


def run(cmd, env, **kw):
    print("   $ " + subprocess.list2cmdline(cmd), flush=True)
    r = subprocess.run(cmd, env=env, **kw)
    if r.returncode != 0:
        raise Refused(f"{os.path.basename(cmd[0])} exited with {r.returncode}")
    return r


def empty(path):
    if os.path.exists(path):
        shutil.rmtree(path)


def developer_environment():
    """The x64 environment of VS 2022's MSVC 14.44 and SDK 10.0.26100.0, and the tools' versions."""
    installer = os.path.join(os.environ.get("ProgramFiles(x86)", r"C:\Program Files (x86)"),
                             "Microsoft Visual Studio", "Installer")
    vswhere = os.path.join(installer, "vswhere.exe")
    if not os.path.isfile(vswhere):
        raise Refused(f"no vswhere at {vswhere}: install Visual Studio 2022 with the C++ workload")
    vs = subprocess.run([vswhere, "-version", VS_RANGE, "-products", "*", "-requires",
                         "Microsoft.VisualStudio.Component.VC.Tools.x86.x64", "-latest", "-property",
                         "installationPath", "-utf8"], capture_output=True, encoding="utf-8").stdout.strip()
    if not vs:
        raise Refused("no Visual Studio 2022 with the C++ x64 tools")
    vcvars = os.path.join(vs, "VC", "Auxiliary", "Build", "vcvars64.bat")
    base = dict(os.environ)
    # vcvarsall calls vswhere by its bare name.
    base["PATH"] = installer + os.pathsep + base.get("PATH", "")
    out = subprocess.run(f'cmd /d /s /c ""{vcvars}" {WINDOWS_SDK} -vcvars_ver={VC_TOOLSET} >nul 2>&1 && set"',
                         capture_output=True, encoding="oem", env=base)
    if out.returncode != 0:
        raise Refused(f"{vcvars} {WINDOWS_SDK} -vcvars_ver={VC_TOOLSET} failed")
    env = {}
    for line in out.stdout.splitlines():
        k, sep, v = line.partition("=")
        if sep and k:
            env[k] = v
    found = {name: shutil.which(name, path=env.get("PATH", env.get("Path", ""))) for name in ("cl", "cmake", "ninja")}
    for name, path in found.items():
        if path is None:
            raise Refused(f"{name} is not on the developer environment's PATH")
        if name != "cl" and not os.path.normcase(path).startswith(os.path.normcase(vs)):
            raise Refused(f"{name} at {path} is not Visual Studio 2022's ({vs})")
    banner = subprocess.run([found["cl"]], capture_output=True, encoding="oem", env=env).stderr
    m = re.search(r"Version (\d+\.\d+\.\d+(?:\.\d+)?) for x64", banner)
    if m is None or not m.group(1).startswith(CL_VERSION):
        raise Refused(f"cl is not MSVC {CL_VERSION}x for x64: {banner.strip().splitlines()[:1]}")
    versions = {
        "visualStudio": vs,
        "cl": m.group(1),
        "vcTools": env.get("VCToolsVersion", "").strip("\\"),
        "windowsSdk": env.get("WindowsSDKVersion", "").strip("\\"),
        "cmake": subprocess.run([found["cmake"], "--version"], capture_output=True, encoding="utf-8",
                                env=env).stdout.splitlines()[0].removeprefix("cmake version "),
        "ninja": subprocess.run([found["ninja"], "--version"], capture_output=True, encoding="utf-8",
                                env=env).stdout.strip(),
    }
    if not versions["vcTools"].startswith(VC_TOOLSET + "."):
        raise Refused(f"VCToolsVersion is {versions['vcTools']}, not {VC_TOOLSET}")
    if versions["windowsSdk"] != WINDOWS_SDK:
        raise Refused(f"WindowsSDKVersion is {versions['windowsSdk']}, not {WINDOWS_SDK}")
    return env, found, versions


def sha256(path):
    h = hashlib.sha256()
    with open(path, "rb") as f:
        for block in iter(lambda: f.read(1 << 20), b""):
            h.update(block)
    return h.hexdigest()


def boost(root):
    tree = os.path.join(root, BOOST_DIR)
    version_hpp = os.path.join(tree, "boost", "version.hpp")
    if not os.path.isfile(version_hpp):
        zip_path = os.path.join(root, "downloads", BOOST_DIR + ".zip")
        if not os.path.isfile(zip_path):
            t = step(f"Boost: download {BOOST_URL}")
            os.makedirs(os.path.dirname(zip_path), exist_ok=True)
            part = zip_path + ".part"
            with urllib.request.urlopen(BOOST_URL) as r, open(part, "wb") as f:
                shutil.copyfileobj(r, f, 1 << 20)
            os.replace(part, zip_path)
            done(t, f"{os.path.getsize(zip_path):,} bytes")
        t = step("Boost: check the zip's SHA-256")
        digest = sha256(zip_path)
        if digest != BOOST_SHA256:
            raise Refused(f"{zip_path} has SHA-256 {digest}, not Boost's published {BOOST_SHA256}")
        done(t, BOOST_SHA256)
        t = step(f"Boost: extract into {tree}")
        staging = os.path.join(root, ".boost-extracting")
        empty(staging)
        with zipfile.ZipFile(zip_path) as z:
            files = sum(1 for i in z.infolist() if not i.is_dir())
            z.extractall(staging)
        os.replace(os.path.join(staging, BOOST_DIR), tree)
        empty(staging)
        done(t, f"{files:,} files")
    with open(version_hpp, encoding="utf-8") as f:
        m = re.search(r"^#define BOOST_VERSION (\d+)", f.read(), re.M)
    if m is None or m.group(1) != BOOST_VERSION:
        raise Refused(f"{version_hpp} gives BOOST_VERSION {m and m.group(1)}, not {BOOST_VERSION}")
    print(f"== Boost: {tree}, BOOST_VERSION {BOOST_VERSION}", flush=True)
    return tree


def quantlib_copy(root, worktree):
    head = subprocess.run(["git", "-C", worktree, "rev-parse", "HEAD"], capture_output=True,
                          encoding="utf-8").stdout.strip()
    if head != QUANTLIB_COMMIT:
        raise Refused(f"{worktree} is at {head or 'no commit'}, not QuantLib v{QUANTLIB_VERSION} ({QUANTLIB_COMMIT})")
    copy = os.path.join(root, "quantlib-" + QUANTLIB_VERSION)
    if not os.path.isfile(os.path.join(copy, "CMakeLists.txt")):
        t = step(f"QuantLib: git archive {QUANTLIB_COMMIT} into {copy}")
        staging = copy + ".copying"
        empty(staging)
        os.makedirs(staging)
        p = subprocess.Popen(["git", "-C", worktree, "archive", "--format=tar", QUANTLIB_COMMIT],
                             stdout=subprocess.PIPE)
        with tarfile.open(fileobj=p.stdout, mode="r|") as tar:
            tar.extractall(staging, filter="data")
        if p.wait() != 0:
            raise Refused("git archive failed")
        os.replace(staging, copy)
        done(t)
    with open(os.path.join(copy, "ql", "version.hpp"), encoding="utf-8") as f:
        if f'#define QL_VERSION "{QUANTLIB_VERSION}"' not in f.read():
            raise Refused(f"{copy} is not QuantLib {QUANTLIB_VERSION}")
    print(f"== QuantLib: {copy}, v{QUANTLIB_VERSION} at {QUANTLIB_COMMIT}", flush=True)
    return copy


def quantlib_build(root, copy, boost_tree, env, tools, versions):
    build = os.path.join(root, "quantlib-build")
    install = os.path.join(root, "quantlib-install")
    configure = ["-S", copy, "-B", build, "-G", "Ninja", "-DCMAKE_BUILD_TYPE=Release",
                 f"-DBOOST_ROOT={boost_tree}", "-DQL_BUILD_TEST_SUITE=OFF", "-DQL_BUILD_EXAMPLES=OFF",
                 "-DCMAKE_UNITY_BUILD=ON", f"-DCMAKE_INSTALL_PREFIX={install}"]
    stamp = {"configure": configure, "toolchain": versions}
    stamp_path = os.path.join(build, STAMP)
    package = os.path.join(install, "lib", "cmake", "QuantLib", "QuantLibConfig.cmake")
    if os.path.isfile(package) and os.path.isfile(stamp_path):
        with open(stamp_path, encoding="utf-8") as f:
            if json.load(f) == stamp:
                print(f"== QuantLib: installed in {install}, built with this configure command and toolchain",
                      flush=True)
                return install
        print("== QuantLib: the configure command or the toolchain changed since the last build; rebuilding",
              flush=True)
    for folder in (build, install, os.path.join(root, "harness-build")):
        empty(folder)
    t = step("QuantLib: configure")
    run([tools["cmake"], *configure], env)
    done(t)
    t = step("QuantLib: build")
    run([tools["cmake"], "--build", build], env)
    done(t)
    t = step("QuantLib: install")
    run([tools["cmake"], "--install", build], env, stdout=subprocess.DEVNULL)
    done(t)
    with open(stamp_path, "w", encoding="utf-8") as f:
        json.dump(stamp, f, indent=2)
    return install


def harness(root, install, env, tools):
    build = os.path.join(root, "harness-build")
    if not os.path.isfile(os.path.join(build, "build.ninja")):
        t = step("Harness: configure")
        run([tools["cmake"], "-S", HARNESS_SOURCE, "-B", build, "-G", "Ninja", "-DCMAKE_BUILD_TYPE=Release",
             f"-DCMAKE_PREFIX_PATH={install}"], env)
        done(t)
    t = step("Harness: build")
    run([tools["cmake"], "--build", build], env)
    done(t)
    exe = os.path.join(build, "cxx-harness.exe")
    if not os.path.isfile(exe):
        raise Refused(f"the build left no {exe}")
    return exe


def main(argv):
    p = argparse.ArgumentParser(description="Set up Boost, QuantLib and the C++ harness under <root>.")
    p.add_argument("root")
    p.add_argument("--quantlib", required=True, help="the QuantLib worktree at the pinned commit")
    p.add_argument("--clean", action="store_true", help="empty the build and install folders first")
    a = p.parse_args(argv)
    root = os.path.abspath(a.root)
    os.makedirs(root, exist_ok=True)
    total = time.perf_counter()
    try:
        t = step("Toolchain: VS 2022's developer environment")
        env, tools, versions = developer_environment()
        for k, v in versions.items():
            print(f"   {k}: {v}")
        done(t)
        if a.clean:
            for folder in ("quantlib-build", "quantlib-install", "harness-build"):
                empty(os.path.join(root, folder))
            print("== Cleaned the build and install folders", flush=True)
        boost_tree = boost(root)
        copy = quantlib_copy(root, os.path.abspath(a.quantlib))
        install = quantlib_build(root, copy, boost_tree, env, tools, versions)
        exe = harness(root, install, env, tools)
    except Refused as e:
        print(f"REFUSED {e}", flush=True)
        return 1
    print(f"== All steps in {time.perf_counter() - total:.1f} s")
    print(f"NANOQL_HARNESS={exe}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
