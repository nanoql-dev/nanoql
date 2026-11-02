# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The wheel parity check: what QuantLib-Python constructs, against what the bindings make (docs/bindings.md).

Usage: python -I tools/bindings/wheelparity.py <QuantLib.py> <surface.json> <wheel-routes.json> [--report <out.md>]
       [--fail-on-gap]

It reads, never runs: the wheel's QuantLib.py (the QuantLib-Python release tools/vectors/requirements.txt pins), a
surface.json, and the routes table (bindings/wheel-routes.json). Each class the wheel constructs (its __init__ calls
_QuantLib.new_<Class>) and each module function that constructs one (the deprecation shims, the engine dispatchers by
traits or tree, the builder functions) falls in one group:

- crosses: a type of the name has a maker in the surface, a constructor, a factory returning it, a preset lookup, an
  engine's calculation or Bind, or static functions;
- route: the table names what a user reaches instead, with its reason, and the target is checked: a type with a maker,
  an enum the surface carries, a C function the surface has, or a language form in prose (an array, a matrix);
- exclusion: a name the table's exclusions match, each a ruled one with its reason (handles, observers and settings,
  SWIG's containers, the stepping parts, the caller's own functions);
- gap: anything else, a route whose target the surface doesn't make among them, and a route or exclusion for a name
  the surface makes itself (a stale one, which an API change left behind).

