# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SviInterpolatedSmileSection (ql/experimental/volatility/sviinterpolatedsmilesection.hpp,
.cpp), on the evaluation date 15 October 2026: SVI fits on an SVI smile's own volatilities (every parameter free; m
fixed), on an equity skew and on floating strikes over an at-the-money volatility: each fit's a, b, sigma, rho and m, its
errors and end criterion, its at-the-money level and strike range, and its volatilities at the strikes and between them.

No output is left out; the fit's own departures are kept clear: no guess sits at an edge of SVI's transforms and no
parameter's check acts.

**The stability filter**, as tools/vectors/gen/experimental/volatility/noarbsabrinterpolatedsmilesection.py applies it:
refits with every input moved one ulp, together, and each volatility, the forward and each guess alone; an output is
written only where every refit leaves it within 1e-12 x max(|base|, |moved|, 1).

The specs: a fit {"optionDays", "forward", "strikes", "floating", "atmVolatility", "vols", "a", "b", "sigma", "rho",
"m", "fixed" [5], "vegaWeighted"}. The wheel's calls, by QuantLib-SWIG 1.44's volatilities.i:
SviInterpolatedSmileSection(optionDate, forward, strikes, hasFloatingStrikes, atmVolatility, vols, a, b, sigma, rho, m,
isAFixed, isBFixed, isSigmaFixed, isRhoFixed, isMFixed, vegaWeighted) and its a(), b(), sigma(), rho(), m(),
rmsError(), maxError(), endCriteria(), atmLevel(), minStrike(), maxStrike() and volatility(strike); SviSmileSection(t, forward, params) for the SVI smile.
Read back by tests/NanoQL.Tests/Vectors/Experimental/Volatility/SviInterpolatedSmileSectionVectorTests.cs."""
import importlib.util
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


nas = _load("noarbsabrinterpolatedsmilesection", "experimental/volatility/noarbsabrinterpolatedsmilesection.py")
R = nas.R
NULL = None


def fit_spec(days, forward, strikes, vols, guesses, fixed, vega_weighted=True, floating=False, atm=0.0):
    a, b, sigma, rho, m = guesses
    return {"optionDays": days, "forward": forward, "strikes": strikes, "floating": floating, "atmVolatility": atm,
            "vols": vols, "a": a, "b": b, "sigma": sigma, "rho": rho, "m": m, "fixed": list(fixed),
            "vegaWeighted": vega_weighted}


def fit(spec):
    def g(x):
        return ql.nullDouble() if x is None else x
    return ql.SviInterpolatedSmileSection(
        R + spec["optionDays"], spec["forward"], spec["strikes"], spec["floating"], spec["atmVolatility"], spec["vols"],
        g(spec["a"]), g(spec["b"]), g(spec["sigma"]), g(spec["rho"]), g(spec["m"]), *spec["fixed"],
        spec["vegaWeighted"])


def outputs(s, value_strikes):
    out = {"a": s.a(), "b": s.b(), "sigma": s.sigma(), "rho": s.rho(), "m": s.m(), "rmsError": s.rmsError(),
           "maxError": s.maxError(), "endCriteria": int(s.endCriteria()), "atmLevel": s.atmLevel(),
           "minStrike": s.minStrike(), "maxStrike": s.maxStrike()}
    out["values"] = [s.volatility(k) for k in value_strikes]
    return out


def cases():
    st = v.settings(R)
    svi = ql.SviSmileSection(1.0, 100.0, [0.02, 0.15, 0.3, -0.5, 0.05])
    sv_strikes = [60.0, 75.0, 90.0, 100.0, 110.0, 125.0, 150.0]
    sv_vols = [svi.volatility(k) for k in sv_strikes]
    free = (False,) * 5
    setups = [
        ("an SVI smile, every parameter free", fit_spec(365, 100.0, sv_strikes, sv_vols, (None,) * 5, free)),
        ("an SVI smile, m fixed", fit_spec(365, 100.0, sv_strikes, sv_vols, (None, None, None, None, 0.05),
                                           (False, False, False, False, True))),
        ("an equity skew", fit_spec(365, 100.0, [70.0, 80.0, 90.0, 100.0, 110.0, 120.0, 130.0],
                                    [0.32, 0.28, 0.245, 0.22, 0.205, 0.198, 0.197], (None,) * 5, free)),
        ("floating strikes", fit_spec(365, 100.0, [-30.0, -20.0, -10.0, 0.0, 10.0, 20.0, 30.0],
                                      [0.10, 0.06, 0.025, 0.0, -0.015, -0.022, -0.023], (None,) * 5, free,
                                      floating=True, atm=0.22)),
    ]
    out = []
    dropped_all = total_all = 0
    for name, spec in setups:
        lo = spec["strikes"][0] + (spec["forward"] if spec["floating"] else 0.0)
        hi = spec["strikes"][-1] + (spec["forward"] if spec["floating"] else 0.0)
        value_strikes = [lo + (hi - lo) * i / 8.0 for i in range(9)]
        expected, strikes, dropped, total = nas.filtered(spec, value_strikes, fit, outputs)
        print(f"{name}: dropped {dropped} of {total} outputs")
        dropped_all += dropped
        total_all += total
        out.append({"name": name, "inputs": {"settings": st, "fit": spec, "valueStrikes": strikes},
                    "expected": expected})
    print(f"experimental/volatility/sviinterpolatedsmilesection: dropped {dropped_all} of {total_all} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/sviinterpolatedsmilesection.hpp", cases)
