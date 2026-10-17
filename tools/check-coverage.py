# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The coverage gate's type check, and its member line (docs/porting-playbook.md §9.5).

Usage: .venv\\Scripts\\python -I tools/check-coverage.py <results folder> [--all <results folder>] [--self-test]

Run it after the coverage runs, each of which writes one Cobertura report into its emptied results folder: the
tagged run, and for --all the whole suite's, both from the repository root:

    dotnet test --solution NanoQL.slnx -c Release --filter-trait "Oracle=QuantLib" --coverlet
        --coverlet-output-format cobertura --results-directory TestResults\\G7
    dotnet test --solution NanoQL.slnx -c Release --coverlet --coverlet-output-format cobertura
        --results-directory TestResults\\G7all

The types it checks are the top-level public types of the .cs files under src/NanoQL/ that the commit adds or
changes (the working tree against HEAD, untracked files included) and that carry an Upstream line. Report entries
match those files by path, so nested, file and compiler-generated classes count toward their file's type.

Red when: the folder doesn't hold exactly one report; the commit ports a public type and the report doesn't list the
NanoQL package; a listed type has no covered line; a ported file carries ExcludeFromCodeCoverage; a type whose only
executable lines are its equality members lacks a "// Equality-tested:" line naming an existing test file; or, with
--all, the whole suite's folder doesn't hold exactly one report, or a NanoQL class of that report resolves to no file,
which would leave every member unmatched and the line at 0.
Listed apart: the types the report doesn't list, with their kinds, and the equality-only types.

With --all it also prints, without failing, the members no test reaches: the public members of the same files whose
lines in the whole suite's report have no hit. A member is a report method of a class the compiler didn't generate;
a lambda, iterator or local function belongs to the member whose source span holds it, and an iterator or async
method, whose code is all in its generated class, is named by that class. A member counts as public when its
declaration, and every type around it, is declared public; a file type is not.
"""
import bisect
import glob
import os
import re
import subprocess
import sys
import tempfile
import xml.etree.ElementTree as ET
from xml.sax.saxutils import quoteattr

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

SRC = "src/NanoQL/"
EQUALITY = {"Equals", "GetHashCode", "op_Equality", "op_Inequality"}
# The member line's reading of C# declarations.
MODIFIERS = {"public", "private", "protected", "internal", "file", "static", "sealed", "abstract", "readonly",
             "partial", "unsafe", "ref", "new", "virtual", "override", "extern", "async", "const", "volatile",
             "required", "implicit", "explicit", "fixed"}
TYPE_KINDS = {"class", "struct", "interface", "enum", "record"}
ACCESSOR_PREFIXES = {"get_": ("get",), "set_": ("set", "init"), "add_": ("add",), "remove_": ("remove",)}
ACCESSOR_KEYWORDS = {kw for keywords in ACCESSOR_PREFIXES.values() for kw in keywords}
LITERAL = re.compile(r'(\$*)(@?)(\$*)("+)')
# An = that is part of an operator's name (operator ==, operator <=) rather than an initialiser's.
OPERATOR_SO_FAR = re.compile(r"\boperator\s*[^\w\s()\[\]]*$")
# An iterator's or async method's generated class: Outer/<Name>d__3.
STATE_MACHINE = re.compile(r"^(.+)/<(\w+)>d__\d+$")


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
    """{normalised path: {"lines": {number: hits}, "methods": {name: [numbers]}, "report methods": [(class, name,
    signature, {number: hits})]}}, the package names, and the NanoQL package's classes whose file resolves to no
    existing file."""
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
            read_class(cls, by_file.setdefault(key, {"lines": {}, "methods": {}, "report methods": []}))
    return by_file, packages, unresolved


def read_class(cls, entry):
    for line in cls.find("lines").iter("line") if cls.find("lines") is not None else []:
        n = int(line.get("number"))
        entry["lines"][n] = max(entry["lines"].get(n, 0), int(line.get("hits")))
    for method in cls.iter("method"):
        numbers = [int(ln.get("number")) for ln in method.iter("line")]
        entry["methods"].setdefault(method.get("name"), []).extend(numbers)
        lines = {}
        for ln in method.iter("line"):
            n = int(ln.get("number"))
            lines[n] = max(lines.get(n, 0), int(ln.get("hits")))
        entry["report methods"].append((cls.get("name"), method.get("name"), method.get("signature") or "", lines))


def public_types(text):
    return [(kind, name) for access, kind, name in sources.top_level_types(text) if access == "public"]


# ---- the member line: C# declarations and the report methods they hold ----

def blank(text):
    """The C# text with its comments, literals and preprocessor lines blanked and its newlines kept, so that its
    brackets balance and its line numbers stay the source's."""
    out, i, fresh = [], 0, True  # fresh: nothing but blanks yet on this line
    while i < len(text):
        c = text[i]
        if c == "\n":
            fresh = True
        elif (fresh and c == "#") or text.startswith("//", i):
            j = text.find("\n", i)
            i = len(text) if j < 0 else j
            continue
        elif text.startswith("/*", i):
            j = text.find("*/", i + 2)
            j = len(text) if j < 0 else j + 2
            out.append("\n" * text.count("\n", i, j))
            i = j
            continue
        elif c in "'\"" or (c in "$@" and LITERAL.match(text, i)):
            j = skip_literal(text, i)
            out.append("0" + "\n" * text.count("\n", i, j))
            i, fresh = j, False
            continue
        elif not c.isspace():
            fresh = False
        out.append(c)
        i += 1
    return "".join(out)


