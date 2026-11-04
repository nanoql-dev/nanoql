# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""A release's notes, the GitHub Release's body (docs/releasing.md): CHANGELOG.md's section for the version, then the
platforms' readings from the release run's own tests. Each system's test summary, the test run's own output, gives its
count of tests and the failing tests' names; each bindings leg's checks, its outcomes, give the checks that went red on
a leg released best-effort. Every system the run tested is named, its summary's file given whether or not it was
written: Windows x64 is the reference platform, whose failures block a release, so a failure there, or a summary missing
or unreadable, exits 1 and writes nothing; another system's missing or unreadable summary is a line saying its tests
didn't complete. A failing test is named whole, a theory's row with its arguments, and counted once. A bindings leg's
missing outcomes stop the release unless the leg is best-effort.

Usage: python -I tools/release/notes.py --version <version> --out <notes.md> [--changelog <CHANGELOG.md>]
       --tests "<system>=<tests.txt>"... [--outcomes "<rid>=<outcomes.txt>"...] [--best-effort "<rid> ..."]
The reference system is the one named "Windows x64", listed first.
"""

import argparse
import pathlib
import re
import sys

REPO = pathlib.Path(__file__).resolve().parents[2]
REFERENCE = "Windows x64"


def section(changelog: str, version: str) -> str:
    """CHANGELOG.md's section for the version, without its heading."""
    m = re.search(rf"^## \[{re.escape(version)}\][^\n]*\n(.*?)(?=^## |^\[[^\]]+\]: |\Z)", changelog, re.M | re.S)
    if not m:
        raise SystemExit(f"CHANGELOG.md has no section for {version}")
    return m.group(1).strip() + "\n"


def summary(path: pathlib.Path) -> tuple[int, int, list[str]] | str:
    """A test run's total, its failures and the failing tests' names, from its own output; or why it doesn't read."""
    if not path.is_file():
        return "no summary was written"
    # The run's output as a terminal shows it: on CI's runners the test runner colours its lines with ANSI escape
    # sequences (ECMA-48's control sequences), which the patterns below read past once they are removed.
    text = re.sub(r"\x1b\[[0-?]*[ -/]*[@-~]", "", path.read_text(encoding="utf-8", errors="replace"))
    total = re.findall(r"^\s*total:\s*(\d+)\s*$", text, re.M)
    failed = re.findall(r"^\s*failed:\s*(\d+)\s*$", text, re.M)
    # A failure's line: "failed <name> (<duration>)", the name a theory's row with its arguments, spaces and all.
    names = sorted(set(re.findall(r"^failed (.+) \([0-9hms ]+\)\s*$", text, re.M)))
    if len(total) != 1 or len(failed) != 1 or int(failed[0]) != len(names):
        return f"its summary doesn't read (total {total}, failed {failed}, {len(names)} names)"
    return int(total[0]), int(failed[0]), names


def reds(path: pathlib.Path) -> list[str]:
    """A bindings leg's checks that went red, from its outcomes (`<check>:<outcome>` each)."""
    return [o.split(":", 1)[0] for o in path.read_text(encoding="utf-8").split() if o.endswith(":failure")]


def main() -> int:
    a = argparse.ArgumentParser(description=__doc__.split("\n\n")[0])
    a.add_argument("--version", required=True)
    a.add_argument("--out", required=True, type=pathlib.Path)
    a.add_argument("--changelog", type=pathlib.Path, default=REPO / "CHANGELOG.md")
    a.add_argument("--tests", action="append", default=[])
    a.add_argument("--outcomes", action="append", default=[])
    a.add_argument("--best-effort", default="")
    args = a.parse_args()
    body = section(args.changelog.read_text(encoding="utf-8"), args.version)
    lines = ["", "## The platforms in this release's run", ""]
    systems = dict(t.split("=", 1) for t in args.tests)
    if REFERENCE not in systems:
        raise SystemExit(f"no test summary for {REFERENCE}, the reference")
    for system in [REFERENCE] + [s for s in systems if s != REFERENCE]:
        read = summary(pathlib.Path(systems[system]))
        if isinstance(read, str):
            if system == REFERENCE:
                raise SystemExit(f"{REFERENCE}, the reference: {read}: the release stops")
            lines.append(f"- **{system}:** its tests didn't complete in this run ({read}).")
            continue
        total, failed, names = read
        if system == REFERENCE and failed:
            raise SystemExit(f"{REFERENCE} fails {failed} of {total:,} tests ({', '.join(names[:5])}): "
                             "the release stops")
        line = f"- **{system}:** {total - failed:,} of {total:,} tests pass"
        if system == REFERENCE:
            line += ", the reference platform."
        elif failed:
            shown = ", ".join(f"`{n}`" for n in names[:10])
            more = f" and {failed - 10} more." if failed > 10 else "."
            line += f"; {failed} fail, reported and not blocking: {shown}{more}"
        else:
            line += "."
        lines.append(line)
    best = set(args.best_effort.split())
    legs = dict(o.split("=", 1) for o in args.outcomes)
    for rid, path in sorted(legs.items()):
        if not pathlib.Path(path).is_file():
            if rid not in best:
                raise SystemExit(f"the {rid} bindings leg wrote no outcomes and isn't best-effort: the release stops")
            lines.append(f"- **The {rid} bindings, best-effort:** its checks didn't report in this run.")
            continue
        red = reds(pathlib.Path(path))
        if red and rid not in best:
            raise SystemExit(f"the {rid} bindings leg is red ({', '.join(red)}) and not best-effort: the release stops")
        if red:
            lines.append(f"- **The {rid} bindings, best-effort:** red checks: {', '.join(red)}.")
        else:
            lines.append(f"- **The {rid} bindings:** every check green.")
    args.out.write_text(body + "\n".join(lines) + "\n", encoding="utf-8", newline="\n")
    print(f"{args.out}: CHANGELOG.md's {args.version} section and {len(systems)} systems' tests, "
          f"{len(legs)} bindings legs")
    return 0


if __name__ == "__main__":
    sys.exit(main())
