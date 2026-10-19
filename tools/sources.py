# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""Shared readers for the porting tools: the repository's files and their headers, the name table, and QuantLib's
units at the pin (docs/porting-playbook.md §1, §5). tools/check-upstream.py, tools/porting-map.py,
tools/divergences.py and tools/check-coverage.py import it."""
import json
import os
import re
import subprocess

UPSTREAM = re.compile(r"^Upstream:\s+(.+?)\s+@\s+(\S+)\s*$")
NAMESPACE = re.compile(r"^\s*namespace\s+([\w.]+)\s*[;{]", re.M)
# "(C)", and also QuantLib's occasional typo, "(2)" (dataparsers.cpp), so that no holder is dropped.
COPYRIGHT = re.compile(r"Copyright\s*\(\w\)")
# nanoql's own copyright line; every other SPDX-FileCopyrightText line is an upstream holder's, QuantLib's or Boost's
# (whose lines read "Copyright John Maddock 2006." or "(C) Copyright John Maddock 2006.").
NANOQL = re.compile(r"\d{4} The nanoql Authors")
# A "// Replaced:" item: a QuantLib test case and the nanoql test file, under tests/NanoQL.Tests/, replacing it.
REPLACED = re.compile(r"(\w+)\s+\(([^()]+)\)")
# A "// Not ported:" item: a QuantLib test case that can never port, and the reason.
NOT_PORTED = re.compile(r"(\w+)\s+\(([^()]+)\)")
# A "// Moved:" item: a QuantLib test case, the line of its check moved to a Divergences/ test, and that test's file,
# under tests/NanoQL.Tests/.
MOVED = re.compile(r"(\w+)\s+line\s+(\d+)\s+\(([^()]+)\)")
# A Boost copyright line that gives only the years, its holders on the lines after it, as uBLAS writes
# "Copyright (c) 2000-2007".
YEARS_ONLY = re.compile(r"(?:\(C\)\s*)?Copyright\s*(?:\([Cc]\))?[\d\s,.\-]*")
DEPRECATED = "disappear in a future release"
TOP_LEVEL_TYPE = re.compile(
    r"^(?:\[[^\]]*\]\s*)*(public|internal|file)?\s*((?:(?:static|sealed|abstract|readonly|partial|unsafe|ref)\s+)*)"
    r"(class|struct|interface|enum|delegate|record\s+class|record\s+struct|record)\s+(?:[\w<>,\s.?\[\]]+\s+)??"
    r"(\w+)\s*(?:<|\(|:|\{|;|where|$)", re.M)


def git_files(root):
    """The repository's files, tracked or untracked but not ignored, as sorted paths with forward slashes."""
    out = subprocess.run(["git", "-C", root, "ls-files", "--cached", "--others", "--exclude-standard", "-z"],
                         capture_output=True, check=True).stdout.decode("utf-8")
    return sorted({p for p in out.split("\0") if p and os.path.isfile(os.path.join(root, p))})


def read_text(path):
    with open(path, encoding="utf-8") as f:
        return f.read()


