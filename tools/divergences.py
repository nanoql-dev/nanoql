# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""Generates docs/divergences.md: every documented divergence from QuantLib (docs/porting-playbook.md §9.3), one entry
per test class under tests/NanoQL.Tests/Divergences/, with its file, its <summary> as text and the names of its facts;
then, in a section of their own, the divergences from QLNet, one entry per test class under
tests/NanoQL.Tests/Divergences/QLNet/, each recording QLNet's value beside nanoql's.

Usage: .venv\\Scripts\\python -I tools/divergences.py [--root <repository>] [--check]

The test files come in the order of their paths, sorted as tools/porting-map.py and the other tools list files
(sources.git_files), QuantLib's first and QLNet's after, and each file's top-level classes in the order it declares
them. A summary's <c> becomes a code
span, and so does the name a <see> (cref or langword), <paramref> or <typeparamref> gives; any other element keeps its
text, and whitespace is collapsed. A fact is a method marked [Fact] or [Theory]. A class without a <summary> is an
error naming its file. With --check, it writes nothing and exits 1 if the committed page differs from a fresh one,
naming the first entry that differs.
"""
import os
import re
import sys
import xml.etree.ElementTree as ElementTree

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import sources  # noqa: E402

FOLDER = "tests/NanoQL.Tests/Divergences/"
# The divergences from QLNet, the oracle of the extensions' QLNet ports, in a section of the page after QuantLib's.
QLNET = FOLDER + "QLNet/"
QLNET_SECTION = "QLNet 1.13.2"
PAGE = "docs/divergences.md"
HEADER = """<!--
  SPDX-FileCopyrightText: 2026 The nanoql Authors
  SPDX-License-Identifier: Apache-2.0
-->
"""
# Comments, strings and character literals, blanked with their line breaks kept, so that braces count true and each
# line keeps its number.
NOISE = re.compile(r'//[^\n]*|/\*.*?\*/|@"(?:""|[^"])*"|"(?:\\.|[^"\\\n])*"|\'(?:\\.|[^\'\\\n])*\'', re.S)
CLASS = re.compile(r"^(?:\[[^\]]*\]\s*)*(?:(?:public|internal|file|static|sealed|abstract|partial|unsafe)\s+)*"
                   r"class\s+(\w+)")
FACT = re.compile(r"^\s*\[\s*(?:Xunit\.)?(?:Fact|Theory)(?:Attribute)?\b")
METHOD = re.compile(r"^\s*(?:\[[^\]]*\]\s*)*public\s+(?:async\s+)?(?:void|Task|ValueTask)\s+(\w+)\s*\(")
# Elements whose content is a block of its own, kept apart from the text around it.
BLOCKS = {"para", "br", "list", "listheader", "item", "term", "description"}


class DivergenceError(ValueError):
    """A test file the page can't be generated from."""


def collapse(text):
    return re.sub(r"[ \t\r\n]+", " ", text).strip()


def code(text):
    """text as a Markdown code span, its fence longer than any run of backticks it holds."""
    fence = "`" * (max((len(run) for run in re.findall(r"`+", text)), default=0) + 1)
    pad = " " if text.startswith("`") or text.endswith("`") else ""
    return f"{fence}{pad}{text}{pad}{fence}"


def markdown(element):
    """An XML documentation element's content as Markdown text, whitespace not yet collapsed."""
    out = [element.text or ""]
    for child in element:
        name = child.get("cref") or child.get("langword") or child.get("name")
        if child.tag == "c":
            out.append(code(collapse("".join(child.itertext()))))
        elif child.tag in ("see", "seealso", "paramref", "typeparamref") and name and not "".join(child.itertext()):
            out.append(code(name))
        elif child.tag in BLOCKS:
            out.append(" " + markdown(child) + " ")
        else:
            out.append(markdown(child))
        out.append(child.tail or "")
    return "".join(out)


def blank(match):
    return re.sub(r"[^\n]", " ", match.group(0))


def top_level_classes(text):
    """The top-level classes of a C# file, as (name, index of its declaration's line, index of its closing line)."""
    lines = NOISE.sub(blank, text).split("\n")
    top = 1 if any(re.match(r"\s*namespace\s+[\w.]+\s*(\{|$)", line) for line in lines) else 0
    found, level, current = [], 0, None
    for i, line in enumerate(lines):
        if current is None and level == top:
            m = CLASS.match(line.strip())
            if m:
                current = [m.group(1), i, False]
        for ch in line:
            if ch == "{":
                level += 1
                if current is not None:
                    current[2] = True
            elif ch == "}":
                level -= 1
                if current is not None and current[2] and level == top:
                    found.append((current[0], current[1], i))
                    current = None
    return found


def summary(path, lines, at, name):
    """The <summary> of the class declared on lines[at], from the /// lines above it and its attributes."""
    i = at - 1
    while i >= 0 and lines[i].strip().startswith("["):
        i -= 1
    end = i + 1
    while i >= 0 and lines[i].strip().startswith("///"):
        i -= 1
    doc = "\n".join(re.sub(r"^\s*///", "", line) for line in lines[i + 1:end])
    try:
        element = ElementTree.fromstring(f"<doc>{doc}</doc>").find("summary")
    except ElementTree.ParseError as e:
        raise DivergenceError(f"{path}: class {name}'s documentation comment isn't well-formed XML ({e})") from None
    if element is None:
        raise DivergenceError(f"{path}: class {name} has no <summary>")
    return collapse(markdown(element))


