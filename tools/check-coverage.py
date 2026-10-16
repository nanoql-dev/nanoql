# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The coverage gate's type check (docs/porting-playbook.md §9.5).

Usage: .venv\\Scripts\\python -I tools/check-coverage.py <results folder> [--self-test]

Run it after the coverage run, which writes one Cobertura report into the emptied results folder:

    dotnet test --solution NanoQL.slnx -c Release --filter-trait "Oracle=QuantLib" --coverlet
        --coverlet-output-format cobertura --results-directory TestResults\\G7

The types it checks are the top-level public types of the .cs files under src/NanoQL/ that the commit adds or
changes (the working tree against HEAD, untracked files included) and that carry an Upstream line. Report entries
match those files by path, so nested, file and compiler-generated classes count toward their file's type.

Red when: the folder doesn't hold exactly one report; the commit ports a public type and the report doesn't list the
NanoQL package; a listed type has no covered line; a ported file carries ExcludeFromCodeCoverage; or a type whose
only executable lines are its equality members lacks a "// Equality-tested:" line naming an existing test file.
Listed apart: the types the report doesn't list, with their kinds, and the equality-only types.
"""
import glob
import os
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import quoteattr

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

SRC = "src/NanoQL/"
EQUALITY = {"Equals", "GetHashCode", "op_Equality", "op_Inequality"}


def changed_files(root):
    """The .cs files under src/NanoQL/ that the working tree adds or changes against HEAD."""
    out = subprocess.run(["git", "-C", root, "status", "--porcelain", "--untracked-files=all", "-z"],
                         capture_output=True, check=True).stdout.decode("utf-8").split("\0")
    paths, i = [], 0
    while i < len(out):
        entry = out[i]
        i += 1
        if len(entry) < 4:
            continue
        status, path = entry[:2], entry[3:]
        if "R" in status or "C" in status:
            i += 1  # the rename's source path follows
        if "D" in status or not path.startswith(SRC) or not path.endswith(".cs"):
            continue
        paths.append(path)
    return sorted(paths)


def ported_files(root):
    """Every .cs file under src/NanoQL/ that carries an Upstream line."""
    return [p for p in sources.git_files(root)
            if p.startswith(SRC) and p.endswith(".cs") and sources.Header(os.path.join(root, p)).upstream]


def read_report(path):
    """{normalised path: {"lines": {number: hits}, "methods": {name: [numbers]}}}, the package names, and the NanoQL
    package's classes whose file resolves to no existing file."""
    tree = ET.parse(path).getroot()
    roots = [s.text or "" for s in tree.iter("source")]
    packages = [p.get("name") for p in tree.iter("package")]
    by_file, unresolved = {}, []
    for package in tree.iter("package"):
        for cls in package.iter("class"):
            filename = cls.get("filename") or ""
            candidates = [os.path.join(r, filename) for r in roots] if roots else [filename]
            found = next((c for c in candidates if os.path.isfile(c)), None)
            if found is None and package.get("name") == "NanoQL":
                unresolved.append(f"{cls.get('name')} ({filename})")
            key = os.path.normcase(os.path.normpath(found or candidates[0]))
            read_class(cls, by_file.setdefault(key, {"lines": {}, "methods": {}}))
    return by_file, packages, unresolved


def read_class(cls, entry):
    for line in cls.find("lines").iter("line") if cls.find("lines") is not None else []:
        n = int(line.get("number"))
        entry["lines"][n] = max(entry["lines"].get(n, 0), int(line.get("hits")))
    for method in cls.iter("method"):
        numbers = [int(ln.get("number")) for ln in method.iter("line")]
        entry["methods"].setdefault(method.get("name"), []).extend(numbers)


def public_types(text):
    return [(kind, name) for access, kind, name in sources.top_level_types(text) if access == "public"]


