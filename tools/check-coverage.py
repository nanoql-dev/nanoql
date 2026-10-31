# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""The coverage gate's type check, and its member line (docs/porting-playbook.md §9.5).

Usage: .venv\\Scripts\\python -I tools/check-coverage.py <results folder> [--qlnet <results folder>]
           [--all <results folder>] [--since <commit>]

Run it after the coverage runs, each of which writes one Cobertura report into its emptied results folder: the
tagged run, and for --all the rest of the suite's, the tests the tagged run leaves out, both from the repository root:

    dotnet test --solution NanoQL.slnx -c Release --filter-trait "Oracle=QuantLib" --coverage
        --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
        --coverage-settings tests\\NanoQL.Tests\\CodeCoverage.xml --results-directory TestResults\\G7
    dotnet test --solution NanoQL.slnx -c Release --filter-not-trait "Oracle=QuantLib" --coverage
        --coverage-output-format cobertura --coverage-output coverage.cobertura.xml
        --coverage-settings tests\\NanoQL.Tests\\CodeCoverage.xml --results-directory TestResults\\G7rest

With --qlnet, the second tagged run's folder, the QLNet oracle's (run A', the same command with
--filter-trait "Oracle=QLNet" into TestResults\\G7qlnet): a type whose file's Upstream line names QLNet passes on that
run's covered lines alone, and every other type on the first run's, so each extensions type passes against the library
it comes from. A QLNet type the commit changes needs --qlnet.

The members report reads the reports merged, each line's hits the most any gives, so the runs count as one run of the
whole suite; a whole-suite report in --all's folder reads the same.

The types it checks are the top-level public types of the .cs files under src/NanoQL/ that the commit adds or
changes (the working tree against HEAD, untracked files included) and that carry an Upstream line, and of every such
file under src/NanoQL.Extensions/, the extensions layer, whose types the tagged tests reach or an "// Own-tested:" line
lists apart. Report entries
match those files by path, so nested, file and compiler-generated classes count toward their file's type.

Red when: the folder (or --qlnet's) doesn't hold exactly one report; the commit ports a public type and the report
doesn't list the NanoQL package (or its extensions type's, NanoQL.Extensions); a QLNet type of the commit and no
--qlnet; a listed type has no covered line; a ported file carries ExcludeFromCodeCoverage; a type whose only
executable lines are its equality members lacks a "// Equality-tested:" line naming an existing test file; or, with
--all, its folder doesn't hold exactly one report, or a NanoQL class of that report resolves to no file, which would
leave every member unmatched and the line at 0.
A type with no covered line but an "// Own-tested:" line whose first word names an existing test file (from the
repository root or tests/NanoQL.Tests/) is listed apart instead: a type whose every executable line is nanoql's own,
which no QuantLib-checked test can reach, such as a refusal of what QuantLib can't compile (the S-xcr run's stop 4).
Listed apart: the types the report doesn't list, with their kinds, the equality-only types and the own-tested ones. A
constructor the source doesn't declare (field and property initialisers, a primary constructor's parameters) counts
as no code, as the coverage engines don't agree on listing one.

