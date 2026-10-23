# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdInterpolation (ql/math/interpolations/abcdinterpolation.hpp) and AbcdSquared
(ql/termstructures/volatility/abcd.hpp), which the wheel doesn't bind, each output computed by the C++ harness on
objects built inside the call. The interpolations fit
tools/vectors/gen/cxx/termstructures/volatility/abcdcalibration.py's term structures: set 0 with every guess null
(QuantLib's Null<Real>(), its default guess, free); set 1 with the default guesses given; set 4 with c fixed; set 1 with
b and d fixed; and set 5 vega weighted. Each gives the fitted a, b, c and d, the adjustment factors, the errors and the
end criteria, the interpolated volatility at six times (extrapolating past the times) and k(t) at five within them. Then
the interpolator factory Abcd's traits, global and requiredPoints; and the refusals: a negative time, one point, a guess
with c negative, and set 2, whose fit QuantLib refuses. And AbcdSquared, the instantaneous covariance of two abcd rates
fixing at T and S, for two parameter sets and three (T, S) pairs at four times.

**The stability filter** (the S7 plan's S7R6) runs on the fits' outputs, as abcdcalibration.py's does: each fit is rerun
with every volatility moved one ulp up, then down, together, then each alone; an output is written only where every
refit leaves it within 1e-12 x max(|base|, |moved|, 1), and the generator prints "dropped n of m outputs" per case. Left
out, a documented divergence: a fixed with d free (tests/NanoQL.Tests/Divergences/AbcdCalibrationFixedATests.cs). A
refused value is {"throws": true}. Each case's input "fit" is the spec
tools/cxx/src/termstructures/volatility/abcdspec.hpp reads;
tests/NanoQL.Tests/Vectors/Cxx/Numerics/Interpolations/AbcdInterpolationCxxVectorTests.cs reads it back."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cal = _load("harness_abcdcalibration", "cxx/termstructures/volatility/abcdcalibration.py")
SETS, fit = cal.SETS, cal.fit
TODAY = cal.TODAY
POINTS = [0.0, 0.25, 1.0, 4.0, 12.0, 30.0]
K_POINTS = [0.5, 0.75, 4.0, 12.0, 20.0]
FITTED = ["a", "b", "c", "d", "k", "rmsError", "maxError", "endCriteria", "value", "kAt"]
N = None
SQUARED = [(-0.06, 0.17, 0.54, 0.17), (0.1, 0.5, 1.2, 0.15)]
FIXINGS = [(2.0, 3.0), (5.0, 5.0), (1.0, 10.0)]
SQUARED_TIMES = [0.0, 0.5, 0.9, 1.9]


def specs():
    return [
        ("set 0, every guess null", fit(SETS[0], [N, N, N, N])),
        ("set 1, the default guesses given", fit(SETS[1])),
        ("set 4, c fixed", fit(SETS[4], fixed=(False, False, True, False))),
        ("set 1, b and d fixed", fit(SETS[1], fixed=(False, True, False, True))),
        ("set 5, vega weighted", fit(SETS[5], weighted=True)),
    ]


def cases():
    with cxx.connect() as H:
        out = []
        dropped_all = total_all = 0
        st = H.settings(v.enc(TODAY))
        for name, spec in specs():
            base = H.call("AbcdInterpolation", spec, POINTS, K_POINTS)
            keep = set(FITTED)
            for moved in cal.moves(spec["blackVols"]):
                refit = H.call("AbcdInterpolation", {**spec, "blackVols": moved}, POINTS, K_POINTS)
                keep = {k for k in keep if cal.within(base[k], refit[k])}
            dropped = len(FITTED) - len(keep)
            dropped_all += dropped
            total_all += len(FITTED)
            print(f"{name}: dropped {dropped} of {len(FITTED)} outputs")
            out.append({"name": name,
                        "inputs": {"settings": st, "kind": "interpolation", "fit": spec, "points": POINTS,
                                   "kPoints": K_POINTS},
                        "expected": {k: base[k] for k in FITTED if k in keep}})
        out.append({"name": "the factory's traits", "inputs": {"settings": st, "kind": "traits"},
                    "expected": H.call("Abcd::traits")})
        one_point = {**fit(SETS[1]), "t": [0.5], "blackVols": [0.1]}
        for name, spec, x in (("refused: a negative time", fit(SETS[1]), -1.0),
                              ("refused: one point", one_point, 1.0),
                              ("refused: c negative", fit(SETS[1], [-0.06, 0.17, -0.1, 0.17]), 1.0),
                              ("refused: set 2", fit(SETS[2]), 1.0)):
            out.append({"name": name, "inputs": {"settings": st, "kind": "refusal", "fit": spec, "x": x},
                        "expected": {"value": v.attempt(lambda s=spec, x=x: H.call("AbcdInterpolation::value", s, x))}})
        for p in SQUARED:
            for t, s in FIXINGS:
                out.append({"name": f"AbcdSquared {p}, T {t}, S {s}",
                            "inputs": {"settings": st, "kind": "squared", "parameters": list(p), "T": t, "S": s,
                                       "times": SQUARED_TIMES},
                            "expected": {"value": H.call("AbcdSquared", *p, t, s, SQUARED_TIMES)}})
        print(f"file: dropped {dropped_all} of {total_all} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/interpolations/abcdinterpolation.hpp", cases)