A function crosses when it has a route, or when every class it constructs crosses or has one. The table's notes, a
residual difference a crossing class keeps, go in the report. With --report it writes the parity table as Markdown;
with --fail-on-gap it exits 1 on any gap.
"""
import argparse
import json
import re
import sys
from collections import Counter, defaultdict
from pathlib import Path

IDENTIFIER = re.compile(r"[A-Za-z_][A-Za-z0-9_]*")


def short(name):
    return name.rsplit(".", 1)[-1]


def wheel_constructs(source):
    """The classes whose __init__ constructs them, and the public module functions that construct one, each with the
    classes it returns."""
    classes, current = set(), None
    for line in source.splitlines():
        m = re.match(r"^class (\w+)\(", line)
        if m:
            current = m.group(1)
            continue
        if line and not line[0].isspace():
            current = None
        if current and re.search(r"_QuantLib\.new_" + re.escape(current) + r"\(", line):
            classes.add(current)
    functions = {}
    for m in re.finditer(r"^def (\w+)\(.*?(?=^\S)", source, re.M | re.S):
        name, body = m.group(1), m.group(0)
        targets = set(re.findall(r"(?:return|cls =) (\w+)\(", body)) | set(re.findall(r"cls = (\w+)$", body, re.M))
        targets = sorted(t for t in targets if t in classes or t.startswith("_"))
        if targets and not name.startswith("_") and name not in classes:
            functions[name] = targets
    return classes, functions


class Surface:
    """What a surface.json makes: each type's makers and forms, its functions' names, its enums."""

    def __init__(self, surface):
        self.forms = defaultdict(set)
        for t in surface["types"]:
            self.forms[short(t["name"])].add(t["form"])
        self.makers = defaultdict(set)
        self.owners = set()
        for f in surface["functions"]:
            owner = short(f["type"])
            returns = short((f.get("returns") or {}).get("type") or "")
            if f["kind"] in ("static", "get", "method"):
                self.owners.add(owner)
            if f["kind"] == "new":
                self.makers[owner].add("new")
            elif f["kind"] in ("static", "get") and f.get("static") and returns == owner:
                self.makers[owner].add("factory")
            elif f["kind"] in ("static", "method") and f["member"] == "Calculate":
                self.makers[owner].add("calculate")
            elif f["kind"] == "bind":
                self.makers[owner].add("bind")
        for p in surface["presets"]:
            if p.get("lookup"):
                self.makers[short(p["member"].split("::")[0])].add("lookup")
        self.functions = {f["name"] for f in surface["functions"]}
        self.enums = {e.get("cname") or short(e["name"]) for e in surface["enums"]}

    def statics_only(self, name):
        return "static" in self.forms.get(name, set()) and name in self.owners

    def made(self, name):
        return bool(self.makers.get(name)) or self.statics_only(name)


def classify(name, surface, table):
    """(group, detail) for a wheel name."""
    route = table["routes"].get(name)
    stale = surface.made(name)
    if route is not None:
        if stale:
            return "gap", "a stale route: the surface makes the name itself"
        kind, target = route.get("kind", "type"), route["to"]
        if kind == "enum":
            ok = target in surface.enums
        elif kind == "function":
            ok = target in surface.functions
        elif kind == "language":
            ok = not IDENTIFIER.fullmatch(target)
        else:
            ok = surface.made(target)
        detail = f"{route['why']} -> {target}"
        return ("route", detail) if ok else ("gap", detail + " (the surface doesn't make the target)")
    for pattern, why in table["exclusions"].items():
        if re.fullmatch(pattern, name):
            return ("gap", "a stale exclusion: the surface makes the name itself") if stale else ("exclusion", why)
    if name in surface.forms:
        makers = surface.makers.get(name, set())
        if makers:
            return "crosses", ", ".join(sorted(makers))
        if surface.statics_only(name):
            return "crosses", "static functions"
        if "withheld" in surface.forms[name]:
            return "exclusion", "a stepping part (BIR3's (A))"
        return "gap", "the type crosses with no maker (forms " + ", ".join(sorted(surface.forms[name])) + ")"
    return "gap", "no nanoql type of this name and no route"


def check(source, surface, table):
    classes, functions = wheel_constructs(source)
    rows = [("class", name) + classify(name, surface, table) for name in sorted(classes)]
    for name, targets in sorted(functions.items()):
        if name in table["routes"]:
            rows.append(("function", name) + classify(name, surface, table))
            continue
        groups = [classify(t, surface, table)[0] for t in targets]
        rows.append(("function", name, "gap" if "gap" in groups else "route", "constructs " + ", ".join(targets)))
    return rows


def write_report(path, rows, notes):
    with path.open("w", encoding="utf-8", newline="\n") as out:
        out.write("| What | QuantLib-Python's name | Group | Detail |\n|---|---|---|---|\n")
        for what, name, group, detail in rows:
            note = notes.get(name)
            out.write(f"| {what} | `{name}` | {group} | {detail}{'; note: ' + note if note else ''} |\n")


def main(argv):
    p = argparse.ArgumentParser(description="QuantLib-Python's constructions against a surface.json.")
    p.add_argument("quantlib_py", type=Path)
    p.add_argument("surface", type=Path)
    p.add_argument("routes", type=Path)
    p.add_argument("--report", type=Path)
    p.add_argument("--fail-on-gap", action="store_true")
    a = p.parse_args(argv)
    sys.stdout.reconfigure(encoding="utf-8")
    table = json.loads(a.routes.read_text(encoding="utf-8"))
    rows = check(a.quantlib_py.read_text(encoding="utf-8"), Surface(json.loads(a.surface.read_text(encoding="utf-8"))),
                 table)
    groups = Counter(r[2] for r in rows)
    print(f"QuantLib-Python's constructions: {len(rows)} ({sum(r[0] == 'class' for r in rows)} classes, "
          f"{sum(r[0] == 'function' for r in rows)} functions); " + ", ".join(f"{g} {groups[g]}" for g in
                                                                              ("crosses", "route", "exclusion", "gap")))
    for what, name, group, detail in rows:
        if group == "gap":
            print(f"  gap: {what} {name}: {detail}")
    for name, note in sorted(table.get("notes", {}).items()):
        print(f"  note: {name}: {note}")
    if a.report:
        write_report(a.report, rows, table.get("notes", {}))
    return 1 if a.fail_on_gap and groups["gap"] else 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
