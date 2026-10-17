# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The harness's self-check (docs/porting-playbook.md §8): each twin, a harness vector and the wheel vector at the
same path without cxx/, agrees output by output.

Usage: python -I tools/vectors/agree.py [<cxx path> ...]

A path is a harness vector's path under tests/vectors, without ".json" (for example cxx/interestrate); with none,
every twin on TWINS. A twin is listed, never inferred from its path: other harness vectors share paths with unrelated
wheel files. Two files agree when they have the same case names and inputs, in order, and the same outputs, each
under R7 (|a - b| <= 1e-10 x max(|a|, |b|, scale), with the output's own scale), with the same kind (boolean,
integer, real, string, null, throws) and each {value, scale}'s scale exactly. Prints, per twin, the outputs agreeing
M/M and how many are bit-identical; exits 1 on any disagreement, or on a listed twin whose file is missing. It needs
no QuantLib.
"""
import json
import math
import os
import struct
import sys

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
VECTORS = os.path.join(REPO, "tests", "vectors")

# Each harness vector whose cases reproduce a wheel generator's.
TWINS = ["cxx/harness/encoding", "cxx/interestrate"]

TOLERANCE = 1e-10
MAX_REPORTED = 20


def load(rel):
    with open(os.path.join(VECTORS, *rel.split("/")) + ".json", "rb") as f:
        return json.loads(f.read().decode("utf-8"))


def kind(x):
    if x is None:
        return "null"
    if isinstance(x, bool):
        return "boolean"
    if isinstance(x, int):
        return "integer"
    if isinstance(x, float):
        return "real"
    if isinstance(x, str):
        return "string"
    if isinstance(x, list):
        return "array"
    if isinstance(x, dict):
        if "throws" in x:
            return "throws"
        if "value" in x:
            return "amount"
    raise ValueError(f"an expected value is a scalar, an array, {{value, scale}} or {{throws}}: {x!r}")


class Tally:
    def __init__(self):
        self.outputs = self.agreeing = self.identical = 0
        self.mismatches = []

    def miss(self, label, why):
        self.mismatches.append(f"{label}: {why}")

    def compare(self, label, h, w):
        kh, kw = kind(h), kind(w)
        if kw == "array":
            if kh != "array" or len(h) != len(w):
                self.outputs += max(len(w), 1)
                self.miss(label, f"harness {h!r}, wheel {w!r}")
                return
            for i, (x, y) in enumerate(zip(h, w)):
                self.compare(f"{label}[{i}]", x, y)
            return
        self.outputs += 1
        if kh != kw:
            self.miss(label, f"harness {kh} {h!r}, wheel {kw} {w!r}")
            return
        scale = 1.0
        if kw == "amount":
            if set(h) != set(w) or h.get("scale") != w.get("scale") or set(w) - {"value", "scale"}:
                self.miss(label, f"harness {h!r}, wheel {w!r}")
                return
            scale, h, w = w["scale"], h["value"], w["value"]
            if kind(h) != kind(w):
                self.miss(label, f"harness {h!r}, wheel {w!r}")
                return
        if isinstance(w, float) and isinstance(h, float):
            if struct.pack("<d", h) == struct.pack("<d", w):
                self.agreeing += 1
                self.identical += 1
            elif math.isfinite(h) and math.isfinite(w) and \
                    abs(h - w) <= TOLERANCE * max(abs(h), abs(w), scale):
                self.agreeing += 1
            else:
                self.miss(label, f"harness {h!r}, wheel {w!r}")
            return
        if h == w and type(h) is type(w):
            self.agreeing += 1
            self.identical += 1
        else:
            self.miss(label, f"harness {h!r}, wheel {w!r}")


def agree(rel):
    """Prints one twin's line and its mismatches; True when it agrees."""
    wheel_rel = rel.removeprefix("cxx/")
    missing = [p for p in (rel, wheel_rel) if not os.path.isfile(os.path.join(VECTORS, *p.split("/")) + ".json")]
    if missing:
        print(f"MISSING {rel}: no tests/vectors/{missing[0]}.json")
        return False
    h, w = load(rel), load(wheel_rel)
    hc, wc = h["cases"], w["cases"]
    if len(hc) != len(wc):
        print(f"DIFFERS {rel}: {len(hc)} cases, {wheel_rel} has {len(wc)}")
        return False
    t = Tally()
    for i, (a, b) in enumerate(zip(hc, wc)):
        if a["name"] != b["name"] or a["inputs"] != b["inputs"]:
            print(f"DIFFERS {rel}: case {i} is '{a['name']}', {wheel_rel}'s '{b['name']}', or their inputs differ")
            return False
        if list(a["expected"]) != list(b["expected"]):
            print(f"DIFFERS {rel}: case '{a['name']}' has outputs {list(a['expected'])}, {wheel_rel}'s "
                  f"{list(b['expected'])}")
            return False
        for k in b["expected"]:
            t.compare(f"{a['name']} > {k}", a["expected"][k], b["expected"][k])
    for m in t.mismatches[:MAX_REPORTED]:
        print(f"MISMATCH {rel}: {m}")
    if len(t.mismatches) > MAX_REPORTED:
        print(f"... and {len(t.mismatches) - MAX_REPORTED} more")
    print(f"{rel} against {wheel_rel}: {len(wc)} cases; outputs agreeing within R7 {t.agreeing}/{t.outputs}, "
          f"bit-identical {t.identical}")
    return t.agreeing == t.outputs and not t.mismatches


def main(args):
    rels = [a.replace("\\", "/").removesuffix(".json") for a in args] or TWINS
    unlisted = [r for r in rels if r not in TWINS]
    if unlisted:
        print(f"NOT A TWIN {', '.join(unlisted)}: the twins are {', '.join(TWINS)}")
        return 1
    ok = [agree(r) for r in rels]
    print(f"twins {sum(ok)}/{len(ok)} agree")
    return 0 if all(ok) else 1


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
