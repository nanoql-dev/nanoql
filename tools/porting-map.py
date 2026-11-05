# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""Generates docs/porting-map.md: every QuantLib unit (an .hpp/.cpp pair) under ql/ and test-suite/ at the pin, with
its status, the nanoql files that name it, and what isn't ported yet (docs/porting-playbook.md §5). The files are the
library's and the tests'; the docs' samples under docs/examples/ are left out, though a sample that follows a QuantLib
example or takes a test's data carries an Upstream line.

Usage: .venv\\Scripts\\python -I tools/porting-map.py <QuantLib v1.44 worktree> [--root <repository>] [--check]

The status comes, in order, from a rule (legacy, deprecated, include-only), from the name table (replaced or not
ported, with the reason; the table wins over an Upstream line), from the Upstream lines of nanoql's files (partial if
any has a "// Partial:" line, otherwise ported), and otherwise "not started". A test file is ported when every case
has a test method or is named on its class's "// Replaced:" line, which the map lists as "replaced: <case> by <test
file>", or on its "// Not ported:" line, a case that can never port, which the map lists as "not ported: <case>
(<reason>)" and leaves out of the cases ported. A "// Moved:" line, a check moved out of a ported case to a
Divergences/ test, is listed on the row whatever its status, as "moved: <case> line <n> to <test file>". A type the
name table renames into a type another unit ports (Greeks to OneAssetOptionResults) is listed as "replaced: <type> by
<nanoql type>". With --check, it writes nothing and exits 1 if the committed map differs from a fresh one.
"""
import os
import re
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

TOKEN = re.compile(r"[A-Za-z_]\w*|::|\S")
STRIP = re.compile(r'//[^\n]*|/\*.*?\*/|"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'', re.S)
CASE = re.compile(r"BOOST_(?:AUTO|DATA|FIXTURE)_TEST_CASE\s*\(\s*(\w+)")
METHOD = re.compile(r"^\s*public\s+(?:async\s+)?(?:void|Task)\s+(\w+)\s*\(", re.M)
GLOBAL_ALIAS = re.compile(r"^\s*global\s+using\s+(\w+)\s*=", re.M)
DESIGN_REPLACED = ("arguments", "engine")
DOCS_EXAMPLES = "docs/examples/"
HEADER = """<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
"""


def strip_code(text):
    text = STRIP.sub(lambda m: " " if m.group(0)[0] in "/" else '""', text)
    out, continued = [], False
    for line in text.splitlines():
        directive = continued or line.lstrip().startswith("#")
        continued = directive and line.rstrip().endswith("\\")
        out.append("" if directive else line)
    return "\n".join(out)


def skip_angles(toks, i):
    """Index just past the '<...>' starting at toks[i]."""
    depth = 0
    while i < len(toks):
        if toks[i] == "<":
            depth += 1
        elif toks[i] == ">":
            depth -= 1
            if depth == 0:
                return i + 1
        elif toks[i] in ("{", ";") and depth:
            return i
        i += 1
    return i


def skip_to(toks, i, stops):
    """Index of the first token in stops at bracket depth 0, from toks[i]."""
    depth = 0
    while i < len(toks):
        t = toks[i]
        if depth == 0 and t in stops:
            return i
        if t in ("(", "["):
            depth += 1
        elif t in (")", "]"):
            depth -= 1
        i += 1
    return i


def matching_brace(toks, i):
    depth = 0
    while i < len(toks):
        if toks[i] == "{":
            depth += 1
        elif toks[i] == "}":
            depth -= 1
            if depth == 0:
                return i
        i += 1
    return i


def declared_types(text):
    """The types a QuantLib header declares in namespace QuantLib, outside detail and anonymous namespaces: classes,
    structs and enums, public nested types as <Outer><Inner>, and typedefs of template instantiations. A struct
    that holds nothing but nested types (DateGeneration) is left out; its nested types are listed."""
    toks = TOKEN.findall(strip_code(text))
    found = []  # [name, holder-scope or None]
    stack = []  # scopes: {"kind": "ns"|"class"|"block", ...}

    def visible():
        names = [s for s in stack if s["kind"] != "block"]
        if not names or any(s["kind"] == "block" for s in stack):
            return None
        if names[0]["kind"] != "ns" or names[0]["name"] != "QuantLib":
            return None
        prefix = ""
        for s in names[1:]:
            if s["kind"] == "ns":
                if s["name"] in ("", "detail") or prefix:
                    return None
            else:
                if s["name"] is None or s["access"] != "public":
                    return None
                prefix += s["name"]
        return prefix

    def member(scope):
        if scope and scope["kind"] == "class":
            scope["members"] = True

    i, after_type = 0, False
    while i < len(toks):
        t = toks[i]
        top = stack[-1] if stack else None
        if t == "namespace":
            j = i + 1
            name = ""
            while j < len(toks) and toks[j] not in ("{", ";", "="):
                name += toks[j]
                j += 1
            if j < len(toks) and toks[j] == "{":
                stack.append({"kind": "ns", "name": name})
                i = j + 1
            else:
                i = skip_to(toks, j, (";",)) + 1
            after_type = False
            continue
        if t == "template" and i + 1 < len(toks) and toks[i + 1] == "<":
            i = skip_angles(toks, i + 1)
            continue
        if t in ("class", "struct", "union", "enum") and not (i > 0 and toks[i - 1] in ("friend", "<", ",", "(")):
            j = i + 1
            if t == "enum" and j < len(toks) and toks[j] in ("class", "struct"):
                j += 1
            while j < len(toks) and (toks[j].startswith("QL_") or toks[j] == "["):
                if toks[j] == "[":
                    while j + 1 < len(toks) and not (toks[j] == "]" and toks[j + 1] == "]"):
                        j += 1
                    j += 2
                else:
                    j += 1
            parts = []
            while j < len(toks) and re.match(r"[A-Za-z_]\w*$", toks[j]) and toks[j] != "final":
                parts.append(toks[j])
                if j + 1 < len(toks) and toks[j + 1] == "::":
                    j += 2
                    continue
                j += 1
                break
            specialised = j < len(toks) and toks[j] == "<"
            end = skip_to(toks, j, ("{", ";"))
            if end >= len(toks) or toks[end] == ";":
                i = end + 1
                if top and top["kind"] == "class" and not after_type:
                    member(top)
                continue
            prefix = visible()
            entry = None
            if top and top["kind"] == "class":
                top["nested"] += 1
            # An instrument's nested arguments and engine classes are folded into the instrument and its static
            # engine (playbook §2.1), so they are never ported and aren't listed.
            nested = bool(prefix) or len(parts) > 1
            plumbing = nested and parts and parts[-1] in DESIGN_REPLACED
            if prefix is not None and parts and not specialised and not plumbing:
                name = prefix + "".join(p[0].upper() + p[1:] for p in parts)
                entry = [name, None]
                found.append(entry)
            if t == "enum":
                i = matching_brace(toks, end) + 1
                after_type = True
                continue
            scope = {"kind": "class", "name": "".join(p[0].upper() + p[1:] for p in parts) if parts else None,
                     "access": "public" if t != "class" else "private", "members": False, "nested": 0, "entry": entry}
            if len(parts) > 1:
                scope["access"] = "public"
            stack.append(scope)
            i = end + 1
            after_type = False
            continue
        if top and top["kind"] == "class" and t in ("public", "protected", "private") and \
                i + 1 < len(toks) and toks[i + 1] == ":":
            top["access"] = t
            i += 2
            continue
        if t in ("typedef", "using") and top and top["kind"] == "ns":
            end = skip_to(toks, i, (";", "{"))
            body = toks[i + 1:end]
            prefix = visible()
            if prefix == "" and "<" in body and toks[end] == ";":
                if t == "typedef":
                    name = body[-1]
                elif len(body) > 1 and body[1] == "=":
                    name = body[0]
                else:
                    name = None
                if name and re.match(r"[A-Za-z_]\w*$", name):
                    found.append([name, None])
            i = end + 1 if toks[end] == ";" else end
            continue
        if t == "{":
            member(top)
            stack.append({"kind": "block"})
        elif t == "}":
            if stack:
                closed = stack.pop()
                if closed["kind"] == "class":
                    if closed["entry"] is not None and closed["nested"] and not closed["members"]:
                        closed["entry"][1] = "holder"
                    after_type = True
                    i += 1
                    continue
        elif t == ";":
            if top and top["kind"] == "class" and not after_type:
                member(top)
        after_type = False
        i += 1
    names = []
    for name, holder in found:
        if holder is None and name not in names:
            names.append(name)
    return names


def cell(items):
    return ", ".join(items) if items else ""


_QUANTLIB = {}


def quantlib_side(ql_root):
    """Per QuantLib unit: its files, the status a rule gives it, the types its headers declare and its test cases.
    Scanned once per process, since the tree at the pin doesn't change."""
    if ql_root not in _QUANTLIB:
        side = {}
        for unit, unit_files in sources.ql_units(ql_root).items():
            texts = {f: sources.ql_read(ql_root, f) for f in unit_files}
            types, cases = [], []
            for f in unit_files:
                if unit.startswith("ql/") and f.endswith(".hpp"):
                    types += [n for n in declared_types(texts[f]) if n not in types]
                elif unit.startswith("test-suite/"):
                    cases += CASE.findall(texts[f])
            rule = sources.rule_status(ql_root, unit, unit_files)
            side[unit] = {"files": unit_files, "rule": rule, "types": types, "cases": cases}
        _QUANTLIB[ql_root] = side
    return _QUANTLIB[ql_root]


