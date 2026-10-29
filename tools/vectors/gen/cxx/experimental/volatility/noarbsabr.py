# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NoArbSabrModel (ql/experimental/volatility/noarbsabr.hpp, .cpp), whose members the wheel's
sections don't reach, each output computed by the C++ harness: models at and near the bounds of each parameter (sigmaI
= alpha f^(beta - 1) at 0.05 and 1, beta at 0.01 and 0.99 and in the table's extrapolation past 0.9, nu at 0.01 and 0.8,
rho at -0.99 and 0.99 and in the extrapolation past 0.75, expiries from 0.1, where the table is extrapolated flat, to
30) and inside them; each model's forward and numerical forward, its parameters and expiry, its absorption probability,
and its call price, digital price and density at strikes from a quarter of the forward to four times it; and
detail::D0Interpolator's absorption probability at each model's parameters. Then QuantLib's refusals: each parameter
past its bound, and an expiry of 0.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): every model has sigmaI sqrt(T) of 0.1 or more, where
nanoql's integrals are QuantLib's single intervals; below it nanoql splits them at the density's peak (the S-xeq run's
XER5 (xxvi)). Nothing is fitted, so no output is filtered.

The model is tools/cxx/src/experimental/volatility/noarbsabr.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Volatility/NoArbSabrModelCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
KEYS = ("expiryTime", "forward", "alpha", "beta", "nu", "rho", "strikes")
MULTIPLES = [0.25, 0.5, 0.8, 0.95, 1.0, 1.05, 1.25, 2.0, 4.0]


def model(t, forward, sigma_i, beta, nu, rho):
    """The model whose sigmaI is sigma_i: alpha = sigma_i f^(1 - beta)."""
    alpha = sigma_i * forward ** (1.0 - beta)
    return dict(zip(KEYS, (t, forward, alpha, beta, nu, rho, [forward * m for m in MULTIPLES])))


def cases():
    out = []
    setups = [
        ("inside the bounds", model(5.0, 0.03, 0.25, 0.6, 0.3, -0.2)),
        ("sigmaI at its minimum 0.05, T 5", model(5.0, 0.03, 0.05, 0.6, 0.3, -0.2)),
        ("sigmaI at its maximum 1, T 0.25", model(0.25, 0.03, 1.0, 0.5, 0.4, 0.0)),
        ("beta at its minimum 0.01", model(2.0, 0.04, 0.3, 0.01, 0.4, 0.1)),
        ("beta 0.95, the table extrapolated toward 1", model(2.0, 0.04, 0.3, 0.95, 0.4, 0.1)),
        ("beta at its maximum 0.99", model(2.0, 0.04, 0.3, 0.99, 0.4, 0.1)),
        ("nu at its minimum 0.01", model(3.0, 0.02, 0.2, 0.5, 0.01, 0.0)),
        ("nu at its maximum 0.8", model(3.0, 0.02, 0.2, 0.5, 0.8, 0.0)),
        ("rho at its minimum -0.99", model(1.0, 0.03, 0.3, 0.5, 0.5, -0.99)),
        ("rho 0.9, the table extrapolated past 0.75", model(1.0, 0.03, 0.3, 0.5, 0.5, 0.9)),
        ("rho at its maximum 0.99", model(1.0, 0.03, 0.3, 0.5, 0.5, 0.99)),
        ("T 0.1, the table extrapolated flat", model(0.1, 0.03, 0.5, 0.4, 0.4, -0.3)),
        ("T at its maximum 30", model(30.0, 0.03, 0.1, 0.7, 0.2, -0.4)),
        ("high absorption: low beta, high sigmaI, long expiry", model(10.0, 0.01, 0.8, 0.1, 0.6, -0.5)),
    ]
    with cxx.connect() as H:
        st = H.settings(R)
        for name, s in setups:
            sigma_i = s["alpha"] * s["forward"] ** (s["beta"] - 1.0)
            assert sigma_i * math.sqrt(s["expiryTime"]) >= 0.1, name
            expected = H.call("NoArbSabrModel::values", *[s[k] for k in KEYS])
            expected["d0"] = H.call("D0Interpolator::value", s["forward"], s["expiryTime"], s["alpha"], s["beta"],
                                    s["nu"], s["rho"])
            out.append({"name": name, "inputs": {"settings": st, "model": s}, "expected": expected})
        for name, s in [("T 0 (refused)", model(0.0, 0.03, 0.25, 0.6, 0.3, -0.2)),
                        ("T past 30 (refused)", model(30.5, 0.03, 0.25, 0.6, 0.3, -0.2)),
                        ("sigmaI below 0.05 (refused)", model(5.0, 0.03, 0.04, 0.6, 0.3, -0.2)),
                        ("sigmaI above 1 (refused)", model(1.0, 0.03, 1.1, 0.6, 0.3, -0.2)),
                        ("beta below 0.01 (refused)", model(1.0, 0.03, 0.3, 0.005, 0.3, -0.2)),
                        ("beta above 0.99 (refused)", model(1.0, 0.03, 0.3, 0.995, 0.3, -0.2)),
                        ("nu below 0.01 (refused)", model(1.0, 0.03, 0.3, 0.6, 0.005, -0.2)),
                        ("nu above 0.8 (refused)", model(1.0, 0.03, 0.3, 0.6, 0.85, -0.2)),
                        ("rho below -0.99 (refused)", model(1.0, 0.03, 0.3, 0.6, 0.3, -0.995)),
                        ("rho above 0.99 (refused)", model(1.0, 0.03, 0.3, 0.6, 0.3, 0.995))]:
            try:
                H.call("NoArbSabrModel::values", *[s[k] for k in KEYS])
                raise SystemExit(f"{name}: QuantLib priced it")
            except RuntimeError:
                out.append({"name": name, "inputs": {"settings": st, "model": s},
                            "expected": {"forward": {"throws": True}}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/volatility/noarbsabr.hpp", cases)
