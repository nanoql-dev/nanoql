# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SabrInterpolatedSmileSection (ql/termstructures/volatility/sabrinterpolatedsmilesection.hpp,
.cpp), which the wheel doesn't bind, each output computed by the C++ harness on sections built inside the call, their
reference date the case's evaluation date. Seven fits of a 5-year smile at a forward of 3% (and one of 0.5% with a
shift of 2%), by both constructors: every parameter free; beta fixed; nu and rho fixed; unweighted; floating strikes,
the volatilities spreads over the at-the-money one; the quote form with one volatility missing, its strike left out;
and the shifted smile. Each gives the fitted alpha, beta, nu and rho, the errors, the end criteria, the strike range,
the level and the exercise time, and at nine strikes the volatility and the variance.

**The stability filter** (the S7 plan's S7R6) runs on the outputs the fit computes, the parameters, the errors, the end
criteria and the volatilities and variances: each section is refitted with every given volatility moved one ulp up,
then down, by math.nextafter, together; then each alone, up and down; an output is written only where every refit
leaves it within 1e-12 x max(|base|, |moved|, 1) (the end criteria where every refit gives the same), and the generator
prints "dropped n of m outputs" per case. A refused value is {"throws": true}. Each case's input "section" is the spec
tools/cxx/src/termstructures/volatility/sabrinterpolatedsmilesection.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SabrInterpolatedSmileSectionCxxVectorTests.cs reads it back."""
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

RELATIVE = 1e-12
TODAY = ql.Date(15, ql.January, 2025)
OPTION = ql.Date(15, ql.January, 2030)
A365 = {"type": "Actual365Fixed"}
FITTED = ["alpha", "beta", "nu", "rho", "rmsError", "maxError", "endCriteria"]
FIXED = ["minStrike", "maxStrike", "atmLevel", "exerciseTime"]
STRIKES = [0.01, 0.015, 0.02, 0.025, 0.03, 0.035, 0.04, 0.05, 0.07]
OFFSETS = [k - 0.03 for k in STRIKES]


def smile(forward, strikes, alpha, beta, nu, rho, shift=0.0):
    """Market volatilities from SABR's formula on the wheel, a little perturbed so that the fit has work to do."""
    t = 5.0
    return [ql.sabrVolatility(k + shift, forward + shift, t, alpha, beta, nu, rho) * (1.0 + 0.002 * math.sin(7.0 * i))
            for i, k in enumerate(strikes)]


def section(vols, strikes=STRIKES, forward=0.03, floating=False, atm=0.0, params=(0.03, 0.8, 0.5, 0.0),
            fixed=(False, False, False, False), weighted=True, shift=0.0, quotes=False):
    return {"optionDate": v.enc(OPTION), "forward": forward, "strikes": strikes, "hasFloatingStrikes": floating,
            "atmVolatility": atm, "vols": vols, "alpha": params[0], "beta": params[1], "nu": params[2],
            "rho": params[3], "isAlphaFixed": fixed[0], "isBetaFixed": fixed[1], "isNuFixed": fixed[2],
            "isRhoFixed": fixed[3], "vegaWeighted": weighted, "dayCounter": A365, "shift": shift, "quotes": quotes}


def specs():
    base = smile(0.03, STRIKES, 0.035, 0.6, 0.4, -0.3)
    atm = base[4]
    missing = list(base)
    missing[2] = None
    shifted_strikes = [k - 0.025 for k in STRIKES]
    return [
        ("every parameter free", section(base)),
        ("beta fixed", section(base, params=(0.03, 0.6, 0.5, 0.0), fixed=(False, True, False, False))),
        ("nu and rho fixed", section(base, params=(0.03, 0.8, 0.4, -0.3), fixed=(False, False, True, True))),
        ("unweighted", section(base, weighted=False)),
        ("floating strikes", section([x - atm for x in base], strikes=OFFSETS, floating=True, atm=atm)),
        ("the quote form, one volatility missing", section(missing, quotes=True)),
        ("shifted by 2%", section(smile(0.005, shifted_strikes, 0.02, 0.5, 0.3, -0.2, 0.02), strikes=shifted_strikes,
                                  forward=0.005, shift=0.02)),
    ]


def within(a, b):
    if isinstance(a, int) or isinstance(b, int) or a is None or b is None or isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or (math.isnan(a) and math.isnan(b)) or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def moves(vols):
    """The given volatilities moved one ulp: all up, all down, then each alone up and down; a missing one stays."""
    out = [[None if x is None else math.nextafter(x, d) for x in vols] for d in (math.inf, -math.inf)]
    for i, x in enumerate(vols):
        if x is None:
            continue
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(x, d)
            out.append(moved)
    return out


def fitted(H, spec, ks):
    out = {name: v.attempt(lambda name=name: H.call("SabrInterpolatedSmileSection::" + name, spec)) for name in FITTED}
    for i, k in enumerate(ks):
        out[("volatility", i)] = v.attempt(lambda k=k: H.call("SabrInterpolatedSmileSection::volatility", spec, k))
        out[("variance", i)] = v.attempt(lambda k=k: H.call("SabrInterpolatedSmileSection::variance", spec, k))
    return out


def cases():
    with cxx.connect() as H:
        out = []
        dropped_all = total_all = 0
        with H.case_settings(v.enc(TODAY)) as st:
            for name, spec in specs():
                spec = v.enc(spec)
                f, s = spec["forward"], spec["shift"]
                ks = [(f + s) * m - s for m in (0.4, 0.6, 0.8, 0.95, 1.0, 1.05, 1.3, 1.8, 2.5)]
                base = fitted(H, spec, ks)
                keep = set(base)
                for moved in moves(spec["vols"]):
                    refit = fitted(H, {**spec, "vols": moved}, ks)
                    keep = {k for k in keep if within(base[k], refit[k])}
                dropped = len(base) - len(keep)
                dropped_all += dropped
                total_all += len(base)
                print(f"{name}: dropped {dropped} of {len(base)} outputs")
                expected = {n: v.attempt(lambda n=n: H.call("SabrInterpolatedSmileSection::" + n, spec)) for n in FIXED}
                expected.update({n: base[n] for n in FITTED if n in keep})
                kept = [i for i in range(len(ks)) if ("volatility", i) in keep and ("variance", i) in keep]
                expected["volatility"] = [base[("volatility", i)] for i in kept]
                expected["variance"] = [base[("variance", i)] for i in kept]
                out.append({"name": name,
                            "inputs": {"settings": st, "section": spec, "strikes": [ks[i] for i in kept]},
                            "expected": expected})
        print(f"file: dropped {dropped_all} of {total_all} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/sabrinterpolatedsmilesection.hpp", cases)