def build(root, ql_root):
    table = sources.load_table(root)
    units = quantlib_side(ql_root)
    files = sources.git_files(root)
    naming = {}  # unit -> [(nanoql path, header)]
    aliases = set()
    everywhere = set()  # every ported file's top-level types, for a type renamed into another unit's file
    for path in files:
        # The docs' samples, some headed as ports of a QuantLib example or a test's data, port no unit.
        if not path.endswith(".cs") or path.startswith(DOCS_EXAMPLES):
            continue
        header = sources.Header(os.path.join(root, path))
        aliases |= set(GLOBAL_ALIAS.findall(header.text))
        if header.upstream:
            everywhere |= {name for _, _, name in sources.top_level_types(header.text)}
        for up in header.upstream:
            naming.setdefault(sources.unit_of(up), []).append((path, header))
    replaced, not_ported = table["replaced"], table["notPorted"]
    rows = {"ql": [], "test-suite": []}
    totals = {}
    for unit in sorted(units, key=lambda u: tuple(u.rsplit("/", 1))):
        top = unit.split("/")[0]
        mine = sorted({p: h for p, h in naming.get(unit, [])}.items())
        shown = [p.removeprefix("src/NanoQL/").removeprefix("tests/NanoQL.Tests/") for p, _ in mine]
        rule = units[unit]["rule"]
        notes = []
        if rule:
            status = "not ported: " + rule
        elif unit in replaced:
            status = "replaced: " + replaced[unit]
        elif unit in not_ported:
            status = "not ported: " + not_ported[unit]
        elif top == "test-suite":
            cases = units[unit]["cases"]
            methods, replaced_cases, not_ported_cases = set(), {}, {}
            for _, h in mine:
                methods |= set(METHOD.findall(h.text))
                replaced_cases.update({c: f for c, f in h.replaced if c in cases})
                not_ported_cases.update({c: r for c, r in h.not_ported if c in cases})
            left = [c for c in cases if c[0].upper() + c[1:] not in methods and c not in replaced_cases
                    and c not in not_ported_cases]
            if not mine:
                status = "not started"
                notes.append(f"{len(cases)} cases")
            else:
                # A case that can never port doesn't keep the file partial, and isn't counted among the cases ported.
                status = "ported" if not left else "partial"
                notes.append(f"{len(cases) - len(left) - len(not_ported_cases)} of {len(cases)} cases")
                if replaced_cases:
                    notes.append("replaced: " + "; ".join(f"{c} by {f}" for c, f in replaced_cases.items()))
                if not_ported_cases:
                    notes.append("not ported: " + "; ".join(f"{c} ({r})" for c, r in not_ported_cases.items()))
                if left:
                    notes.append("not yet: " + cell(left))
        else:
            declared = units[unit]["types"]
            ported = set(aliases)
            partial = []
            for _, h in mine:
                ported |= {name for _, _, name in sources.top_level_types(h.text)}
                if h.partial:
                    partial.append(h.partial)
            status = "not started" if not mine else ("partial" if partial else "ported")
            # A renamed type (the name table's renames: Array is Vector) counts under its nanoql name.
            left = [n for n in declared if table["renames"].get(n, n) not in ported]
            # A type replaced by one another unit ports (Greeks by OneAssetOptionResults) is listed as replaced.
            elsewhere = [n for n in left if n in table["renames"] and table["renames"][n] in everywhere]
            left = [n for n in left if n not in elsewhere]
            if partial:
                # Files split from one unit may carry the same text; it is printed once.
                notes.append("partial: " + "; ".join(dict.fromkeys(partial)))
            if elsewhere:
                notes.append("replaced: " + "; ".join(f"{n} by {table['renames'][n]}" for n in elsewhere))
            if left:
                notes.append("no nanoql type: " + cell(left))
        # A check moved out of a ported case to a Divergences/ test is noted whatever the row's status.
        moved = [(c, n, f) for _, h in mine for c, n, f in h.moved if c is not None]
        if moved:
            notes.append("moved: " + "; ".join(f"{c} line {n} to {f}" for c, n, f in moved))
        own = [h.own_tested for _, h in mine if h.own_tested]
        if own:
            notes.append("own-tested: " + "; ".join(own))
        totals.setdefault(top, {}).setdefault(status.split(":")[0], 0)
        totals[top][status.split(":")[0]] += 1
        rows[top].append((unit, status, cell(shown), "; ".join(notes)))
    return rows, totals


