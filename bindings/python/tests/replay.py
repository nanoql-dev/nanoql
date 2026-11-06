# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""The Python replayer of the vector suite's trace (docs/bindings.md, "The vector suite through each binding"): each
line's C function called through the Python member the generated map names, an overload's through its group's
dispatch, a record's generated equality pair, which no class declares, through == and != (nql_equals); its arguments
converted from the trace's C forms, every result compared with C#'s, doubles by their bits with NaN as NaN, refusals by
their code. Where the dispatch, reading the arguments' own classes, takes another form than C#'s
static choice, the replay counts it and calls the recorded form. A fact replays up to its first call that needs what
the trace couldn't write or the replay didn't make.

With --sweep, after the replay and before its objects are released, each function of the layer's table is called
(docs/bindings.md, "Checking the bindings"), those the trace never calls first, then those it calls: through its Python
member, or a record's equality pair the layer leaves to ==, through its C function checked against == and !=. Its
standing arguments are the replay's objects by class and values by kind and parameter name, up to three choices; then
the widened ones, one change each on the choice that returned: an array-like argument empty, of one element or of
twelve, or an index-like integer past a length or negative. An uncalled function's standing returns join the pool, in
passes until one reaches nothing new; no other call's do. A call past the limit ends the process, naming it and its
probe; the calls run in a fresh temporary folder. The sweep reds on a Python-layer error, code 99 or code 9 in any
attempt, and on an exclusion that names no function of the layer; each function's outcome is a line of the outcomes
file.

Usage: python replay.py <trace.jsonl> [<facts report.tsv>] [--sweep <outcomes.tsv> [--prefix <prefix>]
       [--surface <surface.json>] [--limit <seconds>]]   (the package on the path, its library the production one)
