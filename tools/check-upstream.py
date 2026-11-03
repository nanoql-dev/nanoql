# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The licensing gate's upstream checks, (2) to (7) (docs/porting-playbook.md §8).

Usage: .venv\\Scripts\\python -I tools/check-upstream.py <QuantLib v1.44 worktree> [--boost <Boost 1.90.0 root>]
           [--qlnet <QLNet-v1.13.2 checkout>]

(2) Every .cs, .py, .cpp and .hpp file carries an Upstream line or is listed in tools/no-upstream.txt, not both.
    Every Upstream path exists at the pin, and the file sits in the folder, and declares the namespace, that the name
    table (tools/porting-map.json) gives that path. Every test file's namespace names its folder. A test class's
    "// Replaced:" line names cases of its QuantLib test file that it neither ports nor lists as partial, each with
    a test file that exists under tests/NanoQL.Tests/. Its "// Not ported:" line names cases of its QuantLib test
    file that can never port, each with its reason, which it neither ports nor lists as partial or replaced. Its
    "// Moved:" line names cases of its QuantLib test file, each with a line that holds code of the case's body at the
    pin and a test file that exists under tests/NanoQL.Tests/Divergences/.
(3) The QuantLib copyright lines of the named upstream files appear verbatim and in order, a line holding U+FFFD read
    as its fault-free twin in another file the Upstream line names (one with no twin is a red). Every third-party
    block that tools/third-party-notices.txt lists for a named file appears whole, every line in order, in the file's
    header and in THIRD_PARTY_NOTICES.md, read from the row's source (the QuantLib file, or its fifth column), with its
    licence in the SPDX expression. THIRD_PARTY_NOTICES.md's first fenced block is QuantLib's LICENSE.TXT at the pin,
    verbatim.
(4) The files with a "// Partial:" line are exactly those docs/porting-plan.md lists under "Partial ports".
(5) docs/porting-map.md reproduces (tools/porting-map.py --check); every entry of the name table exists at the pin;
    and no Upstream line names a file that a rule or the name table marks not ported.
(6) The generated tables reproduce: each script under tools/tables/, run on QuantLib at the pin into a temporary
    folder, writes bytes identical to the committed files, and every .bin file under src/NanoQL/ is one they write.
(7) docs/divergences.md reproduces: tools/divergences.py's page, generated from the Divergences/ tests, is the
    committed file byte for byte; a red names the first entry that differs, or the missing page.