def skip_literal(text, i):
    """The index past the string or character literal at i: regular, verbatim, raw or interpolated."""
    if text[i] == "'":
        j = i + 1
        while j < len(text) and text[j] not in "'\n":
            j += 2 if text[j] == "\\" else 1
        return j + 1
    m = LITERAL.match(text, i)
    interpolated, verbatim, quotes = bool(m.group(1) or m.group(3)), bool(m.group(2)), len(m.group(4))
    if quotes >= 3 and not verbatim:
        end = text.find('"' * quotes, m.end())
        return len(text) if end < 0 else end + quotes
    j = m.start(4) + 1
    while j < len(text):
        c = text[j]
        if c == '"' and verbatim and text.startswith('""', j):
            j += 2
        elif c == '"':
            return j + 1
        elif c == "\n" and not verbatim:
            return j
        elif c == "\\" and not verbatim:
            j += 2
        elif interpolated and (text.startswith("{{", j) or text.startswith("}}", j)):
            j += 2
        elif interpolated and c == "{":
            j = skip_hole(text, j)
        else:
            j += 1
    return j


def skip_hole(text, j):
    """The index past an interpolated string's hole, at j, whose code may hold literals of its own."""
    depth = 0
    while j < len(text):
        c = text[j]
        if c in "'\"" or (c in "$@" and LITERAL.match(text, j)):
            j = skip_literal(text, j)
            continue
        depth += (c == "{") - (c == "}")
        j += 1
        if depth == 0:
            return j
    return j


class Declaration:
    """A type, member or accessor that a C# file declares, with the lines it spans."""

    def __init__(self, kind, access, start, parent, name=None, keyword=None):
        self.kind, self.access, self.start, self.end = kind, access, start, start
        self.parent, self.name, self.keyword = parent, name, keyword
        self.depth = parent.depth + 1 if parent else 0

    def public(self):
        """Whether it is declared public, and so is every type around it."""
        d = self
        while d is not None:
            if d.access != "public":
                return False
            d = d.parent
        return True


def access(modifiers, default):
    return next((a for a in ("public", "file", "internal", "protected", "private") if a in modifiers), default)


def split_header(text):
    """A declaration header's modifiers, its attributes skipped, and the words after them."""
    text = text.lstrip()
    while text.startswith("["):
        depth = 0
        for j, c in enumerate(text):
            depth += (c == "[") - (c == "]")
            if depth == 0:
                break
        text = text[j + 1:].lstrip()
    words = re.findall(r"\w+|\S", text)
    k = 0
    while k < len(words) and words[k] in MODIFIERS:
        k += 1
    return words[:k], words[k:]


