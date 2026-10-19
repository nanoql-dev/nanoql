# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for what the wheel doesn't reach of the swaption volatility cubes (ql/termstructures/volatility/swaption/
sabrswaptionvolatilitycube.hpp and swaptionvolcube.hpp), each output computed by the C++ harness: 8 cubes and 20
refusals on the evaluation date Monday 16 June 2025, the cubes those of tools/vectors/gen/termstructures/volatility/
swaption/swaptionvolcube.py, whose functions build them.

The cubes: 6 SABR cubes (QuantLib's test fixture with every parameter free, at-the-money calibrated; beta fixed, not
at-the-money calibrated; beta fixed in a single pass on the shifted matrix and asymmetric smile; beta fixed on the
normal matrix; beta fixed with a cutoff strike of 4%; one strike spread with alpha alone free, in a single pass) and 2
interpolated cubes (the fixture, and one on a constant at-the-money volatility). Each case gives the cube's
maxSwapTenor, which the wheel doesn't bind, as [length, units]; atmStrike(Period, Period), which it doesn't bind
either, at 12 option and swap tenors (QuantLib's optionDateFromTenor and the date overload), the tenor 365D, which
can't be compared with the short index's 1Y, and 0M, whose swap can't be built, among them; and for a SABR cube
marketVolCube(i) for each strike spread i, which QuantLib reads without its calculation (here after it, as nanoql's
cube computes it at construction), each matrix's elements row by row, strike spread after strike spread.

The SABR cubes calibrate, so their outputs go through the stability filter (the S7 plan's S7R6), as
swaptionvolcube.py's do; none of these outputs depends on a fit, and none is dropped.

The refusals, each a cube whose construction or calibration QuantLib refuses, its one output {"throws": true} (the
calculation is sparseSabrParameters): non-increasing strike spreads; no volatility spreads; a row count and a column
count of the spreads that aren't the grid's; a short index longer than the long one; no strike spread; a single option
tenor and a single swap tenor (the guess cube's "Cube::Cube(...): optionTimes.size()<2" and its twin); an error
tolerance of 1e-8; end criteria of 3 iterations; at-the-money calibration and a single pass on a constant
at-the-money volatility, which isn't a SwaptionVolatilityDiscrete; a cutoff strike above every strike; one strike with
four free parameters (the fit's "less functions (1) than available variables (4)"); a two-pass at-the-money-calibrated
cube and a single-pass one whose at-the-money matrix reaches past the cube's last option tenor (the dense grid's
sparse smiles, and the quoted spreads, have no node beyond it); at-the-money and single-pass calibrations past the
cube's last swap tenor; option dates that aren't fixing dates (an at-the-money matrix on NullCalendar, so a date falls
on a weekend); option dates before the evaluation date (an at-the-money matrix fixed two months earlier, no fixing
stored, so the dense grid's 1M option date has no fixing); and vega-weighted fits, whose dense 1M x 5Y point ends on MaxIterations on this date.

A refused value is {"throws": true}. A case's inputs hold its settings, its "cube" spec, its "points" ([option tenor,
swap tenor] pairs) and "dropped" (the filter's dropped indexes per output, here none). Read back by
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SabrSwaptionVolatilityCubeCxxVectorTests.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)
mat, scv = svc.mat, svc.scv

TODAY = svc.TODAY
D, M, Y = svc.D, svc.M, svc.Y
POINTS = [[[1, Y], [2, Y]], [[10, Y], [10, Y]], [[30, Y], [30, Y]], [[5, Y], [7, Y]], [[1, M], [6, M]],
          [[6, M], [1, Y]], [[6, M], [12, M]], [[1, Y], [13, M]], [[2, Y], [18, M]], [[20, Y], [5, Y]],
          [[1, Y], [365, D]], [[5, Y], [0, M]]]


def cubes():
    """(name, spec) for the 8 cubes."""
    curve = svc.flat_curve(TODAY)
    shifts = [[0.001 * (i + 2 * j) for j in range(4)] for i in range(6)]
    asymmetric = [[0.045, 0.015, 0.0, 0.010, 0.030] for _ in range(9)]
    bf = svc.BETA_FIXED
    constant = scv.moving_spec(2, svc.TARGET, ql.ModifiedFollowing, 0.2, svc.A365)
    return [
        ("SABR, every parameter free, at-the-money calibrated", svc.sabr_spec(svc.atm_floating(), curve)),
        ("SABR, beta fixed, not at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, atm_calibrated=False)),
        ("SABR, beta fixed, single pass, shifted matrix, asymmetric smile",
         svc.sabr_spec(svc.atm_floating(shifts=shifts), curve, guess=[0.05, 0.5, 0.4, 0.0], fixed=bf,
                       vol_spreads=asymmetric, single_pass=True)),
        ("SABR, beta fixed, normal matrix",
         svc.sabr_spec(svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N), curve, guess=[0.05, 0.5, 0.4, 0.0],
                       fixed=bf, vol_spreads=svc.scaled(svc.VOL_SPREADS, 0.03))),
        ("SABR, beta fixed, cutoff strike 4%", svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, cutoff_strike=0.04)),
        ("SABR, one strike spread, alpha free, single pass",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=[False, True, True, True], spreads=[0.0],
                       vol_spreads=[[0.0] for _ in range(9)], single_pass=True)),
        ("interpolated, the fixture", svc.interpolated_spec(svc.atm_floating(), curve)),
        ("interpolated, constant at-the-money volatility", svc.interpolated_spec(constant, curve)),
    ]


