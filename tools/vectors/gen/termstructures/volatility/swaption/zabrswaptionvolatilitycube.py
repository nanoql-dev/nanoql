# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ZABR swaption volatility cube (ql/termstructures/volatility/swaption/
zabrswaptionvolatilitycube.hpp, the XabrSwaptionVolatilityCube template on SwaptionVolCubeZabrModel with the
short-maturity lognormal kernel, which the wheel binds as ZabrSwaptionVolatilityCube): 7 calibrated cubes on the
evaluation date Monday 16 June 2025, and testZabrObservability's replacement, a cube on two evaluation dates.

The cubes are QuantLib's test fixture (swaptionvolcube.py's) with the guesses of QuantLib's ZABR cases (alpha 0.2,
beta 0.5, nu 0.4, rho 0, gamma 1): testZabrVols' cube, beta and gamma fixed, at-the-money calibrated, and not;
testZabrWithNonUnitGamma's, gamma fixed at 0.75; testZabrWithFreeGamma's, gamma free; and beta and gamma fixed with
backward-flat interpolation, scored by their maximum errors, and fitted by Simplex. The wheel's cube takes no
single-pass flag, so none calibrates in a single pass.

Each cube gives one case: its four matrices (sparseSabrParameters, denseSabrParameters where the cube is at-the-money
calibrated, marketVolCube and volCubeAtmCalibrated), each as its elements row by row, and their shapes; the volatility
at 72 points on the cube's grid and 72 off it, as the SABR cube's file has them; the volatility by option time and swap
length at 4 points and 3 strikes; and the smile section (a ZabrSmileSection on the lognormal kernel, whose parameters
the wheel doesn't bind: the matrices hold them) by option and swap tenors at 8 points and by option time and swap
length at 4, each as its ATM level, exercise time, shift, volatility type and volatility at its ATM level and 1% on
either side.

The parameter matrices' rmsError and maxError columns are recomputed from each row's parameters: QuantLib's ZABR model
is a section built from a copy of the parameters, which its fit doesn't rebuild after its restarts, so where the
restarts run out, or Simplex's last trial isn't the vertex it returns, the recorded errors are the last trial's while
the parameters are the best restart's. nanoql rebuilds the section at the best parameters, so its errors are those of
QuantLib's section at the parameters it reports: a fresh ZabrShortMaturityLognormalSmileSection on the row's parameters
and forward, at the strikes the fit took (the forward plus each strike spread at or above the cutoff strike), against
the row's market volatilities, with QuantLib's error formulas and equal weights. The generator prints, per case, how
many recorded errors the recomputation changes.

testZabrObservability's replacement: as for the SABR cube, a cube built before the evaluation date moves follows the
date in QuantLib, so QuantLib's case checks that it agrees with one built after the move within 1e-14 at its 45 points;
a nanoql cube is built for one context. So testZabrVols' cube is a case under 16 June 2025 and under 17 June 2025, with
its volatilities at the 45 points, and the generator checks QuantLib's agreement.

The stability filter runs on each case as on the SABR cube's: each is refitted with every input volatility moved one
ulp up, then down, and an output is written only where both refits leave it within 1e-12 x max(|a|, |b|, 1); the
indexes of each output's dropped elements are in the case's inputs "dropped".

A refused value is {"throws": true}. A case's inputs hold its settings, its "cube" spec (swaptionvolcube.py's, of
type "ZabrSwaptionVolatilityCube") and "dropped". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/ZabrSwaptionVolatilityCubeVectorTests.cs."""
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
sabr = _load("wheel_sabrswaptionvolatilitycube", "termstructures/volatility/swaption/sabrswaptionvolatilitycube.py")
mdl = svc.mdl

TODAY, TOMORROW = svc.TODAY, sabr.TOMORROW
GUESS = [0.2, 0.5, 0.4, 0.0, 1.0]
BETA_GAMMA_FIXED = [False, True, False, False, True]
BETA_FIXED = [False, True, False, False, False]


def zabr_spec(guess=GUESS, fixed=BETA_GAMMA_FIXED, **kw):
    spec = svc.sabr_spec(svc.atm_floating(), svc.flat_curve(kw.pop("date", TODAY)), guess=guess, fixed=fixed, **kw)
    spec["type"] = "ZabrSwaptionVolatilityCube"
    return spec


def configurations():
    """(name, spec) for the 7 calibrated cubes."""
    return [
        ("testZabrVols: beta and gamma fixed, at-the-money calibrated", zabr_spec()),
        ("beta and gamma fixed, not at-the-money calibrated", zabr_spec(atm_calibrated=False)),
        ("testZabrWithNonUnitGamma: gamma fixed at 0.75, at-the-money calibrated",
         zabr_spec(guess=[0.2, 0.5, 0.4, 0.0, 0.75])),
        ("testZabrWithFreeGamma: beta fixed, gamma free, at-the-money calibrated", zabr_spec(fixed=BETA_FIXED)),
        ("beta and gamma fixed, backward flat, at-the-money calibrated", zabr_spec(backward_flat=True)),
        ("beta and gamma fixed, maximum errors, at-the-money calibrated", zabr_spec(use_max_error=True)),
        ("beta and gamma fixed, Simplex, at-the-money calibrated",
         zabr_spec(opt_method={"type": "Simplex", "lambda": 0.01})),
    ]


def make_cube(spec):
    """The ZABR cube a spec describes, under the evaluation date the caller has set; it calibrates on its first
    query."""
    assert spec["type"] == "ZabrSwaptionVolatilityCube" and not spec["singlePassCalibration"]
    h = ql.SwaptionVolatilityStructureHandle(svc.make_atm(spec["atm"]))
    criteria, method = spec["endCriteria"], spec["optMethod"]
    cube = ql.ZabrSwaptionVolatilityCube(
        h, [ql.Period(*p) for p in spec["optionTenors"]], [ql.Period(*p) for p in spec["swapTenors"]],
        list(spec["strikeSpreads"]), svc.quotes(spec["volSpreads"]), svc.make_index(spec["swapIndex"]),
        svc.make_index(spec["shortSwapIndex"]), spec["vegaWeightedSmileFit"], svc.quotes(spec["parametersGuess"]),
        list(spec["isParameterFixed"]), spec["isAtmCalibrated"],
        mdl.make_criteria(criteria) if criteria is not None else None,
        spec["maxErrorTolerance"] if spec["maxErrorTolerance"] is not None else v.NULL_REAL,
        mdl.make_method(method) if method is not None else None,
        spec["errorAccept"] if spec["errorAccept"] is not None else v.NULL_REAL,
        spec["useMaxError"], spec["maxGuesses"], spec["backwardFlat"], spec["cutoffStrike"])
    if spec["enableExtrapolation"]:
        cube.enableExtrapolation()
    return cube


def rows(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def errors(values, vols):
    """QuantLib's interpolationError() and interpolationMaxError() with equal weights 1/n (xabrinterpolation.hpp)."""
    n = len(values)
    total, worst = 0.0, -sys.float_info.max
    for x, y in zip(values, vols):
        e = x - y
        total += e * e * (1.0 / n)
        worst = max(worst, abs(x - y))
    return math.sqrt(n * total / (1.0 if n == 1 else n - 1)), worst


