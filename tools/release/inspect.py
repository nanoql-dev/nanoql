# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""A release's packages, inspected before anything is published (docs/releasing.md); it exits 1 on any failed check.

- **The NuGet packages** (`--nuget`): each nuspec's version, authors, a description and tags naming neither QuantLib
  nor QLNet, the licence as the file LICENSE, the icon, the README, the project URL, the repository's URL and commit,
  the release notes' link to CHANGELOG.md at the tag, nanoql's dependencies at the version, and LICENSE, NOTICE,
  THIRD_PARTY_NOTICES.md, icon.png and README.md at the package's root; each symbol package's portable PDBs, their
  Source Link maps naming the repository on GitHub.
- **The wheels** (`--wheels`): the five platforms' (or `--rids`') at one version, each METADATA's summary naming
  neither QuantLib nor QLNet, its description Markdown and not empty, and its development status stable.
- **The C archives** (`--archives`): the five platforms' (or `--rids`'), each by bindings/c/tests/archive_check.py.

Usage: python -I tools/release/inspect.py --version <version> [--nuget <folder> --repo <git URL> [--commit <sha>]]
       [--wheels <folder>] [--archives <folder>] [--rids <rid>...]
"""

import argparse
import email.parser
import pathlib
import re
import subprocess
import sys
import xml.etree.ElementTree as ET
import zipfile

REPO = pathlib.Path(__file__).resolve().parents[2]
ROOT_FILES = ["LICENSE", "NOTICE", "THIRD_PARTY_NOTICES.md", "icon.png", "README.md"]
RIDS = {"win-x64": "win_amd64", "win-arm64": "win_arm64", "linux-x64": "manylinux_2_28_x86_64",
        "linux-arm64": "manylinux_2_28_aarch64", "osx-arm64": "macosx_14_0_arm64"}
NAMED = re.compile(r"quantlib|qlnet", re.I)


def nuget(folder: pathlib.Path, version: str, repo: str, commit: str | None, failures: list[str]) -> None:
    """Each package's nuspec and root files, and its symbol package's PDBs."""
    packages = sorted(folder.glob("*.nupkg"))
    if not packages:
        failures.append(f"no package in {folder}")
    nuget_version = version if version.count(".") == 2 else version + ".0"
    for nupkg in packages:
        with zipfile.ZipFile(nupkg) as z:
            names = z.namelist()
            spec = next(n for n in names if n.endswith(".nuspec") and "/" not in n)
            root = ET.fromstring(z.read(spec))
        ns = {"n": root.tag[1:].split("}")[0]}
        md = root.find("n:metadata", ns)

        def get(tag: str) -> str | None:
            e = md.find(f"n:{tag}", ns)
            return None if e is None else (e.text or "")

        lic = md.find("n:license", ns)
        rep = md.find("n:repository", ns)
        deps = [(d.get("id"), d.get("version")) for d in md.iter(f"{{{ns['n']}}}dependency")]
        checks = {
            "version": get("version") == nuget_version,
            "authors": get("authors") == "The nanoql Authors",
            "description": bool(get("description")) and get("description") != "Package Description",
            "neither QuantLib nor QLNet in the description or tags":
                not NAMED.search((get("description") or "") + (get("tags") or "")),
            "licence file": lic is not None and lic.get("type") == "file" and lic.text == "LICENSE",
            "icon": get("icon") == "icon.png",
            "readme": get("readme") == "README.md",
            "project URL": (get("projectUrl") or "").rstrip("/") == "https://nanoql.org",
            "repository": rep is not None and rep.get("type") == "git"
                          and (rep.get("url") or "").removesuffix(".git") == repo.removesuffix(".git")
                          and bool(rep.get("commit")) and (commit is None or rep.get("commit") == commit),
            "root files": all(f in names for f in ROOT_FILES),
            "release notes' link": (get("releaseNotes") or "").endswith(f"/v{version}/CHANGELOG.md"),
            "nanoql's dependencies at the version":
                all(v in (f"[{nuget_version}, )", nuget_version) for i, v in deps if i.startswith("NanoQL")),
        }
        print(f"{nupkg.name}: {get('id')} {get('version')}; {len(names)} files; dependencies {deps}")
        print(f"  description: {get('description')}")
        print(f"  tags: {get('tags')}; repository {rep.get('url') if rep is not None else None} "
              f"{(rep.get('commit') or '')[:10] if rep is not None else ''}")
        print("  checks: " + ", ".join(f"{k} {'ok' if ok else 'FAILED'}" for k, ok in checks.items()))
        failures += [f"{nupkg.name}: {k}" for k, ok in checks.items() if not ok]
        snupkg = nupkg.with_suffix(".snupkg")
        if not snupkg.exists():
            failures.append(f"{nupkg.name}: no symbol package beside it")
            continue
        with zipfile.ZipFile(snupkg) as z:
            pdbs = [n for n in z.namelist() if n.endswith(".pdb")]
            maps = set()
            for p in pdbs:
                data = z.read(p)
                if data[:4] != b"BSJB":
                    failures.append(f"{snupkg.name}: {p} is not a portable PDB")
                m = re.search(rb'\{"documents":\{[^}]*\}\}', data)
                maps.add(m.group(0).decode() if m else "none")
        want = "https://raw.githubusercontent.com/" + repo.split("github.com/", 1)[-1].removesuffix(".git") + "/"
        print(f"  {snupkg.name}: {len(pdbs)} PDBs; Source Link {sorted(maps)}")
        if not pdbs or not all(want in m for m in maps):
            failures.append(f"{snupkg.name}: a PDB's map doesn't name {want}")
        # A release build's map: its sources recorded under /_/ (ContinuousIntegrationBuild), mapped to the commit.
        exact = '{"documents":{"/_/*":"' + want + f'{commit}/*"}}}}' if commit else None
        if exact and maps != {exact}:
            failures.append(f"{snupkg.name}: a PDB's map isn't a release build's, {exact}")


def wheels(folder: pathlib.Path, version: str, rids: list[str], failures: list[str]) -> None:
    """The platforms' wheels at one version, each METADATA read."""
    platforms = sorted(RIDS[r] for r in rids)
    found = sorted(folder.glob("*.whl"))
    tags = {w.name.removesuffix(".whl").rsplit("-", 1)[1]: w for w in found}
    versions = {w.name.split("-")[1] for w in found}
    print(f"wheels: {len(found)}, versions {sorted(versions)}, platforms {sorted(tags)}")
    if sorted(tags) != platforms or len(found) != len(platforms):
        failures.append(f"the wheels' platforms {sorted(tags)}, not {platforms}")
    if versions != {version}:
        failures.append(f"the wheels' versions {sorted(versions)}, not {version}")
    for w in found:
        with zipfile.ZipFile(w) as z:
            text = z.read(next(n for n in z.namelist() if n.endswith(".dist-info/METADATA"))).decode("utf-8")
        m = email.parser.Parser().parsestr(text)
        checks = {
            "summary": bool(m["Summary"]) and not NAMED.search(m["Summary"]),
            "description Markdown":
                m["Description-Content-Type"] == "text/markdown" and bool((m.get_payload() or "").strip()),
            "stable": "Development Status :: 5 - Production/Stable" in (m.get_all("Classifier") or []),
            "version": m["Version"] == version,
        }
        verdicts = ", ".join(f"{k} {'ok' if ok else 'FAILED'}" for k, ok in checks.items())
        print(f"  {w.name} ({w.stat().st_size:,} bytes): {verdicts}")
        failures += [f"{w.name}: {k}" for k, ok in checks.items() if not ok]


