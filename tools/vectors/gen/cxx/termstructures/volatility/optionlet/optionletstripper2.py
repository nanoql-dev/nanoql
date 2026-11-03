# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's OptionletStripper2 (ql/termstructures/volatility/optionlet/optionletstripper2.hpp), which the
wheel doesn't bind, each output computed by the C++ harness: a stripper 1 of
cxx/termstructures/volatility/optionlet/optionletstripper.py extended with an at-the-money curve of
termstructures/volatility/capfloor/capfloortermvolcurve.py's spec.

The cases are QuantLib's stripper tests' (test-suite/optionletstripper.cpp): the flat surface with the flat 18% curve
(lags 0 and 2, spreads 0), the market surface with its at-the-money curve on 30 April 2015, the front stub (9M and 1Y at
19%), the curve on two settlement days, SOFR with the flat and the skewed curve (lag 2), and SOFR from zero base
volatilities with dontThrow (positive spreads); and the curves QuantLib refuses, each "built": {"throws": true}: the 6M
Ibor cap ("cap/floor length 6M does not contain any optionlets"), a curve on another day counter than the surface's,
at-the-money volatilities of 90%, which no spread up to 0.1 reaches, and of 0.01% on the market surface, below the
minimum attainable price, a SOFR curve 2 years longer than its surface, and on 28 October 2013 a 9M front stub over a
stripper of one optionlet and a smile steep enough to give a negative base volatility at the at-the-money strike. A strip's outputs, each its own, are the at-the-money strikes, prices and spreads,
every date's strikes and volatilities with the at-the-money ones inserted, the fixing dates and times and the
at-the-money rates.

The stability filter runs on the spreads and the inserted volatilities, which a Brent solve gives: the strip is redone
with the curve's volatilities moved one ulp up and down, together and, on a curve of at most 32 volatilities, each
alone, and an output is written only where every move leaves it within 1e-12 x max(|a|, |b|, 1); a dropped spread is
null, its index in the case's "droppedSpreads". Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/OptionletStripper2CxxVectorTests.cs."""
import importlib.util
import math
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
cvc = _load("wheel_capfloortermvolcurve", "termstructures/volatility/capfloor/capfloortermvolcurve.py")
ql, cfs, os1 = ops.ql, ops.cfs, ops.os1
M, Y = ops.M, ops.Y
STABLE = 1e-12
FEW = 32


def curve(tenors, vols, days=0, cal=ops.TARGET, convention=ql.Following, dc=None):
    return cvc.curve_spec(tenors, vols, days, cal=cal, convention=convention, dc=dc or cvc.A365)