class Header:
    """The comment block at the top of a nanoql source file."""

    def __init__(self, path):
        text = read_text(path)
        prefix = "#" if path.endswith(".py") else "//"
        self.lines = []
        for line in text.splitlines():
            stripped = line.strip()
            if not stripped.startswith(prefix):
                break
            self.lines.append(stripped[len(prefix):].strip())
        self.copyrights = []
        self.nanoql = None
        self.license = None
        self.upstream = []
        self.pin = None
        self.partial = self.own_tested = self.equality_tested = None
        self.replaced = []  # (case, test file), or (None, the item) for one that doesn't parse
        self.not_ported = []  # (case, reason), or (None, the item) for one that doesn't parse
        self.moved = []  # (case, line, test file), or (None, None, the item) for one that doesn't parse
        for line in self.lines:
            if line.startswith("SPDX-FileCopyrightText:"):
                value = line[len("SPDX-FileCopyrightText:"):].strip()
                if NANOQL.fullmatch(value):
                    self.nanoql = value
                else:
                    self.copyrights.append(value)
            elif line.startswith("SPDX-License-Identifier:"):
                self.license = line[len("SPDX-License-Identifier:"):].strip()
            elif line.startswith("Upstream:"):
                m = UPSTREAM.match(line)
                self.upstream, self.pin = (m.group(1).split(), m.group(2)) if m else (["<malformed>"], None)
            elif line.startswith("Partial:"):
                self.partial = line[len("Partial:"):].strip()
            elif line.startswith("Replaced:"):
                for item in line[len("Replaced:"):].split(";"):
                    m = REPLACED.fullmatch(item.strip())
                    self.replaced.append((m.group(1), m.group(2).strip()) if m else (None, item.strip()))
            elif line.startswith("Not ported:"):
                for item in line[len("Not ported:"):].split(";"):
                    m = NOT_PORTED.fullmatch(item.strip())
                    self.not_ported.append((m.group(1), m.group(2).strip()) if m else (None, item.strip()))
            elif line.startswith("Moved:"):
                for item in line[len("Moved:"):].split(";"):
                    m = MOVED.fullmatch(item.strip())
                    self.moved.append((m.group(1), int(m.group(2)), m.group(3).strip()) if m else
                                      (None, None, item.strip()))
            elif line.startswith("Own-tested:"):
                self.own_tested = line[len("Own-tested:"):].strip()
            elif line.startswith("Equality-tested:"):
                self.equality_tested = line[len("Equality-tested:"):].strip()
        m = NAMESPACE.search(text) if path.endswith(".cs") else None
        self.namespace = m.group(1) if m else None
        self.text = text


