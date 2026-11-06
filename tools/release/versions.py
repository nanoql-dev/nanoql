# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""A release's versions, checked before anything is built (docs/releasing.md): the tag, v2.0, against every place the
repository states the version. The bindings tool's Versions (its major, minor and patch, and the ABI's version, the
major times 1,000 plus the minor), the C header it generates and surface.json; Directory.Build.props's Version, which
every NuGet package takes, and its AssemblyVersion, the major alone; the wheel's pyproject.toml; and the Java layer's
POM. No project states a version of its own. A mismatch exits 1, naming each source's version.

Usage: python -I tools/release/versions.py <tag or version> [--root <repository>]
"""

import json
import pathlib
import re
import sys
import tomllib


def read(root: pathlib.Path) -> tuple[dict[str, str], list[str]]:
    """Each source's version as its text, and the problems that aren't a version's text."""
    problems = []
    vs = (root / "src/NanoQL.Bind/Emitting/Versions.cs").read_text(encoding="utf-8")
    major, minor, patch = (int(re.search(rf"public const int {n} = (\d+);", vs).group(1))
                           for n in ("Major", "Minor", "Patch"))
    text = f"{major}.{minor}" if patch == 0 else f"{major}.{minor}.{patch}"
    abi = major * 1000 + minor
    header = (root / "bindings/c/include/nanoql.h").read_text(encoding="utf-8")
    surface = json.loads((root / "bindings/surface.json").read_text(encoding="utf-8"))
    props = (root / "Directory.Build.props").read_text(encoding="utf-8")
    version = re.search(r"<Version>([^<]+)</Version>", props)
    assembly = re.search(r"<AssemblyVersion>([^<]+)</AssemblyVersion>", props)
    found = {
        "Versions.cs": text,
        "nanoql.h": (re.search(r'#define NQL_VERSION "([^"]+)"', header) or [None, "none"])[1],
        "surface.json": surface["version"],
        "Directory.Build.props": version.group(1) if version else "none",
        "pyproject.toml":
            tomllib.loads((root / "bindings/python/pyproject.toml").read_text(encoding="utf-8"))["project"]["version"],
        "pom.xml": re.search(r"<artifactId>nanoql</artifactId>\s*<version>([^<]+)</version>",
                             (root / "bindings/java/pom.xml").read_text(encoding="utf-8")).group(1),
    }
    abis = {"Versions.cs": abi, "nanoql.h": int((re.search(r"#define NQL_ABI_VERSION (\d+)", header) or [0, "0"])[1]),
            "surface.json": surface["abi"]}
    if len(set(abis.values())) != 1:
        problems.append("the ABI's version: " + ", ".join(f"{k} {v}" for k, v in abis.items()))
    if not assembly or assembly.group(1) != f"{major}.0.0.0":
        found_assembly = assembly.group(1) if assembly else "none"
        problems.append(f"Directory.Build.props's AssemblyVersion {found_assembly}, not {major}.0.0.0")
    own = sorted(str(p.relative_to(root)) for p in (root / "src").rglob("*.csproj")
                 if "<Version>" in p.read_text(encoding="utf-8"))
    if own:
        problems.append(f"projects with a version of their own: {own}")
    return found, problems


def main() -> int:
    args = sys.argv[1:]
    valued = ("--root",)
    if not args or any(a in valued and (k + 1 == len(args) or args[k + 1].startswith("--"))
                       for k, a in enumerate(args)):
        print(__doc__)
        return 2
    here = pathlib.Path(__file__).resolve().parents[2]
    root = pathlib.Path(args[args.index("--root") + 1]) if "--root" in args else here
    want = args[0].removeprefix("v")
    found, problems = read(root)
    problems += [f"{k} {v}" for k, v in found.items() if v != want]
    print(f"{args[0]}: " + ", ".join(f"{k} {v}" for k, v in found.items()))
    for p in problems:
        print("MISMATCH: " + p)
    return 1 if problems else 0


if __name__ == "__main__":
    sys.exit(main())