def member_kind(words, type_name):
    """A member's kind from its header's words after the modifiers: constructor, operator, method, or property (which
    covers indexers and events)."""
    top, depth = [], 0  # the words outside brackets, each bracketed group kept as its opening bracket
    for w in words:
        if w in ("(", "["):
            if depth == 0:
                top.append(w)
            depth += 1
        elif w in (")", "]"):
            depth -= 1
        elif depth == 0:
            top.append(w)
    if "event" in top:
        return "property"
    if "operator" in top:
        return "operator"
    # A parameter list follows a name (or a generic method's >); a tuple type's ( doesn't.
    for k in range(1, len(top)):
        if top[k] == "(" and (re.match(r"\w", top[k - 1]) or top[k - 1] == ">"):
            return "constructor" if k == 1 and top[0] == type_name else "method"
    return "property"


def declarations(text):
    """The types, members and accessors a C# file declares, each with the lines it spans. Members are what can hold
    code: methods, constructors, operators and conversions, properties, indexers and events, expression-bodied ones
    ending at their semicolon; accessors are their get, set, init, add and remove. Fields, delegates and enum members
    aren't kept, and an extension block's members belong to the type around it."""
    b = blank(text)
    newlines = [k for k, c in enumerate(b) if c == "\n"]
    found = []

    def line(k):
        return bisect.bisect_left(newlines, k) + 1

    def skip_blanks(k):
        while k < len(b) and b[k].isspace():
            k += 1
        return k

    def close(k):
        """The index past the bracket that closes the one at k."""
        depth = 0
        for j in range(k, len(b)):
            depth += (b[j] in "([{") - (b[j] in ")]}")
            if depth == 0:
                return j + 1
        return len(b)

    def expression(k):
        """The index past the semicolon that ends the expression at k, or at the brace that ends its block."""
        depth = 0
        for j in range(k, len(b)):
            if b[j] in "([{":
                depth += 1
            elif b[j] in ")]}":
                if depth == 0:
                    return j
                depth -= 1
            elif b[j] == ";" and depth == 0:
                return j + 1
        return len(b)

    def header(k):
        """A declaration's header from k, up to its first ;, {, }, => or initialiser's = outside brackets, and that
        terminator with its index."""
        depth = 0
        for j in range(k, len(b)):
            c = b[j]
            if c in "([":
                depth += 1
            elif c in ")]":
                depth -= 1
            elif depth == 0 and c in ";{}":
                return b[k:j], c, j
            elif depth == 0 and c == "=":
                if b.startswith("=>", j):
                    return b[k:j], "=>", j
                if not OPERATOR_SO_FAR.search(b, k, j):
                    return b[k:j], "=", j
        return b[k:], "", len(b)

    def declare(kind, acc, k, parent, name=None, keyword=None):
        d = Declaration(kind, acc, line(k), parent, name, keyword)
        found.append(d)
        return d

    def accessors(opening, prop):
        """Reads the accessors in the braces at opening; returns the index past the closing brace."""
        k = opening + 1
        while True:
            k = skip_blanks(k)
            if k >= len(b) or b[k] == "}":
                return k + 1
            text, end, j = header(k)
            mods, words = split_header(text)
            keyword = words[0] if words else ""
            if keyword not in ACCESSOR_KEYWORDS or end not in (";", "{", "=>"):
                return close(opening)
            d = declare("accessor", access(mods, prop.access), k, prop, keyword=keyword)
            k = j + 1 if end == ";" else close(j) if end == "{" else expression(j)
            d.end = line(k - 1)

    def block(k, parent):
        """Reads the declarations from k to the brace that closes their block; returns the index past it."""
        while True:
            k = skip_blanks(k)
            if k >= len(b):
                return k
            if b[k] == "}":
                return k + 1
            text, end, j = header(k)
            mods, words = split_header(text)
            first = words[0] if words else ""
            if end == "{" and first in TYPE_KINDS:
                name = next((w for w in words[1:] if w not in ("class", "struct")), "")
                d = declare("type", access(mods, "internal" if parent is None else "private"), k, parent, name, first)
                k = close(j) if first == "enum" else block(j + 1, d)
                d.end = line(k - 1)
            elif end == "{" and (first == "namespace" or (first == "extension" and words[1:2] in (["("], ["<"]))):
                k = block(j + 1, parent)
            elif end in ("{", "=>") and parent is not None:
                kind = member_kind(words, parent.name)
                d = declare(kind, access(mods, "public" if parent.keyword == "interface" else "private"), k, parent)
                if end == "=>":
                    k = expression(j)
                elif kind == "property":
                    k = accessors(j, d)
                    after = skip_blanks(k)
                    if b.startswith("=", after) and not b.startswith("=>", after):
                        k = expression(after)  # the property's initialiser
                else:
                    k = close(j)
                d.end = line(k - 1)
            elif end == "{":
                k = close(j)
            elif end in ("=", "=>"):
                k = expression(j)
            elif end == ";":
                k = j + 1
            else:
                return j + 1 if end == "}" else j

    block(0, None)
    return found


