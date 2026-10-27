# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Garch11 (ql/models/volatility/garch.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness: the volatility series, the calibrations' four modes and the statics.

Returns simulated by GARCH(1,1) on Python's Mersenne twister (three models, 1,000 returns each, and short series of 10
and 50): each long series calibrated by every mode, its log-likelihood, and from a guess by QuantLib's Simplex, its
alpha, beta, omega and log-likelihood; each short series' volatility series at three parameter sets, its squared
returns and their mean, and its cost; forecasts. The modes' alpha, beta and omega are generated and left out: their
initial guesses fit the autocorrelations by least squares on the conjugate gradient, whose returned point is the last
one its line search accepted (Divergences/LineSearchReturnTests.cs, the S-sr plan's SSR6 (xvii)), so the Simplex
starts a hair away and ends within its tolerance, 1e-8, of QuantLib's point, its cost the same. A calibration that
QuantLib's minimizer doesn't end within its iterations, which master (#2844) and nanoql refuse, isn't asked here (the
S-bar plan's BR6 (vi) is a divergence fact).

The harness unit is tools/cxx/src/models/volatility/garch.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/Volatility/Garch/Garch11CxxVectorTests.cs."""
import math
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
FIRST = 22833  # 7 July 1962
MODELS = [(0.2, 0.3, 0.4), (0.1, 0.8, 0.25), (0.05, 0.9, 1.0)]
PARAMS = [(0.2, 0.3, 0.28), (0.1, 0.8, 0.025), (0.05, 0.9, 0.05)]
MODES = ["MomentMatchingGuess", "GammaGuess", "BestOfTwo", "DoubleOptimization"]


def simulate(rng, alpha, beta, vl, n):
    """n returns of GARCH(1,1), Garch11's forecast recursion on standard normal draws, rounded to 12 digits."""
    omega = (1 - alpha - beta) * vl
    r, var, out = 0.0, 0.0, []
    for _ in range(n):
        var = omega + alpha * r * r + beta * var
        r = round(rng.gauss(0.0, 1.0) * math.sqrt(var), 12)
        out.append(r)
    return out


def cases():
    rng = random.Random(v.SEED)
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for alpha, beta, vl in MODELS:
            values = simulate(rng, alpha, beta, vl, 1000)
            label = f"1,000 returns of GARCH({alpha}, {beta}, {vl})"
            for mode in MODES:
                m = H.call("Garch11.calibrate", values, mode)
                out.append({"name": f"{label}, calibrated by {mode}",
                            "inputs": {"settings": st, "call": "calibrate", "values": values, "mode": mode},
                            "expected": {"logLikelihood": m["logLikelihood"]}})
            guess = [(1 - alpha - beta) * vl * 1.2, alpha * 0.8, beta * 0.9]
            m = H.call("Garch11.calibrateFromGuess", values, *guess)
            out.append({"name": f"{label}, calibrated from a guess",
                        "inputs": {"settings": st, "call": "calibrateFromGuess", "values": values, "guess": guess},
                        "expected": {k: m[k] for k in ("alpha", "beta", "omega", "logLikelihood")}})
        for n in (2, 10, 50):
            values = simulate(rng, 0.2, 0.3, 0.4, n)
            serials = [FIRST + k + (k // 5) * 2 for k in range(n)]
            for alpha, beta, omega in PARAMS:
                r = H.call("Garch11.calculate", alpha, beta, omega, serials, values)
                out.append({"name": f"{n} returns, the volatility series at ({alpha}, {beta}, {omega})",
                            "inputs": {"settings": st, "call": "calculate", "dates": serials, "values": values,
                                       "alpha": alpha, "beta": beta, "omega": omega},
                            "expected": {"dates": r["dates"], "values": r["values"],
                                         "cost": H.call("Garch11.costFunction", values, alpha, beta, omega)}})
            r2 = H.call("Garch11.to_r2", values)
            out.append({"name": f"{n} returns, squared", "inputs": {"settings": st, "call": "to_r2", "values": values},
                        "expected": {"r2": r2["r2"], "mean": r2["mean"]}})
        for alpha, beta, vl in MODELS:
            for r, s2 in ((0.0, 0.0), (0.03, 0.2), (-1.2, 0.9)):
                out.append({"name": f"GARCH({alpha}, {beta}, {vl}), the forecast after {r} at {s2}",
                            "inputs": {"settings": st, "call": "forecast", "alpha": alpha, "beta": beta, "vl": vl, "r": r, "sigma2": s2},
                            "expected": {"forecast": H.call("Garch11.forecast", alpha, beta, vl, r, s2)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/volatility/garch.hpp", cases)
