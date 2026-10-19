# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwaptionVolatilityMatrix (ql/termstructures/volatility/swaption/swaptionvolmatrix.hpp), on
its Matrix constructors, which nanoql's take: 8 matrices and 12 refusals.

The matrices: QuantLib's at-the-money test matrix (6 option tenors from 1M to 30Y, 4 swap tenors from 1Y to 30Y,
swaptionvolstructuresutilities.hpp's volatilities) with a fixed reference date; the same with its reference date the
evaluation date (QuantLib's floating form, which nanoql builds by SwaptionVolatilityMatrix.Create), under the
evaluation date 15 January 2025 and, rebuilt, a year earlier, and with its first volatility changed to 10, as
QuantLib's testSwaptionVolMatrixObservability changes it through its quote (these three replace that case: a nanoql
structure is rebuilt under a new context or new data); a matrix on option dates of normal volatilities on Actual/360;
a shifted matrix with flat extrapolation; a floating shifted one with extrapolation enabled; and a normal matrix with
shifts, which the matrix reads without the lognormal check.

Each matrix's outputs are its reference and maximum dates, strike range, volatility type, option tenors, dates and
times, swap tenors and lengths; and at 25 points (an option tenor, its date, an option time, a swap tenor and a swap
length, a strike and the extrapolate argument), as arrays over the points: volatility and blackVariance on an option
date and a swap tenor and on an option time and a swap length (the overloads the wheel binds), shift in its six
overloads, the smile section on a date and a tenor and on a time and a length (its exercise date and time, type, shift,
ATM level and volatility at the strike) and the volatility at the strike of the other four, locate on a date and a
tenor and on a time and a length (the option's and the swap's indexes), and optionDateFromTime at the point's time. The
points reach between the nodes, the nodes, before the first option time, past the last (with and without
extrapolation), a negative time, swap lengths below the first and past the last, a swap tenor in weeks and one in
months, and a NaN strike. A refused value is {"throws": true}.

The refusals: a volatility matrix with the wrong number of rows or columns, a shift matrix with the wrong number of
rows or columns, a single swap tenor or option tenor (the interpolation's "not enough points"), non-increasing option
and swap tenors, a zero first option tenor, a swap tenor in days, a first option date on the reference date, and
non-increasing option dates. Empty tenors and dates, which QuantLib reads at their first element (undefined), are
nanoql's refusals, a documented divergence, not vectored.

A matrix spec is {"form" ("tenors", "dates" or "floating"), "referenceDate" (not for "floating"), "calendar",
"convention", "optionTenors" or "optionDates", "swapTenors", "volatilities" (rows), "dayCounter", "flatExtrapolation",
"type", "shifts" (rows, or null for QuantLib's default Matrix()), "enableExtrapolation"}, calendars and day counters as
in indexes/interestrateindex.py; built with the wheel's SwaptionVolatilityMatrix.forTenors (QuantLib's fixed-reference
Matrix constructor), its constructor on option dates and its floating Matrix constructor, the proxies of the C++
constructors. The points are swaptionconstantvol.py's pair specs. The functions here build the specs and the matrices
the swaption generators share (pricingengines/swaption/blackswaptionengine and cxx/termstructures/volatility/swaption/
swaptionvoldiscrete import this file). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/SwaptionVolMatrixVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionconstantvol", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption",
                                              "swaptionconstantvol.py"))
scv = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(scv)
irx = scv.irx

TODAY = irx.TODAY
YEAR_BEFORE = ql.Date(15, ql.January, 2024)
TARGET, US_SOFR, UK_EXCHANGE = irx.TARGET, irx.US_SOFR, irx.UK_EXCHANGE
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
SL, N = scv.SL, scv.N
D, W, M, Y = scv.D, scv.W, scv.M, scv.Y
NAN = float("nan")

