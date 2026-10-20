# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SpreadedSwaptionVolatility (ql/termstructures/volatility/swaption/spreadedswaptionvol.hpp):
a spread over each of seven base structures on the evaluation date Monday 16 June 2025: the fixture's at-the-money
matrix (swaptionvolcube.py's, floating on the date), the same matrix with extrapolation enabled, the normal matrix, the
shifted matrix of the SABR cube's file, testSpreadedCube's SABR cube (every parameter free, at-the-money calibrated),
testZabrVols' ZABR cube and the interpolated cube; the spreads 0.0001, 0.002, -0.001, 0.0005 and 0 among them.

Each case gives the structure's reference date, day counter, calendar, convention, maximum date and time, strike
bounds, volatility type and extrapolation flag, which it reads from its base; its volatility by option date (the option tenor's) and swap tenor
at 3 x 2 points and by option time and swap length at 3, each at 3 strikes, and past the base's range with and without
the extrapolation flag; its black variance at the tenor points; its shift by option time and swap length; and its
smile section (a SpreadedSmileSection over the base's) by tenors at 2 points and by times at 1, each as its ATM level
(null where the base's section has none, QuantLib's Null<Real>()), exercise time, shift, volatility type, and its
volatility at the 3 strikes, converted to a normal volatility and to a shifted lognormal one, each at the shift
1.37%, which no base section has (refused where the section has no ATM level): QuantLib's spreaded section compares
the type asked for with a member its constructor leaves uninitialized, so only a conversion to another shift is
defined whatever the member holds.

The stability filter (the S7 plan's S7R6) runs on the cases over a calibrated cube as on the cubes' own files: each is
rebuilt over its base with every input volatility moved one ulp up, then down, and an output is written only where
both rebuilds leave it within 1e-12 x max(|a|, |b|, 1); the indexes of each output's dropped elements are in the
case's inputs "dropped".

A refused value is {"throws": true}. A case's inputs hold its settings, "spreaded" ({"base", "baseExtrapolates",
"spread"}: the base a matrix spec of swaptionvolcube.py's or a cube spec, its extrapolation enabled where
"baseExtrapolates" is true) and "dropped". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/SpreadedSwaptionVolatilityVectorTests.cs."""
import importlib.util
import math
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


svc = _load("wheel_swaptionvolcube", "termstructures/volatility/swaption/swaptionvolcube.py")
zabr = _load("wheel_zabrswaptionvolatilitycube", "termstructures/volatility/swaption/zabrswaptionvolatilitycube.py")
mat = svc.mat

TODAY, M, Y = svc.TODAY, svc.M, svc.Y
TENOR_POINTS = [([1, Y], [2, Y]), ([1, Y], [10, Y]), ([5, Y], [2, Y]), ([5, Y], [10, Y]), ([10, Y], [2, Y]),
                ([10, Y], [10, Y])]
TIME_POINTS = [(0.5, 2.0), (5.0, 10.0), (20.0, 15.0)]
STRIKES = [0.02, 0.04, 0.06]
PAST = ([40, Y], [40, Y])
SECTION_TENORS = [([5, Y], [5, Y]), ([1, Y], [10, Y])]
SECTION_TIMES = [(2.0, 5.0)]
# A shift no base section has: QuantLib's spreaded section compares the type asked for with a member its constructor
# leaves uninitialized (spreadedsmilesection.cpp:26-31, smilesection.cpp:133), so only a conversion to another shift
# is defined whatever the member holds (tests/NanoQL.Tests/Divergences/SpreadedSmileSectionTypeTests.cs).
CONVERSION_SHIFT = 0.0137


def bases():
    """(name, base spec, base extrapolates, spread)."""
    curve = svc.flat_curve(TODAY)
    shifts = [[0.001 * (i + 2 * j) for j in range(4)] for i in range(6)]
    normal = svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N)
    return [
        ("the at-the-money matrix, spread 0.002", svc.atm_floating(), False, 0.002),
        ("the at-the-money matrix extrapolating, spread -0.001", svc.atm_floating(), True, -0.001),
        ("the normal matrix, spread 0.0005", normal, False, 0.0005),
        ("the shifted matrix, spread 0.002", svc.atm_floating(shifts=shifts), False, 0.002),
        ("testSpreadedCube: the SABR cube, every parameter free, spread 0.0001",
         svc.sabr_spec(svc.atm_floating(), curve), False, 0.0001),
        ("testZabrVols' ZABR cube, spread 0.0001", zabr.zabr_spec(), False, 0.0001),
        ("the interpolated cube, spread 0", svc.interpolated_spec(svc.atm_floating(), curve), False, 0.0),
    ]


def make_base(spec):
    """A cube spec has its at-the-money structure under "atm"; any other is a matrix spec."""
    if "atm" not in spec:
        return svc.make_atm(spec)
    if spec["type"] == "ZabrSwaptionVolatilityCube":
        return zabr.make_cube(spec)
    return svc.make_cube(spec)


def is_cube(spec):
    """A calibrated cube, whose outputs the filter checks."""
    return "atm" in spec and spec["type"] in ("SabrSwaptionVolatilityCube", "ZabrSwaptionVolatilityCube")


def moved(spec, direction):
    """The base spec with every input volatility moved one ulp: a cube's as the cubes' filter moves them."""
    return svc.moved(spec, direction)


def section_values(f):
    try:
        s = f()
    except RuntimeError:
        return [dict(v.THROWS)] * (4 + 3 * len(STRIKES))
    atm = s.atmLevel()
    a = v.attempt
    out = [None if atm == v.NULL_REAL else atm, s.exerciseTime(), s.shift(), int(s.volatilityType())]
    for k in STRIKES:
        out += [a(lambda k=k: s.volatility(k)), a(lambda k=k: s.volatility(k, ql.Normal, CONVERSION_SHIFT)),
                a(lambda k=k: s.volatility(k, ql.ShiftedLognormal, CONVERSION_SHIFT))]
    return out


def outputs(base_spec, extrapolates, spread):
    base = make_base(base_spec)
    if extrapolates:
        base.enableExtrapolation()
    s = ql.SpreadedSwaptionVolatility(ql.SwaptionVolatilityStructureHandle(base), ql.QuoteHandle(ql.SimpleQuote(spread)))
    a = v.attempt

    def d(o):
        return s.optionDateFromTenor(ql.Period(*o))

    out = {
        "referenceDate": s.referenceDate(), "dayCounter": s.dayCounter().name(), "calendar": s.calendar().name(),
        "businessDayConvention": s.businessDayConvention(), "maxDate": s.maxDate(), "maxTime": s.maxTime(),
        "minStrike": s.minStrike(), "maxStrike": s.maxStrike(), "volatilityType": int(s.volatilityType()),
        "allowsExtrapolation": s.allowsExtrapolation(),
        "volatilityDatePeriod": [a(lambda o=o, w=w, k=k: s.volatility(d(o), ql.Period(*w), k))
                                 for o, w in TENOR_POINTS for k in STRIKES],
        "volatilityTimeTime": [a(lambda t=t, ln=ln, k=k: s.volatility(t, ln, k)) for t, ln in TIME_POINTS
                               for k in STRIKES],
        "volatilityPast": [a(lambda k=k, e=e: s.volatility(d(PAST[0]), ql.Period(*PAST[1]), k, e))
                           for e in (False, True) for k in STRIKES],
        "blackVariance": [a(lambda o=o, w=w, k=k: s.blackVariance(d(o), ql.Period(*w), k))
                          for o, w in TENOR_POINTS for k in STRIKES],
        "shift": [a(lambda t=t, ln=ln: s.shift(t, ln)) for t, ln in TIME_POINTS],
        "sectionsDatePeriod": [x for o, w in SECTION_TENORS for x in section_values(
            lambda o=o, w=w: s.smileSection(d(o), ql.Period(*w)))],
        "sectionsTimeTime": [x for t, ln in SECTION_TIMES for x in section_values(lambda t=t, ln=ln: s.smileSection(t, ln))],
    }
    return {k: v.enc(x) for k, x in out.items()}


TOTALS = {"dropped": 0, "outputs": 0}


def case(name, base_spec, extrapolates, spread):
    with v.case_settings(TODAY, use_at_par_coupons=True) as st:
        base = outputs(base_spec, extrapolates, spread)
        dropped = {}
        if is_cube(base_spec):
            refits = [outputs(moved(base_spec, d), extrapolates, spread) for d in (math.inf, -math.inf)]
            # The structure's members, read from the base's conventions, don't move; the lists are filtered.
            members = {k: x for k, x in base.items() if not isinstance(x, list)}
            assert all(r[k] == x for r in refits for k, x in members.items()), name
            lists = {k: x for k, x in base.items() if isinstance(x, list)}
            kept, dropped, lost, total = svc.filtered(name, lists, refits)
            base = {k: (kept[k] if k in kept else x) for k, x in base.items()}
            TOTALS["dropped"] += lost
            TOTALS["outputs"] += total
        spec = {"base": base_spec, "baseExtrapolates": extrapolates, "spread": spread}
        return {"name": name, "inputs": {"settings": st, "spreaded": spec, "dropped": dropped}, "expected": base}


def cases():
    out = [case(*row) for row in bases()]
    print(f"spreadedswaptionvol: dropped {TOTALS['dropped']} of {TOTALS['outputs']} filtered outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/spreadedswaptionvol.hpp", cases)