With --all it also prints, without failing, the members no test reaches: the public members of the same files, or
with --since of the files changed since that commit (a slot's, at its last commit), whose lines no report hits.
A member is a report method of a class the compiler didn't generate;
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
import xml.etree.ElementTree as ET

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

SRC = "src/NanoQL/"
# The extensions layer, nanoql's own code with no Upstream line, whose types the gate checks as it checks ported ones.
EXTENSIONS = "src/NanoQL.Extensions/"
CHECKED = (SRC, EXTENSIONS)
# QLNet's paths on an Upstream line: a QLNet port, whose types pass on the QLNet run's coverage (--qlnet).
QLNET_PATHS = ("src/QLNet/", "tests/QLNet.Tests/")
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
        if "D" in status or not path.startswith(CHECKED) or not path.endswith(".cs"):
            continue
        paths.append(path)
    return sorted(paths)


def files_since(root, commit):
    """The .cs files under src/NanoQL/ and src/NanoQL.Extensions/ that the working tree adds or changes against commit,
    committed or not."""
    tracked = subprocess.run(["git", "-C", root, "diff", "--name-only", "--diff-filter=AMR", "-z", commit],
                             capture_output=True, check=True).stdout.decode("utf-8").split("\0")
    untracked = subprocess.run(["git", "-C", root, "ls-files", "--others", "--exclude-standard", "-z"],
                               capture_output=True, check=True).stdout.decode("utf-8").split("\0")
    return sorted({p for p in tracked + untracked if p.startswith(CHECKED) and p.endswith(".cs")})


def ported_files(root):
    """Every .cs file under src/NanoQL/ that carries an Upstream line, and every one under src/NanoQL.Extensions/."""
    return [p for p in sources.git_files(root)
            if p.endswith(".cs") and (p.startswith(EXTENSIONS)
                                      or (p.startswith(SRC) and sources.Header(os.path.join(root, p)).upstream))]


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
            if found is None and package.get("name") in ("NanoQL", "NanoQL.Extensions"):
                unresolved.append(f"{cls.get('name')} ({filename})")
            key = os.path.normcase(os.path.normpath(found or candidates[0]))
            read_class(cls, by_file.setdefault(key, {"lines": {}, "methods": {}, "report methods": []}))
    return by_file, packages, unresolved


def merged(first, second):
    """Two reports' entries as one: each line's hits the most either gives; a report method's lines merged with the
    same method's in the other report, paired by class, name, signature and occurrence, so a member either reaches
    counts as reached, and one report's entries are its own."""
    out = {}
    for key in set(first) | set(second):
        a = first.get(key, {"lines": {}, "methods": {}, "report methods": []})
        b = second.get(key, {"lines": {}, "methods": {}, "report methods": []})
        lines = dict(a["lines"])
        for n, h in b["lines"].items():
            lines[n] = max(lines.get(n, 0), h)
        methods = {name: list(numbers) for name, numbers in a["methods"].items()}
        for name, numbers in b["methods"].items():
            methods.setdefault(name, []).extend(numbers)
        report, at, seen = [], {}, {}
        for cls, name, signature, hits in a["report methods"]:
            k = (cls, name, signature)
            seen[k] = seen.get(k, 0) + 1
            at[(k, seen[k])] = len(report)
            report.append((cls, name, signature, dict(hits)))
        seen = {}
        for cls, name, signature, hits in b["report methods"]:
            k = (cls, name, signature)
            seen[k] = seen.get(k, 0) + 1
            i = at.get((k, seen[k]))
            if i is None:
                report.append((cls, name, signature, dict(hits)))
                continue
            mine = report[i][3]
            for n, h in hits.items():
                mine[n] = max(mine.get(n, 0), h)
        out[key] = {"lines": lines, "methods": methods, "report methods": report}
    return out


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


def implicit_lines(text, entry):
    """A file's lines that only constructors its source doesn't declare hold: field and property initialisers, a
    primary constructor's parameters. Microsoft's coverage lists such a constructor and Coverlet's doesn't; they count
    as no executable line, so a type's code is what its source declares, whichever engine reports it."""
    found = [(name, lines) for _, name, _, lines in entry["report methods"] if name in (".ctor", ".cctor") and lines]
    if not found:
        return set()
    decls = declarations(text)
    return {n for name, lines in found if attribute(decls, name, lines) is None for n in lines}


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
        if not (header.upstream or path.startswith(EXTENSIONS)) or entry is None:
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


def is_qlnet(header):
    """Whether a file's Upstream line names QLNet's paths: its types pass on the QLNet run's coverage."""
    return any(up.startswith(QLNET_PATHS) for up in header.upstream)


def check(root, results, commit_files, all_files, whole=None, member_files=None, qlnet=None):
    """Returns (errors, lines of the report); with qlnet, the QLNet run's results folder, which QLNet's types read;
    with whole, the rest of the suite's results folder (or the whole suite's), the member line too, over member_files,
    the commit's files by default."""
    errors, out = [], []
    reports = sorted(glob.glob(os.path.join(results, "**", "*.cobertura*.xml"), recursive=True))
    if len(reports) != 1:
        return [f"the results folder holds {len(reports)} Cobertura reports, not exactly one"], out
    by_file, packages, unresolved = read_report(reports[0])
    # The engine lists every class of the instrumented module, so in a healthy run every NanoQL entry resolves to a
    # file; one that doesn't means the report's paths don't match the repository's, and no type could be matched.
    for cls in unresolved:
        errors.append(f"the report's class {cls} resolves to no file under its <source> roots")
    qlnet_by_file, qlnet_packages = None, []
    if qlnet is not None:
        qlnet_reports = sorted(glob.glob(os.path.join(qlnet, "**", "*.cobertura*.xml"), recursive=True))
        if len(qlnet_reports) != 1:
            return [f"the --qlnet results folder holds {len(qlnet_reports)} Cobertura reports, not exactly one"], out
        qlnet_by_file, qlnet_packages, qlnet_unresolved = read_report(qlnet_reports[0])
        for cls in qlnet_unresolved:
            errors.append(f"the --qlnet report's class {cls} resolves to no file under its <source> roots")

    def assess(paths, record):
        counted = covered_types = lines_valid = lines_covered = 0
        apart, equality_only = [], []
        for path in paths:
            full = os.path.join(root, path)
            header = sources.Header(full)
            if not (header.upstream or path.startswith(EXTENSIONS)):
                continue
            types = public_types(header.text)
            if not types:
                continue
            kind, name = types[0]
            if record and "ExcludeFromCodeCoverage" in header.text:
                errors.append(f"{path}: carries ExcludeFromCodeCoverage")
            oracle = "QuantLib"
            report = by_file
            if is_qlnet(header):
                oracle = "QLNet"
                if qlnet_by_file is None:
                    if record:
                        errors.append(f"{path}: a QLNet type, and no --qlnet report of the Oracle=QLNet tests")
                    continue
                report = qlnet_by_file
                used.add("QLNet")
            elif record:
                used.add("QuantLib")
            entry = report.get(os.path.normcase(os.path.normpath(full)))
            implicit = implicit_lines(header.text, entry) if entry is not None else set()
            lines = {} if entry is None else {n: h for n, h in entry["lines"].items() if n not in implicit}
            if not lines:
                apart.append(f"{name} ({kind})")
                continue
            lines_valid += len(lines)
            covered = sum(1 for h in lines.values() if h > 0)
            lines_covered += covered
            with_lines = {m for m, numbers in entry["methods"].items() if set(numbers) - implicit}
            if with_lines and with_lines <= EQUALITY:
                tested = header.equality_tested
                if tested and os.path.isfile(os.path.join(root, tested)):
                    equality_only.append(f"{name} (equality-tested: {tested})")
                elif record:
                    errors.append(f"{path}: {name}'s only code is equality, and it has no // Equality-tested: line "
                                  "naming an existing test file")
                continue
            own = (header.own_tested or "").split()
            if not covered and own and any(os.path.isfile(os.path.join(base, own[0]))
                                           for base in (root, os.path.join(root, "tests", "NanoQL.Tests"))):
                apart.append(f"{name} (own-tested: {own[0]})")
                continue
            counted += 1
            if covered:
                covered_types += 1
            elif record:
                errors.append(f"{path}: {name} has no line covered by the Oracle={oracle} tests")
        return counted, covered_types, lines_covered, lines_valid, apart, equality_only

    used = set()
    t, tc, lc, lv, apart, equality_only = assess(commit_files, True)
    if t + len(equality_only) and "QuantLib" in used and "NanoQL" not in packages:
        errors.append("the commit ports a public type, and the report doesn't list the NanoQL package")
    extension_files = [p for p in commit_files if p.startswith(EXTENSIONS)]
    if extension_files and t and "QuantLib" in used and "NanoQL.Extensions" not in packages \
            and any(not is_qlnet(sources.Header(os.path.join(root, p))) for p in extension_files):
        errors.append("the commit changes an extensions type, and the report doesn't list the NanoQL.Extensions package")
    if t and "QLNet" in used and "NanoQL.Extensions" not in qlnet_packages:
        errors.append("the commit changes a QLNet type, and the --qlnet report doesn't list the NanoQL.Extensions "
                      "package")
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
        # The members report only informs: a member no test reaches is listed, never red. It reads the tagged report
        # merged with --all's, the rest of the suite's (or the whole suite's), as one run of the whole suite.
        wholes = sorted(glob.glob(os.path.join(whole, "**", "*.cobertura*.xml"), recursive=True))
        if len(wholes) != 1:
            errors.append(f"the --all results folder holds {len(wholes)} Cobertura reports, not exactly one")
        else:
            rest_by_file, _, rest_unresolved = read_report(wholes[0])
            for cls in rest_unresolved:
                errors.append(f"the --all report's class {cls} resolves to no file under its <source> roots")
            tagged = by_file if qlnet_by_file is None else merged(by_file, qlnet_by_file)
            members = unreached_members(root, commit_files if member_files is None else member_files,
                                        merged(tagged, rest_by_file))
            out.append(f"members no test reaches: {len(members)}" + "".join(f"\n    {m}" for m in members))
    return errors, out


def main(argv):
    qlnet = None
    if "--qlnet" in argv:
        k = argv.index("--qlnet")
        if k + 1 >= len(argv) or argv[k + 1].startswith("--"):
            print(__doc__)
            return 2
        qlnet, argv = argv[k + 1], argv[:k] + argv[k + 2:]
    whole = None
    if "--all" in argv:
        k = argv.index("--all")
        if k + 1 >= len(argv) or argv[k + 1].startswith("--"):
            print(__doc__)
            return 2
        whole, argv = argv[k + 1], argv[:k] + argv[k + 2:]
    since = None
    if "--since" in argv:
        k = argv.index("--since")
        if k + 1 >= len(argv) or argv[k + 1].startswith("--"):
            print(__doc__)
            return 2
        since, argv = argv[k + 1], argv[:k] + argv[k + 2:]
    args = [a for a in argv if not a.startswith("--")]
    if len(args) != 1:
        print(__doc__)
        return 2
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    errors, out = check(root, args[0], changed_files(root), ported_files(root), whole,
                        None if since is None else files_since(root, since), qlnet)
    print("\n".join(out))
    for e in errors:
        print("RED " + e)
    print("check-coverage: " + ("passed" if not errors else f"{len(errors)} errors"))
    return 1 if errors else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
