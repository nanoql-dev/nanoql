# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedSwaptionVolatilityCube
(ql/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.hpp): 10 cubes on the evaluation date
Monday 16 June 2025, on the test fixture of swaptionvolcube.py (whose functions build them) and its variations: an
at-the-money matrix fixed on 18 June 2025, a normal matrix (the volatilities 0.05 and the spreads 0.03 of the
fixture's), a shifted one, one extrapolating flat, the cube itself extrapolating, two strike spreads (the fewest it
takes), option and swap tenors off the at-the-money grid, indexed coupons rather than at-par ones, and a 12-month short
index.

Each cube gives one case: its volatility spreads by strike spread (volSpreads(i), each a matrix of option by swap
tenors, their elements row by row, strike spread after strike spread); and at 16 points (an option tenor, its date, an
option time, a swap tenor and a swap length, a strike and the extrapolate argument), as lists over the points: the
volatility and the Black variance by option date and swap tenor and by time and length (the two overloads the wheel
binds), and the smile section by
option tenor and swap tenor, by date and tenor and by time and length (the cube's own two overloads and the base's
conversion of a tenor to its date), each section as its exercise date, exercise time, volatility type, shift, ATM
level, minimum and maximum strikes, and volatility and variance at the point's strike. The points reach the cube's
nodes, between and beyond them inside the matrix's range, past its maximum date and swap tenor (with and without the
extrapolate argument), an option time of 0, where the section's volatilities are 0/0, a NaN strike, a swap length that
rounds to 0 months, whose swap can't be built, and an option date on a TARGET holiday, which isn't a fixing date, so the
section by date refuses it where the section by time adjusts it. The wheel has no volatility(Period, Period) or
smileSection's other overloads' conversions of its own: QuantLib converts an option tenor with optionDateFromTenor, as
the points do.

The sections' at-the-money strikes are the swap indexes' fixings, read under the case's settings: nanoql's cube reads
the context it was built with. A refused value is {"throws": true}. A case's inputs hold its settings, its "cube" spec
(swaptionvolcube.py's) and its points, {"optionTenor", "optionDate", "optionTime", "swapTenor", "swapLength",
"strike", "extrapolate"}. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/InterpolatedSwaptionVolatilityCubeVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)
mat = svc.mat

TODAY = svc.TODAY
D, M, Y = svc.D, svc.M, svc.Y
NAN = float("nan")
HOLIDAY = ql.Date(25, ql.December, 2030)

# (option tenor, option time, swap tenor, swap length, strike, extrapolate); the option date is the cube's
# optionDateFromTenor, except for the "holiday" point, whose date is HOLIDAY and whose time is its own.
POINTS = [
    ([1, Y], 1.0, [2, Y], 2.0, 0.05, False),
    ([10, Y], 10.0, [10, Y], 10.0, 0.04, False),
    ([30, Y], 30.0, [30, Y], 30.0, 0.06, False),
    ([5, Y], 5.0, [7, Y], 7.0, 0.05, False),
    ([2, Y], 2.0, [5, Y], 5.0, 0.03, False),
    ([20, Y], 20.0, [20, Y], 20.0, 0.055, False),
    ([6, M], 0.5, [1, Y], 1.0, 0.045, False),
    ([1, M], 0.08, [18, M], 1.5, 0.07, False),
    ([35, Y], 35.0, [5, Y], 5.0, 0.05, False),
    ([35, Y], 35.0, [5, Y], 5.0, 0.05, True),
    ([5, Y], 5.0, [40, Y], 40.0, 0.05, False),
    ([5, Y], 5.0, [40, Y], 40.0, 0.05, True),
    ([1, Y], 1.0, [2, Y], 0.01, 0.05, False),
    ([0, D], 0.0, [5, Y], 5.0, 0.05, False),
    ([3, Y], 3.0, [5, Y], 5.0, NAN, False),
    ("holiday", None, [5, Y], 5.0, 0.05, False),
]


def cubes():
    """(name, at-par switch, spec) for the 10 cubes."""
    curve = svc.flat_curve(TODAY)
    shifts = [[0.001 * (i + 2 * j) for j in range(4)] for i in range(6)]
    two = [[r[0] - 0.01, r[4] + 0.01] for r in svc.VOL_SPREADS]
    off_spreads = [[0.04 - 0.002 * k, 0.006, 0.0, -0.003, -0.002 + 0.001 * k] for k in range(6)]
    return [
        ("the fixture", True, svc.interpolated_spec(svc.atm_floating(), curve)),
        ("at-the-money matrix fixed on 18 June 2025", True,
         svc.interpolated_spec(svc.atm_fixed(ql.Date(18, ql.June, 2025)), curve)),
        ("normal matrix", True,
         svc.interpolated_spec(svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N), curve,
                               vol_spreads=svc.scaled(svc.VOL_SPREADS, 0.03))),
        ("shifted matrix", True, svc.interpolated_spec(svc.atm_floating(shifts=shifts), curve)),
        ("matrix extrapolating flat", True, svc.interpolated_spec(svc.atm_floating(flat=True), curve)),
        ("cube extrapolating", True, svc.interpolated_spec(svc.atm_floating(), curve, extrapolate=True)),
        ("two strike spreads", True,
         svc.interpolated_spec(svc.atm_floating(), curve, spreads=[-0.01, 0.01], vol_spreads=two)),
        ("tenors off the matrix's grid", True,
         svc.interpolated_spec(svc.atm_floating(), curve, options=[[2, Y], [7, Y], [20, Y]], swaps=[[3, Y], [15, Y]],
                               vol_spreads=off_spreads)),
        ("indexed coupons", False, svc.interpolated_spec(svc.atm_floating(), curve)),
        ("a 12M short index", True,
         svc.interpolated_spec(svc.atm_floating(), curve, short_index=svc.euribor([12, M], curve))),
    ]