A Boost port's Upstream line (boost/<path> @ boost-1.90.0) needs --boost, the root of Boost 1.90.0's release tree
(tools/cxx/build.py's boost_1_90_0, or the release zip checked against its SHA-256). Its pin is the name table's
boostPin and the root's boost/version.hpp gives 109000; every path exists under the root; the file sits in the folder
the table's boost section gives the path's directory, under src/NanoQL/Boost/, in namespace NanoQL.Numerics; Boost's
copyright lines appear verbatim and in order; the SPDX expression is Apache-2.0 AND BSL-1.0; and one Upstream line
names QuantLib paths or Boost paths, never both.

A QLNet port's Upstream line (src/QLNet/<path> or tests/QLNet.Tests/<file> @ QLNet-v1.13.2) needs --qlnet, a checkout
of QLNet at the pin: git gives its HEAD as the tag's commit, 571d01e9ae72feb32f3ee4c4eeff184fa432c5b4, and its status
as clean, since a version can't pin it (QLNet's develop reads 1.13.2 too). Its pin is the name table's qlnetPin; every
path exists under the checkout; a source file sits in the folder under src/NanoQL.Extensions/, with the grouping
subfolder and namespace, that the table's qlnet section gives the path's directory and file, and a test file at the
name the section gives its QLNet test file, under tests/NanoQL.Tests/QLNet/; QLNet's copyright lines appear verbatim
and in order; the SPDX expression is Apache-2.0 AND BSD-3-Clause; one Upstream line names QLNet paths alone; and
THIRD_PARTY_NOTICES.md's fenced block under its "## QLNet" heading is QLNet's LICENSE at the pin, verbatim. A QLNet test
class's "// Not ported:" line names test methods ([Fact] or [Theory]) of its QLNet test file, each with its reason,
which it doesn't port.
"""
import hashlib
import importlib.util
import json
import os
import re
import subprocess
import sys
import tempfile

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import divergences  # noqa: E402
import sources  # noqa: E402

TOOLS = os.path.dirname(os.path.abspath(__file__))
_spec = importlib.util.spec_from_file_location("porting_map", os.path.join(TOOLS, "porting-map.py"))
porting_map = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(porting_map)
SRC = "src/NanoQL/"
TESTS = "tests/NanoQL.Tests/"
TEST_SUITE = TESTS + "TestSuite/"
DIVERGENCES = "Divergences/"
BOOST_SRC = SRC + "Boost/"
BOOST_NAMESPACE = "NanoQL.Numerics"
BOOST_VERSION = "109000"
# QLNet's ports: the extensions' sources and QLNet's test cases, from a checkout of QLNet at the tag's commit.
QLNET_SRC = "src/NanoQL.Extensions/"
QLNET_TESTS = TESTS + "QLNet/"
QLNET_PATHS = ("src/QLNet/", "tests/QLNet.Tests/")
QLNET_COMMIT = "571d01e9ae72feb32f3ee4c4eeff184fa432c5b4"
QLNET_LICENSE = "Apache-2.0 AND BSD-3-Clause"
QLNET_NOTICES = "## QLNet"
CHECKED = (".cs", ".py", ".cpp", ".hpp")
# (6): the scripts that generate QuantLib's data tables (docs/porting-playbook.md §1), each with a write(ql_root,
# out_root) that writes its tables under out_root at their paths in the repository and returns those paths.
TABLES = "tools/tables/"
TABLE = ".bin"
# A table script's output, by its source's digest and the QuantLib root.
_generated = {}
# A QuantLib test file's text with its comments and string contents blanked, by the QuantLib root and its unit.
_blanked = {}


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


def case_body(ql_root, unit, case):
    """The numbers (1-based) of the lines that hold code of a QuantLib test case's body, between its braces, in the
    unit's .cpp at the pin: comments and string contents aren't code. Empty if the file has no such case."""
    key = (os.path.abspath(ql_root), unit)
    if key not in _blanked:
        text = sources.ql_read(ql_root, unit + ".cpp")
        _blanked[key] = porting_map.STRIP.sub(lambda m: re.sub(r"[^\n]", " ", m.group(0)), text)
    code = _blanked[key]
    m = re.search(r"BOOST_(?:AUTO|DATA|FIXTURE)_TEST_CASE\s*\(\s*" + re.escape(case) + r"\b", code)
    if not m:
        return []
    i, depth = m.end(), 1
    while i < len(code) and depth:
        depth += {"(": 1, ")": -1}.get(code[i], 0)
        i += 1
    i = code.find("{", i)
    if i < 0:
        return []
    line, depth, lines = code.count("\n", 0, i) + 1, 1, []
    for ch in code[i + 1:]:
        depth += {"{": 1, "}": -1}.get(ch, 0)
        if not depth:
            break
        if ch == "\n":
            line += 1
        elif not ch.isspace() and (not lines or lines[-1] != line):
            lines.append(line)
    return lines


def qlnet_state(qlnet_root):
    """(HEAD's commit, whether the status is clean) of a QLNet checkout, or (None, the error) when git can't read it."""
    head = subprocess.run(["git", "-C", qlnet_root, "rev-parse", "HEAD"], capture_output=True, text=True)
    status = subprocess.run(["git", "-C", qlnet_root, "status", "--porcelain"], capture_output=True, text=True)
    if head.returncode != 0 or status.returncode != 0:
        return None, (head.stderr or status.stderr).strip()
    return head.stdout.strip(), status.stdout.strip() == ""


def qlnet_cases(text):
    """The test methods of a QLNet test file: each public void method whose attributes, on the lines just before it,
    hold [Fact] or [Theory]. A commented-out attribute doesn't count."""
    cases, attributes = [], []
    for line in text.splitlines():
        stripped = line.strip()
        if stripped.startswith("["):
            attributes.append(stripped)
            continue
        if not stripped or stripped.startswith("//"):
            continue
        m = re.match(r"public\s+void\s+(\w+)\s*\(", stripped)
        if m and any(re.match(r"\[\s*(?:Fact|Theory)\b", a) for a in attributes):
            cases.append(m.group(1))
        attributes = []
    return cases


def upstream_kind(up):
    """Which library an Upstream path names: "boost", "qlnet" or "quantlib"."""
    if up.startswith("boost/"):
        return "boost"
    if up.startswith(QLNET_PATHS):
        return "qlnet"
    return "quantlib"


def boost_version(boost_root):
    try:
        text = sources.read_text(os.path.join(boost_root, "boost", "version.hpp"))
    except OSError:
        return None
    m = re.search(r"^#define BOOST_VERSION (\d+)", text, re.M)
    return m.group(1) if m else None


class Checker:
    def __init__(self, root, ql_root, boost_root=None, qlnet_root=None):
        self.root, self.ql, self.boost, self.qlnet = root, ql_root, boost_root, qlnet_root
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
        notices = self.read_notices()
        self.notices_lines = [ln.strip() for ln in
                              sources.read_text(os.path.join(self.root, "THIRD_PARTY_NOTICES.md")).splitlines()]
        self.counts["blocks"] = 0
        upstream_files, copyright_ok, partial = 0, 0, set()
        if self.boost is not None:
            version = boost_version(self.boost)
            if version != BOOST_VERSION:
                self.error("boost-root", f"{self.boost}: boost/version.hpp gives BOOST_VERSION {version}, "
                                         f"not {BOOST_VERSION} (Boost 1.90.0)")
        self.counts["qlnet"] = 0
        if self.qlnet is not None:
            head, clean = qlnet_state(self.qlnet)
            if head is None:
                self.error("qlnet-root", f"{self.qlnet}: not a git checkout of QLNet ({clean})")
            elif head != QLNET_COMMIT:
                self.error("qlnet-root", f"{self.qlnet}: HEAD is {head}, not {QLNET_COMMIT} "
                                         f"({self.table['qlnetPin']})")
            elif not clean:
                self.error("qlnet-root", f"{self.qlnet}: its status isn't clean (git status --porcelain)")
        for path in files:
            if not path.endswith(CHECKED):
                continue
            header = sources.Header(os.path.join(self.root, path))
            allowed = any(r.match(path) for r in allow)
            if header.partial is not None:
                partial.add(path)
            if path.startswith(TESTS) and path.endswith(".cs"):
                self.check_test_namespace(path, header)
            if header.replaced:
                self.check_replaced(path, header)
            if header.not_ported:
                self.check_not_ported(path, header)
            if header.moved:
                self.check_moved(path, header)
            if not header.upstream:
                if not allowed:
                    self.error("allowlist", f"{path}: no Upstream line, and not listed in tools/no-upstream.txt")
                continue
            if allowed:
                self.error("allowlist", f"{path}: carries an Upstream line but is listed in tools/no-upstream.txt")
            upstream_files += 1
            kinds = {upstream_kind(up) for up in header.upstream}
            if "qlnet" in kinds and len(kinds) > 1:
                others = " and ".join(sorted(kinds - {"qlnet"}))
                self.error("qlnet-mixed", f"{path}: its Upstream line names QLNet and {others} paths; a file ports "
                                          "from one library")
            elif len(kinds) > 1:
                self.error("boost-mixed", f"{path}: its Upstream line names QuantLib and Boost paths; a file ports "
                                          "from one or the other")
            elif kinds == {"boost"}:
                copyright_ok += self.check_boost(path, header)
            elif kinds == {"qlnet"}:
                self.counts["qlnet"] += 1
                copyright_ok += self.check_qlnet(path, header)
            elif self.check_upstream(path, header) and self.check_copyrights(path, header, notices):
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
        self.check_tables(files)
        self.check_divergences()

    def read_notices(self):
        """tools/third-party-notices.txt's rows, as (QuantLib file, first line, last line, SPDX id, the block's
        source). Each row's QuantLib file exists at the pin, a fifth column names a file of the repository, and the
        block reads from its source, each U+FFFD with its twin in the QuantLib file's pair."""
        rows = []
        for line in read_list(self.root, "tools/third-party-notices.txt"):
            row = line.split("\t")
            if len(row) not in (4, 5):
                self.error("third-party", f"tools/third-party-notices.txt: '{line}' has {len(row)} columns, not 4 or 5")
                continue
            rel, first, last, spdx = row[:4]
            source = row[4] if len(row) == 5 else rel
            if not os.path.isfile(os.path.join(self.ql, rel)):
                self.error("third-party", f"tools/third-party-notices.txt: {rel} doesn't exist at the pin")
                continue
            if source != rel and not os.path.isfile(os.path.join(self.root, source)):
                self.error("third-party", f"tools/third-party-notices.txt: {rel}'s source {source} doesn't exist")
                continue
            port = self.units.get(sources.unit_of(rel), [rel]) if source == rel else ()
            _, faults, problem = sources.notice_block(self.root, self.ql, source, first, last, port)
            if problem:
                self.error("third-party", f"tools/third-party-notices.txt: {problem}")
                continue
            twins = "no other file of its pair has the fault-free line" if source == rel else \
                "a fifth-column source has no twin"
            for fault in faults:
                self.error("fffd", f"tools/third-party-notices.txt: {fault} holds U+FFFD in {rel}'s block, and {twins}")
            rows.append((rel, first, last, spdx, source))
        return rows

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

    def check_replaced(self, path, header):
        """A "// Replaced:" line: cases of the class's QuantLib test file, replaced for good by a nanoql test."""
        units = {sources.unit_of(up) for up in header.upstream}
        if not path.startswith(TEST_SUITE) or len(units) != 1:
            self.error("replaced", f"{path}: a // Replaced: line belongs on a TestSuite class naming one test file")
            return
        unit = next(iter(units))
        cases = porting_map.quantlib_side(self.ql).get(unit, {}).get("cases", [])
        methods = set(porting_map.METHOD.findall(header.text))
        partial = {c.strip() for c in (header.partial or "").split(",")}
        for case, test in header.replaced:
            if case is None:
                self.error("replaced", f"{path}: '{test}' on its // Replaced: line isn't '<case> (<test file>)'")
                continue
            if case not in cases:
                self.error("replaced", f"{path}: Replaced names {case}, which isn't a case of {unit}.cpp")
            elif case[0].upper() + case[1:] in methods:
                self.error("replaced", f"{path}: Replaced names {case}, which is also ported")
            elif case in partial:
                self.error("replaced", f"{path}: Replaced names {case}, which is also on its // Partial: line")
            if not os.path.isfile(os.path.join(self.root, TESTS, test)):
                self.error("replaced", f"{path}: Replaced names {test}, which doesn't exist under {TESTS}")

    def check_not_ported(self, path, header):
        """A "// Not ported:" line: cases of the class's QuantLib test file that can never port, each with its reason
        (a case that exercises only code nanoql doesn't port)."""
        if path.startswith(QLNET_TESTS):
            self.check_qlnet_not_ported(path, header)
            return
        units = {sources.unit_of(up) for up in header.upstream}
        if not path.startswith(TEST_SUITE) or len(units) != 1:
            self.error("not-ported-case", f"{path}: a // Not ported: line belongs on a TestSuite class naming one test "
                                          "file")
            return
        unit = next(iter(units))
        cases = porting_map.quantlib_side(self.ql).get(unit, {}).get("cases", [])
        methods = set(porting_map.METHOD.findall(header.text))
        partial = {c.strip() for c in (header.partial or "").split(",")}
        replaced = {c for c, _ in header.replaced if c is not None}
        for case, reason in header.not_ported:
            if case is None:
                self.error("not-ported-case", f"{path}: '{reason}' on its // Not ported: line isn't '<case> "
                                              "(<reason>)'")
                continue
            if case not in cases:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which isn't a case of {unit}.cpp")
            elif case[0].upper() + case[1:] in methods:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which is also ported")
            elif case in partial:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which is also on its // Partial: line")
            elif case in replaced:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which is also on its // Replaced: "
                                              "line")

    def check_qlnet_not_ported(self, path, header):
        """A QLNet test class's "// Not ported:" line: test methods of its QLNet test file, each with its reason, that
        the class doesn't port."""
        if self.qlnet is None:
            self.error("qlnet-root", f"{path}: a QLNet // Not ported: line needs --qlnet <QLNet-v1.13.2 checkout>")
            return
        if len(header.upstream) != 1 or not header.upstream[0].startswith("tests/QLNet.Tests/") or \
                not os.path.isfile(os.path.join(self.qlnet, header.upstream[0])):
            self.error("not-ported-case", f"{path}: a // Not ported: line belongs on a QLNet test class naming one "
                                          "QLNet test file")
            return
        rel = header.upstream[0]
        cases = qlnet_cases(sources.ql_read(self.qlnet, rel))
        methods = set(porting_map.METHOD.findall(header.text))
        for case, reason in header.not_ported:
            if case is None:
                self.error("not-ported-case", f"{path}: '{reason}' on its // Not ported: line isn't '<case> "
                                              "(<reason>)'")
                continue
            if case not in cases:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which isn't a test of {rel}")
            elif case[0].upper() + case[1:] in methods:
                self.error("not-ported-case", f"{path}: Not ported names {case}, which is also ported")

    def check_moved(self, path, header):
        """A "// Moved:" line: checks of the class's QuantLib cases, each at a line of the case's body at the pin, moved
        to a Divergences/ test."""
        units = {sources.unit_of(up) for up in header.upstream}
        if not path.startswith(TEST_SUITE) or len(units) != 1:
            self.error("moved", f"{path}: a // Moved: line belongs on a TestSuite class naming one test file")
            return
        unit = next(iter(units))
        cases = porting_map.quantlib_side(self.ql).get(unit, {}).get("cases", [])
        for case, line, test in header.moved:
            if case is None:
                self.error("moved", f"{path}: '{test}' on its // Moved: line isn't '<case> line <n> "
                                    f"({DIVERGENCES}<file>)'")
                continue
            if case not in cases:
                self.error("moved", f"{path}: Moved names {case}, which isn't a case of {unit}.cpp")
            else:
                body = case_body(self.ql, unit, case)
                if line not in body:
                    span = f"{body[0]}-{body[-1]}" if body else "none"
                    self.error("moved", f"{path}: Moved names {case} line {line}, which holds no code of the case's "
                                        f"body in {unit}.cpp at the pin (its body's lines: {span})")
            if not test.startswith(DIVERGENCES) or not os.path.isfile(os.path.join(self.root, TESTS, test)):
                self.error("moved", f"{path}: Moved names {test}, which doesn't exist under {TESTS}{DIVERGENCES}")

    def check_divergences(self):
        """(7) docs/divergences.md is, byte for byte, the page tools/divergences.py generates."""
        try:
            entries = divergences.build(self.root)
            self.counts["divergences"] = len(entries)
            detail = divergences.difference(self.root, entries)
        except divergences.DivergenceError as e:
            self.counts["divergences"] = 0
            self.error("divergences", f"{divergences.PAGE}: can't be generated: {e}")
            return
        if detail == "missing page":
            self.error("divergences", f"{divergences.PAGE}: missing page")
        elif detail is not None:
            self.error("divergences", f"{divergences.PAGE} differs from a fresh generation (tools/divergences.py): "
                                      f"{detail}")

    def check_boost(self, path, header):
        """A Boost port. Returns whether its header holds."""
        if self.boost is None:
            self.error("boost-root", f"{path}: a Boost Upstream line needs --boost <Boost 1.90.0 root>")
            return False
        ok = True
        if header.pin != self.table["boostPin"]:
            self.error("boost-pin", f"{path}: Upstream pin {header.pin}, expected {self.table['boostPin']}")
            ok = False
        for up in header.upstream:
            if not os.path.isfile(os.path.join(self.boost, up)):
                self.error("boost-path", f"{path}: Upstream path {up} doesn't exist under the Boost root")
                ok = False
        if not ok:
            return False
        folders = set()
        for up in header.upstream:
            directory = up.rsplit("/", 1)[0]
            entry = self.table["boost"].get(directory)
            if entry is None:
                self.error("folder", f"{path}: {directory} isn't in the name table's boost section")
                return False
            folders.add((BOOST_SRC + entry["folder"]).rstrip("/"))
        actual = path.rsplit("/", 1)[0]
        if folders != {actual}:
            self.error("folder", f"{path}: in {actual}, but the name table's boost section gives "
                                 f"{', '.join(sorted(folders))}")
            ok = False
        if header.namespace != BOOST_NAMESPACE:
            self.error("namespace", f"{path}: namespace {header.namespace}, expected {BOOST_NAMESPACE} for a Boost "
                                    "port")
            ok = False
        expected = []
        for up in header.upstream:
            for line in sources.boost_copyrights(self.boost, up):
                if line not in expected:
                    expected.append(line)
        if header.copyrights != expected:
            missing = [c for c in expected if c not in header.copyrights]
            extra = [c for c in header.copyrights if c not in expected]
            detail = f"missing {missing}" if missing else (f"extra {extra}" if extra else "out of order")
            self.error("copyright", f"{path}: Boost copyright lines differ from the upstream files' ({detail})")
            ok = False
        if header.nanoql is None:
            self.error("copyright", f"{path}: no 'SPDX-FileCopyrightText: <year> The nanoql Authors' line")
            ok = False
        if header.license != "Apache-2.0 AND BSL-1.0":
            self.error("copyright", f"{path}: SPDX-License-Identifier '{header.license}', expected Apache-2.0 AND "
                                    "BSL-1.0")
            ok = False
        return ok

    def check_qlnet(self, path, header):
        """A QLNet port (QLNet's route, in the Boost ports' form). Returns whether its header holds."""
        if self.qlnet is None:
            self.error("qlnet-root", f"{path}: a QLNet Upstream line needs --qlnet <QLNet-v1.13.2 checkout>")
            return False
        ok = True
        if header.pin != self.table["qlnetPin"]:
            self.error("qlnet-pin", f"{path}: Upstream pin {header.pin}, expected {self.table['qlnetPin']}")
            ok = False
        for up in header.upstream:
            if not os.path.isfile(os.path.join(self.qlnet, up)):
                self.error("qlnet-path", f"{path}: Upstream path {up} doesn't exist under the QLNet checkout")
                ok = False
        if not ok:
            return False
        section = self.table["qlnet"]
        if path.startswith(QLNET_TESTS):
            entry = section.get(header.upstream[0]) if len(header.upstream) == 1 else None
            if entry is None or "test" not in entry:
                self.error("folder", f"{path}: a QLNet test file names one QLNet test file of the name table's qlnet "
                                     f"section, not {header.upstream}")
                return False
            expected = f"{QLNET_TESTS}{entry['test']}.cs"
            if path != expected:
                self.error("folder", f"{path}: the name table's qlnet section gives {expected}")
                return False
        elif path.startswith(QLNET_SRC):
            folders, namespaces = set(), set()
            for up in header.upstream:
                directory = up.rsplit("/", 1)[0]
                entry = section.get(directory)
                if entry is None or "folder" not in entry:
                    self.error("folder", f"{path}: {directory} isn't a directory of the name table's qlnet section")
                    return False
                folder = (QLNET_SRC + entry["folder"]).rstrip("/")
                subfolder = section.get(up, {}).get("subfolder")
                if subfolder:
                    folder += "/" + subfolder
                folders.add(folder)
                namespaces.add(entry["namespace"])
            actual = path.rsplit("/", 1)[0]
            if folders != {actual}:
                self.error("folder", f"{path}: in {actual}, but the name table's qlnet section gives "
                                     f"{', '.join(sorted(folders))}")
                ok = False
            if namespaces != {header.namespace}:
                self.error("namespace", f"{path}: namespace {header.namespace}, but the name table's qlnet section "
                                        f"gives {', '.join(sorted(namespaces))}")
                ok = False
        else:
            self.error("folder", f"{path}: a QLNet Upstream line outside {QLNET_SRC} and {QLNET_TESTS}")
            return False
        expected = []
        for up in header.upstream:
            for line in sources.upstream_copyrights(self.qlnet, up):
                if line not in expected:
                    expected.append(line)
        if header.copyrights != expected:
            missing = [c for c in expected if c not in header.copyrights]
            extra = [c for c in header.copyrights if c not in expected]
            detail = f"missing {missing}" if missing else (f"extra {extra}" if extra else "out of order")
            self.error("copyright", f"{path}: QLNet copyright lines differ from the upstream files' ({detail})")
            ok = False
        if header.nanoql is None:
            self.error("copyright", f"{path}: no 'SPDX-FileCopyrightText: <year> The nanoql Authors' line")
            ok = False
        if header.license != QLNET_LICENSE:
            self.error("copyright", f"{path}: SPDX-License-Identifier '{header.license}', expected {QLNET_LICENSE}")
            ok = False
        return ok

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
        expected, faults = sources.port_copyrights(self.ql, header.upstream)
        if faults:
            for fault in faults:
                self.error("fffd", f"{path}: {fault} holds U+FFFD in a copyright line, and no other file its Upstream "
                                   "line names has the fault-free line")
            return False
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
        ok = True
        for rel, first, last, spdx, source in notices:
            if rel not in header.upstream:
                continue
            self.counts["blocks"] += 1
            port = header.upstream if source == rel else ()
            block, faults, _ = sources.notice_block(self.root, self.ql, source, first, last, port)
            twins = "no other file its Upstream line names has the fault-free line" if source == rel else \
                "a fifth-column source has no twin"
            for fault in faults:
                self.error("fffd", f"{path}: {fault} holds U+FFFD in {rel}'s third-party block, and {twins}")
                ok = False
            if faults:
                continue
            at, matched = sources.find_run(header.lines, block)
            if at is None:
                self.error("third-party", f"{path}: doesn't carry the third-party block of {rel} whole and in order; "
                                          f"the block's line {matched + 1} differs: '{block[matched]}'")
                ok = False
            at, matched = sources.find_run(self.notices_lines, block)
            if at is None:
                self.error("third-party", f"THIRD_PARTY_NOTICES.md doesn't carry the third-party block of {rel} (for "
                                          f"{path}) whole and in order; the block's line {matched + 1} differs: "
                                          f"'{block[matched]}'")
                ok = False
            if spdx not in licence.split(" AND "):
                self.error("third-party", f"{path}: the licence {spdx} of {rel}'s third-party block isn't in its SPDX "
                                          "expression")
                ok = False
        return ok

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
        if self.qlnet is None:
            return
        try:
            heading = lines.index(QLNET_NOTICES)
            start = lines.index("```text", heading + 1)
            end = lines.index("```", start + 1)
        except ValueError:
            self.error("notices", f"THIRD_PARTY_NOTICES.md has no ```text fenced block under '{QLNET_NOTICES}'")
            return
        block = "\n".join(lines[start + 1:end]) + "\n"
        licence = sources.ql_read(self.qlnet, "LICENSE").replace("\r\n", "\n")
        if block != licence:
            self.error("notices", f"THIRD_PARTY_NOTICES.md's fenced block under '{QLNET_NOTICES}' isn't QLNet's "
                                  "LICENSE at the pin")

    def check_table(self):
        entries = []
        for section in ("files", "tests", "replaced", "notPorted"):
            entries += list(self.table[section])
        absent = [u for u in entries if not self.unit_exists(u)]
        absent += [d for d in self.table["directories"] if not os.path.isdir(os.path.join(self.ql, d))]
        for u in absent:
            self.error("table-absent", f"tools/porting-map.json: {u} doesn't exist at the pin")
        if self.boost is not None:
            for d in self.table["boost"]:
                if not os.path.isdir(os.path.join(self.boost, d)):
                    self.error("table-absent", f"tools/porting-map.json: {d} doesn't exist under the Boost root")
        if self.qlnet is not None:
            for entry, value in self.table["qlnet"].items():
                exists = os.path.isdir if "folder" in value else os.path.isfile
                if not exists(os.path.join(self.qlnet, entry)):
                    self.error("table-absent", f"tools/porting-map.json: {entry} doesn't exist under the QLNet "
                                               "checkout")
        self.counts["table"] = (len(entries) + len(self.table["directories"]) - len(absent),
                                len(entries) + len(self.table["directories"]))
        self.counts["table-lists"] = len(self.table["replaced"]) + len(self.table["notPorted"])

    def check_map(self):
        if not porting_map.reproduces(self.root, self.ql):
            self.error("map", "docs/porting-map.md differs from a fresh generation (tools/porting-map.py)")

    def generate_tables(self, script):
        """A table script's tables, {path in the repository: bytes}, written into a temporary folder and read back."""
        path = os.path.join(self.root, script)
        with open(path, "rb") as f:
            key = (hashlib.sha256(f.read()).hexdigest(), os.path.abspath(self.ql))
        if key not in _generated:
            spec = importlib.util.spec_from_file_location("table_" + os.path.basename(script)[:-3], path)
            module = importlib.util.module_from_spec(spec)
            spec.loader.exec_module(module)
            out = {}
            with tempfile.TemporaryDirectory() as tmp:
                for rel in module.write(self.ql, tmp):
                    with open(os.path.join(tmp, *rel.split("/")), "rb") as f:
                        out[rel] = f.read()
            _generated[key] = out
        return _generated[key]

    def check_tables(self, files):
        """(6) Regenerating the tables reproduces the committed files, byte for byte, and no other .bin file sits under
        src/NanoQL/."""
        scripts = [p for p in files if p.startswith(TABLES) and p.endswith(".py")]
        generated = {}
        for script in scripts:
            try:
                for rel, data in self.generate_tables(script).items():
                    generated[rel] = (script, data)
            except SystemExit as e:
                self.error("tables", f"{script}: stopped generating its tables ({e})")
        same, size = 0, 0
        for rel, (script, data) in sorted(generated.items()):
            path = os.path.join(self.root, *rel.split("/"))
            if rel not in files or not os.path.isfile(path):
                self.error("tables", f"{rel}: {script} generates it, but the tree doesn't hold it")
                continue
            with open(path, "rb") as f:
                committed = f.read()
            if committed != data:
                at = next((i for i, (a, b) in enumerate(zip(committed, data)) if a != b), min(len(committed), len(data)))
                self.error("tables", f"{rel}: differs from a fresh generation by {script} (first at byte {at}; "
                                     f"{len(committed)} bytes, {len(data)} generated)")
                continue
            same += 1
            size += len(data)
        for rel in files:
            if rel.startswith(SRC) and rel.endswith(TABLE) and rel not in generated:
                self.error("tables", f"{rel}: a table under {SRC} that no script under {TABLES} generates")
        self.counts["tables"] = (same, len(generated), size, len(scripts))


