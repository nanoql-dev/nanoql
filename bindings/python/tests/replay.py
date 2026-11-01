# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python replayer of the vector suite's trace (docs/bindings.md, "The vector suite through each binding"): each
line's C function called through the Python member the generated map names, an overload's through its group's
dispatch, its arguments converted from the trace's C forms, every result compared with C#'s, doubles by their bits with
NaN as NaN, refusals by their code. Where the dispatch, reading the arguments' own classes, takes another form than C#'s
static choice, the replay counts it and calls the recorded form. A fact replays up to its first call that needs what
the trace couldn't write or the replay didn't make.

Usage: python replay.py <trace.jsonl> [<facts report.tsv>]   (the package on the path, its library the production one)
"""

import datetime
import json
import re
import struct
import sys

import numpy as np

import nanoql
import nanoql.experimental  # noqa: F401 - the experimental members, which the trace calls too
from nanoql import _generated, _runtime
from nanoql._runtime import NanoQLError, Period, _functions as _F, _select, _split


class Stop(Exception):
    """An input the trace couldn't write, or an object the replay didn't make."""


STOPS = int(__import__("os").environ.get("NANOQL_REPLAY_STOPS", "0"))  # how many facts' stops to print


def type_key(name):
    """A type's name without each file-local class's compiler prefix (<File>F<hash>__, the hash of its source file's
    path), so a trace replays against a library built from another checkout."""
    return re.sub(r"<[^>]*>F[0-9A-Fa-f]+__", "", name or "")


def hexdouble(s):
    return struct.unpack(">d", bytes.fromhex(s))[0] if isinstance(s, str) else 0.0


def same_double(a, b):
    return (a != a and b != b) or struct.pack("<d", a) == struct.pack("<d", b)


class Replay:
    def __init__(self):
        self.objs = {}
        self.mismatches = 0
        self.refusals = 0
        self.shown = 0
        self.line = 0
        self.fn = ""
        self.dispatched = 0
        self.other_form = 0

    def mismatch(self, what, want, got):
        self.mismatches += 1
        if self.shown < 20:
            self.shown += 1
            print(f"MISMATCH line {self.line} {self.fn}: {what}: recorded {want}, replayed {got}")

    # ---- arguments ----

    def obj(self, v):
        if v is None:
            return None
        if isinstance(v, dict):
            raise Stop("an input the trace couldn't write")
        o = self.objs.get(v)
        if o is None:
            raise Stop(f"object {v}, which the replay didn't make")
        return o

    def element(self, e, cname, v):
        if e in ("double", "double?"):
            return None if v is None and e.endswith("?") else hexdouble(v)
        if e in ("int32", "int32?", "int64"):
            return v
        if e == "enum":
            return _runtime._enum(cname, v) if v is not None else 0
        if e == "bool":
            return bool(v)
        if e in ("date", "date?"):
            return _runtime._date(v or 0)
        if e == "period":
            return Period(v[0], v[1])
        if e == "complex":
            return complex(hexdouble(v[0]), hexdouble(v[1]))
        if e == "string":
            return v
        return self.obj(v)

    def arg(self, kind, v):
        name, cname = _split(kind)
        head, _, rest = name.partition(":")
        if isinstance(v, dict) and "x" in v:
            raise Stop("an input the trace couldn't write")
        if name in ("double", "double?"):
            return None if v is None and name.endswith("?") else hexdouble(v)
        if name in ("int32", "int64", "int32?", "int64?"):
            return v
        if name in ("enum", "enum?"):
            return None if v is None else _runtime._enum(cname, v)
        if name in ("bool", "bool?"):
            return None if v is None and name.endswith("?") else bool(v)
        if name == "string":
            return v
        if name in ("date", "date?"):
            return _runtime._date(v or 0)
        if name in ("period", "period?"):
            return None if v is None else Period(v[0], v[1])
        if name in ("complex", "complex?"):
            return None if v is None else complex(hexdouble(v[0]), hexdouble(v[1]))
        if name in ("handle", "boxed", "boxed?", "snapshot", "engine"):
            return self.obj(v)
        if name == "matrix":
            if not isinstance(v, dict):
                return None
            return np.array([hexdouble(x) for x in v["v"]], dtype=np.float64).reshape(v["r"], v["c"])
        if name in ("vector", "vector&"):
            return None if v is None else np.array([hexdouble(x) for x in v], dtype=np.float64)
        if name == "matrix&":
            if not isinstance(v, dict):
                return None
            return np.array([hexdouble(x) for x in v["v"]], dtype=np.float64).reshape(v["r"], v["c"])
        if head == "span":
            e = _split(rest)[0]
            return np.array([self.element(e, None, x) for x in (v or [])], dtype=np.float64 if e == "double" else np.int32)
        if head == "array":
            if v is None:
                return None
            e, ec = _split(rest)
            return [self.element(e, ec, x) for x in v]
        if head == "jagged":
            return None if v is None else [[hexdouble(x) for x in row] for row in v]
        if head == "map":
            if v is None:
                return None
            ke, ve = rest.split(":", 1)
            (ke, kc), (ve, vc) = _split(ke), _split(ve)
            return [(self.element(ke, kc, p[0]), self.element(ve, vc, p[1])) for p in v]
        raise Stop

    # ---- results ----

    def check(self, kind, res, got):
        name, cname = _split(kind)
        head, _, rest = name.partition(":")
        if name in ("double",):
            want = hexdouble(res)
            if not same_double(want, got):
                self.mismatch("double", repr(want), repr(got))
        elif name in ("int32", "int64", "enum"):
            if res != int(got):
                self.mismatch(name, res, got)
        elif name == "bool":
            if bool(res) != bool(got):
                self.mismatch("bool", res, got)
        elif name == "string":
            if res != got:
                self.mismatch("string", repr(res), repr(got))
        elif name in ("date", "date?"):
            if (res or 0) != _runtime._serial(got):
                self.mismatch("date", res, got)
        elif name == "period":
            if [res[0], res[1]] != [got[0], int(got[1])]:
                self.mismatch("period", res, got)
        elif name == "complex":
            if not (same_double(hexdouble(res[0]), got.real) and same_double(hexdouble(res[1]), got.imag)):
                self.mismatch("complex", res, got)
        elif name.endswith("?") and ":" not in name:
            if (res is None) != (got is None):
                self.mismatch("has-value", "none" if res is None else "a value", "none" if got is None else "a value")
            elif res is not None:
                self.check(name[:-1] + ("@" + cname if cname else ""), res, got)
        elif name in ("handle", "boxed", "snapshot", "new"):
            self.check_obj(res, got)
        elif name == "matrix":
            if res is None or got is None:
                if (res is None) != (got is None):
                    self.mismatch("matrix", res, "None" if got is None else "a matrix")
                return
            if list(got.shape) != [res["r"], res["c"]]:
                self.mismatch("matrix", "its shape", got.shape)
            else:
                self.check_doubles(res["v"], got.ravel())
        elif name == "vector":
            self.check_doubles(res, got)
        elif head == "array":
            e, ec = _split(rest)
            if res is None or got is None:
                if res is not None and len(res) != len(got if got is not None else []):
                    self.mismatch("array", "an array", "null")
                return
            if len(res) != len(got):
                self.mismatch("array", f"{len(res)} elements", f"{len(got)}")
                return
            for r, g in zip(res, got):
                self.check(e + ("@" + ec if ec else ""), r, g)
        elif head == "jagged":
            if len(res) != len(got):
                self.mismatch("jagged array", "its rows", "other rows")
                return
            for r, g in zip(res, got):
                self.check_doubles(r, g)
        elif head == "map":
            ke, ve = rest.split(":", 1)
            items = list(got.items())
            if len(res) != len(items):
                self.mismatch("map", "its pairs", "other pairs")
                return
            for (rk, rv), (gk, gv) in zip(res, items):
                self.check(ke, rk, gk)
                self.check(ve, rv, gv)
        elif head == "tuple":
            kinds = _runtime._top_split(rest.partition("|")[2])
            for k, r, g in zip(kinds, res, got):
                self.check(k, r, g)
        elif name == "distribution":
            for r, g in zip(res, got):
                self.check_doubles(r, g)
        elif name in ("draws", "sequences"):
            values, weights, nxt = got
            self.check_doubles(res[0], np.asarray(values).ravel())
            self.check_doubles(res[1], weights)
            if name == "sequences" and res[2] != values.shape[1]:
                self.mismatch("dimension", res[2], values.shape[1])
            self.check_obj(res[-1], nxt)
        else:
            raise Stop

    def check_doubles(self, res, got):
        if res is None:
            return
        if len(res) != len(got):
            self.mismatch("doubles", f"{len(res)} values", f"{len(got)}")
            return
        for r, g in zip(res, got):
            want = hexdouble(r)
            if not same_double(want, float(g)):
                self.mismatch("double", repr(want), repr(float(g)))
                return

    def check_obj(self, res, got):
        if res is None:
            if got is not None:
                self.mismatch("object", "null", "an object")
            return
        if got is None:
            self.mismatch("object", "an object", "null")
            return
        if "n" in res or res.get("s") not in self.objs:
            # A new object's class: the .NET type's full name C#'s had.
            if "y" in res and type_key(got.type_name) != type_key(res["y"]):
                self.mismatch("object type", res["y"], got.type_name)
            self.objs[res.get("n", res.get("s"))] = got
            return
        if not (self.objs[res["s"]] == got):
            self.mismatch("object", "the same object", "another")

    # ---- a call ----

    def call(self, rec):
        cname = rec["f"]
        self.fn = cname
        entry = _generated._PY.get(cname)
        table = _runtime._Functions.table.get(cname)
        if table is None:
            raise Stop("a function the layer doesn't have")
        # A value type's constructor (Date, Period) has no member: datetime.date and nanoql.Period stand in for it.
        cls_name, member, style, form, group = entry if entry is not None else ("", "", "function", None, None)
        ins, out = table
        args = [self.arg(k, v) for k, v in zip(ins, rec.get("a") or [])]
        status = rec.get("t", 0)
        try:
            result = self.invoke(cls_name, member, style, group, cname, args)
            got_status = 0
        except NanoQLError as e:
            got_status, result = e.code, None
        except Stop:
            raise
        except Exception as e:  # noqa: BLE001 - a Python error is a mismatch to report, not a stop
            self.mismatch("call", f"status {status}", f"{type(e).__name__}: {e}")
            return True
        if got_status != status:
            self.mismatch("status", f"status {status}", f"status {got_status}")
            return True
        if status:
            self.refusals += 1
            return True
        if out is not None:
            try:
                self.check(out, rec.get("r"), result)
            except Stop:
                raise
            except Exception as e:  # noqa: BLE001 - a result the check can't read is a mismatch to report
                self.mismatch("result", repr(rec.get("r"))[:80], f"{type(e).__name__}: {e}")
        after = rec.get("w")
        if after:
            # A span's values after the call, and an in-out Matrix's or Vector's (R15), in order.
            spans = [a for k, a in zip(ins, args) if k.startswith("span:") or k in ("vector&", "matrix&")]
            for r, g in zip(after, spans):
                if g is None:
                    continue
                self.check_doubles(r, g.ravel()) if g.dtype == np.float64 else [self.check("int32", x, y) for x, y in zip(r, g)]
        return True

    def invoke(self, cls_name, member, style, group, cname, args):
        if not cls_name:
            # A value type's constructor (Date, Period) has no member: datetime.date and nanoql.Period stand in for it.
            return _F[cname](*args)
        cls = _runtime._by_cname.get("nql." + cls_name) or getattr(nanoql, cls_name, None) or getattr(nanoql.experimental, cls_name, None) or getattr(_generated, cls_name)
        if group is not None:
            # An overload: the form its group's dispatch takes, by the arguments' own classes; another than C#'s static
            # choice is counted, and the recorded form called.
            self.dispatched += 1
            _, _, chosen = _select(getattr(cls, group), tuple(args), {})
            if chosen != cname:
                self.other_form += 1
                return _F[cname](*args) if style != "new" else self.construct(cls_name, _F[cname](*args))
        if style == "new":
            return cls(*args)
        # A member through the class that declares the C function, as the trace records it: an object's own class may
        # hide it with a member of the same name (C#'s new), and a NULL self (a set's lookups: the current provider)
        # has no class of its own.
        if style == "property":
            if cls_name == "_PeriodMembers":
                return getattr(Period(*args[0]), member)
            return getattr(cls, member).fget(args[0])
        if style == "method":
            if cls_name == "_PeriodMembers":
                return getattr(Period(*args[0]), member)(*args[1:])
            return getattr(cls, member)(*args)
        if style == "index":
            return args[0][args[1] if len(args) == 2 else tuple(args[1:])]
        return getattr(cls, member)(*args)

    def construct(self, cls_name, h):
        cls = _runtime._by_cname.get("nql." + cls_name) or _runtime.Object
        o = object.__new__(cls)
        o._h = h
        return o


STOPS_LEFT = [STOPS]


def main():
    trace = sys.argv[1]
    report = sys.argv[2] if len(sys.argv) > 2 else None
    r = Replay()
    facts = {}
    order = []
    current = None
    whole = True
    stopped = False
    calls = replayed = 0
    with open(trace, encoding="utf-8") as f:
        for line in f:
            r.line += 1
            if line.startswith('{"fact"'):
                if current is not None and whole:
                    facts[current][1] += 1
                current = json.loads(line)["fact"]
                if current not in facts:
                    facts[current] = [0, 0, 0, 0]
                    order.append(current)
                facts[current][0] += 1
                whole, stopped = True, False
                continue
            calls += 1
            if current is not None:
                facts[current][2] += 1
            if stopped:
                continue
            try:
                r.call(json.loads(line))
                replayed += 1
                if current is not None:
                    facts[current][3] += 1
            except Stop as e:
                if STOPS > 0 and whole:
                    STOPS_LEFT[0] -= 1
                    if STOPS_LEFT[0] >= 0:
                        print(f"STOP line {r.line} {current}: {r.fn}: {e}")
                stopped, whole = True, False
    if current is not None and whole:
        facts[current][1] += 1
    if report:
        with open(report, "w", encoding="utf-8", newline="\n") as out:
            out.write("fact\truns\twhole\tcalls\treplayed\n")
            for name in order:
                runs, w, c, rp = facts[name]
                out.write(f"{name}\t{runs}\t{w}\t{c}\t{rp}\n")
    r.objs.clear()
    import gc

    gc.collect()
    whole_facts = sum(1 for v in facts.values() if v[1] == v[0])
    print(f"Python replay: facts replayed whole {whole_facts} of {len(facts)}; calls replayed {replayed} of {calls}; refusals matched "
          f"{r.refusals}; mismatches {r.mismatches}; live handles at the end {nanoql.live_handles()}; overload calls through the dispatch "
          f"{r.dispatched}, of them another form than C#'s {r.other_form}")
    return 0 if r.mismatches == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