def refusals():
    """(name, spec) for the 20 refused cubes."""
    curve = svc.flat_curve(TODAY)
    atm = svc.atm_floating()
    bf = svc.BETA_FIXED
    constant = scv.moving_spec(2, svc.TARGET, ql.ModifiedFollowing, 0.2, svc.A365)
    short = svc.CUBE_OPTIONS[:2]
    rows6 = svc.VOL_SPREADS[:6]
    null_calendar = mat.floating_spec(mat.irx.NULL_CALENDAR, ql.ModifiedFollowing, mat.ATM_OPTIONS, mat.ATM_SWAPS,
                                      mat.ATM_VOLS, svc.A365)
    two_months_earlier = svc.atm_fixed(ql.Date(16, ql.April, 2025))
    return [
        ("non-increasing strike spreads", svc.sabr_spec(atm, curve, spreads=[-0.02, -0.005, -0.005, 0.005, 0.02])),
        ("no volatility spreads", svc.sabr_spec(atm, curve, vol_spreads=[])),
        ("eight rows of spreads for nine pairs", svc.sabr_spec(atm, curve, vol_spreads=svc.VOL_SPREADS[:8])),
        ("a row of four spreads for five strike spreads",
         svc.sabr_spec(atm, curve, vol_spreads=svc.VOL_SPREADS[:2] + [svc.VOL_SPREADS[2][:4]] + svc.VOL_SPREADS[3:])),
        ("a short index longer than the long one",
         svc.sabr_spec(atm, curve, long_index=svc.euribor([1, Y], curve), short_index=svc.euribor([2, Y], curve))),
        ("no strike spread", svc.sabr_spec(atm, curve, spreads=[], vol_spreads=[[] for _ in range(9)])),
        ("a single option tenor",
         svc.sabr_spec(atm, curve, options=[[10, Y]], vol_spreads=svc.VOL_SPREADS[3:6])),
        ("a single swap tenor",
         svc.sabr_spec(atm, curve, swaps=[[10, Y]], vol_spreads=[svc.VOL_SPREADS[i] for i in (1, 4, 7)])),
        ("an error tolerance of 1e-8", svc.sabr_spec(atm, curve, max_error_tolerance=1e-8)),
        ("end criteria of 3 iterations",
         svc.sabr_spec(atm, curve, end_criteria={"maxIterations": 3, "maxStationaryStateIterations": 2,
                                                 "rootEpsilon": 1e-8, "functionEpsilon": 1e-8,
                                                 "gradientNormEpsilon": 1e-8})),
        ("at-the-money calibration on a constant volatility", svc.sabr_spec(constant, curve, fixed=bf)),
        ("a single pass on a constant volatility",
         svc.sabr_spec(constant, curve, fixed=bf, atm_calibrated=False, single_pass=True)),
        ("a cutoff strike above every strike", svc.sabr_spec(atm, curve, cutoff_strike=1.0)),
        ("one strike, four free parameters",
         svc.sabr_spec(atm, curve, spreads=[0.0], vol_spreads=[[0.0] for _ in range(9)], atm_calibrated=False)),
        ("at-the-money calibration past the cube's last option tenor",
         svc.sabr_spec(atm, curve, fixed=bf, options=short, vol_spreads=rows6)),
        ("a single pass past the cube's last option tenor",
         svc.sabr_spec(atm, curve, fixed=bf, options=short, vol_spreads=rows6, single_pass=True)),
        ("at-the-money calibration past the cube's last swap tenor",
         svc.sabr_spec(atm, curve, fixed=bf, swaps=[[2, Y], [10, Y]],
                       vol_spreads=[svc.VOL_SPREADS[i] for i in (0, 1, 3, 4, 6, 7)])),
        ("option dates that aren't fixing dates", svc.sabr_spec(null_calendar, curve, fixed=bf)),
        ("option dates before the evaluation date", svc.sabr_spec(two_months_earlier, curve, fixed=bf)),
        ("vega-weighted fits", svc.sabr_spec(atm, curve, vega=True)),
    ]


def outputs(H, spec):
    out = {"maxSwapTenor": v.attempt(lambda: H.call("SwaptionVolatilityCube::maxSwapTenor", spec)),
           "atmStrike": [v.attempt(lambda p=p: H.call("SwaptionVolatilityCube::atmStrike(Period,Period)", spec, *p))
                         for p in POINTS]}
    if spec["type"] == "SabrSwaptionVolatilityCube":
        out["marketVolCube"] = [x for i in range(len(spec["strikeSpreads"]))
                                for row in H.call("XabrSwaptionVolatilityCube::marketVolCube(Size)", spec, i)
                                for x in row]
    return {k: v.enc(x) for k, x in out.items()}


def cases():
    out = []
    totals = [0, 0]
    with cxx.connect() as H:
        for name, spec in cubes():
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=True) as st:
                base = outputs(H, spec)
                dropped = {}
                if spec["type"] == "SabrSwaptionVolatilityCube":
                    refits = [outputs(H, svc.moved(spec, d)) for d in (math.inf, -math.inf)]
                    base, dropped, lost, total = svc.filtered(name, base, refits)
                    totals[0] += lost
                    totals[1] += total
                out.append({"name": name, "inputs": {"settings": st, "cube": spec, "points": POINTS,
                                                     "dropped": dropped},
                            "expected": base})
        for name, spec in refusals():
            with H.case_settings(v.enc(TODAY), use_at_par_coupons=True) as st:
                built = v.attempt(lambda: (H.call("XabrSwaptionVolatilityCube::sparseSabrParameters", spec), None)[1])
                assert built == v.THROWS, name
                out.append({"name": "refused / " + name, "inputs": {"settings": st, "cube": spec},
                            "expected": {"build": built}})
    print(f"cxx sabrswaptionvolatilitycube: dropped {totals[0]} of {totals[1]} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp", cases)