def recomputed(params, market, spreads, cutoff):
    """The parameter rows with their rmsError and maxError recomputed from their parameters: row layout swapLength,
    optionTime, alpha, beta, nu, rho, gamma, forward, rmsError, maxError, endCriteria; market rows swapLength,
    optionTime, then a volatility per strike spread. Returns the rows and how many errors changed."""
    out, changed = [], 0
    for row, mrow in zip(params, market):
        t, f = row[1], row[7]
        taken = [(f + s, mrow[2 + k]) for k, s in enumerate(spreads) if f + s >= cutoff]
        section = ql.ZabrShortMaturityLognormalSmileSection(t, f, row[2:7])
        rms, worst = errors([section.volatility(k) for k, _ in taken], [y for _, y in taken])
        changed += (rms != row[8]) + (worst != row[9])
        out.append(row[:8] + [rms, worst] + row[10:])
    return out, changed


TOTALS = {"dropped": 0, "outputs": 0, "changed": 0}


def section_values(f):
    try:
        s = f()
    except RuntimeError:
        return [dict(v.THROWS)] * 7
    atm = s.atmLevel()
    a = v.attempt
    return [atm, s.exerciseTime(), s.shift(), int(s.volatilityType()),
            a(lambda: s.volatility(atm - 0.01)), a(lambda: s.volatility(atm)), a(lambda: s.volatility(atm + 0.01))]