def attribute(decls, name, lines):
    """The member or accessor a report method's lines belong to: the innermost that holds the first of them that any
    member holds. A constructor's lines start with the field initialisers', so only a constructor takes them; an
    accessor's name picks it among the accessors of a property written on one line."""
    constructor = name in (".ctor", ".cctor")
    for n in sorted(lines):
        holding = [d for d in decls if d.kind != "type" and d.start <= n <= d.end
                   and (d.kind == "constructor" or not constructor)]
        if not holding:
            continue
        d = max(holding, key=lambda h: h.depth)
        wanted = next((kw for prefix, kw in ACCESSOR_PREFIXES.items() if name.startswith(prefix)), None)
        prop = d.parent if d.kind == "accessor" else d
        if wanted and prop.kind == "property":
            d = next((a for a in holding if a.parent is prop and a.keyword in wanted), d)
        return d
    return None


def shown(cls):
    """A report class's name without its namespace, nested names joined by dots: Calendar.WesternImpl."""
    outer, *nested = cls.split("/")
    return ".".join([outer.rsplit(".", 1)[-1]] + nested)


def unreached_members(root, paths, by_file):
    """The public members of the ported files among paths whose lines in the whole suite's report have no hit, as
    sorted "path: Type.member" lines."""
    listed = set()
    for path in paths:
        full = os.path.join(root, path)
        header = sources.Header(full)
        entry = by_file.get(os.path.normcase(os.path.normpath(full)))
        if not header.upstream or entry is None:
            continue
        decls = declarations(header.text)
        members, generated = [], {}
        for cls, name, signature, lines in entry["report methods"]:
            d = attribute(decls, name, lines) if lines else None
            if d is None:
                continue
            # The compiler's classes and methods (lambdas, local functions, iterators, file types) carry a <.
            if "<" in cls or name.startswith("<"):
                generated.setdefault(d, []).append((cls, lines))
            else:
                members.append((f"{shown(cls)}.{name}{signature}", lines, d))
        # An iterator or async method has no report method of its own: its code is all in its state machine's.
        named = {d for _, _, d in members}
        for d, parts in generated.items():
            machine = next((m for m in (STATE_MACHINE.match(cls) for cls, _ in parts) if m), None)
            if d not in named and machine:
                members.append((f"{shown(machine.group(1))}.{machine.group(2)}", {}, d))
        for member, lines, d in members:
            hits = list(lines.values()) + [h for _, more in generated.get(d, []) for h in more.values()]
            if d.public() and not any(h > 0 for h in hits):
                listed.add(f"{path}: {member}")
    return sorted(listed)


def check(root, results, commit_files, all_files, whole=None):
    """Returns (errors, lines of the report); with whole, the whole suite's results folder, the member line too."""
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
    if whole is not None:
        # The whole suite's report only informs: a member no test reaches is listed, never red.
        wholes = sorted(glob.glob(os.path.join(whole, "**", "*.cobertura*.xml"), recursive=True))
        if len(wholes) != 1:
            errors.append(f"the whole suite's results folder holds {len(wholes)} Cobertura reports, not exactly one")
        else:
            whole_by_file, _, whole_unresolved = read_report(wholes[0])
            for cls in whole_unresolved:
                errors.append(f"the whole suite's report's class {cls} resolves to no file under its <source> roots")
            members = unreached_members(root, commit_files, whole_by_file)
            out.append(f"members no test reaches: {len(members)}" + "".join(f"\n    {m}" for m in members))
    return errors, out