def archives(folder: pathlib.Path, version: str, rids: list[str], failures: list[str]) -> None:
    """The platforms' C archives, each by its own check."""
    for rid in rids:
        found = list(folder.glob(f"nanoql-{version}-{rid}-c-api.*"))
        if len(found) != 1:
            failures.append(f"the {rid} C archive: {len(found)} found")
            continue
        r = subprocess.run([sys.executable, "-I", str(REPO / "bindings/c/tests/archive_check.py"), str(found[0])],
                           capture_output=True, text=True)
        print(f"  {(r.stdout or r.stderr).strip().splitlines()[-1]}")
        if r.returncode != 0:
            failures.append(f"{found[0].name}: archive_check.py exit {r.returncode}")


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--version", required=True)
    a.add_argument("--nuget", type=pathlib.Path)
    a.add_argument("--repo", default="https://github.com/nanoql-dev/nanoql.git")
    a.add_argument("--commit")
    a.add_argument("--wheels", type=pathlib.Path)
    a.add_argument("--archives", type=pathlib.Path)
    a.add_argument("--rids", nargs="+", default=list(RIDS), choices=list(RIDS))
    args = a.parse_args()
    failures: list[str] = []
    if args.nuget:
        nuget(args.nuget, args.version, args.repo, args.commit, failures)
    if args.wheels:
        wheels(args.wheels, args.version, args.rids, failures)
    if args.archives:
        archives(args.archives, args.version, args.rids, failures)
    print("FAILED: " + "; ".join(failures) if failures else "every check passed")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