def facts(lines, first, last):
    """The names of the [Fact] and [Theory] methods declared on lines[first..last]."""
    names, marked = [], False
    for line in lines[first:last + 1]:
        if FACT.match(line):
            marked = True
        m = METHOD.match(line)
        if m:
            if marked:
                names.append(m.group(1))
            marked = False
    return names


def oracle_of(path):
    """The oracle a Divergences/ test file departs from: "QuantLib" for a file in the folder itself, "QLNet" for one in
    its QLNet/ subfolder, or None for any other file."""
    if not path.endswith(".cs"):
        return None
    if path.startswith(FOLDER) and "/" not in path[len(FOLDER):]:
        return "QuantLib"
    if path.startswith(QLNET) and "/" not in path[len(QLNET):]:
        return "QLNet"
    return None


def build(root):
    """The page's entries, one per test class: {"path", "name", "summary", "facts", "oracle"}, QuantLib's first."""
    entries = []
    paths = sources.git_files(root)
    for path in [p for p in paths if oracle_of(p) == "QuantLib"] + [p for p in paths if oracle_of(p) == "QLNet"]:
        text = sources.read_text(os.path.join(root, path))
        lines = text.split("\n")
        code_lines = NOISE.sub(blank, text).split("\n")
        for name, at, last in top_level_classes(text):
            entries.append({"path": path, "name": name, "summary": summary(path, lines, at, name),
                            "facts": facts(code_lines, at, last), "oracle": oracle_of(path)})
    return entries


def render(entries):
    out = [HEADER, "# Divergences", "",
           "Every documented divergence from QuantLib v1.44, one entry per test class under",
           "`tests/NanoQL.Tests/Divergences/` with its file, its summary and its facts, generated from those files by",
           "`tools/divergences.py`: regenerate it with `.venv\\Scripts\\python -I tools/divergences.py` rather than",
           "editing it by hand."]
    section = False
    for entry in entries:
        if entry["oracle"] == "QLNet" and not section:
            section = True
            out += ["", f"## {QLNET_SECTION}", "",
                    "Every documented divergence from QLNet 1.13.2, the oracle of the extensions' ports of QLNet, one",
                    "entry per test class under `tests/NanoQL.Tests/Divergences/QLNet/`, each recording QLNet's value",
                    "beside nanoql's."]
        heading = "###" if section else "##"
        out += ["", f"{heading} {entry['name']}", "",
                f"[`Divergences/{entry['path'][len(FOLDER):]}`](../{entry['path']})", "",
                entry["summary"]]
        if entry["facts"]:
            out += ["", "Facts:", ""] + [f"- {code(name)}" for name in entry["facts"]]
    return "\n".join(out) + "\n"


def entries_of(text):
    """A page's text as (its header, [(entry name, entry text)]), each entry running from its "## " or "### " line,
    without the line breaks that end it, so that an entry added after the last one leaves the last one as it was. The
    QLNet section's own heading is an entry named for the section, holding its introduction."""
    parts = re.split(r"^(?=#{2,3} )", text, flags=re.M)
    return parts[0].rstrip("\n"), [(part.split("\n", 1)[0].lstrip("#").strip(), part.rstrip("\n"))
                                   for part in parts[1:]]


def difference(root, entries=None):
    """None when docs/divergences.md is, byte for byte, a fresh generation (from entries, when build has given them);
    otherwise what differs first: "missing page", the page's line endings or header, or the first entry that
    differs, named by its class."""
    try:
        with open(os.path.join(root, *PAGE.split("/")), "rb") as f:
            committed = f.read()
    except FileNotFoundError:
        return "missing page"
    fresh = render(build(root) if entries is None else entries)
    if committed == fresh.encode("utf-8"):
        return None
    text = committed.decode("utf-8", errors="replace")
    if "\r" in text:
        return "its line endings aren't LF"
    header, have = entries_of(text)
    fresh_header, want = entries_of(fresh)
    if header != fresh_header:
        return "its header differs"
    names, listed = {n for n, _ in want}, {n for n, _ in have}
    for i in range(max(len(have), len(want))):
        h = have[i] if i < len(have) else None
        w = want[i] if i < len(want) else None
        if h == w:
            continue
        if h is None or (w is not None and h[0] != w[0] and w[0] not in listed):
            return f"first differing entry: {w[0]}, missing from the page"
        if h[0] not in names:
            return f"first differing entry: {h[0]}, which no test class has"
        if w is None or h[0] != w[0]:
            return f"first differing entry: {h[0]}, out of order"
        return f"first differing entry: {h[0]}, whose text differs"
    return "its bytes differ"


def main(argv):
    args = [a for a in argv if not a.startswith("--")]
    root = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
    if "--root" in argv:
        i = argv.index("--root")
        if i + 1 >= len(argv):
            print(__doc__)
            return 2
        root = argv[i + 1]
        args.remove(root)
    if args:
        print(__doc__)
        return 2
    try:
        if "--check" in argv:
            detail = difference(root)
            print("divergence page: reproduces" if detail is None else f"divergence page: STALE, {detail}")
            return 0 if detail is None else 1
        text = render(build(root))
    except DivergenceError as e:
        print(f"divergences: {e}")
        return 1
    target = os.path.join(root, *PAGE.split("/"))
    with open(target, "w", encoding="utf-8", newline="\n") as f:
        f.write(text)
    print(f"wrote {target}")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
# REUSE-IgnoreEnd