# swaptionvolstructuresutilities.hpp's AtmVolatility.
ATM_OPTIONS = [[1, M], [6, M], [1, Y], [5, Y], [10, Y], [30, Y]]
ATM_SWAPS = [[1, Y], [5, Y], [10, Y], [30, Y]]
ATM_VOLS = [[0.1300, 0.1560, 0.1390, 0.1220],
            [0.1440, 0.1580, 0.1460, 0.1260],
            [0.1600, 0.1590, 0.1470, 0.1290],
            [0.1640, 0.1470, 0.1370, 0.1220],
            [0.1400, 0.1300, 0.1250, 0.1100],
            [0.1130, 0.1090, 0.1070, 0.0930]]


def tenors_spec(reference, cal, convention, options, swaps, vols, dc, flat=False, vol_type=SL, shifts=None,
                extrapolate=False):
    return {"form": "tenors", "referenceDate": v.enc(reference), "calendar": cal, "convention": int(convention),
            "optionTenors": options, "swapTenors": swaps, "volatilities": vols, "dayCounter": dc,
            "flatExtrapolation": flat, "type": vol_type, "shifts": shifts, "enableExtrapolation": extrapolate}


def floating_spec(cal, convention, options, swaps, vols, dc, flat=False, vol_type=SL, shifts=None, extrapolate=False):
    return {"form": "floating", "calendar": cal, "convention": int(convention), "optionTenors": options,
            "swapTenors": swaps, "volatilities": vols, "dayCounter": dc, "flatExtrapolation": flat, "type": vol_type,
            "shifts": shifts, "enableExtrapolation": extrapolate}


def dates_spec(reference, cal, convention, dates, swaps, vols, dc, flat=False, vol_type=SL, shifts=None,
               extrapolate=False):
    return {"form": "dates", "referenceDate": v.enc(reference), "calendar": cal, "convention": int(convention),
            "optionDates": v.enc(list(dates)), "swapTenors": swaps, "volatilities": vols, "dayCounter": dc,
            "flatExtrapolation": flat, "type": vol_type, "shifts": shifts, "enableExtrapolation": extrapolate}


def matrix(rows):
    m = ql.Matrix(len(rows), len(rows[0]) if rows else 0)
    for i, row in enumerate(rows):
        for j, x in enumerate(row):
            m[i][j] = x
    return m


def make_matrix(spec):
    """The matrix a spec describes, under the evaluation date the caller has set."""
    cal = irx.calendar(spec["calendar"])
    dc = irx.day_counter(spec["dayCounter"])
    swaps = [ql.Period(*p) for p in spec["swapTenors"]]
    vols = matrix(spec["volatilities"])
    shifts = matrix(spec["shifts"]) if spec["shifts"] is not None else ql.Matrix()
    rest = (dc, spec["flatExtrapolation"], spec["type"], shifts)
    if spec["form"] == "tenors":
        m = ql.SwaptionVolatilityMatrix.forTenors(ql.Date(spec["referenceDate"]), cal, spec["convention"],
                                                  [ql.Period(*p) for p in spec["optionTenors"]], swaps, vols, *rest)
    elif spec["form"] == "dates":
        m = ql.SwaptionVolatilityMatrix(ql.Date(spec["referenceDate"]), cal, spec["convention"],
                                        [ql.Date(x) for x in spec["optionDates"]], swaps, vols, *rest)
    elif spec["form"] == "floating":
        m = ql.SwaptionVolatilityMatrix(cal, spec["convention"], [ql.Period(*p) for p in spec["optionTenors"]], swaps,
                                        vols, *rest)
    else:
        raise ValueError(f"unknown form {spec['form']}")
    if spec["enableExtrapolation"]:
        m.enableExtrapolation()
    return m


def shifted(rows, base=0.01, step=0.001):
    return [[base + step * (i + j) for j in range(len(rows[0]))] for i in range(len(rows))]


