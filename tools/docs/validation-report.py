# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The validation report: what the gates measured on one commit, per module, and the statement the README carries,
both written from a gates run's results folder by tools/check-coverage.py's own check, so every number is a script's.

Usage: .venv\\Scripts\\python -I tools/docs/validation-report.py <checkout> <results folder> [--page <out.md>]
       [--built-from <commit>] [--home <out.md>] [--readme <README.md>] [--check]

<checkout> is the commit the gates ran on, as the results folder's commit.txt names it. The results folder holds:

    commit.txt                           the commit the gates ran on
    platform.txt                         where they ran, as the statement words it ("Windows x64")
    coverage/coverage.cobertura.xml      the coverage gate's report, the QuantLib-checked tests
    coverage-qlnet/coverage.cobertura.xml  its QLNet run's, the QLNet-checked tests
    tests.txt                            the tests gate's summary, with its "total: N" line
    vectors.txt                          tools/vectors/count.py's table over every vector file

The reports name each file by its absolute path in the checkout the gates ran in; the script reads them moved onto
<checkout>, so any checkout of that commit measures the same.

A module is one nanoql namespace, read from each ported file's own namespace declaration; the extensions are one
module, NanoQL.Extensions. A module with no public type with executable code and none listed apart (Boost's internal
ports) has no row. The modules' sums are check-coverage's own "every ported file" line, which the script checks.

--page writes the report's page: the statement, the commit it was measured on (and, with --built-from, the commit the
site builds from, whose library code and vectors' counts are the same), and the per-module table with each module's
types listed apart. --home writes the home page's figures, a fragment in the statement's words naming no reference
library: the vectors with their tolerance, the ported tests, and the divergences. --readme rewrites the README's
statement between its two marker comments, or with --check exits 1 when the README's differs from a fresh one.
"""
import collections
import importlib.util
import os
import re
import shutil
import sys
import tempfile
import textwrap

README_START = "<!-- validation statement: written by tools/docs/validation-report.py -->"
README_END = "<!-- end of the validation statement -->"
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)\s*[;{]", re.M)
COMMIT = re.compile(r"commit: types (\d+)/(\d+); lines (\d+)/(\d+)")
LISTED = re.compile(r"^\s*listed apart, [^:]+: (\d+)(?:: (.*))?$")
WHOLE = re.compile(r"every ported file: types (\d+)/(\d+); lines (\d+)/(\d+); listed apart (\d+) not in the report, "
                   r"(\d+) equality only")
TOTAL = re.compile(r"^\s*total:\s*(\d+)\s*$", re.M)
VECTORS = re.compile(r"^total\s+([\d,]+)\s+([\d,]+)\s+([\d,]+)\s+([\d,]+)\s+\((\d+) files\)", re.M)
VECTOR_ROW = re.compile(r"^(\S+)\s+([\d,]+)\s+([\d,]+)\s+([\d,]+)\s+([\d,]+)$", re.M)
WORDS = ["no", "one", "two", "three", "four", "five", "six", "seven", "eight", "nine", "ten", "eleven", "twelve"]
# Why a public type with code that no QuantLib-checked test runs is checked otherwise; a new one stops the script.
UNCOVERED_REASONS = {
    "FactorReductionFunctions":
        "every output of which is QuantLib's defect fixed, is checked by its own tests",
}


def load_check_coverage(root):
    spec = importlib.util.spec_from_file_location("check_coverage", os.path.join(root, "tools", "check-coverage.py"))
    cc = importlib.util.module_from_spec(spec)
    sys.path.insert(0, os.path.join(root, "tools"))
    spec.loader.exec_module(cc)
    cache, read = {}, cc.read_report

    def memo(path):
        if path not in cache:
            cache[path] = rerooted(read, path, root)
        return cache[path]

    cc.read_report = memo
    return cc


def rerooted(read, path, root):
    """read(path) with the report's file paths moved from the checkout the gates ran in onto root, through a copy."""
    with open(path, "rb") as f:
        data = f.read()
    first = re.search(rb'filename="([^"]*?)[\\/]src[\\/]NanoQL', data)
    new = os.path.abspath(root).encode("utf-8")
    if first is None or os.path.normcase(first.group(1).decode("utf-8")) == os.path.normcase(new.decode("utf-8")):
        return read(path)
    folder = tempfile.mkdtemp(prefix="validation-report-")
    try:
        copy = os.path.join(folder, os.path.basename(path))
        with open(copy, "wb") as f:
            f.write(data.replace(b'filename="' + first.group(1), b'filename="' + new))
        return read(copy)
    finally:
        shutil.rmtree(folder)