--prefix names the functions swept, nql by default (the prefix's package and its experimental module imported first);
--surface gives their parameters' names, the checkout's bindings/surface.json by default.
"""

import datetime
import faulthandler
import importlib
import json
import math
import os
import re
import shutil
import struct
import sys
import tempfile
import time
from collections import defaultdict
from pathlib import Path

import numpy as np

import nanoql
import nanoql.experimental  # noqa: F401 - the experimental members, which the trace calls too
from nanoql import _generated, _runtime
from nanoql._runtime import NanoQLError, Period, _functions as _F, _select, _split


class Stop(Exception):
    """An input the trace couldn't write, or an object the replay didn't make."""


# Each C function's Python member, nanoql's and those of the user's packages the run uses (use_prefix).
PY = dict(_generated._PY)


def use_prefix(prefix):
    """A user's package, by its prefix: imported with its experimental module, which registers its functions in the
    runtime's table, and its members added to the map, so the replay and the sweep call its functions through them."""
    if prefix != "nql":
        importlib.import_module(prefix)
        importlib.import_module(prefix + ".experimental")
        PY.update(importlib.import_module(prefix + "._generated")._PY)


def class_of(cname, cls_name):
    """The class of a C function's member, by the function's prefix: a user's class may take one of nanoql's names."""
    return _runtime._by_cname.get(cname.split("_", 1)[0] + "." + cls_name)


STOPS = int(__import__("os").environ.get("NANOQL_REPLAY_STOPS", "0"))  # how many facts' stops to print
# Off the reference platform, a double within this relative difference, or else within this absolute one, is a match,
# each kind tallied with its widest.
TOLERANCE = float(__import__("os").environ.get("NANOQL_REPLAY_TOLERANCE", "0"))
ABS_TOLERANCE = float(__import__("os").environ.get("NANOQL_REPLAY_ABS_TOLERANCE", "0"))
NEAR = [0, 0.0]  # doubles within the relative tolerance, not bit for bit, and the widest relative difference
SMALL = [0, 0.0]  # doubles within the absolute tolerance alone, and the widest absolute difference


def type_key(name):
    """A type's name without each file-local class's compiler prefix (<File>F<hash>__, the hash of its source file's
    path), so a trace replays against a library built from another checkout."""
    return re.sub(r"<[^>]*>F[0-9A-Fa-f]+__", "", name or "")


def hexdouble(s):
    return struct.unpack(">d", bytes.fromhex(s))[0] if isinstance(s, str) else 0.0


def same_double(a, b):
    if (a != a and b != b) or struct.pack("<d", a) == struct.pack("<d", b):
        return True
    d, m = abs(a - b), max(abs(a), abs(b))
    if not (math.isfinite(a) and math.isfinite(b)):
        return False
    if TOLERANCE > 0 and d <= TOLERANCE * m:
        NEAR[0] += 1
        NEAR[1] = max(NEAR[1], d / m) if m else NEAR[1]
        return True
    if ABS_TOLERANCE > 0 and d <= ABS_TOLERANCE:
        SMALL[0] += 1
        SMALL[1] = max(SMALL[1], d)
        return True
    return False


class Replay:
    def __init__(self):
        self.objs = {}
        self.seen = {}  # each object's first sight: the fact run, by name and occurrence, and the call in it
        self.run = ("", 0)
        self.run_line = 0
        self.occurrences = defaultdict(int)
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

    def fact(self, name):
        """A fact run starts: its name and its occurrence, which order the pool the sweep calls with."""
        self.run = (name, self.occurrences[name])
        self.occurrences[name] += 1
        self.run_line = 0

    def saw(self, i):
        # An object made or used here: its first sight in the facts' name order, wherever the recording put it.
        here = (self.run, self.run_line)
        if i not in self.seen or here < self.seen[i]:
            self.seen[i] = here

    # ---- arguments ----

    def obj(self, v):
        if v is None:
            return None
        if isinstance(v, dict):
            raise Stop("an input the trace couldn't write")
        o = self.objs.get(v)
        if o is None:
            raise Stop(f"object {v}, which the replay didn't make")
        self.saw(v)
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
        if head == "tuple":
            # A tuple from its JSON array, each element by its kind.
            kinds = _runtime._top_split(rest.partition("|")[2])
            return tuple(self.arg(k, x) for k, x in zip(kinds, v))
        if head == "array" and rest.startswith("tuple:"):
            return None if v is None else [self.arg(rest, x) for x in v]
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
        elif head == "tuple?":
            # A returned collection's nullable tuple: null against None.
            if (res is None) != (got is None):
                self.mismatch("has-value", "none" if res is None else "a tuple", "none" if got is None else "a tuple")
            elif res is not None:
                self.check("tuple:" + rest, res, got)
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
            self.saw(res.get("n", res.get("s")))
            return
        self.saw(res["s"])
        if not (self.objs[res["s"]] == got):
            self.mismatch("object", "the same object", "another")

    # ---- a call ----

    def call(self, rec):
        cname = rec["f"]
        self.fn = cname
        self.run_line += 1
        entry = PY.get(cname)
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
            # A span's values after the call, and an in-out Matrix's or Vector's, in order.
            spans = [a for k, a in zip(ins, args) if k.startswith("span:") or k in ("vector&", "matrix&")]
            for r, g in zip(after, spans):
                if g is None:
                    continue
                self.check_doubles(r, g.ravel()) if g.dtype == np.float64 else [self.check("int32", x, y) for x, y in zip(r, g)]
        return True

    def invoke(self, cls_name, member, style, group, cname, args):
        if not cls_name:
            pair = re.search(r"_op_(Equality|Inequality)$", cname)
            if pair:
                # A record's generated equality pair, which no class declares: Python's own == and != (nql_equals).
                return (args[0] == args[1]) if pair.group(1) == "Equality" else (args[0] != args[1])
            # A value type's constructor (Date, Period) has no member: datetime.date and nanoql.Period stand in for it.
            return _F[cname](*args)
        cls = class_of(cname, cls_name) or getattr(nanoql, cls_name, None) or getattr(nanoql.experimental, cls_name, None) or getattr(_generated, cls_name)
        if group is not None:
            # An overload: the form its group's dispatch takes, by the arguments' own classes; another than C#'s static
            # choice is counted, and the recorded form called.
            self.dispatched += 1
            try:
                _, _, chosen = _select(getattr(cls, group), tuple(args), {})
            except TypeError as e:
                # The arguments' classes, which the dispatch read, name the one no form takes.
                raise TypeError(f"{e}: {', '.join(type(a).__name__ for a in args)}") from None
            if chosen != cname:
                self.other_form += 1
                return _F[cname](*args) if style != "new" else self.construct(cname, cls_name, _F[cname](*args))
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

    def construct(self, cname, cls_name, h):
        cls = class_of(cname, cls_name) or _runtime.Object
        o = object.__new__(cls)
        o._h = h
        return o


STOPS_LEFT = [STOPS]

# ---- the sweep ----

SWEEP_LIMIT = 30.0  # seconds a call may take before the sweep ends the process, naming it

# The functions the sweep skips, in both its stages, each with its reason and its time on the arguments it gives. An
# entry silences the check for its function; one naming a function the layer no longer has reds.
EXCLUDED = {
    "nql_ZabrInterpolatedSmileSection_ZabrFullFd_Create":
        "a ZABR smile section pricing each strike by finite differences, 52.3 s",
    "nql_FixedLocalVolSurface_WithInterpolation__NoArbSabr":
        "a local volatility surface refitting a no-arbitrage SABR section per expiry, 387.9 s",
    "nql_NoArbSabrInterpolatedSmileSection_Create__Date_double_doubleArray_bool_double_doubleArray":
        "a no-arbitrage SABR smile section fitting its strikes, 64.7 s at its first choice",
    "nql_NoArbSabr_Interpolate":
        "a no-arbitrage SABR fit, 25.6 s at its first choice and 112.5 s on arrays of twelve",
    "nql_ZabrFullFdInterpolatedSmileSection_Create":
        "a ZABR smile section pricing each strike by finite differences, 70.4 s at its second choice and 68.7 s to "
        "72.7 s widened",
}

# The widened values: after a function's standing attempts, on the choice that returned (each one tried, when none
# did), one change each, its objects not joining the pool: an array-like parameter of no element, of one and of MANY,
# all of them at once where there are two or more; and an index-like integer, by its name, past a length and negative.
MANY = 12  # past the standing table's three values and the eleven points its sizes of ten make; under its samples' 16
PAST = 1000  # an index past the lengths the pool's objects hold
INDEX = frozenset({"i", "j", "k", "t", "index", "branch", "iname", "inamei", "inamej", "jname", "ivariable", "ivar",
                   "ivar1", "ivar2", "ifactor", "leg", "direction"})

# The widened values' exemptions, each with its reason, as an exclusion has one: the functions whose index-like
# parameters take no index probe, and the parameter kinds no array probe lengthens.
EXEMPT_INDEX = {
    re.compile(r"Polynomial_(Alpha|Beta|Moment)$"):
        "an orthogonal polynomial's order, defined for every value and computed up to it, has no length to be past: "
        "MomentBasedGaussianPolynomial's Alpha(1000) takes past 30 s",
}
EXEMPT_KINDS = {"map": "a map's keys can't repeat, so the value table makes no longer one"}

CALLED = re.compile(r'"f":"([^"]+)"')
PAIR = re.compile(r"_op_(Equality|Inequality)$")


def pool_of(r):
    """The replay's objects, at most 40 of a class, each at its first sight in the facts' name order (a fact run, then
    a call in it), so the recording's order of facts doesn't move which objects a function is called with: but for a
    fact whose replay stops before it reaches an object another fact made."""
    pool = defaultdict(list)
    for i in sorted(r.objs, key=lambda i: r.seen[i]):
        o = r.objs[i]
        if isinstance(o, _runtime.Object) and len(pool[type(o)]) < 40 and not getattr(o, "closed", False):
            pool[type(o)].append(o)
    return pool


def array_like(kind):
    k, _ = _split(kind)
    head = k.partition(":")[0]
    return head not in EXEMPT_KINDS and (k in ("vector", "vector&", "matrix", "matrix&", "jagged")
                                         or head in ("span", "array"))


def index_like(kind, name):
    k, _ = _split(kind)
    return k.rstrip("?") in ("int32", "int64") and (name or "").lower() in INDEX


def probes(cname, ins, ps):
    """Each widened attempt's name and the parameters it changes: by a length, or to an index."""
    arrays = [i for i, k in enumerate(ins) if array_like(k)]
    out = []
    for i in arrays:
        nm = ps[i] or i
        out += [(f"zero:{nm}", {i: 0}), (f"one:{nm}", {i: 1}), (f"many:{nm}", {i: MANY})]
    if len(arrays) >= 2:
        out += [(f"{p}:*", dict.fromkeys(arrays, n)) for p, n in (("zero", 0), ("one", 1), ("many", MANY))]
    if not any(p.search(cname) for p in EXEMPT_INDEX):
        out += [(f"{p}:{nm}", {i: ("index", v)}) for i, (k, nm) in enumerate(zip(ins, ps)) if index_like(k, nm)
                for p, v in (("past", PAST), ("neg", -1))]
    return out


class Sweep:
    """The values and objects the sweep calls with, by kind and parameter name: the pool's objects of the parameter's
    class, the first three of each class deriving from it."""

    def __init__(self, pool):
        self.pool = pool
        self.cache = {}

    def objects(self, cname):
        cls = _runtime.BoundEngine if cname is None else _runtime._by_cname.get(cname)
        if cls is None:
            return []
        if cls not in self.cache:
            self.cache[cls] = [o for k, v in self.pool.items() if issubclass(k, cls) for o in v[:3]]
        return self.cache[cls]

    def joined(self, o):
        if isinstance(o, _runtime.Object) and len(self.pool[type(o)]) < 40:
            self.pool[type(o)].append(o)
            self.cache.clear()

    def args(self, ins, ps, choice, change=None):
        """A call's arguments: the standing table's at a choice, with a widened attempt's changes."""
        out = []
        for i, (k, nm) in enumerate(zip(ins, ps)):
            c = (change or {}).get(i)
            out.append(self.value(k, nm, choice) if c is None else c[1] if isinstance(c, tuple)
                       else self.widened(k, nm, choice, c))
        return out

    def widened(self, kind, name, choice, n):
        """The array-like value of ``kind`` with ``n`` elements: doubles and dates rising, integers from 1, objects the
        pool's in turn, the rest the standing table's element repeated."""
        k, cname = _split(kind)
        head, _, rest = k.partition(":")
        dbl = [0.5 * (i + 1) for i in range(n)]
        if k in ("vector", "vector&"):
            return np.array(dbl, dtype=np.float64)
        if k in ("matrix", "matrix&"):
            return np.eye(n, dtype=np.float64)
        if k == "jagged":
            return [list(dbl) for _ in range(n)]
        if head == "span":
            e = _split(rest)[0]
            return np.array(dbl, dtype=np.float64) if e == "double" else np.arange(1, n + 1, dtype=np.int32)
        if rest.startswith("tuple:"):
            return [self.value(rest, name, choice + i) for i in range(n)]
        e, ec = _split(rest)
        e = e.rstrip("?")
        if e in ("handle", "boxed", "snapshot"):
            objs = self.objects(ec)
            if not objs:
                raise LookupError(f"no object of {ec}")
            return [objs[i % len(objs)] for i in range(n)]
        if e == "double":
            return dbl
        if e == "date":
            return [_runtime._date(45672 + 182 * i) for i in range(n)]
        if e in ("int32", "int64"):
            return list(range(1, n + 1))
        ek = e + ("@" + ec if ec else "")
        return [self.value(ek, name, choice) for _ in range(n)]

    def value(self, kind, name, choice):
        k, cname = _split(kind)
        head, _, rest = k.partition(":")
        n = (name or "").lower()
        if k.endswith("?") and ":" not in k:
            return None if choice == 0 else self.value(k[:-1] + ("@" + cname if cname else ""), name, choice - 1)
        if k == "double":
            for key, v in (("strike", 100.0), ("spot", 100.0), ("underlying", 100.0), ("vol", 0.2), ("sigma", 0.2),
                           ("rate", 0.03), ("yield", 0.03), ("time", 1.0), ("maturity", 1.0), ("tolerance", 1e-6),
                           ("accuracy", 1e-6), ("notional", 100.0), ("nominal", 100.0), ("weight", 0.5)):
                if key in n:
                    return v
            return (0.5, 1.0, 0.05)[choice % 3]
        if k in ("int32", "int64"):
            for key, v in (("seed", 42), ("dimension", 1), ("days", 2), ("size", 10), ("steps", 10), ("samples", 16),
                           ("points", 10), ("index", 0), ("year", 2025), ("month", 1), ("day", 15), ("count", 3)):
                if key in n:
                    return v
            return (1, 2, 0)[choice % 3]
        if k == "bool":
            return choice % 2 == 1
        if k == "enum":
            e = _runtime._enums.get(cname or "")
            return choice if e is None else list(e)[min(choice, len(e) - 1)]
        if k == "string":
            for key, v in (("calendar", "TARGET"), ("currency", "USD"), ("code", "USD"), ("index", "Euribor6M"),
                           ("daycounter", "Actual365Fixed"), ("xml", "<a/>"), ("name", "TARGET")):
                if key in n:
                    return v
            return ("x", "TARGET", "USD")[choice % 3]
        if k == "date":
            return _runtime._date(45672 + 30 * choice)
        if k == "period":
            return Period(1 + choice, 3)
        if k == "complex":
            return complex(1.0, 0.0)
        if k in ("handle", "boxed", "snapshot", "engine"):
            objs = self.objects(cname)
            if not objs:
                raise LookupError(f"no object of {cname}")
            return objs[choice % len(objs)]
        if k in ("vector", "vector&"):
            return np.array([0.5, 1.0, 2.0], dtype=np.float64)
        if k in ("matrix", "matrix&"):
            return np.eye(2, dtype=np.float64)
        if k == "jagged":
            return [[0.5, 1.0], [1.5, 2.0]]
        if head == "span":
            e = _split(rest)[0]
            if e == "double":
                return np.array([0.5, 1.0, 2.0], dtype=np.float64)
            return np.array([1, 2, 3], dtype=np.int32)
        if head == "tuple":
            return tuple(self.value(x, name, choice) for x in _runtime._top_split(rest.partition("|")[2]))
        if head == "array":
            if rest.startswith("tuple:"):
                return [self.value(rest, name, choice), self.value(rest, name, choice + 1)]
            e, ec = _split(rest)
            if e in ("handle", "boxed", "snapshot"):
                objs = self.objects(ec)
                if not objs:
                    raise LookupError(f"no object of {ec}")
                return objs[:2] if choice == 0 else objs[choice % len(objs):][:2] or objs[:1]
            if e == "double":
                return [0.5, 1.0, 2.0]
            if e == "date":
                return [_runtime._date(45672 + 182 * i) for i in range(3)]
            if e in ("int32", "int64"):
                return [1, 2, 3]
            ek = e + ("@" + ec if ec else "")
            return [self.value(ek, name, choice), self.value(ek, name, choice)]
        if head == "map":
            ke, ve = rest.split(":", 1)
            return [(self.value(ke, "key", choice), self.value(ve, "value", choice))]
        raise LookupError(f"no value for the kind {kind}")


def one_line(text):
    return str(text)[:160].replace("\t", " ").replace("\r", " ").replace("\n", " ")


def is_red(o):
    return o[0] in ("code 99", "code 9") or o[0].startswith("python")


def sweep_call(r, cname, entry, args, limit, label):
    """One call, its label on standard error first and a call past the limit ending the process: through the
    function's member, or a record's equality pair the layer leaves to the language's ==, through its C function
    checked against it. Its outcome, and what it returned."""
    sys.stderr.write(label + "\n")
    sys.stderr.flush()
    faulthandler.dump_traceback_later(limit, exit=True)
    try:
        if entry is None and PAIR.search(cname) and isinstance(args[0], _runtime.Object):
            got = _F[cname](*args)
            want = (args[0] == args[1]) if cname.endswith("_op_Equality") else (args[0] != args[1])
            if bool(got) != bool(want):
                raise AssertionError(f"the C function says {got}, the language's operator {want}")
        else:
            cls_name, member, style, _form, group = entry if entry else ("", "", "function", None, None)
            got = r.invoke(cls_name, member, style, group, cname, args)
        return ("returned", type(got).__name__), got
    except NanoQLError as e:
        return (f"code {e.code}", one_line(e)), None
    except Stop as e:
        return ("unreached", f"stop: {one_line(e)}"), None
    except Exception as e:  # noqa: BLE001 - a Python-layer error reds
        return (f"python {type(e).__name__}", one_line(e)), None
    finally:
        faulthandler.cancel_dump_traceback_later()


def sweep_one(r, s, cname, ins, ps, planted, limit, joins, widened):
    """A function's standing attempts, up to the first that returns, its returned objects joining the pool where
    ``joins``; then, when one reached the call, its widened attempts, each one's outcome counted in ``widened`` (an
    attempt no value could be made for as "unmade"). The standing outcomes, the widened ones by probe, and the standing
    calls made."""
    entry = PY.get(cname)
    tried, wide, calls = [], [], 0
    for choice in range(3):
        try:
            args = planted.get(cname) or s.args(ins, ps, choice)
        except LookupError as e:
            tried.append(("unreached", str(e)))
            break
        o, got = sweep_call(r, cname, entry, args, limit, cname)
        calls += 1
        if joins:
            s.joined(got)
        tried.append(o)
        if o[0] == "returned" or cname in planted:
            break
    if all(o[0] == "unreached" for o in tried):
        return tried, wide, calls
    bases = [c for c, o in enumerate(tried) if o[0] == "returned"] or \
            [c for c, o in enumerate(tried) if o[0] != "unreached"]
    for probe, change in probes(cname, ins, ps):
        for choice in bases:
            label = probe if len(bases) == 1 else f"{probe}@{choice}"
            try:
                args = s.args(ins, ps, choice, change)
            except LookupError as e:
                o = ("unmade", one_line(e))
            else:
                o, _ = sweep_call(r, cname, entry, args, limit, f"{cname} {label}")
            widened[o[0]] += 1
            wide.append((label, o))
    return tried, wide, calls


def sweep_line(cname, tried, wide, stage):
    """A function's outcome line, and its red if it has one: a red in any attempt, standing or widened, first, its
    probe beside its kind; then a return; then a refusal. The widened attempts' outcomes close the line."""
    bad = [o for o in tried if is_red(o)] + [(f"{o[0]} ({p})", o[1]) for p, o in wide if is_red(o)]
    if bad:
        verdict, o = "red", bad[0]
    elif any(o[0] == "returned" for o in tried):
        verdict, o = "returned", next(o for o in tried if o[0] == "returned")
    else:
        verdict, o = "refused", next(o for o in tried if o[0] != "unreached")
    shown = "; ".join(f"{p} {w[0]}" for p, w in wide)
    return verdict, f"{cname}\t{verdict}\t{o[0]}\t{o[1]}\t{stage}\t{shown}\n", (o if bad else None)


def tally(counts):
    return ", ".join(f"{k} {v}" for k, v in sorted(counts.items(), key=lambda kv: (-kv[1], kv[0]))) or "none"


def made(counts):
    return sum(v for k, v in counts.items() if k != "unmade")


def sweep(r, called, outcomes, prefix="nql", surface=None, limit=SWEEP_LIMIT, excluded=None, planted=None):
    """Calls each function of the prefix's table the trace doesn't call, then each it calls, its exclusions apart, on
    the standing values and then widened; writes each one's outcome to the outcomes file and returns the reds' count.
    ``planted`` gives a function its standing arguments (for its own tests), ``excluded`` replaces the exclusions."""
    excluded = EXCLUDED if excluded is None else excluded
    planted = planted or {}
    use_prefix(prefix)
    table = _runtime._Functions.table
    own = prefix + "_"
    targets = sorted(n for n in table if n.startswith(own) and n not in called and n not in excluded)
    traced = sorted(n for n in table if n.startswith(own) and n in called and n not in excluded)
    stale = sorted(n for n in excluded if n.startswith(own) and n not in table)
    names = {}
    if surface is not None:
        functions = json.loads(Path(surface).read_text(encoding="utf-8"))["functions"]
        names = {fn["name"]: [x["name"] for x in fn["params"]] for fn in functions}

    def params(cname):
        ins, _ = table[cname]
        ps = names.get(cname, [])
        return ins, (["self"] + ps) if ps and len(ins) == len(ps) + 1 else (ps + [None] * len(ins))[:len(ins)]

    s = Sweep(pool_of(r))
    red, unreached, counts, widened = [], {}, defaultdict(int), defaultdict(int)
    attempts = passes = 0
    outcomes = os.path.abspath(outcomes)
    here = os.getcwd()
    sandbox = tempfile.mkdtemp(prefix="nanoql-sweep-")
    os.chdir(sandbox)
    t0 = time.time()
    with open(outcomes, "w", encoding="utf-8", newline="\n") as out:
        pending = targets
        while pending and passes < 50:
            passes += 1
            nxt = []
            for cname in pending:
                ins, ps = params(cname)
                tried, wide, calls = sweep_one(r, s, cname, ins, ps, planted, limit, True, widened)
                attempts += calls
                if all(o[0] == "unreached" for o in tried):
                    unreached[cname] = tried[0][1]
                    nxt.append(cname)
                    continue
                verdict, line, bad = sweep_line(cname, tried, wide, passes)
                if bad:
                    red.append((cname, bad))
                counts[verdict] += 1
                out.write(line)
                out.flush()
            if len(nxt) == len(pending):
                break
            pending = nxt
        for cname in pending:
            out.write(f"{cname}\tunreached\t{unreached.get(cname, '')}\t\t{passes}\t\n")
        t1 = time.time()

        # The functions the trace calls, on the final pool, their objects not joining it.
        trace_counts, trace_widened = defaultdict(int), defaultdict(int)
        trace_calls = 0
        for cname in traced:
            ins, ps = params(cname)
            tried, wide, calls = sweep_one(r, s, cname, ins, ps, planted, limit, False, trace_widened)
            trace_calls += calls
            if all(o[0] == "unreached" for o in tried):
                trace_counts["unreached"] += 1
                out.write(f"{cname}\tunreached\t{tried[0][1]}\t\ttrace\t\n")
                continue
            verdict, line, bad = sweep_line(cname, tried, wide, "trace")
            if bad:
                red.append((cname, bad))
            trace_counts[verdict] += 1
            out.write(line)
            out.flush()
        for cname in sorted(n for n in excluded if n.startswith(own)):
            out.write(f"{cname}\texcluded\t{excluded[cname]}\t\t0\t\n")
    os.chdir(here)
    for cname in stale:
        red.append((cname, ("stale exclusion", "it names no function of the layer")))
    if red:
        print(f"sweep: the calls' folder kept for the diagnosis, {sandbox}")
    else:
        shutil.rmtree(sandbox, ignore_errors=True)
    print(f"sweep: {len(targets)} targets, {sum(n.startswith(own) for n in excluded)} excluded; {attempts} calls in "
          f"{passes} passes, {t1 - t0:.1f} s; returned {counts['returned']}, refused {counts['refused']}, "
          f"unreached {len(pending)}; widened attempts {made(widened)}: {tally(widened)}")
    print(f"sweep: the trace's {len(traced)} functions, {trace_calls + made(trace_widened)} calls in "
          f"{time.time() - t1:.1f} s; {tally(trace_counts)}; widened attempts {made(trace_widened)}: "
          f"{tally(trace_widened)}")
    print(f"sweep: red {len(red)}")
    for cname, (kind, detail) in red:
        print(f"RED {cname}: {kind}: {detail}")
    return len(red)


def main():
    argv = sys.argv[1:]
    # An option with no value after it, or no trace, gets the usage.
    valued = ("--sweep", "--prefix", "--surface", "--limit")
    if any(a in valued and (k + 1 == len(argv) or argv[k + 1].startswith("--")) for k, a in enumerate(argv)):
        print(__doc__)
        return 2

    def option(name, default=None):
        if name not in argv:
            return default
        k = argv.index(name)
        value = argv[k + 1]
        del argv[k:k + 2]
        return value

    outcomes = option("--sweep")
    prefix = option("--prefix", "nql")
    surface = option("--surface")
    limit = float(option("--limit", str(SWEEP_LIMIT)))
    if not argv:
        print(__doc__)
        return 2
    trace = argv[0]
    report = argv[1] if len(argv) > 1 else None
    if outcomes is not None and surface is None:
        surface = str(Path(__file__).resolve().parents[2] / "surface.json")
        if not Path(surface).is_file():
            # Without the parameters' names the values go by kind alone and the verdicts move.
            print(f"sweep: no surface at {surface}; --surface names one")
            return 1
    if outcomes is not None:
        # A user's package and its members, for the replay as for the sweep.
        use_prefix(prefix)
    r = Replay()
    facts = {}
    order = []
    called = set()
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
                r.fact(current)
                whole, stopped = True, False
                continue
            calls += 1
            if outcomes is not None:
                m = CALLED.search(line)
                if m:
                    called.add(m.group(1))
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
    reds = 0 if outcomes is None else sweep(r, called, outcomes, prefix, surface, limit)
    r.objs.clear()
    import gc

    gc.collect()
    whole_facts = sum(1 for v in facts.values() if v[1] == v[0])
    print(f"Python replay: facts replayed whole {whole_facts} of {len(facts)}; calls replayed {replayed} of {calls}; refusals matched "
          f"{r.refusals}; mismatches {r.mismatches}; live handles at the end {nanoql.live_handles()}; overload calls through the dispatch "
          f"{r.dispatched}, of them another form than C#'s {r.other_form}")
    if TOLERANCE > 0 or ABS_TOLERANCE > 0:
        print(f"tolerance {TOLERANCE:g} relative, {ABS_TOLERANCE:g} absolute: {NEAR[0]} doubles within the relative, not bit for bit, "
              f"the widest {NEAR[1]:.3g}; {SMALL[0]} more within the absolute, the widest {SMALL[1]:.3g}")
    return 0 if r.mismatches == 0 and reds == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