def matrices():
    """(name, evaluation date, spec) for the 8 matrices."""
    changed = [row[:] for row in ATM_VOLS]
    changed[0][0] = 10.0
    normal = [[0.0060, 0.0072, 0.0081], [0.0065, 0.0074, 0.0079], [0.0071, 0.0076, 0.0078], [0.0068, 0.0070, 0.0072]]
    option_dates = [ql.Date(15, ql.April, 2025), ql.Date(15, ql.January, 2026), ql.Date(15, ql.January, 2027),
                    ql.Date(17, ql.January, 2030)]
    small_options = [[3, M], [1, Y], [2, Y], [5, Y], [10, Y]]
    small_swaps = [[2, Y], [5, Y], [10, Y]]
    small = [[0.30, 0.27, 0.24], [0.28, 0.26, 0.23], [0.26, 0.25, 0.22], [0.22, 0.21, 0.20], [0.20, 0.19, 0.18]]
    return [
        ("at the money, fixed reference date", TODAY,
         tenors_spec(TODAY, TARGET, ql.ModifiedFollowing, ATM_OPTIONS, ATM_SWAPS, ATM_VOLS, A365)),
        ("at the money, floating", TODAY,
         floating_spec(TARGET, ql.ModifiedFollowing, ATM_OPTIONS, ATM_SWAPS, ATM_VOLS, A365)),
        ("at the money, floating, a year earlier", YEAR_BEFORE,
         floating_spec(TARGET, ql.ModifiedFollowing, ATM_OPTIONS, ATM_SWAPS, ATM_VOLS, A365)),
        ("at the money, first volatility changed", TODAY,
         tenors_spec(TODAY, TARGET, ql.ModifiedFollowing, ATM_OPTIONS, ATM_SWAPS, changed, A365)),
        ("option dates, normal, Actual/360", TODAY,
         dates_spec(TODAY, US_SOFR, ql.Following, option_dates, small_swaps, normal, A360, vol_type=N)),
        ("shifted, flat extrapolation", TODAY,
         tenors_spec(ql.Date(17, ql.January, 2025), UK_EXCHANGE, ql.Following, small_options, small_swaps, small,
                     THIRTY360_BB, True, SL, shifted(small))),
        ("floating, shifted, extrapolation enabled", ql.Date(18, ql.January, 2025),
         floating_spec(TARGET, ql.ModifiedFollowing, small_options, small_swaps, small, A365, False, SL,
                       shifted(small, 0.02, -0.001), True)),
        ("normal with shifts, flat extrapolation", TODAY,
         tenors_spec(TODAY, TARGET, ql.Preceding, small_options[:4], small_swaps, normal, A365, True, N,
                     shifted(normal[:4], 0.005, 0.0005))),
    ]


def refusals():
    """(name, spec) for the 12 refused constructions."""
    two = [[0.2, 0.21], [0.22, 0.23]]
    opts, swaps = [[1, Y], [2, Y]], [[2, Y], [5, Y]]
    dates = [ql.Date(15, ql.January, 2026), ql.Date(15, ql.January, 2027)]
    return [
        ("too few rows", tenors_spec(TODAY, TARGET, ql.Following, opts + [[3, Y]], swaps, two, A365)),
        ("too few columns", tenors_spec(TODAY, TARGET, ql.Following, opts, swaps + [[7, Y]], two, A365)),
        ("shift rows", tenors_spec(TODAY, TARGET, ql.Following, opts, swaps, two, A365, shifts=[[0.01, 0.01]])),
        ("shift columns", tenors_spec(TODAY, TARGET, ql.Following, opts, swaps, two, A365,
                                      shifts=[[0.01], [0.01]])),
        ("a single swap tenor", tenors_spec(TODAY, TARGET, ql.Following, opts, [[2, Y]], [[0.2], [0.21]], A365)),
        ("a single option tenor", tenors_spec(TODAY, TARGET, ql.Following, [[1, Y]], swaps, [[0.2, 0.21]], A365)),
        ("non-increasing option tenors", tenors_spec(TODAY, TARGET, ql.Following, [[2, Y], [1, Y]], swaps, two, A365)),
        ("non-increasing swap tenors", tenors_spec(TODAY, TARGET, ql.Following, opts, [[5, Y], [24, M]], two, A365)),
        ("zero first option tenor", tenors_spec(TODAY, TARGET, ql.Following, [[0, D], [1, Y]], swaps, two, A365)),
        ("swap tenor in days", tenors_spec(TODAY, TARGET, ql.Following, opts, [[30, D], [2, Y]], two, A365)),
        ("first option date on the reference date", dates_spec(TODAY, TARGET, ql.Following, [TODAY, dates[1]], swaps,
                                                                two, A365)),
        ("non-increasing option dates", dates_spec(TODAY, TARGET, ql.Following, [dates[1], dates[0]], swaps, two,
                                                   A365)),
    ]