def top_level_types(text):
    """The top-level types a C# file declares, as (accessibility, kind, name), outside any other type's braces."""
    body = re.sub(r"//[^\n]*|/\*.*?\*/", "", text, flags=re.S)
    body = re.sub(r'@"(?:""|[^"])*"|"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'', '""', body)
    # Top-level types sit at brace depth 0 under a file-scoped namespace (or none), and at depth 1 inside a block one.
    block_namespace = re.search(r"^\s*namespace\s+[\w.]+\s*(\{|$)", body, re.M) is not None
    top = 1 if block_namespace else 0
    types, level = [], 0
    for line in body.splitlines():
        if level == top:
            m = TOP_LEVEL_TYPE.match(line.strip())
            if m:
                types.append((m.group(1) or "internal", re.sub(r"\s+", " ", m.group(3)), m.group(4)))
        level += line.count("{") - line.count("}")
    return types


def load_table(root):
    with open(os.path.join(root, "tools", "porting-map.json"), encoding="utf-8") as f:
        return json.load(f)


def pascal(words):
    return words.replace(" ", "")


def unit_of(path):
    return path.rsplit(".", 1)[0]


def ql_units(ql_root):
    """QuantLib's units at the pin: each .hpp/.cpp pair under ql/ and test-suite/, with its files."""
    units = {}
    for top in ("ql", "test-suite"):
        for dp, _, fns in os.walk(os.path.join(ql_root, top)):
            for fn in fns:
                if fn.endswith((".hpp", ".cpp")):
                    rel = os.path.relpath(os.path.join(dp, fn), ql_root).replace(os.sep, "/")
                    units.setdefault(unit_of(rel), []).append(rel)
    return {u: sorted(files, key=lambda f: (not f.endswith(".hpp"), f)) for u, files in sorted(units.items())}


def ql_read(ql_root, rel):
    with open(os.path.join(ql_root, rel), encoding="utf-8", errors="replace") as f:
        return f.read()


def rule_status(ql_root, unit, files):
    """The status a rule gives a QuantLib unit (legacy, deprecated, include-only), or None."""
    if unit.startswith("ql/legacy/"):
        return "legacy"
    if unit.startswith("ql/") and unit.endswith("/all"):
        return "include-only header"
    if any(DEPRECATED in ql_read(ql_root, f) for f in files):
        return "deprecated"
    return None


def leading_comments(text):
    """The lines of a C++ file's leading comments, with the comment markers removed and a blank comment line as "".
    They run up to the first code or #include: an include guard and any other preprocessor line before them doesn't end
    them, since some Boost files put their notice after the guard."""
    lines = []
    in_block = False
    for line in text.splitlines():
        stripped = line.strip()
        if in_block:
            in_block = "*/" not in stripped
            lines.append(stripped.split("*/")[0].lstrip("*").strip())
            continue
        if not stripped:
            continue
        if stripped.startswith("/*"):
            in_block = "*/" not in stripped
            lines.append(stripped[2:].split("*/")[0].lstrip("*").strip())
            continue
        if stripped.startswith("//"):
            lines.append(stripped.lstrip("/").strip())
            continue
        if stripped.startswith("#") and not stripped[1:].lstrip().startswith("include"):
            continue
        break
    return lines


def boost_copyrights(boost_root, rel):
    """The copyright notices of a Boost file as written: every line of its leading comments that holds "Copyright",
    verbatim and in order, in any of Boost's forms ("Copyright John Maddock 2006.", "(C) Copyright John Maddock 2006.",
    "Copyright (c) 2006 Johan Rade"). A line that gives only the years takes its holders from the lines after it, up
    to a blank comment line or the next "Copyright" line, joined by one space: "Copyright (c) 2000-2007 Joerg Walter,
    Mathias Koch, Gunter Winkler"."""
    with open(os.path.join(boost_root, rel), encoding="utf-8", errors="replace") as f:
        lines = leading_comments(f.read())
    notices = []
    for i, line in enumerate(lines):
        if "Copyright" not in line:
            continue
        if YEARS_ONLY.fullmatch(line):
            holders = []
            for following in lines[i + 1:]:
                if not following or "Copyright" in following:
                    break
                holders.append(following)
            line = " ".join([line] + holders)
        notices.append(line)
    return notices


def upstream_copyrights(ql_root, rel):
    """The Copyright (C) lines of a QuantLib file's header comments, verbatim and in order, before its first code. A
    block comment's leading "*" is comment syntax and isn't part of the line (primitivepolynomials.hpp's
    " * Copyright (C) 2002 Peter Jäckel ...")."""
    return [text for _, text in upstream_copyright_lines(ql_root, rel)]


def upstream_copyright_lines(ql_root, rel):
    """upstream_copyrights, each line with its 1-based line number."""
    lines = []
    in_block = False
    for number, line in enumerate(ql_read(ql_root, rel).splitlines(), 1):
        stripped = line.strip()
        if in_block:
            if COPYRIGHT.search(stripped):
                lines.append((number, stripped.lstrip("*").strip()))
            if "*/" in stripped:
                in_block = False
            continue
        if not stripped:
            continue
        if stripped.startswith("/*"):
            if COPYRIGHT.search(stripped):
                lines.append((number, stripped.lstrip("/*").strip()))
            in_block = "*/" not in stripped
            continue
        if stripped.startswith("//"):
            if COPYRIGHT.search(stripped):
                lines.append((number, stripped.lstrip("/").strip()))
            continue
        break
    return lines


# U+FFFD, the replacement character: a byte fault in QuantLib's copy of a notice, not the notice's text
# (brownianbridge.hpp:29 has "J\ufffdckel" where brownianbridge.cpp has "Jäckel").
FFFD = "\ufffd"


def comment_lines(text):
    """Every line of a C++ file as (1-based number, text without comment markers, whether it is code). A comment
    line loses its "//", or its "/*", "*/" and a block comment's leading "*"; a blank line is "" and not code."""
    out = []
    in_block = False
    for number, line in enumerate(text.splitlines(), 1):
        stripped = line.strip()
        if in_block:
            if "*/" in stripped:
                stripped = stripped[:stripped.index("*/")]
                in_block = False
            out.append((number, stripped.strip().lstrip("*").strip(), False))
        elif not stripped:
            out.append((number, "", False))
        elif stripped.startswith("//"):
            out.append((number, stripped.lstrip("/").strip(), False))
        elif stripped.startswith("/*"):
            body = stripped[2:]
            if "*/" in body:
                body = body[:body.index("*/")]
            else:
                in_block = True
            out.append((number, body.strip().lstrip("*").strip(), False))
        else:
            out.append((number, stripped, True))
    return out


def source_lines(root, ql_root, rel):
    """A notice source as comment_lines gives it: a QuantLib file (a path under ql/ or test-suite/, read at the pin),
    or a text file of the repository (a third-party row's fifth column), each of whose lines is text, stripped."""
    if rel.startswith(("ql/", "test-suite/")):
        return comment_lines(ql_read(ql_root, rel))
    return [(n, line.strip(), False) for n, line in enumerate(read_text(os.path.join(root, rel)).splitlines(), 1)]


def twin(line, others):
    """The fault-free reading of a line holding U+FFFD: the one distinct line among others (lines of the port's other
    files) that differs from it only by one character where it has each U+FFFD, or None if there is no such line, or
    more than one."""
    pattern = re.compile("[^\ufffd]".join(re.escape(part) for part in line.split(FFFD)))
    found = {o for o in others if FFFD not in o and pattern.fullmatch(o)}
    return next(iter(found)) if len(found) == 1 else None


def read_fault_free(line, others):
    """(the reading of line, whether it is fault-free): a line holding U+FFFD is read as its twin among others."""
    if FFFD not in line:
        return line, True
    fixed = twin(line, others)
    return (fixed, True) if fixed is not None else (line, False)


def port_copyrights(ql_root, rels):
    """The Copyright (C) lines of a port's upstream files (those its Upstream line names), verbatim, deduplicated, in
    order of first appearance. A line holding U+FFFD is read as its fault-free twin in another of the port's files
    (§1); one with no twin is a fault. Returns (lines, faults), each fault "<file>:<line number>"."""
    texts = {rel: [t for _, t, code in comment_lines(ql_read(ql_root, rel)) if not code] for rel in rels}
    lines, faults = [], []
    for rel in rels:
        others = [t for other in rels if other != rel for t in texts[other]]
        for number, text in upstream_copyright_lines(ql_root, rel):
            text, ok = read_fault_free(text, others)
            if not ok:
                faults.append(f"{rel}:{number}")
            if text not in lines:
                lines.append(text)
    return lines, faults


def notice_block(root, ql_root, rel, first, last, port=()):
    """A third-party notice block (tools/third-party-notices.txt): the lines of rel, without comment markers, from the
    first line that reads first, through the last line that reads last before the code that follows it. rel is the
    row's QuantLib file, or its fifth column. Each line holding U+FFFD is read as its fault-free twin in another file of
    port, the QuantLib files the porting file's Upstream line names. Returns (lines, faults, error): faults as
    port_copyrights gives them, error a message when the block can't be read."""
    lines = source_lines(root, ql_root, rel)
    start = next((i for i, (_, text, code) in enumerate(lines) if not code and text == first), None)
    if start is None:
        return [], [], f"{rel} has no comment line '{first}', the row's first line"
    end = None
    for i in range(start, len(lines)):
        if lines[i][2]:
            break
        if lines[i][1] == last:
            end = i
    if end is None:
        return [], [], f"{rel} has no comment line '{last}', the row's last line, after its first line"
    others = [t for other in port if other != rel
              for _, t, code in comment_lines(ql_read(ql_root, other)) if not code]
    block, faults = [], []
    for number, text, _ in lines[start:end + 1]:
        text, ok = read_fault_free(text, others)
        if not ok:
            faults.append(f"{rel}:{number}")
        block.append(text)
    return block, faults, None


def find_run(lines, block):
    """Where block's lines appear in lines as one run, in order: (its index, len(block)), or (None, the longest
    partial run's length), so that a failure can name the block's first line that differs."""
    best = 0
    for i in range(len(lines)):
        n = 0
        while n < len(block) and i + n < len(lines) and lines[i + n] == block[n]:
            n += 1
        if n == len(block):
            return i, n
        best = max(best, n)
    return None, best
# REUSE-IgnoreEnd
