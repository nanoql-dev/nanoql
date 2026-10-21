# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmDividendHandler (ql/methods/finitedifferences/utilities/fdmdividendhandler.hpp, .cpp), on
the evaluation date 15 October 2026: the dividends' times, dates and amounts, and applyTo(a, t) at each dividend's
time, where each value moves to the value at the underlying less the dividend (by linear interpolation along the equity
direction, floored at the grid's lowest underlying), and at a time with no dividend, where nothing moves.

Four schedules (two dividends; one before the reference date, at a negative time; a dividend larger than the grid's
lowest underlying; and two on Act/360), on a Black-Scholes mesher, a two-dimensional mesher with the equity in the
second direction and one with it in the first. A grid of three or more dimensions, where QuantLib moves only some of the
lines, is nanoql's divergence (tests/NanoQL.Tests/Divergences/FdmDividendHandler3dTests.cs).

The specs are fdmmeshercomposite.py's composites and fdmblackscholesmesher.py's dividends. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmDividendHandler(schedule, mesher, referenceDate, dayCounter, equityDirection), the C++
constructor; dividendTimes, dividendDates, dividends and applyTo the C++ members, applyTo writing the wheel's Array in
place. Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmDividendHandlerVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
bsm, bsp = mc.bsm, mc.bsm.bsp
R = bsm.R
SCHEDULES = [("two dividends", [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)], bsm.bvts.A365),
             ("one before the reference date", [bsm.fixed(1.5, -20), bsm.fixed(2.5, 150)], bsm.bvts.A365),
             ("a large dividend", [bsm.fixed(60.0, 120)], bsm.bvts.A365),
             ("on Act/360", [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)], bsm.bvts.A360)]
FLAT = bsp.PROCESSES[0][1]
MESHERS = [("Black-Scholes", mc.composite_spec(bsm.bs_spec(21, FLAT, 1.0, 100.0)), 0),
           ("equity second", mc.composite_spec(mc.U4, bsm.bs_spec(9, FLAT, 1.0, 100.0)), 1),
           ("equity first", mc.composite_spec(bsm.bs_spec(9, FLAT, 1.0, 100.0), mc.C3), 0)]


def arr(a):
    return v.enc([a[i] for i in range(len(a))])


def cases():
    st = v.settings(R)
    rng = random.Random(20261028)
    out = []
    for mname, mspec, direction in MESHERS:
        mesher, keep = mc.composite(mspec)
        n = mesher.layout().size()
        for sname, divs, dc in SCHEDULES:
            h = ql.FdmDividendHandler(bsm.dividends(divs), mesher, R, bsm.bvts.day_counter(dc), direction)
            times = list(h.dividendTimes())
            a = [rng.uniform(0.0, 40.0) for _ in range(n)]
            applied = []
            for t in times + [0.6]:
                x = ql.Array(a)
                h.applyTo(x, t)
                applied.append(arr(x))
            out.append({"name": f"{mname}, {sname}",
                        "inputs": {"settings": st, "mesher": mspec, "dividends": divs, "referenceDate": R.serialNumber(),
                                   "dayCounter": dc, "direction": direction, "array": a, "times": times + [0.6]},
                        "expected": {"dividendTimes": v.enc(times), "dividendDates": v.enc(list(h.dividendDates())),
                                     "dividends": v.enc(list(h.dividends())), "applyTo": applied}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmdividendhandler.hpp", cases)