# (option tenor, option time, swap tenor, swap length, strike, extrapolate); "max+" is the maximum time plus 1, "mid"
# the middle of the option times.
POINTS = [
    ([1, Y], 1.0, [5, Y], 5.0, 0.03, False),
    ([6, M], 0.5, [1, Y], 1.0, 0.02, False),
    ([1, M], 1.0 / 12, [10, Y], 10.0, 0.05, False),
    ([5, Y], 5.0, [30, Y], 30.0, 0.04, False),
    ([10, Y], 10.0, [7, Y], 7.0, 0.03, False),
    ([2, Y], 2.0, [2, Y], 2.0, 0.025, False),
    ([3, M], 0.25, [5, Y], 5.0, -0.01, False),
    ([18, M], 1.5, [3, Y], 3.0, 0.035, False),
    ([7, Y], 7.25, [15, Y], 15.0, 0.03, False),
    ([1, Y], 0.9, [6, M], 0.5, 0.03, False),
    ([1, Y], 1.1, [6, M], 0.5, 0.03, True),
    ([2, Y], 0.01, [8, Y], 8.0, 0.03, False),
    ([1, M], 0.02, [1, Y], 1.0, 0.03, False),
    ([0, D], 0.0, [5, Y], 5.0, 0.03, False),
    ([30, Y], 30.0, [30, Y], 30.0, 0.03, False),
    ([31, Y], "max+", [5, Y], 5.0, 0.03, False),
    ([31, Y], "max+", [5, Y], 5.0, 0.03, True),
    ([1, Y], -0.25, [5, Y], 5.0, 0.03, False),
    ([1, Y], 1.0, [35, Y], 35.0, 0.03, False),
    ([1, Y], 1.0, [35, Y], 35.0, 0.03, True),
    ([1, Y], 1.0, [2, W], 2.0 / 52, 0.03, False),
    ([6, M], "mid", [18, M], 1.5, 0.03, False),
    ([5, Y], 4.0, [10, Y], 10.0, NAN, False),
    ([10, Y], 9.5, [20, Y], 20.0, 0.06, True),
    ([3, Y], 3.0, [40, Y], 40.0, 0.03, True),
]
assert len(POINTS) == 25


def points_of(s):
    out = []
    times = list(s.optionTimes())
    for option, time, swap, length, strike, extrapolate in POINTS:
        if time == "max+":
            t = s.maxTime() + 1.0
        elif time == "mid":
            t = (times[0] + times[-1]) / 2
        else:
            t = time
        out.append({"optionTenor": option, "optionDate": v.enc(s.optionDateFromTenor(scv.period(option))),
                    "optionTime": t, "swapTenor": swap, "swapLength": length, "strike": v.enc(strike),
                    "extrapolate": extrapolate})
    return out


def located(f):
    try:
        row, column = f()
    except RuntimeError:
        return dict(v.THROWS)
    return [row, column]


