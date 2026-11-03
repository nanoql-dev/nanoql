# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SABR swaption volatility cube (ql/termstructures/volatility/swaption/
sabrswaptionvolatilitycube.hpp, the XabrSwaptionVolatilityCube template on SwaptionVolCubeSabrModel, which the wheel
binds as SabrSwaptionVolatilityCube): 13 calibrated cubes on the evaluation date Monday 16 June 2025, and
testObservability's replacement, 4 cubes on two evaluation dates.

The cubes are QuantLib's test fixture (swaptionvolcube.py's, whose functions build them) calibrated with every
parameter free (testSabrVols' cube) and with beta fixed, each at-the-money calibrated and not, and with
backward-flat interpolation; in a single pass, at-the-money calibrated and not, on testSabrAtmCalibrationWithAsymmetric
Smile's shifted matrix and smile; on a normal matrix (testSabrNormalTwoPassUsesStrikeSpreadMoneyness's volatilities
and spreads, on the flat curve); fitted by Simplex; scored by their maximum errors; with a cutoff strike of 4%, which
drops the lowest strikes from the fits; and on one strike spread with alpha alone free, in a single pass, where the
dense spreads are the quoted ones.

Each cube gives one case: its four matrices (sparseSabrParameters, denseSabrParameters where the cube has dense
parameters, marketVolCube and volCubeAtmCalibrated), each as its elements row by row, and their shapes; the volatility
at 72 points on the cube's grid and 72 off it, the option tenors 1Y, 10Y and 30Y on the swap tenors 2Y, 10Y and 30Y, and
2Y, 5Y and 20Y on 5Y, 15Y and 20Y, each at the at-the-money strike plus the five strike spreads and -1%, 1% and 3%,
extrapolating; the volatility by option time and swap length at 4 points and 3 strikes; and the smile section (a
SabrSmileSection) by option and swap tenors at 8 points and by option time and swap length at 4, each as its alpha,
beta, nu, rho, ATM level, exercise time, shift, volatility type and volatility at its ATM level and 1% on either side,
past the cube's range only where the point extrapolates.

testObservability's replacement: a cube built before the evaluation date moves follows the date in QuantLib and
recalibrates, so QuantLib's case checks that it agrees with one built after the move, within 1e-14 at its 45 points; a
nanoql cube is built for one context. So the SABR cube with every parameter free and the interpolated cube are each a
case under 16 June 2025 and under 17 June 2025, the at-the-money matrix floating on the date and the curve on it, with
their volatilities at the 45 points (the cube's option and swap tenors, at 3% plus each strike spread, not
extrapolating). The generator checks QuantLib's agreement for each pair.

The stability filter: each SABR case is refitted with every input volatility (the at-the-money matrix's and every
spread) moved one ulp up, then down, and an output is written only where both refits leave it within 1e-12 x max(|a|,
|b|, 1); the indexes of each output's dropped elements are in the case's inputs "dropped", and the generator prints the
counts per output, per case and for the file. With every parameter free the fits are ill-conditioned and the filter
drops much of what the dense cube interpolates; with beta fixed it keeps nearly all.