# ---- self-test: synthetic files and a synthetic report ----

HEADER = ("// SPDX-FileCopyrightText: 2026 The nanoql Authors\n// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause\n"
          "// Upstream: ql/time/date.hpp @ v1.44\n")
# The ported type whose members the member line reads; the whole suite's report cites its lines.
PORTED = """namespace NanoQL;

public sealed class Ported
{
    private int x = 1;

    private int Size { get; } = 2;

    public Ported(int x)
    {
        this.x = x;
    }

    public int Length { get => x; private set => x = value; }

    public int F() => 1;

    public int Unreached() => 2;

    public int Untagged() => 3;

    public static Ported operator +(Ported a, Ported b) => a;

    public IEnumerable<int> Pending()
    {
        yield return 4;
    }

    private int Secret() => 5;

    protected static class Nested
    {
        public static int Deep() => 6;
    }
}

file static class Helper
{
    public static int Shared() => 7;
}
"""


def cobertura(source, package, classes):
    """A Cobertura report's text; classes maps each class to its file and its methods, (name, signature, {line:
    hits})."""
    parts = []
    for cls, (filename, methods) in classes.items():
        body = "".join(f'<method name={quoteattr(m)} signature={quoteattr(s)}><lines>'
                       + "".join(f'<line number="{n}" hits="{h}"/>' for n, h in lines.items()) + "</lines></method>"
                       for m, s, lines in methods)
        merged = {}
        for _, _, lines in methods:
            for n, h in lines.items():
                merged[n] = max(merged.get(n, 0), h)
        parts.append(f'<class name={quoteattr("NanoQL." + cls)} filename="{filename}"><methods>{body}</methods><lines>'
                     + "".join(f'<line number="{n}" hits="{h}"/>' for n, h in merged.items()) + "</lines></class>")
    return (f'<coverage><sources><source>{source}</source></sources><packages><package name="{package}"><classes>'
            + "".join(parts) + "</classes></package></packages></coverage>")


