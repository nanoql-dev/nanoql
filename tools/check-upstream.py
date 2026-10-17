# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The licensing gate's upstream checks, (2) to (5) (docs/porting-playbook.md §8).

Usage: .venv\\Scripts\\python -I tools/check-upstream.py <QuantLib v1.44 worktree> [--boost <Boost 1.90.0 root>]
       [--self-test]

(2) Every .cs, .py, .cpp and .hpp file carries an Upstream line or is listed in tools/no-upstream.txt, not both.
    Every Upstream path exists at the pin, and the file sits in the folder, and declares the namespace, that the name
    table (tools/porting-map.json) gives that path. Every test file's namespace names its folder. A test class's
    "// Replaced:" line names cases of its QuantLib test file that it neither ports nor lists as partial, each with
    a test file that exists under tests/NanoQL.Tests/.
(3) The QuantLib copyright lines of the named upstream files appear verbatim and in order, a line holding U+FFFD read
    as its fault-free twin in another file the Upstream line names (one with no twin is a red). Every third-party
    block that tools/third-party-notices.txt lists for a named file appears whole, every line in order, in the file's
    header and in THIRD_PARTY_NOTICES.md, read from the row's source (the QuantLib file, or its fifth column), with its
    licence in the SPDX expression. THIRD_PARTY_NOTICES.md's first fenced block is QuantLib's LICENSE.TXT at the pin,
    verbatim.
(4) The files with a "// Partial:" line are exactly those docs/porting-plan.md lists under "Partial ports".
(5) docs/porting-map.md reproduces (tools/porting-map.py --check); every entry of the name table exists at the pin;
    and no Upstream line names a file that a rule or the name table marks not ported.