def render(rows, totals):
    out = [HEADER, "# Porting map", "",
           "Every QuantLib v1.44 unit (an `.hpp`/`.cpp` pair) under `ql/` and `test-suite/`, with its status, the nanoql",
           "files that name it in their Upstream line, and what isn't ported yet. The files are the library's and the",
           "tests'; the docs' samples under `docs/examples/`, which follow QuantLib's examples or take a test's data,",
           "are left out. Generated by `tools/porting-map.py` from the QuantLib worktree at the tag, the name table",
           "(`tools/porting-map.json`) and nanoql's headers; do not edit it by hand.", "",
           "\"No nanoql type\" lists the types a unit declares that no nanoql file naming the unit declares under the same",
           "name: classes, structs and enums in `namespace QuantLib`, public nested types as `<Outer><Inner>`, and",
           "typedefs of template instantiations. An instrument's nested `arguments` and `engine` classes aren't listed,",
           "because the instrument and its static engine take their place (playbook §2.1).", "",
           "## Summary", "",
           "| Status | `ql/` units | `test-suite/` units |",
           "|---|---:|---:|"]
    statuses = sorted({s for t in totals.values() for s in t})
    for s in statuses:
        out.append(f"| {s} | {totals.get('ql', {}).get(s, 0):,} | {totals.get('test-suite', {}).get(s, 0):,} |")
    out.append(f"| total | {sum(totals.get('ql', {}).values()):,} | {sum(totals.get('test-suite', {}).values()):,} |")
    for top, title in (("ql", "Library"), ("test-suite", "Test suite")):
        out += ["", f"## {title}"]
        directory = None
        for unit, status, files, notes in rows[top]:
            d = unit.rsplit("/", 1)[0]
            if d != directory:
                directory = d
                out += ["", f"### `{d}/`", "", "| Unit | Status | nanoql files | Notes |", "|---|---|---|---|"]
            out.append(f"| `{unit.rsplit('/', 1)[1]}` | {status} | {files} | {notes} |")
    return "\n".join(out) + "\n"


def reproduces(root, ql_root):
    """Whether docs/porting-map.md is, byte for byte, a fresh generation."""
    try:
        with open(os.path.join(root, "docs", "porting-map.md"), "rb") as f:
            return f.read() == render(*build(root, ql_root)).encode("utf-8")
    except FileNotFoundError:
        return False


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if "--root" in argv:
        root = argv[argv.index("--root") + 1]
        args.remove(root)
    if len(args) != 1:
        print(__doc__)
        return 2
    if "--check" in argv:
        same = reproduces(root, args[0])
        print("porting map: reproduces" if same else "porting map: STALE, differs from a fresh generation")
        return 0 if same else 1
    text = render(*build(root, args[0]))
    target = os.path.join(root, "docs", "porting-map.md")
    with open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote {target}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