def synthetic(tmp, scenario):
    root = os.path.join(tmp, scenario, "repo")
    results = os.path.join(tmp, scenario, "results")
    whole = os.path.join(tmp, scenario, "whole")
    os.makedirs(results)
    os.makedirs(whole)
    files = {
        "Ported.cs": HEADER + PORTED,
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
    reports = wholes = 1
    if scenario == "no report":
        reports = 0
    elif scenario == "two reports":
        reports = 2
    elif scenario == "no whole-suite report":
        wholes = 0
    elif scenario == "two whole-suite reports":
        wholes = 2
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
    xml = cobertura(source, package, {cls: (cls.split("/")[0] + ".cs", [(m, "()", {10 + i: h})
                                                                       for i, m in enumerate(methods[cls])])
                                      for cls, h in hits.items()})
    for k in range(reports):
        with open(os.path.join(results, f"coverage.cobertura.{k}.xml"), "w", encoding="utf-8") as f:
            f.write(xml)
    # The whole suite's report: each of Ported.cs's report methods at its declaration's lines.
    text = files["Ported.cs"]

    def at(marker):
        return text[:text.index(marker)].count("\n") + 1

    def method(name, signature, hit, *markers):
        return name, signature, {at(m): hit for m in markers}

    if scenario == "a moved source root in the whole-suite report":
        source = os.path.join(tmp, "elsewhere", SRC)
    whole_xml = cobertura(source, package, {
        "Ported": ("Ported.cs", [
            method(".ctor", "(System.Int32)", 0, "private int x", "private int Size", "public Ported(", "this.x = x"),
            method("get_Length", "()", 0, "Length {"),
            method("set_Length", "(System.Int32)", 0, "Length {"),
            method("F", "()", 1, "F() =>"),
            method("Unreached", "()", 0, "Unreached() =>"),
            method("Untagged", "()", 1, "Untagged() =>"),
            method("op_Addition", "(NanoQL.Ported,NanoQL.Ported)", 0, "operator +"),
            method("Secret", "()", 0, "Secret() =>")]),
        "Ported/Nested": ("Ported.cs", [method("Deep", "()", 0, "Deep() =>")]),
        "Ported/<Pending>d__6": ("Ported.cs", [method("MoveNext", "()", 0, "yield return")]),
        "<Ported>F0A1B2C3D4E5F6__Helper": ("Ported.cs", [method("Shared", "()", 0, "Shared() =>")])})
    for k in range(wholes):
        with open(os.path.join(whole, f"coverage.cobertura.{k}.xml"), "w", encoding="utf-8") as f:
            f.write(whole_xml)
    commit = [SRC + name for name in ("Ported.cs", "Kind.cs", "Iter.cs", "Hidden.cs", "Own.cs", "Results.cs")]
    return root, results, whole, commit


def self_test():
    reds = ["no report", "two reports", "no package", "no covered line", "ExcludeFromCodeCoverage",
            "equality-only without its line", "equality members and an uncovered method", "a moved source root"]
    whole_reds = ["no whole-suite report", "two whole-suite reports", "a moved source root in the whole-suite report"]
    greens = {"an enum absent from the report is listed apart": lambda o: "Kind (enum)" in o,
              "a type covered only through its generated nested class counts": lambda o: "types 2/2" in o,
              "an internal type and an allowlisted file at 0 aren't counted": lambda o: "types 2/2" in o,
              "an equality-only type is listed apart": lambda o: "Results (equality-tested" in o,
              "without --all, no member line": lambda o: "members no test reaches" not in o}
    # With --all; the listed lines are "    src/NanoQL/Ported.cs: Ported.<member>".
    members = {"a public method no test runs is listed":
               lambda o: "members no test reaches: 5" in o and "    src/NanoQL/Ported.cs: Ported.Unreached()" in o,
               "a private method or accessor no test runs isn't listed":
               lambda o: "Secret" not in o and "set_Length" not in o,
               "a public method only an untagged test runs isn't listed": lambda o: "Untagged" not in o,
               "a property getter, an operator and a constructor are listed under their names":
               lambda o: all(f"Ported.cs: Ported.{m}\n" in o + "\n" for m in
                             ("get_Length()", "op_Addition(NanoQL.Ported,NanoQL.Ported)", ".ctor(System.Int32)")),
               "a public member of a protected nested type isn't listed": lambda o: "Deep" not in o,
               "a file type's method isn't listed": lambda o: "Shared" not in o,
               "an iterator no test runs is listed by its method's name, and no generated method is":
               lambda o: "Ported.cs: Ported.Pending\n" in o + "\n" and "<" not in o}
    passed = 0
    with tempfile.TemporaryDirectory() as tmp:
        for scenario in reds + whole_reds:
            root, results, whole, commit = synthetic(tmp, scenario)
            errors, _ = check(root, results, commit, commit, whole if scenario in whole_reds else None)
            ok = bool(errors)
            passed += ok
            print(f"  {'red as expected' if ok else 'MISSED'}: {scenario}")
        root, results, whole, commit = synthetic(tmp, "green")
        for run, tests in ((None, greens), (whole, members)):
            errors, out = check(root, results, commit, commit, run)
            text = "\n".join(out)
            for name, test in tests.items():
                ok = not errors and test(text)
                passed += ok
                print(f"  {'green as expected' if ok else 'FAILED'}: {name}")
            if errors:
                print("\n".join("    " + e for e in errors))
    total = len(reds) + len(whole_reds) + len(greens) + len(members)
    print(f"self-test: {passed}/{total}")
    return 0 if passed == total else 1


def main(argv):
    if "--self-test" in argv:
        return self_test()
    whole = None
    if "--all" in argv:
        k = argv.index("--all")
        if k + 1 >= len(argv) or argv[k + 1].startswith("--"):
            print(__doc__)
            return 2
        whole, argv = argv[k + 1], argv[:k] + argv[k + 2:]
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    errors, out = check(root, args[0], changed_files(root), ported_files(root), whole)
    print("\n".join(out))
    for e in errors:
        print("RED " + e)
    print("check-coverage: " + ("passed" if not errors else f"{len(errors)} errors"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
