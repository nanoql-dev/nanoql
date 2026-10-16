# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The licensing gate's upstream checks, (2) to (5) (docs/porting-playbook.md §8).

Usage: .venv\\Scripts\\python -I tools/check-upstream.py <QuantLib v1.44 worktree> [--self-test]

(2) Every .cs and .py file carries an Upstream line or is listed in tools/no-upstream.txt, not both. Every
    Upstream path exists at the pin, and the file sits in the folder, and declares the namespace, that the name table
    (tools/porting-map.json) gives that path. Every test file's namespace names its folder.
(3) The QuantLib copyright lines of the named upstream files appear verbatim and in order, with every third-party
    block tools/third-party-notices.txt lists for them, and its licence in the SPDX expression. THIRD_PARTY_NOTICES.md's
    fenced block is QuantLib's LICENSE.TXT at the pin, verbatim.
(4) The files with a "// Partial:" line are exactly those docs/porting-plan.md lists under "Partial ports".
(5) docs/porting-map.md reproduces (tools/porting-map.py --check); every entry of the name table exists at the pin;
    and no Upstream line names a file that a rule or the name table marks not ported.

--self-test copies the tree to a temporary folder, adds a ported probe file, and applies one perturbation per check
to a fresh copy each time; it exits 0 only if every perturbation is caught by the check it targets.
"""
import importlib.util
import os
import re
import shutil
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("porting_map", os.path.join(TOOLS, "porting-map.py"))
porting_map = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(porting_map)
SRC = "src/NanoQL/"
TESTS = "tests/NanoQL.Tests/"
TEST_SUITE = TESTS + "TestSuite/"


def glob_regex(pattern):
    out = ""
    i = 0
    while i < len(pattern):
        if pattern.startswith("**/", i):
            out += "(?:.*/)?"
            i += 3
        elif pattern.startswith("**", i):
            out += ".*"
            i += 2
        elif pattern[i] == "*":
            out += "[^/]*"
            i += 1
        elif pattern[i] == "?":
            out += "[^/]"
            i += 1
        else:
            out += re.escape(pattern[i])
            i += 1
    return re.compile(out + r"\Z")


def read_list(root, rel):
    with open(os.path.join(root, rel), encoding="utf-8") as f:
        return [ln.rstrip("\n") for ln in f if ln.strip() and not ln.lstrip().startswith("#")]


def listed_partials(root):
    """The paths docs/porting-plan.md lists under its "Partial ports" heading."""
    text = sources.read_text(os.path.join(root, "docs", "porting-plan.md"))
    m = re.search(r"^## Partial ports\s*$(.*?)(?=^## |\Z)", text, re.M | re.S)
    if not m:
        return None
    return {p for p in re.findall(r"^\s*[-*]\s+`([^`]+)`", m.group(1), re.M)}


class Checker:
    def __init__(self, root, ql_root):
        self.root, self.ql = root, ql_root
        self.errors = []  # (code, message)
        self.table = sources.load_table(root)
        self.units = sources.ql_units(ql_root)
        self.counts = {}

    def error(self, code, message):
        self.errors.append((code, message))

    def unit_exists(self, unit):
        return unit in self.units

    def not_ported_reason(self, unit):
        if unit in self.table["notPorted"]:
            return self.table["notPorted"][unit]
        files = self.units.get(unit)
        return sources.rule_status(self.ql, unit, files) if files else None

    def run(self):
        files = sources.git_files(self.root)
        allow = [glob_regex(p.strip()) for p in read_list(self.root, "tools/no-upstream.txt")]
        notices = [ln.split("\t") for ln in read_list(self.root, "tools/third-party-notices.txt")]
        upstream_files, copyright_ok, partial = 0, 0, set()
        for path in files:
            if not path.endswith((".cs", ".py")):
                continue
            header = sources.Header(os.path.join(self.root, path))
            allowed = any(r.match(path) for r in allow)
            if header.partial is not None:
                partial.add(path)
            if path.startswith(TESTS) and path.endswith(".cs"):
                self.check_test_namespace(path, header)
            if not header.upstream:
                if not allowed:
                    self.error("allowlist", f"{path}: no Upstream line, and not listed in tools/no-upstream.txt")
                continue
            if allowed:
                self.error("allowlist", f"{path}: carries an Upstream line but is listed in tools/no-upstream.txt")
            upstream_files += 1
            if self.check_upstream(path, header) and self.check_copyrights(path, header, notices):
                copyright_ok += 1
        self.counts["upstream"] = upstream_files
        self.counts["copyright"] = copyright_ok
        listed = listed_partials(self.root)
        if listed is None:
            self.error("partial", "docs/porting-plan.md has no '## Partial ports' section")
            listed = set()
        for p in sorted(partial - listed):
            self.error("partial", f"{p}: has a // Partial: line but docs/porting-plan.md doesn't list it")
        for p in sorted(listed - partial):
            self.error("partial", f"{p}: listed in docs/porting-plan.md but has no // Partial: line")
        self.counts["partial"] = len(partial)
        self.check_notices()
        self.check_table()
        self.check_map()

    def check_test_namespace(self, path, header):
        folder = path[len(TESTS):].split("/")[0]
        if "/" not in path[len(TESTS):]:
            return
        expected = f"NanoQL.Tests.{folder}"
        if header.namespace is None:
            # A class outside every folder namespace would escape the Oracle trait's placement test too.
            self.error("test-namespace", f"{path}: declares no namespace, expected {expected} for its folder")
        elif header.namespace != expected:
            self.error("test-namespace", f"{path}: namespace {header.namespace}, expected {expected} for its folder")

    def check_upstream(self, path, header):
        ok = True
        if header.pin != self.table["pin"]:
            self.error("path-absent", f"{path}: Upstream pin {header.pin}, expected {self.table['pin']}")
            ok = False
        for up in header.upstream:
            if not os.path.isfile(os.path.join(self.ql, up)):
                self.error("path-absent", f"{path}: Upstream path {up} doesn't exist at the pin")
                ok = False
                continue
            reason = self.not_ported_reason(sources.unit_of(up))
            if reason:
                self.error("not-ported", f"{path}: Upstream names {up}, which is not ported ({reason})")
                ok = False
        if not ok:
            return False
        units = {sources.unit_of(up) for up in header.upstream}
        if path.startswith(TEST_SUITE):
            return self.check_test_file(path, units)
        if not path.startswith(SRC):
            self.error("folder", f"{path}: an Upstream line outside {SRC} and {TEST_SUITE}")
            return False
        folders, namespaces = set(), set()
        for unit in units:
            directory = unit.rsplit("/", 1)[0]
            d = self.table["directories"].get(directory)
            if d is None or not unit.startswith("ql/"):
                self.error("folder", f"{path}: {unit}'s directory isn't in the name table")
                return False
            entry = self.table["files"].get(unit)
            if entry is None and unit not in self.table["replaced"]:
                self.error("folder", f"{path}: {unit} isn't in the name table's files")
                return False
            folder = (SRC + d["folder"]).rstrip("/")
            if entry and entry.get("subfolder"):
                folder += "/" + sources.pascal(entry["words"])
            folders.add(folder)
            namespaces.add(d["namespace"])
        actual = path.rsplit("/", 1)[0]
        if folders != {actual}:
            self.error("folder", f"{path}: in {actual}, but the name table gives {', '.join(sorted(folders))}")
            ok = False
        if header.namespace is not None and namespaces != {header.namespace}:
            self.error("namespace", f"{path}: namespace {header.namespace}, but the name table gives "
                                    f"{', '.join(sorted(namespaces))}")
            ok = False
        return ok

    def check_test_file(self, path, units):
        if len(units) != 1 or not next(iter(units)).startswith("test-suite/"):
            self.error("folder", f"{path}: a TestSuite file names one test-suite file, not {sorted(units)}")
            return False
        unit = next(iter(units))
        entry = self.table["tests"].get(unit)
        if entry is None:
            self.error("folder", f"{path}: {unit} isn't in the name table's tests")
            return False
        expected = f"{TEST_SUITE}{sources.pascal(entry['words'])}Tests.cs"
        if path != expected:
            self.error("folder", f"{path}: the name table gives {expected}")
            return False
        return True

    def check_copyrights(self, path, header, notices):
        expected = []
        for up in header.upstream:
            for line in sources.upstream_copyrights(self.ql, up):
                if line not in expected:
                    expected.append(line)
        if header.copyrights != expected:
            missing = [c for c in expected if c not in header.copyrights]
            extra = [c for c in header.copyrights if c not in expected]
            detail = f"missing {missing}" if missing else (f"extra {extra}" if extra else "out of order")
            self.error("copyright", f"{path}: QuantLib copyright lines differ from the upstream files' ({detail})")
            return False
        if header.nanoql is None or not re.fullmatch(r"\d{4} The nanoql Authors", header.nanoql):
            self.error("copyright", f"{path}: no 'SPDX-FileCopyrightText: <year> The nanoql Authors' line")
            return False
        licence = header.license or ""
        if not re.fullmatch(r"Apache-2\.0 AND BSD-3-Clause(?: AND [\w.+-]+)*", licence):
            self.error("copyright", f"{path}: SPDX-License-Identifier '{licence}', expected Apache-2.0 AND BSD-3-Clause")
            return False
        for row in notices:
            if len(row) != 4 or row[0] not in header.upstream:
                continue
            _, first, last, spdx = row
            body = "\n".join(header.lines)
            if first.strip() not in body or last.strip() not in body or spdx not in licence.split(" AND "):
                self.error("copyright", f"{path}: the third-party block of {row[0]} or its licence {spdx} is missing")
                return False
        return True

    def check_notices(self):
        lines = sources.read_text(os.path.join(self.root, "THIRD_PARTY_NOTICES.md")).splitlines()
        try:
            start = lines.index("```text")
            end = lines.index("```", start + 1)
        except ValueError:
            self.error("notices", "THIRD_PARTY_NOTICES.md has no ```text fenced block")
            return
        block = "\n".join(lines[start + 1:end]) + "\n"
        licence = sources.ql_read(self.ql, "LICENSE.TXT").replace("\r\n", "\n")
        if block != licence:
            self.error("notices", "THIRD_PARTY_NOTICES.md's fenced block isn't QuantLib's LICENSE.TXT at the pin")

    def check_table(self):
        entries = []
        for section in ("files", "tests", "replaced", "notPorted"):
            entries += list(self.table[section])
        absent = [u for u in entries if not self.unit_exists(u)]
        absent += [d for d in self.table["directories"] if not os.path.isdir(os.path.join(self.ql, d))]
        for u in absent:
            self.error("table-absent", f"tools/porting-map.json: {u} doesn't exist at the pin")
        self.counts["table"] = (len(entries) + len(self.table["directories"]) - len(absent),
                                len(entries) + len(self.table["directories"]))
        self.counts["table-lists"] = len(self.table["replaced"]) + len(self.table["notPorted"])

    def check_map(self):
        if not porting_map.reproduces(self.root, self.ql):
            self.error("map", "docs/porting-map.md differs from a fresh generation (tools/porting-map.py)")


def report(checker):
    c = checker.counts
    print(f"(2) files with an Upstream line: {c['upstream']}; every other .cs and .py file is allowlisted")
    print(f"(3) copyright lines verbatim: {c['copyright']}/{c['upstream']} files")
    print("(3) THIRD_PARTY_NOTICES.md carries LICENSE.TXT verbatim")
    print(f"(4) partial files: {c['partial']}, listed in docs/porting-plan.md: {c['partial']}")
    print(f"(5) table entries at the pin: {c['table'][0]}/{c['table'][1]} "
          f"(replaced and not-ported: {c['table-lists']}); porting map reproduces")
    for code, message in checker.errors:
        print(f"RED [{code}] {message}")
    print("check-upstream: " + ("passed" if not checker.errors else f"{len(checker.errors)} errors"))


PROBE = "src/NanoQL/Time/SelfTestProbe.cs"
PROBE_TEST = "tests/NanoQL.Tests/Conventions/SelfTestProbeTests.cs"


def probe_text(ql_root, upstream=("ql/time/weekday.hpp", "ql/time/weekday.cpp"), namespace="NanoQL.Time",
               name="SelfTestProbe"):
    lines = []
    for up in upstream:
        for c in sources.upstream_copyrights(ql_root, up):
            if "// SPDX-FileCopyrightText: " + c not in lines:
                lines.append("// SPDX-FileCopyrightText: " + c)
    return "\n".join(lines + [
        "// SPDX-FileCopyrightText: 2026 The nanoql Authors",
        "// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause",
        f"// Upstream: {' '.join(upstream)} @ v1.44",
        "// Ported to C# from QuantLib and modified by the nanoql Authors.",
        "",
        f"namespace {namespace};",
        "",
        f"internal static class {name}",
        "{",
        "}",
        ""])


def edit(root, rel, old, new):
    path = os.path.join(root, rel)
    text = sources.read_text(path)
    assert old in text, (rel, old)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text.replace(old, new, 1))


def write(root, rel, text):
    path = os.path.join(root, rel)
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)


def append(root, rel, text):
    with open(os.path.join(root, rel), "a", encoding="utf-8", newline="\n") as f:
        f.write(text)


def perturbations(ql_root):
    """(name, the error code it must raise, a text that error must contain, how to apply it)."""
    first_copyright = "// SPDX-FileCopyrightText: " + sources.upstream_copyrights(ql_root, "ql/time/weekday.hpp")[0]
    orphan = ("// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0\n"
              "\nnamespace NanoQL;\n\ninternal static class SelfTestOrphan\n{\n}\n")

    def move(root):
        os.makedirs(os.path.join(root, "src/NanoQL/Coupons"), exist_ok=True)
        shutil.move(os.path.join(root, PROBE), os.path.join(root, "src/NanoQL/Coupons/SelfTestProbe.cs"))

    def not_ported(root):
        text = sources.read_text(os.path.join(root, PROBE))
        text = re.sub(r"// Upstream: .*", "// Upstream: ql/qldefines.hpp @ v1.44", text)
        write(root, PROBE, text)

    return [
        ("missing Upstream line", "allowlist", "no Upstream line, and not listed",
         lambda r: edit(r, PROBE, "// Upstream: ql/time/weekday.hpp ql/time/weekday.cpp @ v1.44\n", "")),
        ("a path absent at the pin", "path-absent", "doesn't exist at the pin",
         lambda r: edit(r, PROBE, "ql/time/weekday.cpp @", "ql/time/weekdays.cpp @")),
        ("an Upstream pin at another tag", "path-absent", "Upstream pin v1.43",
         lambda r: edit(r, PROBE, "@ v1.44", "@ v1.43")),
        ("wrong folder", "folder", "but the name table gives", move),
        ("wrong namespace", "namespace", "namespace NanoQL.Coupons, but the name table gives",
         lambda r: edit(r, PROBE, "namespace NanoQL.Time;", "namespace NanoQL.Coupons;")),
        ("a misnamed TestSuite file", "folder", "the name table gives tests/NanoQL.Tests/TestSuite/DatesTests.cs",
         lambda r: write(r, "tests/NanoQL.Tests/TestSuite/SelfTestWrongTests.cs",
                         probe_text(ql_root, ("test-suite/dates.cpp",), "NanoQL.Tests.TestSuite", "SelfTestWrongTests"))),
        ("a missing copyright line", "copyright", "copyright lines differ",
         lambda r: edit(r, PROBE, first_copyright + "\n", "")),
        ("a missing nanoql line", "copyright", "The nanoql Authors' line",
         lambda r: edit(r, PROBE, "// SPDX-FileCopyrightText: 2026 The nanoql Authors\n", "")),
        ("a bare SPDX expression", "copyright", "expected Apache-2.0 AND BSD-3-Clause",
         lambda r: edit(r, PROBE, "Apache-2.0 AND BSD-3-Clause", "Apache-2.0")),
        ("a missing third-party block", "copyright", "third-party block",
         lambda r: append(r, "tools/third-party-notices.txt",
                          "ql/time/weekday.hpp\tBEGIN SELF-TEST BLOCK\tEND SELF-TEST BLOCK\tMIT\n")),
        ("an unlisted // Partial: line", "partial", "doesn't list it",
         lambda r: edit(r, PROBE, "// Ported to C#", "// Partial: io::long_weekday\n// Ported to C#")),
        ("a listed file with no // Partial: line", "partial", "but has no // Partial: line",
         lambda r: append(r, "docs/porting-plan.md", "\n- `" + PROBE + "`: io::long_weekday\n")),
        ("a stale map", "map", "differs from a fresh generation",
         lambda r: edit(r, "docs/porting-map.md", "# Porting map", "# Porting map (stale)")),
        ("a table entry absent at the pin", "table-absent", "ql/nosuchfile doesn't exist at the pin",
         lambda r: edit(r, "tools/porting-map.json", '"replaced": {\n', '"replaced": {\n    "ql/nosuchfile": "x",\n')),
        ("an Upstream line naming a not-ported file", "not-ported", "which is not ported", not_ported),
        ("a file with no upstream missing from the allowlist", "allowlist", "SelfTestOrphan.cs: no Upstream line",
         lambda r: write(r, "src/NanoQL/SelfTestOrphan.cs", orphan)),
        ("a file with an Upstream line on the allowlist", "allowlist", "carries an Upstream line but is listed",
         lambda r: append(r, "tools/no-upstream.txt", PROBE + "\n")),
        ("THIRD_PARTY_NOTICES.md no longer verbatim", "notices", "isn't QuantLib's LICENSE.TXT",
         lambda r: edit(r, "THIRD_PARTY_NOTICES.md", "RiskMap srl", "RiskMap s.r.l.")),
        ("a test file in another folder's namespace", "test-namespace", "namespace NanoQL.Tests.Vectors, expected",
         lambda r: edit(r, PROBE_TEST, "namespace NanoQL.Tests.Conventions;", "namespace NanoQL.Tests.Vectors;")),
        ("a test file with no namespace", "test-namespace", "declares no namespace",
         lambda r: edit(r, PROBE_TEST, "namespace NanoQL.Tests.Conventions;\n", "")),
    ]


def self_test(root, ql_root):
    files = sources.git_files(root)
    with tempfile.TemporaryDirectory() as tmp:
        base = os.path.join(tmp, "base")
        for rel in files:
            os.makedirs(os.path.dirname(os.path.join(base, rel)), exist_ok=True)
            shutil.copyfile(os.path.join(root, rel), os.path.join(base, rel))
        subprocess.run(["git", "init", "-q", base], check=True)
        write(base, PROBE, probe_text(ql_root))
        write(base, PROBE_TEST, "// SPDX-FileCopyrightText: 2026 The nanoql Authors\n"
                                "// SPDX-License-Identifier: Apache-2.0\n\nnamespace NanoQL.Tests.Conventions;\n\n"
                                "internal static class SelfTestProbeTests\n{\n}\n")
        porting_map.main([ql_root, "--root", base])
        baseline = Checker(base, ql_root)
        baseline.run()
        if baseline.errors:
            report(baseline)
            print("self-test: the unperturbed copy isn't green")
            return 1
        caught = 0
        cases = perturbations(ql_root)
        for i, (name, code, text, apply) in enumerate(cases):
            copy = os.path.join(tmp, f"p{i}")
            shutil.copytree(base, copy)
            apply(copy)
            checker = Checker(copy, ql_root)
            checker.run()
            hit = any(c == code and text in message for c, message in checker.errors)
            caught += hit
            print(f"  {'caught' if hit else 'MISSED'}: {name} [{code}: {text}]")
        print(f"self-test: {caught}/{len(cases)} perturbations caught")
        return 0 if caught == len(cases) else 1


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(TOOLS)
    if "--self-test" in argv:
        return self_test(root, args[0])
    checker = Checker(root, args[0])
    checker.run()
    report(checker)
    return 1 if checker.errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
