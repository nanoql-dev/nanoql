# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SwaptionVolatilityCube (ql/termstructures/volatility/swaption/swaptionvolcube.hpp), the
base of the swaption volatility cubes, through both cubes the wheel builds: 12 cubes on the evaluation date Monday
16 June 2025, 7 InterpolatedSwaptionVolatilityCubes and 5 SabrSwaptionVolatilityCubes.

The cubes are QuantLib's test fixture (test-suite/swaptionvolatilitycube.cpp's CommonVars, on
swaptionvolstructuresutilities.hpp's data: the 6 x 4 at-the-money matrix from 1M to 30Y options and 1Y to 30Y swaps,
floating on the evaluation date on TARGET, Modified Following and Actual/365 (Fixed); the 3 x 3 cube of 1Y, 10Y and
30Y options on 2Y, 10Y and 30Y swaps with five strike spreads; EuriborSwapIsdaFixA 2Y and 1Y on a flat 5% curve) and
its variations: an at-the-money matrix fixed on Wednesday 18 June 2025, so the cube's reference date and grid are the
matrix's, not the evaluation date's; a normal matrix and a shifted one; a constant at-the-money volatility two business
days out (a cube that doesn't fill a dense grid takes any structure); indexed coupons rather than at-par ones (the
context's at-par switch, which the swap indexes' fixings read); a 12-month short index, equal to the 1Y tenor; and long
and short indexes with exogenous discounting curves. The SABR cubes have beta fixed, and three of them aren't
at-the-money calibrated.

Each cube gives one case: what the cube answers from its at-the-money structure (its reference date, maximum date
and time, volatility type, calendar, business-day convention and day counter; the wheel doesn't bind maxSwapTenor,
which the harness's file vectors) and what it holds (strike range, strike spreads,
the volatility spreads as given, the vega-weighting flag, the two indexes' names, whether it extrapolates, and its
option tenors, dates and times, swap tenors and lengths); optionDateFromTime at 6 times; the shift at 8 points, by
option date and swap tenor and by time and length; and atmStrike at 28 option dates and swap tenors: the cube's own
option dates and the at-the-money matrix's, on swap tenors on either side of the short index's (6M, 1Y, 12M, 13M, 18M,
2Y, 5Y, 10Y, 30Y), an option date on the evaluation date, one on a Saturday, which isn't a fixing date, one before
the evaluation date, with no fixing stored, and one 40 years out, and the tenors 365D, which can't be compared with the short index's 1Y,
and 0M, whose swap can't be built. The wheel has no atmStrike(Period, Period), which QuantLib defines as the date
overload at optionDateFromTenor, so the points carry the option tenor and its date.