A Boost port's Upstream line (boost/<path> @ boost-1.90.0) needs --boost, the root of Boost 1.90.0's release tree
(tools/cxx/build.py's boost_1_90_0, or the release zip checked against its SHA-256). Its pin is the name table's
boostPin and the root's boost/version.hpp gives 109000; every path exists under the root; the file sits in the folder
the table's boost section gives the path's directory, under src/NanoQL/Boost/, in namespace NanoQL.Numerics; Boost's
copyright lines appear verbatim and in order; the SPDX expression is Apache-2.0 AND BSL-1.0; and one Upstream line
names QuantLib paths or Boost paths, never both.

--self-test (which needs --boost) copies the tree to a temporary folder, adds a ported probe file and a Boost probe,
and applies one perturbation per check to a fresh copy each time; it exits 0 only if every perturbation is caught by
the check it targets.
"""
import importlib.util
import json
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
BOOST_SRC = SRC + "Boost/"
BOOST_NAMESPACE = "NanoQL.Numerics"
BOOST_VERSION = "109000"
CHECKED = (".cs", ".py", ".cpp", ".hpp")


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


def boost_version(boost_root):
    try:
        text = sources.read_text(os.path.join(boost_root, "boost", "version.hpp"))
    except OSError:
        return None
    m = re.search(r"^#define BOOST_VERSION (\d+)", text, re.M)
    return m.group(1) if m else None


class Checker:
    def __init__(self, root, ql_root, boost_root=None):
        self.root, self.ql, self.boost = root, ql_root, boost_root
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
            if not header.upstream:
                if not allowed:
                    self.error("allowlist", f"{path}: no Upstream line, and not listed in tools/no-upstream.txt")
                continue
            if allowed:
                self.error("allowlist", f"{path}: carries an Upstream line but is listed in tools/no-upstream.txt")
            upstream_files += 1
            boost = [up.startswith("boost/") for up in header.upstream]
            if any(boost) and not all(boost):
                self.error("boost-mixed", f"{path}: its Upstream line names QuantLib and Boost paths; a file ports "
                                          "from one or the other")
            elif all(boost):
                copyright_ok += self.check_boost(path, header)
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

    def check_boost(self, path, header):
        """A Boost port (D19). Returns whether its header holds."""
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
        self.counts["table"] = (len(entries) + len(self.table["directories"]) - len(absent),
                                len(entries) + len(self.table["directories"]))
        self.counts["table-lists"] = len(self.table["replaced"]) + len(self.table["notPorted"])

    def check_map(self):
        if not porting_map.reproduces(self.root, self.ql):
            self.error("map", "docs/porting-map.md differs from a fresh generation (tools/porting-map.py)")


def report(checker):
    c = checker.counts
    if checker.boost is not None:
        print(f"Boost root: {checker.boost}")
    print(f"(2) files with an Upstream line: {c['upstream']}; every other .cs, .py, .cpp and .hpp file is allowlisted")
    print(f"(3) copyright lines verbatim: {c['copyright']}/{c['upstream']} files; third-party blocks whole: "
          f"{c['blocks']} (file, block) pairs, each in the file's header and in THIRD_PARTY_NOTICES.md")
    print("(3) THIRD_PARTY_NOTICES.md carries LICENSE.TXT verbatim")
    print(f"(4) partial files: {c['partial']}, listed in docs/porting-plan.md: {c['partial']}")
    print(f"(5) table entries at the pin: {c['table'][0]}/{c['table'][1]} "
          f"(replaced and not-ported: {c['table-lists']}); porting map reproduces")
    for code, message in checker.errors:
        print(f"RED [{code}] {message}")
    print("check-upstream: " + ("passed" if not checker.errors else f"{len(checker.errors)} errors"))


PROBE = "src/NanoQL/Time/SelfTestProbe.cs"
PROBE_TEST = "tests/NanoQL.Tests/Conventions/SelfTestProbeTests.cs"
# A synthetic port of a real Boost header, checked against the real tree.
BOOST_PROBE = "src/NanoQL/Boost/Math/Tools/SelfTestBoostProbe.cs"
BOOST_PROBE_UPSTREAM = "boost/math/tools/cubic_roots.hpp"
# Boost's notice layouts, each a synthetic port whose copyright lines are written out as Boost 1.90.0's files have them:
# the plain form; a notice after the include guard; and holders on the line after the years, joined with them.
BOOST_LAYOUTS = [
    ("boost/math/distributions/normal.hpp", "Math/Distributions", "SelfTestNormalProbe",
     ["Copyright John Maddock 2006, 2007.", "Copyright Paul A. Bristow 2006, 2007.", "Copyright Matt Borland 2024."]),
    ("boost/math/special_functions/detail/fp_traits.hpp", "Math/SpecialFunctions/Detail", "SelfTestFpTraitsProbe",
     ["Copyright (c) 2006 Johan Rade", "Copyright (c) 2024 Matt Borland"]),
    ("boost/numeric/ublas/matrix_sparse.hpp", "Numeric/Ublas", "SelfTestMatrixSparseProbe",
     ["Copyright (c) 2000-2007 Joerg Walter, Mathias Koch, Gunter Winkler"]),
]
UBLAS_PROBE = "src/NanoQL/Boost/Numeric/Ublas/SelfTestMatrixSparseProbe.cs"
DATES_TESTS = "tests/NanoQL.Tests/TestSuite/DatesTests.cs"
# Third-party blocks (§1), each probe a synthetic port of real QuantLib files whose copyright lines and blocks are
# written out as those files have them at the pin, never read through the checker.
RULE = "=" * 75
JAECKEL = "Copyright (C) 2002 Peter Jäckel \"Monte Carlo Methods in Finance\"."
# Jäckel's permission notice: primenumbers.hpp:20-28 and .cpp:20-28, brownianbridge.hpp:26-34 (U+FFFD at line 29)
# and .cpp:20-28.
JAECKEL_BLOCK = [RULE, "NOTE: The following copyright notice applies to the original code,", "", JAECKEL,
                 "All rights reserved.", "", "Permission to use, copy, modify, and distribute this software is freely",
                 "granted, provided that this notice is preserved.", RULE]
# primitivepolynomials.hpp:3-18: the block starts at its provenance paragraph, whose "©" line is a notice the copyright
# reader doesn't match.
PRIMITIVE_BLOCK = ["this file is a slightly edited version of", "PrimitivePolynomialsModuloTwoUpToDegree27.h",
                   "© 2002 \"Monte Carlo Methods in Finance\"", "as provided ready for compilation in the directory",
                   "\"PrimitivePolynomialsModuloTwo\" on the CD accompanying the book",
                   "\"Monte Carlo Methods in Finance\" by Peter Jäckel.", ""] + JAECKEL_BLOCK
# A block whose source is a fifth column, a text file of the repository rather than the QuantLib file.
FIFTH_SOURCE = "LICENSES/LicenseRef-SelfTest.txt"
FIFTH_BLOCK = ["SELF-TEST LICENCE", "Copyright (c) 2026, the self-test.", "",
               "Redistribution of this text is permitted, provided that it is kept whole.", "END OF SELF-TEST LICENCE"]
PRIMES = ("ql/math/primenumbers.hpp", "ql/math/primenumbers.cpp")
BRIDGE = ("ql/methods/montecarlo/brownianbridge.hpp", "ql/methods/montecarlo/brownianbridge.cpp")
PRIMITIVE = ("ql/math/randomnumbers/primitivepolynomials.hpp",)
FIFTH_UPSTREAM = ("ql/patterns/curiouslyrecurring.hpp",)
PROBE_PRIMES = "src/NanoQL/Numerics/SelfTestPrimesProbe.cs"
PROBE_PRIMES_SECOND = "src/NanoQL/Numerics/SelfTestPrimesSecondProbe.cs"
PROBE_PRIMITIVE = "src/NanoQL/Numerics/RandomNumbers/SelfTestPrimitiveProbe.cs"
PROBE_BRIDGE = "src/NanoQL/Methods/MonteCarlo/SelfTestBridgeProbe.cs"
PROBE_FIFTH = "src/NanoQL/Patterns/SelfTestFifthColumnProbe.cs"
# Each block probe: (path, Upstream files, namespace, its copyright lines as written out, licence, its blocks).
BLOCK_PROBES = [
    (PROBE_PRIMES, PRIMES, "NanoQL.Numerics", ["Copyright (C) 2003 Ferdinando Ametrano", JAECKEL],
     "Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel", [JAECKEL_BLOCK]),
    (PROBE_PRIMES_SECOND, PRIMES, "NanoQL.Numerics", ["Copyright (C) 2003 Ferdinando Ametrano", JAECKEL],
     "Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel", [JAECKEL_BLOCK]),
    # The starred layout: the reader drops primitivepolynomials.hpp's " * " (§1).
    (PROBE_PRIMITIVE, PRIMITIVE, "NanoQL.Numerics", [JAECKEL],
     "Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel", [PRIMITIVE_BLOCK]),
    # The pair whose .hpp line carries U+FFFD and whose .cpp line has "ä": read once, as "Jäckel".
    (PROBE_BRIDGE, BRIDGE, "NanoQL.Methods", ["Copyright (C) 2003 Ferdinando Ametrano",
                                              "Copyright (C) 2006 StatPro Italia srl",
                                              "Copyright (C) 2009 Bojan Nikolic", JAECKEL],
     "Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel", [JAECKEL_BLOCK]),
    (PROBE_FIFTH, FIFTH_UPSTREAM, "NanoQL.Patterns", ["Copyright (C) 2003 RiskMap srl"],
     "Apache-2.0 AND BSD-3-Clause AND LicenseRef-SelfTest", [FIFTH_BLOCK]),
]
BLOCK_ROWS = ([f"{rel}\t{RULE}\t{RULE}\tLicenseRef-Jaeckel" for rel in PRIMES + BRIDGE]
              + [f"{PRIMITIVE[0]}\t{PRIMITIVE_BLOCK[0]}\t{RULE}\tLicenseRef-Jaeckel",
                 f"{FIFTH_UPSTREAM[0]}\t{FIFTH_BLOCK[0]}\t{FIFTH_BLOCK[-1]}\tLicenseRef-SelfTest\t{FIFTH_SOURCE}"])
# The name table's entries the probes need, added only where the real table has none.
BLOCK_TABLE = {
    "directories": {"ql/math/randomnumbers": {"folder": "Numerics/RandomNumbers", "namespace": "NanoQL.Numerics"},
                    "ql/methods/montecarlo": {"folder": "Methods/MonteCarlo", "namespace": "NanoQL.Methods"},
                    "ql/patterns": {"folder": "Patterns", "namespace": "NanoQL.Patterns"}},
    "files": {"ql/math/primenumbers": {"words": "Prime Numbers"},
              "ql/math/randomnumbers/primitivepolynomials": {"words": "Primitive Polynomials"},
              "ql/methods/montecarlo/brownianbridge": {"words": "Brownian Bridge"}},
}


def block_probe_text(upstream, namespace, copyrights, licence, blocks, name):
    lines = ["// SPDX-FileCopyrightText: " + c for c in copyrights]
    lines += ["// SPDX-FileCopyrightText: 2026 The nanoql Authors",
              f"// SPDX-License-Identifier: {licence}",
              f"// Upstream: {' '.join(upstream)} @ v1.44",
              "// Ported to C# from QuantLib and modified by the nanoql Authors."]
    for block in blocks:
        lines += [("// " + line).rstrip() for line in block]
    return "\n".join(lines + ["", f"namespace {namespace};", "", f"internal static class {name}", "{", "}", ""])


def notices_text(blocks):
    """THIRD_PARTY_NOTICES.md's sections for blocks, each in a fenced text block, as the real file lays them out."""
    out = ""
    for block in blocks:
        out += "\n```text\n" + "\n".join(block) + "\n```\n"
    return out


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


def boost_probe_text(copyrights, upstream=BOOST_PROBE_UPSTREAM, name="SelfTestBoostProbe"):
    return "\n".join(["// SPDX-FileCopyrightText: " + c for c in copyrights]
                     + ["// SPDX-FileCopyrightText: 2026 The nanoql Authors",
                        "// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0",
                        f"// Upstream: {upstream} @ boost-1.90.0",
                        "// Ported to C# from Boost and modified by the nanoql Authors.",
                        "",
                        f"namespace {BOOST_NAMESPACE};",
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


def replace_all(root, rel, old, new):
    path = os.path.join(root, rel)
    text = sources.read_text(path)
    assert old in text, (rel, old)
    with open(path, "w", encoding="utf-8", newline="\n") as f:
        f.write(text.replace(old, new))


def perturbations(ql_root, boost_root, tmp):
    """(name, the error code it must raise, a text that error must contain, how to apply it). Applying one may
    return the Boost root the checker then runs with, or "none" for a run without --boost."""
    first_copyright = "// SPDX-FileCopyrightText: " + sources.upstream_copyrights(ql_root, "ql/time/weekday.hpp")[0]
    boost_copyright = ("// SPDX-FileCopyrightText: "
                       + sources.boost_copyrights(boost_root, BOOST_PROBE_UPSTREAM)[0] + "\n")
    orphan = ("// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0\n"
              "\nnamespace NanoQL;\n\ninternal static class SelfTestOrphan\n{\n}\n")

    def move(root):
        os.makedirs(os.path.join(root, "src/NanoQL/Coupons"), exist_ok=True)
        shutil.move(os.path.join(root, PROBE), os.path.join(root, "src/NanoQL/Coupons/SelfTestProbe.cs"))

    def not_ported(root):
        text = sources.read_text(os.path.join(root, PROBE))
        text = re.sub(r"// Upstream: .*", "// Upstream: ql/qldefines.hpp @ v1.44", text)
        write(root, PROBE, text)

    def old_boost(root):
        # Another release's tree: its version.hpp, and the probe's upstream file, so only the version differs.
        fake = os.path.join(tmp, "boost-1.89.0")
        write(fake, "boost/version.hpp", "#define BOOST_VERSION 108900\n")
        write(fake, BOOST_PROBE_UPSTREAM, sources.read_text(os.path.join(boost_root, BOOST_PROBE_UPSTREAM)))
        return fake

    def boost_moved(root):
        shutil.move(os.path.join(root, BOOST_PROBE), os.path.join(root, "src/NanoQL/Boost/Math/SelfTestBoostProbe.cs"))

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
        ("a third-party row whose first line isn't in its file", "third-party",
         "ql/time/weekday.hpp has no comment line 'BEGIN SELF-TEST BLOCK'",
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
        ("a Boost path absent under the root", "boost-path", "doesn't exist under the Boost root",
         lambda r: edit(r, BOOST_PROBE, "cubic_roots.hpp @", "cubic_rootz.hpp @")),
        ("a Boost Upstream pin at another release", "boost-pin", "Upstream pin boost-1.89.0",
         lambda r: edit(r, BOOST_PROBE, "@ boost-1.90.0", "@ boost-1.89.0")),
        ("a Boost root whose version.hpp isn't 1.90.0", "boost-root", "gives BOOST_VERSION 108900", old_boost),
        ("a missing Boost copyright line", "copyright", "Boost copyright lines differ",
         lambda r: edit(r, BOOST_PROBE, boost_copyright, "")),
        ("a Boost port's SPDX expression without BSL-1.0", "copyright", "expected Apache-2.0 AND BSL-1.0",
         lambda r: edit(r, BOOST_PROBE, "Apache-2.0 AND BSL-1.0", "Apache-2.0")),
        ("a Boost port in the wrong folder", "folder", "but the name table's boost section gives", boost_moved),
        ("a Boost port in the wrong namespace", "namespace", "expected NanoQL.Numerics for a Boost port",
         lambda r: edit(r, BOOST_PROBE, "namespace NanoQL.Numerics;", "namespace NanoQL.Time;")),
        ("QuantLib and Boost paths on one Upstream line", "boost-mixed", "names QuantLib and Boost paths",
         lambda r: edit(r, BOOST_PROBE, "cubic_roots.hpp @", "cubic_roots.hpp ql/time/weekday.hpp @")),
        ("a Boost Upstream line without --boost", "boost-root", "needs --boost", lambda r: "none"),
        ("an unlisted .cpp with no Upstream line", "allowlist", "SelfTestOrphan.cpp: no Upstream line",
         lambda r: write(r, "tools/selftest/SelfTestOrphan.cpp", "// SPDX-FileCopyrightText: 2026 The nanoql Authors\n"
                                                                  "// SPDX-License-Identifier: Apache-2.0\n")),
        ("a uBLAS notice carried without its holders", "copyright", "Boost copyright lines differ",
         lambda r: edit(r, UBLAS_PROBE, "2000-2007 Joerg Walter, Mathias Koch, Gunter Winkler\n", "2000-2007\n")),
        ("a // Replaced: line naming a missing test file", "replaced", "which doesn't exist under",
         lambda r: edit(r, DATES_TESTS, "(Divergences/NullDateTests.cs)", "(Divergences/NoSuchTests.cs)")),
        ("a replaced case that isn't a case of the QuantLib file", "replaced", "nullDates, which isn't a case of",
         lambda r: edit(r, DATES_TESTS, "// Replaced: nullDate (", "// Replaced: nullDates (")),
        ("a replaced case that is also ported", "replaced", "testConsistency, which is also ported",
         lambda r: edit(r, DATES_TESTS, "// Replaced: nullDate (Divergences/NullDateTests.cs)",
                        "// Replaced: nullDate (Divergences/NullDateTests.cs); "
                        "testConsistency (Divergences/NullDateTests.cs)")),
        ("a replaced case also on the // Partial: line", "replaced", "nullDate, which is also on its // Partial: line",
         lambda r: edit(r, DATES_TESTS, ", parseDates, intraday\n", ", parseDates, intraday, nullDate\n")),
        ("a // Replaced: item that isn't '<case> (<test file>)'", "replaced", "isn't '<case> (<test file>)'",
         lambda r: edit(r, DATES_TESTS, "// Replaced: nullDate (", "// Replaced: nullDate, (")),
        ("a // Replaced: line outside a TestSuite class", "replaced", "belongs on a TestSuite class",
         lambda r: edit(r, PROBE, "// Ported to C#",
                        "// Replaced: nullDate (Divergences/NullDateTests.cs)\n// Ported to C#")),
        ("a third-party block missing a middle line", "third-party",
         f"{PROBE_PRIMES}: doesn't carry the third-party block of {PRIMES[0]} whole and in order; the block's line 5",
         lambda r: edit(r, PROBE_PRIMES, "// All rights reserved.\n", "")),
        ("a third-party block out of order", "third-party",
         f"{PROBE_PRIMES}: doesn't carry the third-party block of {PRIMES[0]} whole and in order; the block's line 4",
         lambda r: edit(r, PROBE_PRIMES, f"// {JAECKEL}\n// All rights reserved.\n",
                        f"// All rights reserved.\n// {JAECKEL}\n")),
        ("a third-party block absent from THIRD_PARTY_NOTICES.md", "third-party",
         f"THIRD_PARTY_NOTICES.md doesn't carry the third-party block of {PRIMES[0]} (for {PROBE_PRIMES})",
         lambda r: replace_all(r, "THIRD_PARTY_NOTICES.md", "granted, provided that this notice is preserved.\n", "")),
        ("a fifth-column source with one changed line", "third-party",
         f"{PROBE_FIFTH}: doesn't carry the third-party block of {FIFTH_UPSTREAM[0]} whole and in order; the block's "
         "line 4",
         lambda r: edit(r, FIFTH_SOURCE, "provided that it is kept whole.", "provided that it is kept.")),
        ("a starred notice carried with its star", "copyright",
         f"{PROBE_PRIMITIVE}: QuantLib copyright lines differ from the upstream files' (missing",
         lambda r: edit(r, PROBE_PRIMITIVE, f"// SPDX-FileCopyrightText: {JAECKEL}",
                        f"// SPDX-FileCopyrightText: * {JAECKEL}")),
        ("a U+FFFD line carried instead of its fault-free twin", "copyright",
         f"{PROBE_BRIDGE}: QuantLib copyright lines differ from the upstream files' (missing",
         lambda r: edit(r, PROBE_BRIDGE, f"// SPDX-FileCopyrightText: {JAECKEL}",
                        "// SPDX-FileCopyrightText: " + JAECKEL.replace("ä", sources.FFFD))),
        ("a U+FFFD line with no fault-free twin", "fffd",
         f"{PROBE_BRIDGE}: {BRIDGE[0]}:29 holds U+FFFD in a copyright line",
         lambda r: edit(r, PROBE_BRIDGE, f"// Upstream: {' '.join(BRIDGE)} @", f"// Upstream: {BRIDGE[0]} @")),
        ("a primitive-polynomials block missing its © line", "third-party",
         f"{PROBE_PRIMITIVE}: doesn't carry the third-party block of {PRIMITIVE[0]} whole and in order; the block's "
         "line 3",
         lambda r: edit(r, PROBE_PRIMITIVE, "// © 2002 \"Monte Carlo Methods in Finance\"\n", "")),
        ("a split pair's second file missing the block", "third-party",
         f"{PROBE_PRIMES_SECOND}: doesn't carry the third-party block of {PRIMES[0]} whole and in order; the block's "
         "line 1",
         lambda r: edit(r, PROBE_PRIMES_SECOND, "\n".join("// " + line if line else "//" for line in JAECKEL_BLOCK)
                        + "\n", "")),
        ("a third-party block's licence missing from the SPDX expression", "third-party",
         f"{PROBE_PRIMES}: the licence LicenseRef-Jaeckel of {PRIMES[0]}'s third-party block isn't in its SPDX "
         "expression",
         lambda r: edit(r, PROBE_PRIMES, "Apache-2.0 AND BSD-3-Clause AND LicenseRef-Jaeckel",
                        "Apache-2.0 AND BSD-3-Clause")),
        ("a third-party row of three columns", "third-party", f"'{PRIMES[0]}\t{RULE}\t{RULE}' has 3 columns, not 4 or 5",
         lambda r: append(r, "tools/third-party-notices.txt", f"{PRIMES[0]}\t{RULE}\t{RULE}\n")),
        ("a third-party row whose QuantLib file isn't at the pin", "third-party",
         "tools/third-party-notices.txt: ql/math/noprimenumbers.hpp doesn't exist at the pin",
         lambda r: append(r, "tools/third-party-notices.txt",
                          f"ql/math/noprimenumbers.hpp\t{RULE}\t{RULE}\tLicenseRef-Jaeckel\n")),
        ("a third-party row whose fifth column doesn't exist", "third-party",
         f"{FIFTH_UPSTREAM[0]}'s source LICENSES/LicenseRef-Missing.txt doesn't exist",
         lambda r: append(r, "tools/third-party-notices.txt",
                          f"{FIFTH_UPSTREAM[0]}\t{FIFTH_BLOCK[0]}\t{FIFTH_BLOCK[-1]}\tLicenseRef-SelfTest\t"
                          "LICENSES/LicenseRef-Missing.txt\n")),
        ("a third-party row whose last line comes before its first", "third-party",
         f"{PRIMES[0]} has no comment line '{JAECKEL_BLOCK[1]}', the row's last line, after its first line",
         lambda r: append(r, "tools/third-party-notices.txt",
                          f"{PRIMES[0]}\t{JAECKEL_BLOCK[-2]}\t{JAECKEL_BLOCK[1]}\tLicenseRef-Jaeckel\n")),
        ("a U+FFFD in a fifth-column source", "fffd",
         f"tools/third-party-notices.txt: {FIFTH_SOURCE}:4 holds U+FFFD in {FIFTH_UPSTREAM[0]}'s block, and a "
         "fifth-column source has no twin",
         lambda r: edit(r, FIFTH_SOURCE, "Redistribution", "Redistributi" + sources.FFFD + "n")),
    ]


def self_test(root, ql_root, boost_root):
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
        write(base, BOOST_PROBE, boost_probe_text(sources.boost_copyrights(boost_root, BOOST_PROBE_UPSTREAM)))
        boost_table = {"boost/math/tools": {"folder": "Math/Tools"}}
        for upstream, folder, name, copyrights in BOOST_LAYOUTS:
            write(base, f"{BOOST_SRC}{folder}/{name}.cs", boost_probe_text(copyrights, upstream, name))
            boost_table[upstream.rsplit("/", 1)[0]] = {"folder": folder}
        edit(base, "tools/porting-map.json", '"boost": {}', '"boost": ' + json.dumps(boost_table))
        table_path = os.path.join(base, "tools", "porting-map.json")
        table = sources.load_table(base)
        for section, entries in BLOCK_TABLE.items():
            for key, value in entries.items():
                table[section].setdefault(key, value)
        with open(table_path, "w", encoding="utf-8", newline="\n") as f:
            json.dump(table, f, indent=2, ensure_ascii=False)
            f.write("\n")
        for path, upstream, namespace, copyrights, licence, blocks in BLOCK_PROBES:
            name = path.rsplit("/", 1)[1][:-3]
            write(base, path, block_probe_text(upstream, namespace, copyrights, licence, blocks, name))
        append(base, "tools/third-party-notices.txt", "\n".join(BLOCK_ROWS) + "\n")
        write(base, FIFTH_SOURCE, "\n".join(FIFTH_BLOCK) + "\n")
        append(base, "THIRD_PARTY_NOTICES.md", notices_text([JAECKEL_BLOCK, PRIMITIVE_BLOCK, FIFTH_BLOCK]))
        porting_map.main([ql_root, "--root", base])
        baseline = Checker(base, ql_root, boost_root)
        baseline.run()
        if baseline.errors:
            report(baseline)
            print("self-test: the unperturbed copy isn't green")
            return 1
        caught = 0
        cases = perturbations(ql_root, boost_root, tmp)
        for i, (name, code, text, apply) in enumerate(cases):
            copy = os.path.join(tmp, f"p{i}")
            shutil.copytree(base, copy)
            run_with = apply(copy)
            checker = Checker(copy, ql_root, None if run_with == "none" else run_with or boost_root)
            checker.run()
            hit = any(c == code and text in message for c, message in checker.errors)
            caught += hit
            print(f"  {'caught' if hit else 'MISSED'}: {name} [{code}: {text}]")
        print(f"self-test: {caught}/{len(cases)} perturbations caught")
        return 0 if caught == len(cases) else 1


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
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(TOOLS)
    if "--self-test" in argv:
        if boost_root is None:
            print("check-upstream: --self-test needs --boost <Boost 1.90.0 root>, for its Boost probe")
            return 2
        return self_test(root, args[0], boost_root)
    checker = Checker(root, args[0], boost_root)
    checker.run()
    report(checker)
    return 1 if checker.errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