def matrix_case(name, evaluation_date, spec):
    with v.case_settings(evaluation_date) as st:
        s = make_matrix(spec)
        pts = points_of(s)
        expected = {"referenceDate": v.enc(s.referenceDate()), "maxDate": v.enc(s.maxDate()),
                    "minStrike": s.minStrike(), "maxStrike": s.maxStrike(), "volatilityType": int(s.volatilityType()),
                    "optionTenors": [[p.length(), int(p.units())] for p in s.optionTenors()],
                    "optionDates": v.enc(list(s.optionDates())), "optionTimes": list(s.optionTimes()),
                    "swapTenors": [[p.length(), int(p.units())] for p in s.swapTenors()],
                    "swapLengths": list(s.swapLengths())}

        def over(f):
            out = []
            for p in pts:
                o, d, t = scv.period(p["optionTenor"]), ql.Date(p["optionDate"]), p["optionTime"]
                sw, ln = scv.period(p["swapTenor"]), p["swapLength"]
                x = NAN if p["strike"] == "NaN" else p["strike"]
                out.append(f(o, d, t, sw, ln, x, p["extrapolate"]))
            return out

        a = v.attempt
        expected["volatilityDatePeriod"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.volatility(d, sw, x, e)))
        expected["volatilityTimeTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.volatility(t, ln, x, e)))
        expected["blackVarianceDatePeriod"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.blackVariance(d, sw, x, e)))
        expected["blackVarianceTimeTime"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.blackVariance(t, ln, x, e)))
        expected["shiftPeriodPeriod"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(o, sw, e)))
        expected["shiftDatePeriod"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(d, sw, e)))
        expected["shiftTimePeriod"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(t, sw, e)))
        expected["shiftPeriodTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(o, ln, e)))
        expected["shiftDateTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(d, ln, e)))
        expected["shiftTimeTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.shift(t, ln, e)))
        expected["smileSectionDatePeriod"] = over(
            lambda o, d, t, sw, ln, x, e: scv.summary_or_throw(lambda: s.smileSection(d, sw, e), x))
        expected["smileSectionTimeTime"] = over(
            lambda o, d, t, sw, ln, x, e: scv.summary_or_throw(lambda: s.smileSection(t, ln, e), x))
        expected["smileVolatilityPeriodPeriod"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.smileSection(o, sw, e).volatility(x)))
        expected["smileVolatilityTimePeriod"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.smileSection(t, sw, e).volatility(x)))
        expected["smileVolatilityPeriodTime"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.smileSection(o, ln, e).volatility(x)))
        expected["smileVolatilityDateTime"] = over(
            lambda o, d, t, sw, ln, x, e: a(lambda: s.smileSection(d, ln, e).volatility(x)))
        expected["locateDatePeriod"] = over(lambda o, d, t, sw, ln, x, e: located(lambda: s.locate(d, sw)))
        expected["locateTimeTime"] = over(lambda o, d, t, sw, ln, x, e: located(lambda: s.locate(t, ln)))
        expected["optionDateFromTime"] = over(lambda o, d, t, sw, ln, x, e: a(lambda: s.optionDateFromTime(t)))
        return {"name": f"{name} / {evaluation_date.ISO()}",
                "inputs": {"settings": st, "matrix": spec, "points": pts}, "expected": expected}


def refusal_case(name, spec):
    with v.case_settings(TODAY) as st:
        return {"name": "refused / " + name, "inputs": {"settings": st, "matrix": spec},
                "expected": {"build": v.attempt(lambda: (make_matrix(spec), None)[1])}}


def cases():
    out = [matrix_case(*row) for row in matrices()]
    out += [refusal_case(*row) for row in refusals()]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    fixed = by_name[f"at the money, fixed reference date / {TODAY.ISO()}"]
    floating = by_name[f"at the money, floating / {TODAY.ISO()}"]
    earlier = by_name[f"at the money, floating, a year earlier / {YEAR_BEFORE.ISO()}"]
    changed = by_name[f"at the money, first volatility changed / {TODAY.ISO()}"]
    # QuantLib's observability case: the floating matrix follows the evaluation date, and the data change moves the
    # volatility; the fixed matrix, under the same date, equals the floating one.
    assert floating["volatilityTimeTime"] == fixed["volatilityTimeTime"]
    assert earlier["optionDates"] != floating["optionDates"]
    assert changed["volatilityTimeTime"][12] != fixed["volatilityTimeTime"][12]
    for c in out:
        if c["name"].startswith("refused / "):
            assert c["expected"]["build"] == v.THROWS, c["name"]
    assert not any(isinstance(x, float) and math.isnan(x) for x in fixed["optionTimes"])


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/swaptionvolmatrix.hpp", cases)