The SABR cubes calibrate, so their outputs go through the stability filter: each SABR case is refitted with every input
volatility (the at-the-money matrix's and every spread) moved one ulp up, then down, and an output is written only where
both refits leave it within 1e-12 x max(|a|, |b|, 1); the indexes of the dropped elements of each output are in the
case's inputs "dropped", and the generator prints the counts. No output here depends on a fit, so none is dropped.

A refused value is {"throws": true}. A case's inputs hold its settings and its "cube" spec (the functions below build
it), and its points. Every output is a flat list. The cube functions here are shared by the cube generators of this
folder and by tools/vectors/gen/cxx/termstructures/volatility/swaption/sabrswaptionvolatilitycube.py, which import
this file. A cube spec is {"type" ("InterpolatedSwaptionVolatilityCube" or "SabrSwaptionVolatilityCube"), "atm" (a
matrix spec of swaptionvolmatrix.py or a structure spec of swaptionconstantvol.py), "optionTenors", "swapTenors",
"strikeSpreads", "volSpreads" (rows), "swapIndex", "shortSwapIndex" ({"type": "EuriborSwapIsdaFixA", "tenor",
"forwarding"}, with "discounting" for exogenous discounting, curves as indexes/interestrateindex.py's curve_spec),
"vegaWeightedSmileFit", "enableExtrapolation"} and, for a SABR cube, "parametersGuess" (rows), "isParameterFixed",
"isAtmCalibrated", "endCriteria" (null, or {"maxIterations", "maxStationaryStateIterations", "rootEpsilon",
"functionEpsilon", "gradientNormEpsilon"}), "maxErrorTolerance" (null for QuantLib's Null<Real>()), "optMethod"
(null, or a method of models/model.py), "errorAccept" (null), "useMaxError", "maxGuesses", "backwardFlat",
"cutoffStrike" and "singlePassCalibration", the wheel's SabrSwaptionVolatilityCube's arguments, which are the C++
constructor's (sabrswaptionvolatilitycube.hpp:186-206). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/SwaptionVolCubeVectorTests.cs."""
import copy
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


mat = _load("wheel_swaptionvolmatrix", "termstructures/volatility/swaption/swaptionvolmatrix.py")
mdl = _load("wheel_model", "models/model.py")
scv, irx = mat.scv, mat.irx

TODAY = ql.Date(16, ql.June, 2025)
TARGET, A365 = irx.TARGET, irx.A365
SL, N = mat.SL, mat.N
D, W, M, Y = mat.D, mat.W, mat.M, mat.Y
NAN = float("nan")

# swaptionvolstructuresutilities.hpp's VolatilityCube.
CUBE_OPTIONS = [[1, Y], [10, Y], [30, Y]]
CUBE_SWAPS = [[2, Y], [10, Y], [30, Y]]
STRIKE_SPREADS = [-0.020, -0.005, +0.000, +0.005, +0.020]
VOL_SPREADS = [[0.0599, 0.0049, 0.0000, -0.0001, 0.0127],
               [0.0729, 0.0086, 0.0000, -0.0024, 0.0098],
               [0.0738, 0.0102, 0.0000, -0.0039, 0.0065],
               [0.0465, 0.0063, 0.0000, -0.0032, -0.0010],
               [0.0558, 0.0084, 0.0000, -0.0050, -0.0057],
               [0.0576, 0.0083, 0.0000, -0.0043, -0.0014],
               [0.0437, 0.0059, 0.0000, -0.0030, -0.0006],
               [0.0533, 0.0078, 0.0000, -0.0045, -0.0046],
               [0.0545, 0.0079, 0.0000, -0.0042, -0.0020]]
GUESS = [0.2, 0.5, 0.4, 0.0]
FREE = [False, False, False, False]
BETA_FIXED = [False, True, False, False]

# The stability filter's measure.
STABLE = 1e-12


def flat_curve(reference, forward=0.05):
    """QuantLib's test curve, flatRate(forward, Actual365Fixed()): floating on the evaluation date, so fixed on it."""
    return irx.curve_spec(reference, forward)


def euribor(tenor, forwarding, discounting=False):
    spec = {"type": "EuriborSwapIsdaFixA", "tenor": tenor, "forwarding": forwarding}
    if discounting is not False:
        spec["discounting"] = discounting
    return spec


def atm_floating(vols=None, vol_type=SL, shifts=None, flat=False):
    """The fixture's at-the-money matrix, floating on the evaluation date."""
    return mat.floating_spec(TARGET, ql.ModifiedFollowing, mat.ATM_OPTIONS, mat.ATM_SWAPS,
                             vols if vols is not None else mat.ATM_VOLS, A365, flat, vol_type, shifts)


def atm_fixed(reference, vols=None, vol_type=SL, shifts=None):
    return mat.tenors_spec(reference, TARGET, ql.ModifiedFollowing, mat.ATM_OPTIONS, mat.ATM_SWAPS,
                           vols if vols is not None else mat.ATM_VOLS, A365, False, vol_type, shifts)


def scaled(rows, factor):
    return [[factor * x for x in r] for r in rows]


def interpolated_spec(atm, curve, options=CUBE_OPTIONS, swaps=CUBE_SWAPS, spreads=STRIKE_SPREADS,
                      vol_spreads=VOL_SPREADS, long_index=None, short_index=None, vega=False, extrapolate=False):
    return {"type": "InterpolatedSwaptionVolatilityCube", "atm": atm, "optionTenors": options, "swapTenors": swaps,
            "strikeSpreads": spreads, "volSpreads": vol_spreads,
            "swapIndex": long_index if long_index is not None else euribor([2, Y], curve),
            "shortSwapIndex": short_index if short_index is not None else euribor([1, Y], curve),
            "vegaWeightedSmileFit": vega, "enableExtrapolation": extrapolate}


def sabr_spec(atm, curve, guess=GUESS, fixed=FREE, atm_calibrated=True, options=CUBE_OPTIONS, swaps=CUBE_SWAPS,
              spreads=STRIKE_SPREADS, vol_spreads=VOL_SPREADS, long_index=None, short_index=None, vega=False,
              extrapolate=False, guess_rows=None, end_criteria=None, max_error_tolerance=None, opt_method=None,
              error_accept=None, use_max_error=False, max_guesses=50, backward_flat=False, cutoff_strike=0.0001,
              single_pass=False):
    spec = interpolated_spec(atm, curve, options, swaps, spreads, vol_spreads, long_index, short_index, vega,
                             extrapolate)
    spec["type"] = "SabrSwaptionVolatilityCube"
    rows = guess_rows if guess_rows is not None else [list(guess) for _ in range(len(options) * len(swaps))]
    spec.update({"parametersGuess": rows, "isParameterFixed": list(fixed), "isAtmCalibrated": atm_calibrated,
                 "endCriteria": end_criteria, "maxErrorTolerance": max_error_tolerance, "optMethod": opt_method,
                 "errorAccept": error_accept, "useMaxError": use_max_error, "maxGuesses": max_guesses,
                 "backwardFlat": backward_flat, "cutoffStrike": cutoff_strike, "singlePassCalibration": single_pass})
    return spec


def make_atm(spec):
    """The at-the-money structure a spec describes, under the evaluation date the caller has set."""
    return mat.make_matrix(spec) if "form" in spec else scv.make_surface(spec)


def make_index(spec):
    tenor = ql.Period(*spec["tenor"])
    if "discounting" in spec:
        return ql.EuriborSwapIsdaFixA(tenor, irx.handle(spec["forwarding"]), irx.handle(spec["discounting"]))
    return ql.EuriborSwapIsdaFixA(tenor, irx.handle(spec["forwarding"]))


def quotes(rows):
    out = ql.QuoteHandleVectorVector()
    for r in rows:
        row = ql.QuoteHandleVector()
        for x in r:
            row.append(ql.QuoteHandle(ql.SimpleQuote(x)))
        out.append(row)
    return out


def make_cube(spec):
    """The cube a spec describes, built with the wheel's InterpolatedSwaptionVolatilityCube or
    SabrSwaptionVolatilityCube, the proxies of the C++ constructors, under the evaluation date and at-par switch the
    caller has set. The SABR cube calibrates on its first query."""
    h = ql.SwaptionVolatilityStructureHandle(make_atm(spec["atm"]))
    options = [ql.Period(*p) for p in spec["optionTenors"]]
    swaps = [ql.Period(*p) for p in spec["swapTenors"]]
    args = (h, options, swaps, list(spec["strikeSpreads"]), quotes(spec["volSpreads"]), make_index(spec["swapIndex"]),
            make_index(spec["shortSwapIndex"]), spec["vegaWeightedSmileFit"])
    if spec["type"] == "InterpolatedSwaptionVolatilityCube":
        cube = ql.InterpolatedSwaptionVolatilityCube(*args)
    else:
        criteria = spec["endCriteria"]
        method = spec["optMethod"]
        cube = ql.SabrSwaptionVolatilityCube(
            *args, quotes(spec["parametersGuess"]), list(spec["isParameterFixed"]), spec["isAtmCalibrated"],
            endCriteria=mdl.make_criteria(criteria) if criteria is not None else None,
            maxErrorTolerance=spec["maxErrorTolerance"] if spec["maxErrorTolerance"] is not None else v.NULL_REAL,
            optMethod=mdl.make_method(method) if method is not None else None,
            errorAccept=spec["errorAccept"] if spec["errorAccept"] is not None else v.NULL_REAL,
            useMaxError=spec["useMaxError"], maxGuesses=spec["maxGuesses"], backwardFlat=spec["backwardFlat"],
            cutoffStrike=spec["cutoffStrike"], singlePassCalibration=spec["singlePassCalibration"])
    if spec["enableExtrapolation"]:
        cube.enableExtrapolation()
    return cube


def moved(spec, direction):
    """The spec with every input volatility moved one ulp toward direction: the at-the-money structure's volatilities
    and every volatility spread."""
    out = copy.deepcopy(spec)
    atm = out["atm"]
    if "form" in atm:
        atm["volatilities"] = [[math.nextafter(x, direction) for x in r] for r in atm["volatilities"]]
    else:
        atm["volatility"] = math.nextafter(atm["volatility"], direction)
    out["volSpreads"] = [[math.nextafter(x, direction) for x in r] for r in out["volSpreads"]]
    return out


def is_stable(a, b):
    if isinstance(a, float) and isinstance(b, float):
        return abs(a - b) <= STABLE * max(abs(a), abs(b), 1.0)
    return a == b


def filtered(name, base, refits):
    """The stability filter on one case: base and each refit are {output: flat list of encoded values}. Returns the
    kept values and the dropped indexes per output, and prints the counts."""
    kept, dropped, total, lost = {}, {}, 0, 0
    for key, values in base.items():
        out = [i for i, x in enumerate(values) if not all(is_stable(x, r[key][i]) for r in refits)]
        if out:
            dropped[key] = out
        kept[key] = [x for i, x in enumerate(values) if i not in set(out)]
        total += len(values)
        lost += len(out)
    detail = ", ".join(f"{k} {len(d)} of {len(base[k])}" for k, d in dropped.items())
    print(f"{name}: dropped {lost} of {total} outputs" + (f" ({detail})" if detail else ""))
    return kept, dropped, lost, total


def flat(m):
    """A Matrix's elements, row by row."""
    return [m[i][j] for i in range(m.rows()) for j in range(m.columns())]


def a(f):
    return v.attempt(f)


def period(p):
    return ql.Period(*p)


# The points: (option tenor, swap tenor); the option date is the cube's optionDateFromTenor, or one of the special dates.
ATM_STRIKE_POINTS = (
    [([o, Y], s) for o in (1, 10, 30) for s in ([2, Y], [10, Y], [30, Y])]
    + [([1, M], [6, M]), ([6, M], [1, Y]), ([6, M], [12, M]), ([1, Y], [13, M]), ([1, Y], [18, M]),
       ([5, Y], [5, Y]), ([2, Y], [1, Y]), ([5, Y], [12, M]), ([10, Y], [13, M]), ([30, Y], [18, M]),
       ([1, Y], [365, D]), ([5, Y], [0, M])])
SPECIAL_DATES = [("evaluation date", [5, Y]), ("Saturday", [2, Y]), ("before the evaluation date", [2, Y]),
                 ("40 years out", [10, Y])]
TIMES = [0.0, 0.5, 1.0, 7.25, 30.0, 35.0]
SHIFT_POINTS = [([1, Y], 1.0, [2, Y], 2.0), ([10, Y], 10.0, [10, Y], 10.0), ([5, Y], 5.0, [7, Y], 7.0),
                ([30, Y], 30.0, [30, Y], 30.0)]


def special_date(cube, which):
    ref = cube.referenceDate()
    if which == "evaluation date":
        return ql.Settings.instance().evaluationDate
    if which == "Saturday":
        return ql.Date(21, ql.June, 2025)
    if which == "before the evaluation date":
        return ql.Date(16, ql.May, 2025)
    return TARGET_CAL.advance(ref, 40, ql.Years)


TARGET_CAL = ql.TARGET()


def points_of(cube):
    out = []
    for option, swap in ATM_STRIKE_POINTS:
        out.append({"optionTenor": option, "optionDate": v.enc(cube.optionDateFromTenor(period(option))),
                    "swapTenor": swap})
    for which, swap in SPECIAL_DATES:
        out.append({"optionTenor": None, "optionDate": v.enc(special_date(cube, which)), "swapTenor": swap})
    return out


def base_outputs(cube, pts):
    s = cube
    out = {"referenceDate": [v.enc(s.referenceDate())], "maxDate": [v.enc(s.maxDate())], "maxTime": [s.maxTime()],
           "volatilityType": [int(s.volatilityType())], "calendar": [s.calendar().name()],
           "businessDayConvention": [int(s.businessDayConvention())], "dayCounter": [s.dayCounter().name()],
           "minStrike": [s.minStrike()], "maxStrike": [s.maxStrike()], "strikeSpreads": list(s.strikeSpreads()),
           "volSpreads": [q.value() for row in ql.SwaptionVolatilityCube.volSpreads(s) for q in row],
           "vegaWeightedSmileFit": [s.vegaWeightedSmileFit()],
           "swapIndexBase": [s.swapIndexBase().name()], "shortSwapIndexBase": [s.shortSwapIndexBase().name()],
           "allowsExtrapolation": [s.allowsExtrapolation()],
           "optionTenors": [x for p in s.optionTenors() for x in (p.length(), int(p.units()))],
           "optionDates": v.enc(list(s.optionDates())), "optionTimes": list(s.optionTimes()),
           "swapTenors": [x for p in s.swapTenors() for x in (p.length(), int(p.units()))],
           "swapLengths": list(s.swapLengths())}
    out["optionDateFromTime"] = [a(lambda t=t: s.optionDateFromTime(t)) for t in TIMES]
    out["shiftDatePeriod"] = [a(lambda o=o, w=w: s.shift(s.optionDateFromTenor(period(o)), period(w)))
                              for o, _, w, _ in SHIFT_POINTS]
    out["shiftTimeTime"] = [a(lambda t=t, ln=ln: s.shift(t, ln)) for _, t, _, ln in SHIFT_POINTS]
    out["atmStrike"] = [a(lambda p=p: s.atmStrike(ql.Date(p["optionDate"]), period(p["swapTenor"]))) for p in pts]
    return {k: v.enc(x) for k, x in out.items()}


def cubes():
    """(name, evaluation date, at-par switch, spec) for the 12 cubes."""
    curve = flat_curve(TODAY)
    exogenous = flat_curve(TODAY, 0.04)
    shifts = [[0.001 * (i + 2 * j) for j in range(4)] for i in range(6)]
    return [
        ("interpolated, the fixture", TODAY, True, interpolated_spec(atm_floating(), curve)),
        ("interpolated, at-the-money matrix fixed on 18 June 2025", TODAY, True,
         interpolated_spec(atm_fixed(ql.Date(18, ql.June, 2025)), curve)),
        ("interpolated, normal matrix", TODAY, True,
         interpolated_spec(atm_floating(scaled(mat.ATM_VOLS, 0.05), N), curve, vol_spreads=scaled(VOL_SPREADS, 0.03))),
        ("interpolated, shifted matrix", TODAY, True,
         interpolated_spec(atm_floating(shifts=shifts), curve)),
        ("interpolated, indexed coupons", TODAY, False, interpolated_spec(atm_floating(), curve)),
        ("interpolated, a 12M short index", TODAY, True,
         interpolated_spec(atm_floating(), curve, short_index=euribor([12, M], curve))),
        ("interpolated, exogenous discounting", TODAY, True,
         interpolated_spec(atm_floating(), curve, long_index=euribor([2, Y], curve, exogenous),
                           short_index=euribor([1, Y], curve, exogenous))),
        ("SABR, beta fixed", TODAY, True, sabr_spec(atm_floating(), curve, fixed=BETA_FIXED)),
        ("SABR, beta fixed, at-the-money matrix fixed on 18 June 2025", TODAY, True,
         sabr_spec(atm_fixed(ql.Date(18, ql.June, 2025)), curve, fixed=BETA_FIXED)),
        ("SABR, beta fixed, constant at-the-money volatility, not at-the-money calibrated", TODAY, True,
         sabr_spec(scv.moving_spec(2, TARGET, ql.ModifiedFollowing, 0.2, A365), curve, fixed=BETA_FIXED,
                   atm_calibrated=False)),
        ("SABR, beta fixed, indexed coupons, not at-the-money calibrated", TODAY, False,
         sabr_spec(atm_floating(), curve, fixed=BETA_FIXED, atm_calibrated=False)),
        ("SABR, beta fixed, normal matrix, not at-the-money calibrated", TODAY, True,
         sabr_spec(atm_floating(scaled(mat.ATM_VOLS, 0.05), N), curve, guess=[0.05, 0.5, 0.4, 0.0], fixed=BETA_FIXED,
                   atm_calibrated=False, vol_spreads=scaled(VOL_SPREADS, 0.03))),
    ]


def calculated(cube, spec):
    """The cube after its first calculation, which recomputes its grid from its at-the-money structure's reference
    date (QuantLib's inspectors read before it give the grid of the evaluation date): nanoql's cube computes it when
    it is built."""
    if spec["type"] == "SabrSwaptionVolatilityCube":
        cube.sparseSabrParameters()
    else:
        cube.volatility(1.0, 2.0, 0.05, True)
    return cube


def outputs_of(spec, pts=None):
    cube = calculated(make_cube(spec), spec)
    pts = pts if pts is not None else points_of(cube)
    return base_outputs(cube, pts), pts


def cube_case(name, evaluation_date, at_par, spec):
    with v.case_settings(evaluation_date, use_at_par_coupons=at_par) as st:
        base, pts = outputs_of(spec)
        dropped = {}
        if spec["type"] == "SabrSwaptionVolatilityCube":
            refits = [outputs_of(moved(spec, d), pts)[0] for d in (math.inf, -math.inf)]
            base, dropped, _, _ = filtered(name, base, refits)
        return {"name": name, "inputs": {"settings": st, "cube": spec, "points": pts, "dropped": dropped},
                "expected": base}


def cases():
    out = [cube_case(*row) for row in cubes()]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    fixture = by_name["interpolated, the fixture"]
    fixed = by_name["interpolated, at-the-money matrix fixed on 18 June 2025"]
    indexed = by_name["interpolated, indexed coupons"]
    # The cube's reference date and grid are its at-the-money structure's.
    assert fixed["referenceDate"] == [ql.Date(18, ql.June, 2025).serialNumber()]
    assert fixed["optionDates"] != fixture["optionDates"]
    # The at-par switch moves the at-the-money strikes; the forbidden points throw.
    assert indexed["atmStrike"][:9] != fixture["atmStrike"][:9]
    assert fixture["atmStrike"][19:21] == [v.THROWS, v.THROWS], fixture["atmStrike"][19:21]
    assert fixture["atmStrike"][22:24] == [v.THROWS, v.THROWS], fixture["atmStrike"][22:24]
    # The SABR and interpolated cubes on one structure and indexes give one at-the-money strike.
    assert by_name["SABR, beta fixed"]["atmStrike"] == fixture["atmStrike"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/swaptionvolcube.hpp", cases)
