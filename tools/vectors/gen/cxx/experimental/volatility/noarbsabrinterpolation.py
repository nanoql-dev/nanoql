# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NoArbSabrInterpolation (ql/experimental/volatility/noarbsabrinterpolation.hpp), which the
wheel doesn't expose (its interpolated section is tools/vectors/gen/experimental/volatility/
noarbsabrinterpolatedsmilesection.py's), each output computed by the C++ harness: fits at expiries of 5 and 10 years on
a no-arbitrage SABR smile's own volatilities (every parameter free, beta fixed, rho fixed, unweighted, scored by the
maximum error) and on a market skew (QuantLib's default acceptance, and an acceptance of 1e-6 that forces the Halton
restarts, 5 of them); each fit's expiry, forward, alpha, beta, nu and rho, its errors and end criterion, its weights,
and its values at the strikes, between them and outside them (allowExtrapolation set); and the NoArbSabr factory's
interpolation on the smile. Then QuantLib's refusals: an expiry past 30 years, and fewer volatilities than strikes
(the harness's, as QuantLib reads them unchecked) left out.

The outputs nanoql fixes are left out: every expiry is 5 years or more, so every trial point's sigmaI sqrt(T) is above
0.1 (the transformation keeps sigmaI in [0.05, 1]), where nanoql's integrals are QuantLib's single intervals; nanoql's
split at the peak reaches the fits' trial points below it.

**The stability filter**: every fit is refitted with every input volatility moved one ulp up, then down, by
math.nextafter, together and each alone; a scalar output is written only where every refit leaves it within 1e-12 x
max(|base|, |moved|, 1) (an end criterion where every refit gives the same), an array's element likewise, the indexes
of each array's dropped elements in the case's inputs "dropped"; the generator prints the counts per case and for the
file. Expiry, forward and the weights, which no fit computes, aren't filtered.

The specs are tools/cxx/src/experimental/volatility/noarbsabrinterpolation.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/NoArbSabrInterpolationCxxVectorTests.cs."""
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

R = 46310
TOLERANCE = 1e-12
UNFILTERED = ("expiry", "forward", "interpolationWeights")


def interp(x, y, t, forward, guesses=(None,) * 4, fixed=(False,) * 4, vega=True, accept=0.002, use_max=False,
           guesses_max=50):
    a, b, n, r = guesses
    return {"x": x, "y": y, "t": t, "forward": forward, "alpha": a, "beta": b, "nu": n, "rho": r,
            "alphaIsFixed": fixed[0], "betaIsFixed": fixed[1], "nuIsFixed": fixed[2], "rhoIsFixed": fixed[3],
            "vegaWeighted": vega, "errorAccept": accept, "useMaxError": use_max, "maxGuesses": guesses_max}


def points(x):
    lo, hi = x[0], x[-1]
    return [lo * 0.8] + [lo + (hi - lo) * i / 8.0 for i in range(9)] + [hi * 1.2]


def stable(a, b):
    if isinstance(a, int) or isinstance(b, int):
        return a == b
    return abs(a - b) <= TOLERANCE * max(abs(a), abs(b), 1.0)


def moved(s):
    out = []
    for d in (math.inf, -math.inf):
        m = dict(s)
        m["y"] = [math.nextafter(y, d) for y in s["y"]]
        out.append(m)
        for i in range(len(s["y"])):
            m = dict(s)
            m["y"] = list(s["y"])
            m["y"][i] = math.nextafter(s["y"][i], d)
            out.append(m)
    return out


def filtered(base, refits):
    expected, dropped, lost, total = {}, {}, 0, 0
    for k, x in base.items():
        if isinstance(x, list):
            out = [] if k in UNFILTERED else [i for i, e in enumerate(x)
                                              if not all(stable(e, r[k][i]) for r in refits)]
            if out:
                dropped[k] = out
            expected[k] = [e for i, e in enumerate(x) if i not in set(out)]
            lost += len(out)
            total += len(x)
        else:
            if k in UNFILTERED or all(stable(x, r[k]) for r in refits):
                expected[k] = x
            else:
                lost += 1
            total += 1
    return expected, dropped, lost, total


def cases():
    out = []
    f = 0.03
    smile_x = [f * m for m in (0.5, 0.7, 0.85, 1.0, 1.2, 1.5, 2.0)]
    section = ql.NoArbSabrSmileSection(5.0, f, [0.3 * f ** 0.4, 0.6, 0.35, -0.25])
    smile_y = [section.volatility(k) for k in smile_x]
    long_section = ql.NoArbSabrSmileSection(10.0, f, [0.2 * f ** 0.5, 0.5, 0.25, 0.1])
    long_y = [long_section.volatility(k) for k in smile_x]
    skew_x = [0.01, 0.02, 0.025, 0.03, 0.035, 0.04, 0.06]
    skew_y = [0.45, 0.33, 0.30, 0.28, 0.27, 0.265, 0.27]
    setups = [
        ("a no-arbitrage smile at 5 years, every parameter free", interp(smile_x, smile_y, 5.0, f)),
        ("a no-arbitrage smile at 5 years, beta fixed at 0.6",
         interp(smile_x, smile_y, 5.0, f, (None, 0.6, None, None), (False, True, False, False))),
        ("a no-arbitrage smile at 10 years, rho fixed at 0.1",
         interp(smile_x, long_y, 10.0, f, (None, None, None, 0.1), (False, False, False, True))),
        ("a no-arbitrage smile at 10 years, unweighted", interp(smile_x, long_y, 10.0, f, vega=False)),
        ("a no-arbitrage smile at 5 years, the maximum error", interp(smile_x, smile_y, 5.0, f, use_max=True)),
        ("a market skew at 5 years, beta fixed at 0.5",
         interp(skew_x, skew_y, 5.0, f, (None, 0.5, None, None), (False, True, False, False))),
        ("a market skew at 5 years, acceptance 1e-6, 5 restarts",
         interp(skew_x, skew_y, 5.0, f, (None, 0.5, None, None), (False, True, False, False), accept=1e-6,
                guesses_max=5)),
    ]
    dropped_all = total_all = 0
    with cxx.connect() as H:
        st = H.settings(R)
        for name, s in setups:
            pts = points(s["x"])
            base = H.call("NoArbSabrInterpolation::values", s, pts)
            refits = [H.call("NoArbSabrInterpolation::values", m, pts) for m in moved(s)]
            expected, dropped, lost, total = filtered(base, refits)
            print(f"{name}: dropped {lost} of {total} outputs")
            dropped_all += lost
            total_all += total
            out.append({"name": name, "inputs": {"settings": st, "interpolation": s, "points": pts,
                                                 "dropped": dropped}, "expected": expected})
        s = interp(smile_x, smile_y, 5.0, f, (None, 0.6, None, None), (False, True, False, False), vega=False)
        pts = points(s["x"])
        base = {"values": H.call("NoArbSabr::interpolate", s, pts)}
        refits = [{"values": H.call("NoArbSabr::interpolate", m, pts)} for m in moved(s)]
        expected, dropped, lost, total = filtered(base, refits)
        name = "the factory: the smile at 5 years, beta fixed, unweighted"
        print(f"{name}: dropped {lost} of {total} outputs")
        dropped_all += lost
        total_all += total
        out.append({"name": name, "inputs": {"settings": st, "factory": s, "points": pts, "dropped": dropped},
                    "expected": expected})
        s = interp(smile_x, smile_y, 31.0, f)
        try:
            H.call("NoArbSabrInterpolation::values", s, [f])
            raise SystemExit("an expiry past 30 years: QuantLib priced it")
        except RuntimeError:
            out.append({"name": "an expiry past 30 years (refused)",
                        "inputs": {"settings": st, "interpolation": s, "points": [f], "dropped": {}},
                        "expected": {"alpha": {"throws": True}}})
        print(f"cxx/experimental/volatility/noarbsabrinterpolation: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/noarbsabrinterpolation.hpp", cases)