def check(root, results, commit_files, all_files):
    """Returns (errors, lines of the report)."""
    errors, out = [], []
    reports = sorted(glob.glob(os.path.join(results, "**", "*.cobertura*.xml"), recursive=True))
    if len(reports) != 1:
        return [f"the results folder holds {len(reports)} Cobertura reports, not exactly one"], out
    by_file, packages, unresolved = read_report(reports[0])
    # Coverlet lists every class of the instrumented module, so in a healthy run every NanoQL entry resolves to a
    # file; one that doesn't means the report's paths don't match the repository's, and no type could be matched.
    for cls in unresolved:
        errors.append(f"the report's class {cls} resolves to no file under its <source> roots")

    def assess(paths, record):
        counted = covered_types = lines_valid = lines_covered = 0
        apart, equality_only = [], []
        for path in paths:
            full = os.path.join(root, path)
            header = sources.Header(full)
            if not header.upstream:
                continue
            types = public_types(header.text)
            if not types:
                continue
            kind, name = types[0]
            if record and "ExcludeFromCodeCoverage" in header.text:
                errors.append(f"{path}: carries ExcludeFromCodeCoverage")
            entry = by_file.get(os.path.normcase(os.path.normpath(full)))
            if entry is None or not entry["lines"]:
                apart.append(f"{name} ({kind})")
                continue
            lines_valid += len(entry["lines"])
            covered = sum(1 for h in entry["lines"].values() if h > 0)
            lines_covered += covered
            with_lines = {m for m, numbers in entry["methods"].items() if numbers}
            if with_lines and with_lines <= EQUALITY:
                tested = header.equality_tested
                if tested and os.path.isfile(os.path.join(root, tested)):
                    equality_only.append(f"{name} (equality-tested: {tested})")
                elif record:
                    errors.append(f"{path}: {name}'s only code is equality, and it has no // Equality-tested: line "
                                  "naming an existing test file")
                continue
            counted += 1
            if covered:
                covered_types += 1
            elif record:
                errors.append(f"{path}: {name} has no line covered by the Oracle=QuantLib tests")
        return counted, covered_types, lines_covered, lines_valid, apart, equality_only

    t, tc, lc, lv, apart, equality_only = assess(commit_files, True)
    if t + len(equality_only) and "NanoQL" not in packages:
        errors.append("the commit ports a public type, and the report doesn't list the NanoQL package")
    out.append(f"commit: types {tc}/{t}; lines {lc}/{lv}")
    out.append(f"  listed apart, not in the report: {len(apart)}" + (f": {', '.join(apart)}" if apart else ""))
    out.append(f"  listed apart, equality only: {len(equality_only)}"
               + (f": {', '.join(equality_only)}" if equality_only else ""))
    own = [f"{p}: {sources.Header(os.path.join(root, p)).own_tested}" for p in commit_files
           if sources.Header(os.path.join(root, p)).own_tested]
    out.append(f"  own-tested members: {len(own)}" + "".join(f"\n    {o}" for o in own))
    t2, tc2, lc2, lv2, apart2, eq2 = assess(all_files, False)
    out.append(f"every ported file: types {tc2}/{t2}; lines {lc2}/{lv2}; "
               f"listed apart {len(apart2)} not in the report, {len(eq2)} equality only")
    return errors, out


# ---- self-test: synthetic files and a synthetic report ----

HEADER = ("// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause\n"
          "// Upstream: ql/time/date.hpp @ v1.44\n")


