# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""Shared readers for the porting tools: the repository's files and their headers, the name table, and QuantLib's
units at the pin (docs/porting-playbook.md §1, §5). tools/check-upstream.py, tools/porting-map.py and
tools/check-coverage.py import it."""
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
    """The Copyright (C) lines of a QuantLib file's header comments, verbatim and in order, before its first code."""
    lines = []
    in_block = False
    for line in ql_read(ql_root, rel).splitlines():
        stripped = line.strip()
        if in_block:
            if COPYRIGHT.search(stripped):
                lines.append(stripped)
            if "*/" in stripped:
                in_block = False
            continue
        if not stripped:
            continue
        if stripped.startswith("/*"):
            if COPYRIGHT.search(stripped):
                lines.append(stripped.lstrip("/*").strip())
            in_block = "*/" not in stripped
            continue
        if stripped.startswith("//"):
            if COPYRIGHT.search(stripped):
                lines.append(stripped.lstrip("/").strip())
            continue
        break
    return lines
# REUSE-IgnoreEnd