def module(root, cc, path):
    if path.startswith(cc.EXTENSIONS):
        return "NanoQL.Extensions"
    with open(os.path.join(root, path), encoding="utf-8-sig") as f:
        m = NAMESPACE.search(f.read())
    return m.group(1) if m else "(no namespace)"


def names(line):
    count, text = LISTED.match(line).groups()
    found = [] if text is None else re.split(r", (?=[A-Za-z_][\w`]* \()", text)
    if len(found) != int(count):
        raise SystemExit(f"can't read the listed types of: {line}")
    return found


def word(n):
    return WORDS[n] if n < len(WORDS) else f"{n:,}"


def measure(root, results):
    cc = load_check_coverage(root)
    run_a, run_q = os.path.join(results, "coverage"), os.path.join(results, "coverage-qlnet")
    groups = collections.defaultdict(list)
    for p in cc.ported_files(root):
        groups[module(root, cc, p)].append(p)
    rows, uncovered = [], []
    for m in sorted(groups):
        errors, out = cc.check(root, run_a, groups[m], [], qlnet=run_q)
        head = COMMIT.match(out[0])
        if head is None:
            raise SystemExit(f"{m}: check-coverage gave no totals: {errors}")
        if head.group(2) == "0" and out[1].rstrip().endswith(": 0") and out[2].rstrip().endswith(": 0"):
            continue
        tc, t, lc, lv = (int(x) for x in head.groups())
        apart, equality = names(out[1]), names(out[2])
        own = [a for a in apart if "(own-tested:" in a]
        no_code = [a for a in apart if "(own-tested:" not in a]
        bad = [e for e in errors if "has no line covered" in e]
        other = [e for e in errors if "has no line covered" not in e]
        if other:
            raise SystemExit(f"{m}: " + "; ".join(other))
        uncovered += [re.search(r": (\S+) has no line covered", e).group(1) for e in bad]
        rows.append({"module": m, "run": tc, "types": t, "covered": lc, "lines": lv, "no_code": no_code,
                     "equality": equality, "own": own})
    errors, out = cc.check(root, run_a, [], cc.ported_files(root), qlnet=run_q)
    whole = WHOLE.search(out[-1])
    if whole is None:
        raise SystemExit(f"check-coverage's whole line not found: {out[-1]}")
    sums = [sum(r[k] for r in rows) for k in ("run", "types", "covered", "lines")]
    apart = sum(len(r["no_code"]) + len(r["own"]) for r in rows)
    equality = sum(len(r["equality"]) for r in rows)
    if [*sums, apart, equality] != [int(x) for x in whole.groups()]:
        raise SystemExit(f"the modules' sums {sums}, {apart}, {equality} aren't check-coverage's: {out[-1]}")
    for u in uncovered:
        if u not in UNCOVERED_REASONS:
            raise SystemExit(f"{u} has no line covered and no reason the report can give")
    return rows, uncovered


def read_folder(root, results):
    def text(name):
        with open(os.path.join(results, name), encoding="utf-8") as f:
            return f.read()

    commit, platform = text("commit.txt").strip(), text("platform.txt").strip()
    totals = TOTAL.findall(text("tests.txt"))
    if len(totals) != 1:
        raise SystemExit(f"tests.txt holds {len(totals)} total lines, not one")
    vectors = text("vectors.txt")
    m = VECTORS.search(vectors)
    if m is None:
        raise SystemExit("vectors.txt holds no count.py total line")
    cases, outputs, overridden, _, files = (int(x.replace(",", "")) for x in m.groups())
    qlnet = [int(r.group(3).replace(",", "")) for r in VECTOR_ROW.finditer(vectors)
             if r.group(1).startswith("qlnet/")]
    divergences, qlnet_divergences = count_divergences(root)
    return {"commit": commit, "platform": platform, "tests": int(totals[0]), "cases": cases, "outputs": outputs,
            "overridden": overridden, "files": files, "qlnet_files": len(qlnet), "qlnet_outputs": sum(qlnet),
            "divergences": divergences, "qlnet_divergences": qlnet_divergences}