def synthetic(tmp, scenario):
    root = os.path.join(tmp, scenario, "repo")
    results = os.path.join(tmp, scenario, "results")
    os.makedirs(results)
    files = {
        "Ported.cs": HEADER + "namespace NanoQL;\n\npublic sealed class Ported\n{\n    public int F() => 1;\n}\n",
        "Kind.cs": HEADER + "namespace NanoQL;\n\npublic enum Kind\n{\n    A,\n}\n",
        "Iter.cs": HEADER + "namespace NanoQL;\n\npublic static class Iter\n{\n}\n",
        "Hidden.cs": HEADER + "namespace NanoQL;\n\ninternal static class Hidden\n{\n}\n",
        "Own.cs": "// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0\n"
                  "namespace NanoQL;\n\npublic sealed class Own\n{\n}\n",
        "Results.cs": HEADER + "// Equality-tested: tests/ResultsEqualityTests.cs\nnamespace NanoQL;\n\n"
                      "public record Results\n{\n}\n",
        "tests/ResultsEqualityTests.cs": "// test\n",
    }
    hits = {"Ported": 1, "Iter/<Iter>d__1": 1, "Hidden": 0, "Own": 0, "Results": 0}
    methods = {"Ported": ["F"], "Iter/<Iter>d__1": ["MoveNext"], "Hidden": ["G"], "Own": ["H"],
               "Results": ["Equals", "GetHashCode"]}
    package = "NanoQL"
    reports = 1
    if scenario == "no report":
        reports = 0
    elif scenario == "two reports":
        reports = 2
    elif scenario == "no package":
        package = "Other"
    elif scenario == "no covered line":
        hits["Ported"] = 0
    elif scenario == "ExcludeFromCodeCoverage":
        files["Ported.cs"] = files["Ported.cs"].replace("public sealed class", "[System.Diagnostics.CodeAnalysis."
                                                        "ExcludeFromCodeCoverage]\npublic sealed class")
    elif scenario == "equality-only without its line":
        files["Results.cs"] = files["Results.cs"].replace("// Equality-tested: tests/ResultsEqualityTests.cs\n", "")
    elif scenario == "equality members and an uncovered method":
        methods["Results"] = ["Equals", "GetHashCode", "Compute"]
    for rel, text in files.items():
        path = os.path.join(root, *([SRC] if not rel.startswith("tests/") else []), rel)
        os.makedirs(os.path.dirname(path), exist_ok=True)
        with open(path, "w", encoding="utf-8", newline="\n") as f:
            f.write(text)
    source = os.path.join(root, SRC) if scenario != "a moved source root" else os.path.join(tmp, "elsewhere", SRC)
    classes = []
    for cls, h in hits.items():
        filename = cls.split("/")[0] + ".cs"
        body = "".join(f'<method name="{m}" signature="()"><lines><line number="{10 + i}" hits="{h}"/></lines></method>'
                       for i, m in enumerate(methods[cls]))
        lines = "".join(f'<line number="{10 + i}" hits="{h}"/>' for i in range(len(methods[cls])))
        classes.append(f'<class name={quoteattr("NanoQL." + cls)} filename="{filename}"><methods>{body}</methods>'
                       f'<lines>{lines}</lines></class>')
    xml = (f'<coverage><sources><source>{source}</source></sources><packages><package name="{package}"><classes>'
           + "".join(classes) + "</classes></package></packages></coverage>")
    for k in range(reports):
        with open(os.path.join(results, f"coverage.cobertura.{k}.xml"), "w", encoding="utf-8") as f:
            f.write(xml)
    commit = [SRC + name for name in ("Ported.cs", "Kind.cs", "Iter.cs", "Hidden.cs", "Own.cs", "Results.cs")]
    return root, results, commit


def self_test():
    reds = ["no report", "two reports", "no package", "no covered line", "ExcludeFromCodeCoverage",
            "equality-only without its line", "equality members and an uncovered method", "a moved source root"]
    greens = {"an enum absent from the report is listed apart": lambda o: "Kind (enum)" in o,
              "a type covered only through its generated nested class counts": lambda o: "types 2/2" in o,
              "an internal type and an allowlisted file at 0 aren't counted": lambda o: "types 2/2" in o,
              "an equality-only type is listed apart": lambda o: "Results (equality-tested" in o}
    passed = 0
    with tempfile.TemporaryDirectory() as tmp:
        for scenario in reds:
            root, results, commit = synthetic(tmp, scenario)
            errors, _ = check(root, results, commit, commit)
            ok = bool(errors)
            passed += ok
            print(f"  {'red as expected' if ok else 'MISSED'}: {scenario}")
        root, results, commit = synthetic(tmp, "green")
        errors, out = check(root, results, commit, commit)
        text = "\n".join(out)
        for name, test in greens.items():
            ok = not errors and test(text)
            passed += ok
            print(f"  {'green as expected' if ok else 'FAILED'}: {name}")
        if errors:
            print("\n".join("    " + e for e in errors))
    total = len(reds) + len(greens)
    print(f"self-test: {passed}/{total}")
    return 0 if passed == total else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    errors, out = check(root, args[0], changed_files(root), ported_files(root))
    print("\n".join(out))
    for e in errors:
        print("RED " + e)
    print("check-coverage: " + ("passed" if not errors else f"{len(errors)} errors"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
