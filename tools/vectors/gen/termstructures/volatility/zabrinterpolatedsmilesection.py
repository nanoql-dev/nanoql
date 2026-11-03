# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ZabrInterpolatedSmileSection (ql/termstructures/volatility/zabrinterpolatedsmilesection.hpp)
through the wheel's four named classes, ZabrShortMaturityLognormalInterpolatedSmileSection,
ZabrShortMaturityNormalInterpolatedSmileSection, ZabrLocalVolatilityInterpolatedSmileSection and
ZabrFullFdInterpolatedSmileSection (QuantLib-Python's %template names), on the evaluation date 15 January 2025: fits of
a one-year smile at a forward of 3%, ZABR's own (α 0.08, β 0.7, ν 0.3, ρ −0.25, γ 1) a little perturbed. For each
short-maturity kernel: β and γ fixed; every parameter free; γ fitted with β fixed; unweighted; floating strikes, the
volatilities spreads over the at-the-money one; the quote form with one volatility missing, its strike left out; and
every parameter fixed. For the local-volatility kernel, whose fit solves the Dupire equation at each trial: β and γ
fixed, floating strikes, the quote form and every parameter fixed. For the full finite-difference kernel: every
parameter fixed. And the refusals: an option date before the evaluation date, and β fixed at 1.5. Each gives α, β, ν and
ρ, the errors and the end criteria, the exercise date and time, the reference date, the day counter, the strike range
and the level, and at nine strikes the volatility and the variance.

**The stability filter** runs on the outputs the fit computes, the parameters, the errors, the end criteria and the
volatilities and variances: each section is refitted with its forward, its at-the-money volatility and every given
volatility moved one ulp up, then down, by math.nextafter, together; then each alone, up and down; an output is written
only where every refit leaves it within 1e-12 x max(|base|, |moved|, 1) (the end criteria where every refit gives the
same), and the generator prints "dropped n of m outputs" per case. The full finite-difference kernel's errors,
volatilities and variances are left out: its prices fail the filter at every strike
(tools/vectors/gen/termstructures/volatility/zabrsmilesection.py), which moving the volatilities of a section with every
parameter fixed can't show. A refused value is {"throws": true}.

Each case's input "section" is {"kernel", "optionDate", "forward", "strikes", "hasFloatingStrikes", "atmVolatility",
"vols" (null for a missing quote), "alpha", "beta", "nu", "rho", "gamma" (null for ZABR's default guess),
"isAlphaFixed", "isBetaFixed", "isNuFixed", "isRhoFixed", "isGammaFixed", "vegaWeighted", "quotes"}. The wheel's
classes, positional, are the C++ constructors' proxies, the quote form taking a SimpleQuote without a value for a
missing quote; nanoql's named classes build the date form by Create from the context. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/ZabrInterpolatedSmileSectionVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

RELATIVE = 1e-12
TODAY = ql.Date(15, ql.January, 2025)
OPTION = ql.Date(15, ql.January, 2026)
FORWARD = 0.03
TRUE_PARAMETERS = (0.08, 0.7, 0.3, -0.25, 1.0)
KERNELS = {"ShortMaturityLognormal": ql.ZabrShortMaturityLognormalInterpolatedSmileSection,
           "ShortMaturityNormal": ql.ZabrShortMaturityNormalInterpolatedSmileSection,
           "LocalVolatility": ql.ZabrLocalVolatilityInterpolatedSmileSection,
           "FullFd": ql.ZabrFullFdInterpolatedSmileSection}
FITTED = ["alpha", "beta", "nu", "rho", "rmsError", "maxError", "endCriteria"]
FD_VALUED = ["rmsError", "maxError"]
STRIKES = [0.012, 0.018, 0.024, 0.0285, 0.03, 0.0315, 0.039, 0.054, 0.075]
OFFSETS = [k - FORWARD for k in STRIKES]
N = None


def smile():
    """The market volatilities: ZABR's own at the strikes, by its short-maturity lognormal kernel, perturbed."""
    t = ql.Actual365Fixed().yearFraction(TODAY, OPTION)
    s = ql.ZabrShortMaturityLognormalSmileSection(t, FORWARD, list(TRUE_PARAMETERS))
    return [s.volatility(k) * (1.0 + 0.002 * math.sin(7.0 * i)) for i, k in enumerate(STRIKES)]


def section(kernel, vols, strikes=STRIKES, floating=False, atm=0.0, params=(N, 0.7, N, N, 1.0),
            fixed=(False, True, False, False, True), weighted=True, quotes=False, option=OPTION):
    return {"kernel": kernel, "optionDate": v.enc(option), "forward": FORWARD, "strikes": list(strikes),
            "hasFloatingStrikes": floating, "atmVolatility": atm, "vols": list(vols), "alpha": params[0],
            "beta": params[1], "nu": params[2], "rho": params[3], "gamma": params[4], "isAlphaFixed": fixed[0],
            "isBetaFixed": fixed[1], "isNuFixed": fixed[2], "isRhoFixed": fixed[3], "isGammaFixed": fixed[4],
            "vegaWeighted": weighted, "quotes": quotes}


def specs():
    base = smile()
    atm = base[4]
    missing = list(base)
    missing[2] = None
    every_fixed = dict(params=TRUE_PARAMETERS, fixed=(True,) * 5)
    out = []
    for kernel in ("ShortMaturityLognormal", "ShortMaturityNormal"):
        out += [
            (f"{kernel}: beta and gamma fixed", section(kernel, base)),
            (f"{kernel}: every parameter free", section(kernel, base, params=(N,) * 5, fixed=(False,) * 5)),
            (f"{kernel}: gamma fitted, beta fixed",
             section(kernel, base, params=(N, 0.7, N, N, 0.9), fixed=(False, True, False, False, False))),
            (f"{kernel}: unweighted", section(kernel, base, weighted=False)),
            (f"{kernel}: floating strikes",
             section(kernel, [x - atm for x in base], strikes=OFFSETS, floating=True, atm=atm)),
            (f"{kernel}: the quote form, one volatility missing", section(kernel, missing, quotes=True)),
            (f"{kernel}: every parameter fixed", section(kernel, base, **every_fixed)),
        ]
    kernel = "LocalVolatility"
    out += [
        (f"{kernel}: beta and gamma fixed", section(kernel, base)),
        (f"{kernel}: floating strikes", section(kernel, [x - atm for x in base], strikes=OFFSETS, floating=True, atm=atm)),
        (f"{kernel}: the quote form, one volatility missing", section(kernel, missing, quotes=True)),
        (f"{kernel}: every parameter fixed", section(kernel, base, **every_fixed)),
        ("FullFd: every parameter fixed", section("FullFd", base, **every_fixed)),
    ]
    return out


def refusals():
    base = smile()
    return [
        ("refused: an option date before the evaluation date",
         section("ShortMaturityLognormal", base, option=ql.Date(14, ql.January, 2025))),
        ("refused: beta fixed at 1.5", section("ShortMaturityLognormal", base, params=(N, 1.5, N, N, 1.0))),
    ]


def make_section(spec):
    """The section a spec describes, built with the wheel under the evaluation date the caller has set."""
    cls = KERNELS[spec["kernel"]]
    params = [ql.nullDouble() if spec[p] is None else spec[p] for p in ("alpha", "beta", "nu", "rho", "gamma")]
    flags = [spec[f] for f in ("isAlphaFixed", "isBetaFixed", "isNuFixed", "isRhoFixed", "isGammaFixed",
                               "vegaWeighted")]
    d = ql.Date(spec["optionDate"])
    if spec["quotes"]:
        quotes = [ql.QuoteHandle(ql.SimpleQuote(ql.nullDouble() if x is None else x)) for x in spec["vols"]]
        return cls(d, ql.QuoteHandle(ql.SimpleQuote(spec["forward"])), spec["strikes"], spec["hasFloatingStrikes"],
                   ql.QuoteHandle(ql.SimpleQuote(spec["atmVolatility"])), quotes, *params, *flags)
    return cls(d, spec["forward"], spec["strikes"], spec["hasFloatingStrikes"], spec["atmVolatility"], spec["vols"],
               *params, *flags)


def within(a, b):
    if isinstance(a, int) or isinstance(b, int) or a is None or b is None or isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or (math.isnan(a) and math.isnan(b)) or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def moves(spec):
    """The spec with its forward, at-the-money volatility and given volatilities moved one ulp: all up, all down, then
    each alone up and down; a missing volatility stays."""
    def move(x, d):
        return None if x is None else math.nextafter(x, d)

    out = []
    for d in (math.inf, -math.inf):
        out.append({**spec, "forward": move(spec["forward"], d), "atmVolatility": move(spec["atmVolatility"], d),
                    "vols": [move(x, d) for x in spec["vols"]]})
    for d in (math.inf, -math.inf):
        out.append({**spec, "forward": move(spec["forward"], d)})
        out.append({**spec, "atmVolatility": move(spec["atmVolatility"], d)})
        for i, x in enumerate(spec["vols"]):
            if x is not None:
                moved = list(spec["vols"])
                moved[i] = move(x, d)
                out.append({**spec, "vols": moved})
    return out


def fitted(spec, ks):
    s = make_section(spec)
    out = {"alpha": v.attempt(s.alpha), "beta": v.attempt(s.beta), "nu": v.attempt(s.nu), "rho": v.attempt(s.rho),
           "rmsError": v.attempt(s.rmsError), "maxError": v.attempt(s.maxError),
           "endCriteria": v.attempt(lambda: int(s.endCriteria()))}
    for i, k in enumerate(ks):
        out[("volatility", i)] = v.attempt(lambda k=k: s.volatility(k))
        out[("variance", i)] = v.attempt(lambda k=k: s.variance(k))
    return out


def inspectors(spec):
    s = make_section(spec)
    dc = s.dayCounter()
    return {"exerciseDate": v.attempt(lambda: v.enc(s.exerciseDate())),
            "exerciseTime": v.attempt(s.exerciseTime),
            "referenceDate": v.attempt(lambda: v.enc(s.referenceDate())),
            "dayCounter": None if dc.empty() else dc.name(),
            "minStrike": v.attempt(s.minStrike),
            "maxStrike": v.attempt(s.maxStrike),
            "atmLevel": v.attempt(s.atmLevel)}


def section_case(name, spec):
    ks = [FORWARD * m for m in (0.4, 0.6, 0.8, 0.95, 1.0, 1.05, 1.3, 1.8, 2.5)]
    with v.case_settings(TODAY) as st:
        base = fitted(spec, ks)
        if spec["kernel"] == "FullFd":
            keep = {k for k in base if k not in FD_VALUED and not isinstance(k, tuple)}
        else:
            keep = set(base)
            for moved in moves(spec):
                refit = fitted(moved, ks)
                keep = {k for k in keep if within(base[k], refit[k])}
        print(f"{name}: dropped {len(base) - len(keep)} of {len(base)} outputs")
        expected = inspectors(spec)
        expected.update({n: base[n] for n in FITTED if n in keep})
        kept = [i for i in range(len(ks)) if ("volatility", i) in keep and ("variance", i) in keep]
        expected["volatility"] = [base[("volatility", i)] for i in kept]
        expected["variance"] = [base[("variance", i)] for i in kept]
        return {"name": name, "inputs": {"settings": st, "kind": "section", "section": v.enc(spec),
                                         "strikes": [ks[i] for i in kept]},
                "expected": expected}


def refusal_case(name, spec):
    with v.case_settings(TODAY) as st:
        return {"name": name, "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
                "expected": {"section": v.attempt(lambda: make_section(spec).alpha())}}


def cases():
    return [section_case(n, s) for n, s in specs()] + [refusal_case(n, s) for n, s in refusals()]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/zabrinterpolatedsmilesection.hpp", cases)