def count_divergences(root):
    """docs/divergences.md's entries: QuantLib's, each a "## " heading before the QLNet section's own "## QLNet"
    heading, and QLNet's, each a "### " heading within it. Each count must equal its test files under
    tests/NanoQL.Tests/Divergences/ (QLNet's in QLNet/), one entry per test class."""
    with open(os.path.join(root, "docs", "divergences.md"), encoding="utf-8") as f:
        lines = f.read().split("\n")
    qlnet_at = next((i for i, line in enumerate(lines) if line.startswith("## QLNet")), len(lines))
    quantlib = sum(1 for line in lines[:qlnet_at] if line.startswith("## "))
    qlnet = sum(1 for line in lines[qlnet_at:] if line.startswith("### "))
    tests = os.path.join(root, "tests", "NanoQL.Tests", "Divergences")
    files = sum(1 for n in os.listdir(tests) if n.endswith(".cs"))
    qlnet_tests = os.path.join(tests, "QLNet")
    qlnet_files = sum(1 for n in os.listdir(qlnet_tests) if n.endswith(".cs")) if os.path.isdir(qlnet_tests) else 0
    if (quantlib, qlnet) != (files, qlnet_files):
        raise SystemExit(f"docs/divergences.md lists {quantlib} QuantLib and {qlnet} QLNet entries, against "
                         f"{files} and {qlnet_files} test files")
    return quantlib, qlnet


def own_split(own):
    """The own-tested types by kind, in words, from each one's module: the extensions', the experimental ones, the term
    structures, and the rest."""
    kinds = collections.Counter()
    for m in own:
        if m == "NanoQL.Extensions":
            kinds["of them extensions"] += 1
        elif m.startswith("NanoQL.Experimental"):
            kinds["experimental"] += 1
        elif m == "NanoQL.TermStructures":
            kinds["a term structure"] += 1
        else:
            kinds["other"] += 1
    parts = []
    for label in ("of them extensions", "experimental", "a term structure", "other"):
        n = kinds.get(label, 0)
        if n == 0:
            continue
        if label == "a term structure":
            parts.append("one a term structure" if n == 1 else f"{word(n)} term structures")
        elif label == "other":
            parts.append(f"{word(n)} others")
        else:
            parts.append(f"{word(n)} {label}")
    return ", ".join(parts[:-1]) + (" and " if len(parts) > 1 else "") + parts[-1] if parts else ""


def wrapped(lines):
    """The statement's lines wrapped at 120 columns, a bullet's continuation under its text."""
    out = []
    for line in lines:
        out += [] if line else [""]
        out += textwrap.wrap(line, 120, subsequent_indent="  " if line.startswith("- ") else "",
                             break_long_words=False, break_on_hyphens=False)
    return out


def statement(facts, rows, uncovered):
    run, types = sum(r["run"] for r in rows), sum(r["types"] for r in rows)
    covered, lines = sum(r["covered"] for r in rows), sum(r["lines"] for r in rows)
    no_code = sum(len(r["no_code"]) for r in rows)
    own = [r["module"] for r in rows for _ in r["own"]]
    equality = sum(len(r["equality"]) for r in rows)
    share = 100 * facts["overridden"] / facts["outputs"]
    text = [
        f"Checked against QuantLib 1.44 on {facts['platform']}.",
        "",
        f"- **The vectors:** {facts['outputs']:,} reference outputs in {facts['files']:,} vector files, of which "
        f"{word(facts['qlnet_files'])} files and {facts['qlnet_outputs']:,} outputs check QLNet's additions against "
        "QLNet 1.13.2. Each output agrees within a relative 1e-10 of the larger of the two values and the output's "
        f"scale (an amount's is its notional), except {facts['overridden']:,} ({share:.2f}%) held to a documented "
        "looser tolerance.",
        "- **The tests:** QuantLib's own tests, ported at QuantLib's tolerances.",
    ]
    types_line = (f"- **The types:** {run:,} of the {types:,} public types with executable code are run by a "
                  "QuantLib-checked test, QLNet's additions by a QLNet-checked one.")
    for u in uncovered:
        types_line += f" The one apart, `{u.rsplit('.', 1)[-1]}`, {UNCOVERED_REASONS[u]}."
    types_line += (f" {no_code + len(own):,} more are listed apart, {no_code:,} with no executable code and "
                   f"{len(own)} own-tested ({own_split(own)}), and {word(equality)} are equality-only. "
                   f"{100 * covered / lines:.1f}% of the ported lines are covered.")
    text.append(types_line)
    text.append(f"- **The divergences:** {facts['divergences']:,} documented divergences from QuantLib, each with its "
                "own test class.")
    return wrapped(text)


