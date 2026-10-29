# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NoArbSabrInterpolatedSmileSection and NoArbSabrInterpolation
(ql/experimental/volatility/noarbsabrinterpolatedsmilesection.hpp, .cpp, noarbsabrinterpolation.hpp), on the
evaluation date 15 October 2026: no-arbitrage SABR fits on QuantLib's interpolation case's smile (beta, nu and rho
fixed), on a rates smile with every parameter free but beta, and on floating strikes over an
at-the-money volatility: each fit's alpha, beta, nu and rho, its errors and end criterion, its at-the-money level and
strike range, and its volatilities at the strikes and between them.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every fit's trials stay at sigma_I sqrt(T) of at least
0.1, where nanoql's integrals are QuantLib's (XER5 (xxvi)). QuantLib's case with nu and rho free is left out: its
optimizer's trials pass below 0.1 whatever alpha's guess, and nanoql's split there moves the converged alpha by 4e-8,
within the optimizer's tolerance (the S-xeq run's R3-3).

**The stability filter** (the S7 plan's S7R6): every fit is refitted with every input moved one ulp up, then down, by
math.nextafter (the volatilities, the strikes, the forward, the at-the-money volatility and the guesses), together, and
each volatility, the forward and each guess alone; an output is written only where every refit leaves it within 1e-12
x max(|base|, |moved|, 1) (equal, for the end criterion), the others dropped by leaving their keys out (a volatility by
leaving its strike out of "valueStrikes"); the generator prints the dropped outputs per case and for the file.

The specs: a fit {"optionDays", "forward", "strikes", "floating", "atmVolatility", "vols", "alpha", "beta", "nu",
"rho", "fixed" [4], "vegaWeighted"}. The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i:
NoArbSabrInterpolatedSmileSection(optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, alpha, beta,
nu, rho, isAlphaFixed, isBetaFixed, isNuFixed, isRhoFixed, vegaWeighted) and its alpha(), beta(), nu(), rho(),
rmsError(), maxError(), endCriteria(), atmLevel(), minStrike(), maxStrike() and volatility(strike). Read back by
tests/NanoQL.Tests/Vectors/Experimental/Volatility/NoArbSabrInterpolatedSmileSectionVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
TOLERANCE = 1e-12
QL_STRIKES = [0.03 + 0.002 * i for i in range(31)]
QL_VOLS = [0.773729077752926, 0.763916242454194, 0.754773878663612, 0.746222305031368, 0.738193023523582,
           0.730629785825930, 0.723484825471685, 0.716716812668892, 0.710290301049393, 0.704174528906769,
           0.698342635400901, 0.692771033345972, 0.687438902593476, 0.682327777297265, 0.677421206991904,
           0.672704476238547, 0.668164371832768, 0.663788984329375, 0.659567547226380, 0.655490294349232,
           0.651548341349061, 0.647733583657137, 0.644038608699086, 0.640456620061898, 0.636981371712714,
           0.633607110719560, 0.630328527192861, 0.627140710386248, 0.624039110072250, 0.621019502453590,
           0.618077959983455]
SCALARS = ("alpha", "beta", "nu", "rho", "rmsError", "maxError")


def fit_spec(days, forward, strikes, vols, alpha, beta, nu, rho, fixed, vega_weighted=True, floating=False,
             atm=0.0):
    return {"optionDays": days, "forward": forward, "strikes": strikes, "floating": floating, "atmVolatility": atm,
            "vols": vols, "alpha": alpha, "beta": beta, "nu": nu, "rho": rho, "fixed": list(fixed),
            "vegaWeighted": vega_weighted}


def fit(spec):
    s = ql.NoArbSabrInterpolatedSmileSection(
        R + spec["optionDays"], spec["forward"], spec["strikes"], spec["floating"], spec["atmVolatility"], spec["vols"],
        spec["alpha"], spec["beta"], spec["nu"], spec["rho"], *spec["fixed"], spec["vegaWeighted"])
    return s


def outputs(s, value_strikes):
    out = {"alpha": s.alpha(), "beta": s.beta(), "nu": s.nu(), "rho": s.rho(), "rmsError": s.rmsError(),
           "maxError": s.maxError(), "endCriteria": int(s.endCriteria()), "atmLevel": s.atmLevel(),
           "minStrike": s.minStrike(), "maxStrike": s.maxStrike()}
    out["values"] = [s.volatility(k) for k in value_strikes]
    return out


NUMERIC = ("forward", "atmVolatility", "alpha", "beta", "nu", "rho", "a", "b", "sigma", "m")


def nudge(x, d):
    return x if x is None else math.nextafter(x, d)


def moved_sets(spec):
    """The refits' specs (S7R6): every input up, every one down (the volatilities, the strikes, the forward, the
    at-the-money volatility and the guesses), then each volatility, the forward and each guess alone, up and down."""
    def moved(change):
        s = dict(spec)
        s.update(change)
        return s
    sets = []
    for d in (math.inf, -math.inf):
        change = {"vols": [nudge(x, d) for x in spec["vols"]], "strikes": [nudge(x, d) for x in spec["strikes"]]}
        change.update({k: nudge(spec[k], d) for k in NUMERIC if k in spec})
        sets.append(moved(change))
    for i in range(len(spec["vols"])):
        for d in (math.inf, -math.inf):
            vols = list(spec["vols"])
            vols[i] = nudge(vols[i], d)
            sets.append(moved({"vols": vols}))
    for k in NUMERIC:
        if k in spec and spec[k] is not None:
            for d in (math.inf, -math.inf):
                sets.append(moved({k: nudge(spec[k], d)}))
    return sets


def stable(a, b):
    if isinstance(a, int):
        return a == b
    return abs(a - b) <= TOLERANCE * max(abs(a), abs(b), 1.0)


def filtered(spec, value_strikes, fitter=fit, outs=outputs, moved=moved_sets):
    """The base fit's outputs left after S7R6's refits, and the count dropped of the count written before the filter."""
    base = outs(fitter(spec), value_strikes)
    keep = {k: True for k in base if k != "values"}
    keep_value = [True] * len(value_strikes)
    for refit in moved(spec):
        o = outs(fitter(refit), value_strikes)
        for k in keep:
            keep[k] = keep[k] and stable(base[k], o[k])
        keep_value = [kv and stable(a, b) for kv, a, b in zip(keep_value, base["values"], o["values"])]
    expected = {k: base[k] for k in keep if keep[k]}
    strikes = [k for k, kv in zip(value_strikes, keep_value) if kv]
    expected["values"] = [x for x, kv in zip(base["values"], keep_value) if kv]
    total = len(keep) + len(value_strikes)
    dropped = sum(1 for k in keep if not keep[k]) + keep_value.count(False)
    return expected, strikes, dropped, total


def cases():
    st = v.settings(R)
    setups = [
        ("QuantLib's case, beta, nu and rho fixed", fit_spec(365, 0.039, QL_STRIKES, QL_VOLS, 0.2 / 0.039 ** -0.5,
                                                             0.6, 0.02, 0.01, (False, True, True, True), False)),
        ("a rates smile, beta fixed", fit_spec(730, 0.03, [0.015, 0.02, 0.025, 0.03, 0.035, 0.04, 0.05],
                                               [0.42, 0.36, 0.32, 0.30, 0.295, 0.30, 0.32], 0.05, 0.5, 0.4, -0.2,
                                               (False, True, False, False))),
        ("floating strikes", fit_spec(365, 0.03, [-0.01, -0.005, 0.0, 0.005, 0.01, 0.02],
                                      [0.06, 0.025, 0.0, -0.01, -0.012, -0.005], 0.05, 0.5, 0.4, -0.2,
                                      (False, True, False, False), floating=True, atm=0.30)),
    ]
    out = []
    dropped_all = total_all = 0
    for name, spec in setups:
        lo = spec["forward"] + (spec["strikes"][0] if spec["floating"] else 0.0)
        hi = spec["forward"] + (spec["strikes"][-1] if spec["floating"] else 0.0)
        if not spec["floating"]:
            lo, hi = spec["strikes"][0], spec["strikes"][-1]
        value_strikes = [lo + (hi - lo) * i / 8.0 for i in range(9)]
        expected, strikes, dropped, total = filtered(spec, value_strikes)
        print(f"{name}: dropped {dropped} of {total} outputs")
        dropped_all += dropped
        total_all += total
        out.append({"name": name, "inputs": {"settings": st, "fit": spec, "valueStrikes": strikes},
                    "expected": expected})
    print(f"experimental/volatility/noarbsabrinterpolatedsmilesection: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/noarbsabrinterpolatedsmilesection.hpp", cases)