def report(checker):
    c = checker.counts
    if checker.boost is not None:
        print(f"Boost root: {checker.boost}")
    if checker.qlnet is not None:
        print(f"QLNet checkout: {checker.qlnet} ({checker.table['qlnetPin']}); QLNet ports {c['qlnet']}, name table "
              f"qlnet entries {len(checker.table['qlnet'])}")
    print(f"(2) files with an Upstream line: {c['upstream']}; every other .cs, .py, .cpp and .hpp file is allowlisted")
    print(f"(3) copyright lines verbatim: {c['copyright']}/{c['upstream']} files; third-party blocks whole: "
          f"{c['blocks']} (file, block) pairs, each in the file's header and in THIRD_PARTY_NOTICES.md")
    print("(3) THIRD_PARTY_NOTICES.md carries LICENSE.TXT verbatim"
          + (", and QLNet's LICENSE under its '## QLNet' heading" if checker.qlnet is not None else ""))
    print(f"(4) partial files: {c['partial']}, listed in docs/porting-plan.md: {c['partial']}")
    print(f"(5) table entries at the pin: {c['table'][0]}/{c['table'][1]} "
          f"(replaced and not-ported: {c['table-lists']}); porting map reproduces")
    print(f"(6) generated tables reproducing: {c['tables'][0]}/{c['tables'][1]} files, {c['tables'][2]:,} bytes, from "
          f"{c['tables'][3]} scripts under {TABLES}")
    print(f"(7) the divergence page reproduces, {c['divergences']} entries")
    for code, message in checker.errors:
        print(f"RED [{code}] {message}")
    print("check-upstream: " + ("passed" if not checker.errors else f"{len(checker.errors)} errors"))


def main(argv):
    # Notices quote names such as "Jäckel" and "©", which a console's code page may not hold.
    sys.stdout.reconfigure(errors="backslashreplace")
    argv = list(argv)
    boost_root = None
    if "--boost" in argv:
        i = argv.index("--boost")
        if i + 1 >= len(argv):
            print(__doc__)
            return 2
        boost_root = argv.pop(i + 1)
        argv.pop(i)
    qlnet_root = None
    if "--qlnet" in argv:
        i = argv.index("--qlnet")
        if i + 1 >= len(argv):
            print(__doc__)
            return 2
        qlnet_root = argv.pop(i + 1)
        argv.pop(i)
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(TOOLS)
    checker = Checker(root, args[0], boost_root, qlnet_root)
    checker.run()
    report(checker)
    return 1 if checker.errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