def home(facts):
    """The home page's figures: one line, which the page includes after "independent reference values:". Its
    tolerance and its tests are the statement's words, the reference library unnamed."""
    share = 100 * facts["overridden"] / facts["outputs"]
    return (f"{facts['outputs']:,} reference outputs in {facts['files']:,} committed vector files, each within a "
            "relative 1e-10 of the larger of the two values and the output's scale (an amount's is its notional), "
            f"except {facts['overridden']:,} ({share:.2f}%) held to a documented looser tolerance; the reference "
            f"library's own tests, ported at their tolerances; and "
            f"{facts['divergences'] + facts['qlnet_divergences']:,} departures from the "
            "references, each documented and tested.\n")


def page(facts, rows, uncovered, built_from):
    out = ["<!--", "  SPDX-FileCopyrightText: 2026 The nanoql Authors", "  SPDX-License-Identifier: Apache-2.0", "-->",
           "", "# Validation", ""]
    out += statement(facts, rows, uncovered)
    out += ["", f"Measured on commit `{facts['commit']}`: {facts['tests']:,} tests, every one passing, and "
            f"{facts['cases']:,} vector cases."]
    if built_from and built_from != facts["commit"]:
        out += ["", f"These pages are built from commit `{built_from}`, whose library code and vectors' counts are the "
                "same as the measured commit's."]
    out += ["", "## By module", "",
            "| Module | Public types run | Lines covered | No executable lines | Equality only | Own-tested |",
            "|---|---:|---:|---:|---:|---:|"]
    for r in rows:
        out.append(f"| `{r['module']}` | {r['run']:,}/{r['types']:,} | {r['covered']:,}/{r['lines']:,} "
                   f"({100 * r['covered'] / r['lines'] if r['lines'] else 0:.1f}%) | {len(r['no_code']):,} | "
                   f"{len(r['equality']):,} | {len(r['own']):,} |")
    for r in rows:
        if r["no_code"] or r["equality"] or r["own"]:
            out += ["", f"### `{r['module']}`", ""]
            for title, listed in (("No executable lines", r["no_code"]), ("Equality only", r["equality"]),
                                  ("Own-tested", r["own"])):
                if listed:
                    out.append(f"- **{title}:** " + ", ".join(listed))
    return "\n".join(out) + "\n"


def main(argv):
    if len(argv) < 2:
        print(__doc__)
        return 2
    root, results = os.path.abspath(argv[0]), os.path.abspath(argv[1])

    def option(name):
        return argv[argv.index(name) + 1] if name in argv else None

    facts = read_folder(root, results)
    rows, uncovered = measure(root, results)
    if option("--page"):
        with open(option("--page"), "w", encoding="utf-8", newline="\n") as f:
            f.write(page(facts, rows, uncovered, option("--built-from")))
        print(f"wrote {option('--page')}")
    if option("--home"):
        with open(option("--home"), "w", encoding="utf-8", newline="\n") as f:
            f.write(home(facts))
        print(f"wrote {option('--home')}")
    if option("--readme"):
        path = option("--readme")
        with open(path, encoding="utf-8") as f:
            readme = f.read()
        start, end = readme.find(README_START), readme.find(README_END)
        if start < 0 or end < start:
            raise SystemExit(f"{path} has no statement markers")
        fresh = readme[:start + len(README_START)] + "\n" + "\n".join(statement(facts, rows, uncovered)) + "\n" + \
            readme[end:]
        if "--check" in argv:
            if fresh != readme:
                print(f"{path}: the statement differs from a fresh one")
                return 1
            print(f"{path}: the statement reproduces")
        elif fresh != readme:
            with open(path, "w", encoding="utf-8", newline="\n") as f:
                f.write(fresh)
            print(f"wrote {path}'s statement")
    if not option("--page") and not option("--home") and not option("--readme"):
        print("\n".join(statement(facts, rows, uncovered)))
    return 0


if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    sys.exit(main(sys.argv[1:]))