def points_of(cube):
    out = []
    for option, time, swap, length, strike, extrapolate in POINTS:
        if option == "holiday":
            date, time, tenor = HOLIDAY, cube.timeFromReference(HOLIDAY), None
        else:
            date, tenor = cube.optionDateFromTenor(ql.Period(*option)), option
        out.append({"optionTenor": tenor, "optionDate": v.enc(date), "optionTime": time, "swapTenor": swap,
                    "swapLength": length, "strike": v.enc(strike), "extrapolate": extrapolate})
    return out


def section(f, strike):
    try:
        s = f()
    except RuntimeError:
        return [dict(v.THROWS)] * 9
    a = v.attempt
    return [a(lambda: s.exerciseDate()), a(lambda: s.exerciseTime()), a(lambda: int(s.volatilityType())),
            a(lambda: s.shift()), a(lambda: s.atmLevel()), a(lambda: s.minStrike()), a(lambda: s.maxStrike()),
            a(lambda: s.volatility(strike)), a(lambda: s.variance(strike))]


def cube_case(name, at_par, spec):
    with v.case_settings(TODAY, use_at_par_coupons=at_par) as st:
        cube = svc.calculated(svc.make_cube(spec), spec)
        pts = points_of(cube)
        a = v.attempt
        expected = {"volSpreads": [x for i in range(len(spec["strikeSpreads"])) for x in svc.flat(cube.volSpreads(i))]}

        def over(f):
            out = []
            for p in pts:
                tenor = ql.Period(*p["optionTenor"]) if p["optionTenor"] is not None else None
                d, t = ql.Date(p["optionDate"]), p["optionTime"]
                sw, ln = ql.Period(*p["swapTenor"]), p["swapLength"]
                x = NAN if p["strike"] == "NaN" else p["strike"]
                out.append(f(tenor, d, t, sw, ln, x, p["extrapolate"]))
            return out

        expected["volatilityDatePeriod"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: cube.volatility(d, sw, x, e)))
        expected["volatilityTimeTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: cube.volatility(t, ln, x, e)))
        expected["blackVarianceDatePeriod"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: cube.blackVariance(d, sw, x, e)))
        expected["blackVarianceTimeTime"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: cube.blackVariance(t, ln, x, e)))
        expected["smileSectionPeriodPeriod"] = [
            y for s in over(lambda o, d, t, sw, ln, x, e: section(
                lambda: cube.smileSection(o if o is not None else d, sw, e), x)) for y in s]
        expected["smileSectionDatePeriod"] = [
            y for s in over(lambda o, d, t, sw, ln, x, e: section(lambda: cube.smileSection(d, sw, e), x)) for y in s]
        expected["smileSectionTimeTime"] = [
            y for s in over(lambda o, d, t, sw, ln, x, e: section(lambda: cube.smileSection(t, ln, e), x)) for y in s]
        return {"name": name, "inputs": {"settings": st, "cube": spec, "points": pts},
                "expected": {k: v.enc(x) for k, x in expected.items()}}


def cases():
    out = [cube_case(*row) for row in cubes()]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    fixture = by_name["the fixture"]
    # The holiday: refused by date, adjusted by time; time 0: NaN volatilities; a 0M length: refused.
    assert fixture["volatilityDatePeriod"][15] == v.THROWS
    assert isinstance(fixture["volatilityTimeTime"][15], float)
    assert fixture["volatilityDatePeriod"][13] == "NaN"
    assert fixture["volatilityTimeTime"][12] == v.THROWS
    # Past the maximum date and swap tenor: refused unless extrapolating.
    assert fixture["volatilityDatePeriod"][8] == v.THROWS and isinstance(fixture["volatilityDatePeriod"][9], float)
    assert fixture["volatilityDatePeriod"][10] == v.THROWS and isinstance(fixture["volatilityDatePeriod"][11], float)
    assert by_name["indexed coupons"]["volatilityDatePeriod"] != fixture["volatilityDatePeriod"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/interpolatedswaptionvolatilitycube.hpp", cases)
