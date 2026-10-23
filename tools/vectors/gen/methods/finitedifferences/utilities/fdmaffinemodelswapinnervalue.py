# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmAffineModelSwapInnerValue (ql/methods/finitedifferences/utilities/
fdmaffinemodelswapinnervalue.hpp, .cpp), through QuantLib-Python's FdmAffineG2ModelSwapInnerValue and
FdmAffineHullWhiteModelSwapInnerValue, with the curve it builds at each point (fdmaffinemodeltermstructure.hpp, .cpp),
on the evaluation date 15 January 2025.

A swap's exercise value at every point of the finite-difference engines' meshers (tools/vectors/gen/methods/
finitedifferences/operators/fdmg2op.py's factor meshers, 12 by 10 points for G2 and 21 for Hull-White, to the swap's
last exercise), at each of its exercise times: the coupons starting on or after the exercise date, forecast on the
forwarding model's curve at the point and discounted on the discounting model's, floored at 0. On the first G2 and the
first Hull-White model of fdmg2op.py, discounting on a flat 2.7% curve and forwarding on a flat 3.1% one: payer and
receiver swaps on Euribor 6M in and out of the money, one with a floating spread, and an overnight-indexed swap on ESTR,
each exercisable at its first three fixed-leg starts. Each case's outputs are the inner value at each exercise time,
point by point in the layout's order, and the average inner value at the first, which is the inner value. A time not
among the exercise times, where QuantLib reads the map's end (undefined behaviour), is nanoql's refusal
(tests/NanoQL.Tests/Conventions/InnerValueAmericanRefusalTests.cs), not generated.

A case holds the model specs (discounting and forwarding), the swap (tools/vectors/gen/instruments/swaption.py's spec),
the mesher and the exercise dates; the times are the discount curve's year fractions from its reference date. The
wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmAffineG2ModelSwapInnerValue(disModel, fwdModel, swap, exerciseDates,
mesher, direction) and FdmAffineHullWhiteModelSwapInnerValue likewise, exerciseDates a TimeToDateMap; innerValue and
avgInnerValue the C++ members, at the layout's iterator walked by begin and increment. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmAffineModelSwapInnerValueVectorTests.cs."""
import importlib.util
import os
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


g2op = _load("fdmg2op", "methods/finitedifferences/operators/fdmg2op.py")
swp, spm, mc = g2op.swp, g2op.spm, g2op.mc
fvf, irx = swp.fvf, swp.irx
TODAY = swp.TODAY
M, Y = ql.Months, ql.Years


def fixed_starts(swap):
    return [ql.as_coupon(c).accrualStartDate() for c in swap.fixedLeg()]


def time_map(curve, dates):
    """QuantLib's TimeToDateMap of the dates by their times on the curve, a later date at an equal time replacing an
    earlier, as the engines' map assigns."""
    m = ql.TimeToDateMap()
    for d in dates:
        m[curve.dayCounter().yearFraction(curve.referenceDate(), d)] = d
    return m


def forwarding_model(model, forecast):
    return dict(model, curve=forecast)


def swaps():
    exercise, start, maturity = swp.dates_of(ql.Period(1, Y), ql.Period(5, Y))
    atm = 0.031
    out = [("payer at the money", swp.vanilla(swp.PAYER, 1e6, atm, start, maturity)),
           ("payer out of the money", swp.vanilla(swp.PAYER, 1e6, 1.3 * atm, start, maturity)),
           ("receiver in the money", swp.vanilla(swp.RECEIVER, 1e6, 1.2 * atm, start, maturity)),
           ("receiver, floating spread 0.5%", swp.vanilla(swp.RECEIVER, 1e6, atm, start, maturity, spread=0.005)),
           ("overnight-indexed payer", swp.overnight(swp.PAYER, 1e6, 0.03, start, start + ql.Period(3, Y)))]
    return out


def inner_values(calculator, mesher, times):
    layout = mesher.layout()
    out = []
    for t in times:
        it = layout.begin()
        row = []
        for _ in range(layout.size()):
            row.append(calculator.innerValue(it, t))
            it.increment()
        out.append(v.enc(row))
    it = layout.begin()
    avg = []
    for _ in range(layout.size()):
        avg.append(calculator.avgInnerValue(it, times[0]))
        it.increment()
    return out, v.enc(avg)


def cases():
    out = []
    for model in (g2op.G2S[0], g2op.HULL_WHITES[0]):
        dis = g2op.make_model(model)
        fwd_spec = forwarding_model(model, swp.FORECAST)
        fwd = g2op.make_model(fwd_spec)
        curve = irx.handle(model["curve"])
        for sname, sspec in swaps():
            with v.case_settings(TODAY) as st:
                swap = fvf.make_swap(sspec)
                dates = fixed_starts(swap)[:3]
                maturity = curve.dayCounter().yearFraction(curve.referenceDate(), dates[-1])
                sizes = (12, 10) if model["type"] == "G2" else (21,)
                mspec = mc.composite_spec(*g2op.factor_meshers(model, sizes, maturity))
                mesher, keep = spm.composite(mspec)
                t2d = time_map(curve, dates)
                cls = (ql.FdmAffineG2ModelSwapInnerValue if model["type"] == "G2"
                       else ql.FdmAffineHullWhiteModelSwapInnerValue)
                calculator = cls(dis, fwd, swap, t2d, mesher, 0)
                times = list(t2d.keys())
                inner, avg = inner_values(calculator, mesher, times)
                out.append({"name": f"{model['type']}, {sname}",
                            "inputs": {"settings": st, "model": model, "forwardingModel": fwd_spec, "swap": sspec,
                                       "mesher": mspec, "exerciseDates": [v.enc(d) for d in dates]},
                            "expected": {"innerValue": inner, "avgInnerValue": avg}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.hpp", cases)