A refused value is {"throws": true}. A case's inputs hold its settings, its "cube" spec (swaptionvolcube.py's) and
"dropped". Read back by tests/NanoQL.Tests/Vectors/TermStructures/SabrSwaptionVolatilityCubeVectorTests.cs."""
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
    "wheel_swaptionvolcube", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "swaption", "swaptionvolcube.py"))
svc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(svc)
mat, mdl = svc.mat, svc.mdl

TODAY = svc.TODAY
TOMORROW = ql.Date(17, ql.June, 2025)
M, Y = svc.M, svc.Y

GRID_OPTIONS, GRID_SWAPS = [[1, Y], [10, Y], [30, Y]], [[2, Y], [10, Y], [30, Y]]
OFF_OPTIONS, OFF_SWAPS = [[2, Y], [5, Y], [20, Y]], [[5, Y], [15, Y], [20, Y]]
OFFSETS = svc.STRIKE_SPREADS + [-0.01, 0.01, 0.03]
# (option tenor, swap tenor, extrapolate) and (option time, swap length, extrapolate)
SECTION_TENORS = [([1, Y], [2, Y], False), ([10, Y], [10, Y], False), ([30, Y], [30, Y], False),
                  ([5, Y], [7, Y], False), ([10, Y], [3, Y], False), ([2, Y], [20, Y], False),
                  ([6, M], [1, Y], False), ([35, Y], [5, Y], True)]
SECTION_TIMES = [(0.5, 1.5, False), (3.0, 4.0, False), (15.0, 20.0, False), (40.0, 40.0, True)]
TIME_STRIKES = [0.03, 0.05, 0.07]
DUMMY_STRIKE = 0.03


def configurations():
    """(name, spec) for the 13 calibrated cubes."""
    curve = svc.flat_curve(TODAY)
    shifts = [[0.001 * (i + 2 * j) for j in range(4)] for i in range(6)]
    asymmetric = [[0.045, 0.015, 0.0, 0.010, 0.030] for _ in range(9)]
    normal = svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N)
    simplex = {"type": "Simplex", "lambda": 0.01}
    one = [[0.0] for _ in range(9)]
    alpha_free = [False, True, True, True]
    bf = svc.BETA_FIXED
    return [
        ("testSabrVols: every parameter free, at-the-money calibrated", svc.sabr_spec(svc.atm_floating(), curve)),
        ("every parameter free, not at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, atm_calibrated=False)),
        ("beta fixed, at-the-money calibrated", svc.sabr_spec(svc.atm_floating(), curve, fixed=bf)),
        ("beta fixed, not at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, atm_calibrated=False)),
        ("beta fixed, backward flat, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, backward_flat=True)),
        ("every parameter free, backward flat, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, backward_flat=True)),
        ("beta fixed, single pass, at-the-money calibrated, shifted matrix, asymmetric smile",
         svc.sabr_spec(svc.atm_floating(shifts=shifts), curve, guess=[0.05, 0.5, 0.4, 0.0], fixed=bf,
                       vol_spreads=asymmetric, single_pass=True)),
        ("beta fixed, single pass, not at-the-money calibrated, shifted matrix, asymmetric smile",
         svc.sabr_spec(svc.atm_floating(shifts=shifts), curve, guess=[0.05, 0.5, 0.4, 0.0], fixed=bf,
                       vol_spreads=asymmetric, atm_calibrated=False, single_pass=True)),
        ("beta fixed, normal matrix, at-the-money calibrated",
         svc.sabr_spec(normal, curve, guess=[0.05, 0.5, 0.4, 0.0], fixed=bf,
                       vol_spreads=svc.scaled(svc.VOL_SPREADS, 0.03))),
        ("beta fixed, Simplex, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, opt_method=simplex)),
        ("beta fixed, maximum errors, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, use_max_error=True)),
        ("beta fixed, cutoff strike 4%, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=bf, cutoff_strike=0.04)),
        ("one strike spread, alpha free, single pass, at-the-money calibrated",
         svc.sabr_spec(svc.atm_floating(), curve, fixed=alpha_free, spreads=[0.0], vol_spreads=one,
                       single_pass=True)),
    ]


def has_dense(spec):
    return spec["isAtmCalibrated"] or spec["singlePassCalibration"]


def section_values(f):
    try:
        s = f()
    except RuntimeError:
        return [dict(v.THROWS)] * 11
    atm = s.atmLevel()
    a = v.attempt
    return [s.alpha(), s.beta(), s.nu(), s.rho(), atm, s.exerciseTime(), s.shift(), int(s.volatilityType()),
            a(lambda: s.volatility(atm - 0.01)), a(lambda: s.volatility(atm)), a(lambda: s.volatility(atm + 0.01))]


def volatilities(cube, options, swaps, offsets, extrapolate):
    out = []
    for o in options:
        d = cube.optionDateFromTenor(ql.Period(*o))
        for s in swaps:
            k = cube.atmStrike(d, ql.Period(*s))
            for sp in offsets:
                out.append(v.attempt(lambda sp=sp, s=s: cube.volatility(d, ql.Period(*s), k + sp, extrapolate)))
    return out


def outputs(spec):
    cube = svc.calculated(svc.make_cube(spec), spec)
    out = {}
    matrices = [("sparseSabrParameters", cube.sparseSabrParameters())]
    if has_dense(spec):
        matrices.append(("denseSabrParameters", cube.denseSabrParameters()))
    matrices += [("marketVolCube", cube.marketVolCube()), ("volCubeAtmCalibrated", cube.volCubeAtmCalibrated())]
    out["shapes"] = [x for _, m in matrices for x in (m.rows(), m.columns())]
    for name, m in matrices:
        out[name] = svc.flat(m)
    out["volatilityGrid"] = volatilities(cube, GRID_OPTIONS, GRID_SWAPS, OFFSETS, True)
    out["volatilityOffGrid"] = volatilities(cube, OFF_OPTIONS, OFF_SWAPS, OFFSETS, True)
    out["volatilityTimeTime"] = [v.attempt(lambda t=t, ln=ln, k=k, e=e: cube.volatility(t, ln, k, e))
                                 for t, ln, e in SECTION_TIMES for k in TIME_STRIKES]
    out["sectionsPeriodPeriod"] = [x for o, s, e in SECTION_TENORS for x in section_values(
        lambda o=o, s=s, e=e: cube.smileSection(ql.Period(*o), ql.Period(*s), e))]
    out["sectionsTimeTime"] = [x for t, ln, e in SECTION_TIMES for x in section_values(
        lambda t=t, ln=ln, e=e: cube.smileSection(t, ln, e))]
    return {k: v.enc(x) for k, x in out.items()}


TOTALS = {"dropped": 0, "outputs": 0}


def sabr_case(name, spec):
    with v.case_settings(TODAY, use_at_par_coupons=True) as st:
        base = outputs(spec)
        refits = [outputs(svc.moved(spec, d)) for d in (math.inf, -math.inf)]
        kept, dropped, lost, total = svc.filtered(name, base, refits)
        TOTALS["dropped"] += lost
        TOTALS["outputs"] += total
        return {"name": name, "inputs": {"settings": st, "cube": spec, "dropped": dropped}, "expected": kept}


def observability_points(cube):
    """testObservability's 45 volatilities: the cube's option and swap tenors at 3% plus each strike spread."""
    return [v.attempt(lambda o=o, s=s, sp=sp: cube.volatility(cube.optionDateFromTenor(ql.Period(*o)), ql.Period(*s),
                                                              DUMMY_STRIKE + sp, False))
            for o in svc.CUBE_OPTIONS for s in svc.CUBE_SWAPS for sp in svc.STRIKE_SPREADS]


def observability_cases():
    """testObservability's replacement: each cube type built before and after the evaluation date moves a day."""
    out = []
    for kind, make in (("SABR cube, every parameter free", lambda c: svc.sabr_spec(svc.atm_floating(), c)),
                       ("interpolated cube", lambda c: svc.interpolated_spec(svc.atm_floating(), c))):
        # QuantLib's case: a cube built before the move and calibrated after it agrees with one built after it, on
        # the curve of the moved date (QuantLib's floats with it).
        with v.case_settings(TODAY, use_at_par_coupons=True):
            before = svc.make_cube(make(svc.flat_curve(TOMORROW)))
            ql.Settings.instance().evaluationDate = TOMORROW
            after = svc.make_cube(make(svc.flat_curve(TOMORROW)))
            v0, v1 = observability_points(before), observability_points(after)
            assert all(abs(a - b) <= 1e-14 for a, b in zip(v0, v1)), kind
        for date in (TODAY, TOMORROW):
            spec = make(svc.flat_curve(date))
            name = f"testObservability: {kind}, built on {date.ISO()}"
            with v.case_settings(date, use_at_par_coupons=True) as st:
                base = {"volatility": observability_points(svc.calculated(svc.make_cube(spec), spec))}
                dropped = {}
                if spec["type"] == "SabrSwaptionVolatilityCube":
                    refits = [{"volatility": observability_points(svc.calculated(svc.make_cube(svc.moved(spec, d)),
                                                                                 spec))}
                              for d in (math.inf, -math.inf)]
                    base, dropped, lost, total = svc.filtered(name, base, refits)
                    TOTALS["dropped"] += lost
                    TOTALS["outputs"] += total
                out.append({"name": name, "inputs": {"settings": st, "cube": spec, "dropped": dropped},
                            "expected": {k: v.enc(x) for k, x in base.items()}})
    return out


def cases():
    out = [sabr_case(*row) for row in configurations()]
    out += observability_cases()
    print(f"sabrswaptionvolatilitycube: dropped {TOTALS['dropped']} of {TOTALS['outputs']} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/sabrswaptionvolatilitycube.hpp", cases)
