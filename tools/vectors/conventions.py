# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
# The code below quotes licence tags as data; REUSE reads only the two lines above.
# REUSE-IgnoreStart
"""Shared helpers for the conventions generators under tools/vectors/gen/conventions/: the presets files' canonical
form, which ConventionsXml.Write writes (docs/conventions.md).

A conventions generator names its output, a path from the repository root, in a constant OUTPUT, and calls write(),
which writes it under the output root: the repository by default, a temporary folder when tools/vectors/reproduce.py
passes one. The form: a licence comment, then the Conventions element with its records in order, attributes name
and base first, two-space indents, LF line ends, UTF-8 without a byte-order mark and a final newline.
"""
import os
import sys

NAMESPACE = "https://nanoql.org/schemas/conventions"
REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
NANOQL_HEADER = ["SPDX-FileCopyrightText: 2026 The nanoql Authors", "SPDX-License-Identifier: Apache-2.0"]


def _text(s):
    return s.replace("&", "&amp;").replace("<", "&lt;").replace(">", "&gt;")


def _attribute(s):
    return _text(s).replace('"', "&quot;")


def record(element, name, base=None, fields=()):
    """A record: its element, name, optional base, and fields as (field, value) pairs in the schema's order, a
    value a string or a list of nested (field, value) pairs."""
    attrs = [("name", name)] + ([("base", base)] if base is not None else [])
    return (element, attrs, list(fields))


def _lines(node, depth):
    element, attrs, children = node
    pad = "  " * depth
    head = pad + "<" + element + "".join(f' {k}="{_attribute(v)}"' for k, v in attrs)
    if isinstance(children, str):
        return [head + ">" + _text(children) + "</" + element + ">"]
    if not children:
        return [head + " />"]
    out = [head + ">"]
    for field, value in children:
        out += _lines((field, [], value), depth + 1)
    return out + [pad + "</" + element + ">"]


def document(records, header=NANOQL_HEADER):
    """The file's text: the licence comment and the Conventions element."""
    lines = ["<!--"] + [("  " + h) if h else "" for h in header] + ["-->"]
    root = f'<Conventions xmlns="{NAMESPACE}"'
    if not records:
        lines.append(root + " />")
    else:
        lines.append(root + ">")
        for r in records:
            lines += _lines(r, 1)
        lines.append("</Conventions>")
    return "\n".join(lines) + "\n"


def write(output, records, header=NANOQL_HEADER):
    """Writes the records to OUTPUT under the output root, the command line's first argument or the repository."""
    root = sys.argv[1] if len(sys.argv) > 1 else REPO
    path = os.path.join(root, *output.split("/"))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with open(path, "wb") as f:
        f.write(document(records, header).encode("utf-8"))
# REUSE-IgnoreEnd
