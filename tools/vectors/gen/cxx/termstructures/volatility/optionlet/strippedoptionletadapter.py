# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StrippedOptionletAdapter (ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp),
each output computed by the C++ harness: the members the wheel doesn't bind, on adapters over the strippers of
cxx/termstructures/volatility/optionlet/optionletstripper.py.

Each adapter's smile sections at six option times (the first fixing time, between fixing times, the last, and past it
on an adapter that extrapolates) give their exercise time, minimum and maximum strikes, type, shift, and their
volatilities at eleven strikes, the stripper's own, between them and outside them, where the cubic spline
extrapolates (Lagrange from four strikes, natural below), floored at 0; and the adapter's volatility by tenor at
eight tenors, with and without the extrapolation flag ({"throws": true} where it refuses). Each section field is
its own output, "smile.<field>", a list over the times, {"throws": true} where the adapter refuses the time. A
section's at-the-money level isn't read: QuantLib's holds Null<Real>() in a SimpleQuote, whose read throws
"invalid SimpleQuote" (tests/NanoQL.Tests/Divergences/NullRealSentinelTests.cs).
Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/StrippedOptionletAdapterCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ops = _load("cxx_optionletstripper", "cxx/termstructures/volatility/optionlet/optionletstripper.py")
M, Y = ops.M, ops.Y
ADAPTED = ["flat surface, payment lag 0", "market surface, payment lag 2", "survey surface",
           "survey surface, Euribor 3M, lag 1", "SOFR, flat, lag 2"]
TENORS = [[6, M], [1, Y], [18, M], [2, Y], [3, Y], [5, Y], [40, Y], [1, int(ops.ql.Weeks)]]
SECTION_FIELDS = ["exerciseTime", "minStrike", "maxStrike", "volatilityType", "shift", "volatilities"]


def case(H, name, today, fixings, spec, extrapolate):
    kw = {} if fixings is None else {"fixings": fixings}
    with H.case_settings(v.enc(today), **kw) as st:
        times = H.call("OptionletStripper::optionletFixingTimes", spec)
        strikes = spec["surface"]["strikes"]
        k0, k1 = strikes[0], strikes[-1]
        probe = sorted(set(strikes + [0.5 * (strikes[0] + strikes[1]), 0.5 * (strikes[-2] + strikes[-1]),
                                      k0 - 0.005, k1 + 0.01, k1 + 0.05]))
        at = [times[0], 0.5 * (times[0] + times[1]), times[len(times) // 2], times[-1]]
        if extrapolate:
            at += [times[-1] + 1.0, 0.5 * times[0]]
        sections = [v.attempt(lambda t=t: H.call("StrippedOptionletAdapter::smileSection", spec, extrapolate, t, probe))
                    for t in at]

        def field(k):
            return [x if "throws" in x and len(x) == 1 else x[k] for x in sections]
        by_tenor = [[t, k, e] for t in TENORS for k, e in ((0.5 * (k0 + k1), False), (k1, True))]
        vols = [v.attempt(lambda p=p: H.call("StrippedOptionletAdapter::volatility(Period)", spec, extrapolate, *p))
                for p in by_tenor]
        return {"name": name + (", extrapolating" if extrapolate else ""),
                "inputs": {"settings": st, "stripper": spec, "enableExtrapolation": extrapolate, "times": at,
                           "strikes": probe, "byTenor": by_tenor},
                "expected": {**{f"smile.{k}": field(k) for k in SECTION_FIELDS}, "volatilityByTenor": vols}}


def cases():
    rows = {row[0]: row for row in ops.strippers()}
    with cxx.connect() as H:
        return [case(H, *rows[name], e) for name in ADAPTED for e in (False, True)]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/strippedoptionletadapter.hpp", cases)