def outputs(spec, count=False):
    cube = make_cube(spec)
    cube.sparseSabrParameters()
    spreads, cutoff = spec["strikeSpreads"], spec["cutoffStrike"]
    market, calibrated = rows(cube.marketVolCube()), rows(cube.volCubeAtmCalibrated())
    sparse, changed = recomputed(rows(cube.sparseSabrParameters()), market, spreads, cutoff)
    matrices = [("sparseSabrParameters", sparse)]
    if spec["isAtmCalibrated"]:
        dense, more = recomputed(rows(cube.denseSabrParameters()), calibrated, spreads, cutoff)
        changed += more
        matrices.append(("denseSabrParameters", dense))
    matrices += [("marketVolCube", market), ("volCubeAtmCalibrated", calibrated)]
    if count:
        TOTALS["changed"] += changed
        print(f"  the recomputation changes {changed} recorded errors")
    out = {"shapes": [x for _, m in matrices for x in (len(m), len(m[0]))]}
    for name, m in matrices:
        out[name] = [x for r in m for x in r]
    out["volatilityGrid"] = sabr.volatilities(cube, sabr.GRID_OPTIONS, sabr.GRID_SWAPS, sabr.OFFSETS, True)
    out["volatilityOffGrid"] = sabr.volatilities(cube, sabr.OFF_OPTIONS, sabr.OFF_SWAPS, sabr.OFFSETS, True)
    out["volatilityTimeTime"] = [v.attempt(lambda t=t, ln=ln, k=k, e=e: cube.volatility(t, ln, k, e))
                                 for t, ln, e in sabr.SECTION_TIMES for k in sabr.TIME_STRIKES]
    out["sectionsPeriodPeriod"] = [x for o, s, e in sabr.SECTION_TENORS for x in section_values(
        lambda o=o, s=s, e=e: cube.smileSection(ql.Period(*o), ql.Period(*s), e))]
    out["sectionsTimeTime"] = [x for t, ln, e in sabr.SECTION_TIMES for x in section_values(
        lambda t=t, ln=ln, e=e: cube.smileSection(t, ln, e))]
    return {k: v.enc(x) for k, x in out.items()}


def zabr_case(name, spec):
    with v.case_settings(TODAY, use_at_par_coupons=True) as st:
        print(name)
        base = outputs(spec, count=True)
        refits = [outputs(svc.moved(spec, d)) for d in (math.inf, -math.inf)]
        kept, dropped, lost, total = svc.filtered(name, base, refits)
        TOTALS["dropped"] += lost
        TOTALS["outputs"] += total
        return {"name": name, "inputs": {"settings": st, "cube": spec, "dropped": dropped}, "expected": kept}


def points(cube):
    """testZabrObservability's 45 volatilities: the cube's option and swap tenors at 3% plus each strike spread."""
    return [v.attempt(lambda o=o, s=s, sp=sp: cube.volatility(cube.optionDateFromTenor(ql.Period(*o)), ql.Period(*s),
                                                              sabr.DUMMY_STRIKE + sp, False))
            for o in svc.CUBE_OPTIONS for s in svc.CUBE_SWAPS for sp in svc.STRIKE_SPREADS]


def observability_cases():
    # QuantLib's case: a cube built before the move and calibrated after it agrees with one built after it, on the
    # curve of the moved date.
    with v.case_settings(TODAY, use_at_par_coupons=True):
        before = make_cube(zabr_spec(date=TOMORROW))
        ql.Settings.instance().evaluationDate = TOMORROW
        after = make_cube(zabr_spec(date=TOMORROW))
        v0, v1 = points(before), points(after)
        assert all(abs(a - b) <= 1e-14 for a, b in zip(v0, v1))
    out = []
    for date in (TODAY, TOMORROW):
        spec = zabr_spec(date=date)
        name = f"testZabrObservability: testZabrVols' cube, built on {date.ISO()}"
        with v.case_settings(date, use_at_par_coupons=True) as st:
            base = {"volatility": points(make_cube(spec))}
            refits = [{"volatility": points(make_cube(svc.moved(spec, d)))} for d in (math.inf, -math.inf)]
            base, dropped, lost, total = svc.filtered(name, base, refits)
            TOTALS["dropped"] += lost
            TOTALS["outputs"] += total
            out.append({"name": name, "inputs": {"settings": st, "cube": spec, "dropped": dropped},
                        "expected": {k: v.enc(x) for k, x in base.items()}})
    return out


def cases():
    out = [zabr_case(*row) for row in configurations()]
    out += observability_cases()
    print(f"zabrswaptionvolatilitycube: dropped {TOTALS['dropped']} of {TOTALS['outputs']} outputs; the recomputation "
          f"changed {TOTALS['changed']} recorded errors")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/swaption/zabrswaptionvolatilitycube.hpp", cases)