def rows():
    """(name, evaluation date, fixings, stripper spec, curve spec)."""
    by_name = {row[0]: row for row in ops.strippers()}
    flat0, flat2 = by_name["flat surface, payment lag 0"], by_name["flat surface, payment lag 2"]
    sofr = by_name["SOFR, flat, lag 2"]
    years10 = [(i, Y) for i in range(1, 11)]
    apr15 = ql.Date(30, 4, 2015)
    market = ops.stripper(cfs.surface_spec(os1.MARKET_TENORS, os1.MARKET_STRIKES, os1.MARKET_VOLS, 0),
                          ops.euribor(6, ops.irx.curve_spec(apr15, 0.04)))
    market_curve = curve(cvc.MARKET_TENORS, cvc.MARKET_VOLS)
    sofr_skew = dict(sofr[3])
    sofr_skew["surface"] = cfs.surface_spec(
        os1.SOFR_TENORS, os1.SOFR_STRIKES,
        [[(25.0 + 1.5 * (i + 1) + s) / 10000.0 for s in (4.0, 2.0, 0.0)] for i in range(10)], 2, cal=ops.US_SOFR,
        convention=ql.ModifiedFollowing, dc=ops.A360)
    sofr_zero = dict(sofr[3])
    sofr_zero["surface"] = cfs.surface_spec(os1.SOFR_TENORS, os1.SOFR_STRIKES, [[0.0] * 3 for _ in range(10)], 2,
                                            cal=ops.US_SOFR, convention=ql.ModifiedFollowing, dc=ops.A360)
    sofr_zero["dontThrow"] = True

    def sofr_curve(vols):
        return curve(os1.SOFR_TENORS, vols, 2, cal=ops.US_SOFR, convention=ql.ModifiedFollowing, dc=ops.A360)

    oct13 = ql.Date(28, 10, 2013)
    euribor_oct13 = ops.euribor(6, ops.irx.curve_spec(oct13, 0.04))
    one_optionlet = ops.stripper(cfs.surface_spec([(6, M), (1, Y)], [0.02, 0.03, 0.04, 0.05], [[0.20] * 4] * 2, 0),
                                 euribor_oct13)
    steep = ops.stripper(cfs.surface_spec([(1, Y), (2, Y), (3, Y)], [0.02, 0.03, 0.035], [[0.40, 0.30, 0.10]] * 3, 0),
                         euribor_oct13)

    return [
        ("flat, lag 0", flat0[1], None, flat0[3], curve(years10, [0.18] * 10)),
        ("flat, lag 2", flat2[1], None, flat2[3], curve(years10, [0.18] * 10)),
        ("market curve", apr15, None, market, market_curve),
        ("front stub", flat0[1], None, flat0[3], curve([(9, M), (1, Y)], [0.19, 0.19])),
        ("curve on two settlement days", flat0[1], None, flat0[3], curve(years10, [0.18] * 10, days=2)),
        ("SOFR, flat curve", sofr[1], sofr[2], sofr[3], sofr_curve([30.0 / 10000.0] * 10)),
        ("SOFR, skewed", sofr[1], sofr[2], sofr_skew,
         sofr_curve([(25.0 + 1.5 * (i + 1) + 2.0) / 10000.0 + 0.0001 for i in range(10)])),
        ("SOFR, zero base volatilities", sofr[1], sofr[2], sofr_zero, sofr_curve([0.05] * 10)),
        ("refused: a 6M Ibor cap", flat0[1], None, flat0[3], curve([(6, M), (1, Y)], [0.18, 0.18])),
        ("refused: another day counter", flat0[1], None, flat0[3], curve(years10, [0.18] * 10, dc=ops.A360)),
        ("refused: a spread beyond 0.1", flat0[1], None, flat0[3], curve(years10, [0.9] * 10)),
        ("refused: below the minimum attainable price", apr15, None, market,
         curve(cvc.MARKET_TENORS, [1e-4] * len(cvc.MARKET_TENORS))),
        ("refused: an overnight cap beyond the surface", sofr[1], sofr[2], sofr[3],
         curve(os1.SOFR_TENORS + [(12, Y)], [30.0 / 10000.0] * 11, 2, cal=ops.US_SOFR, convention=ql.ModifiedFollowing,
               dc=ops.A360)),
        ("refused: a front stub over one optionlet", oct13, None, one_optionlet, curve([(9, M), (1, Y)], [0.19, 0.19])),
        ("refused: a negative base volatility at the at-the-money strike", oct13, None, steep,
         curve([(1, Y), (2, Y), (3, Y)], [0.20] * 3)),
    ]


def moves(vols):
    out = [[math.nextafter(x, math.inf) for x in vols], [math.nextafter(x, -math.inf) for x in vols]]
    if len(vols) <= FEW:
        for i in range(len(vols)):
            for direction in (math.inf, -math.inf):
                moved = list(vols)
                moved[i] = math.nextafter(vols[i], direction)
                out.append(moved)
    return out


def stable(a, b):
    return a == b or abs(a - b) <= STABLE * max(abs(a), abs(b), 1.0)


def case(H, dropped_total, name, today, fixings, spec, crv):
    kw = {} if fixings is None else {"fixings": fixings}
    with H.case_settings(v.enc(today), **kw) as st:
        base = v.attempt(lambda: H.call("OptionletStripper2::strip", spec, crv))
        inputs = {"settings": st, "stripper": spec, "curve": crv}
        if "throws" in base:
            return {"name": name, "inputs": inputs, "expected": {"built": base}}
        keep = [True] * len(base["spreadsVol"])
        for vols in moves(crv["volatilities"]):
            moved = v.attempt(lambda: H.call("OptionletStripper2::strip", spec, {**crv, "volatilities": vols}))
            for j, s in enumerate(base["spreadsVol"]):
                keep[j] = keep[j] and "throws" not in moved and stable(s, moved["spreadsVol"][j])
        dropped = [j for j, k in enumerate(keep) if not k]
        dropped_total[0] += len(dropped)
        dropped_total[1] += len(keep)
        print(f"{name}: dropped {len(dropped)} of {len(keep)} spreads")
        base["spreadsVol"] = [None if j in dropped else s for j, s in enumerate(base["spreadsVol"])]
        return {"name": name, "inputs": {**inputs, "droppedSpreads": dropped}, "expected": {"built": True, **base}}


def cases():
    total = [0, 0]
    with cxx.connect() as H:
        out = [case(H, total, *row) for row in rows()]
    print(f"cxx/termstructures/volatility/optionlet/optionletstripper2: dropped {total[0]} of {total[1]} spreads")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/optionletstripper2.hpp", cases)
