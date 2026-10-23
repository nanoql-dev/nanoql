# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for ZabrModel (ql/termstructures/volatility/zabr.hpp, .cpp), which no wheel class exposes, each output
computed by the C++ harness (docs/porting-playbook.md §9.4): the inspectors, and the lognormal, normal and local
volatilities in their scalar and vector forms, for 24 models over γ 1 (the closed form of x) and γ away from it (x
integrated by AdaptiveRungeKutta), γ 0 among them, β 0, 1 and near 1, ν 0, ρ near ±1, a zero forward and expiries from
0.05 to 30 years; then QuantLib's refusals: each constructor argument out of its range, in QuantLib's order of checks
(two bad arguments among them, refused by the first check), and the vector members' strikes that aren't strictly
ascending, or not positive where β is 1.

Each "model" case's inputs hold the model's spec ("model": {"expiryTime", "forward", "alpha", "beta", "nu", "rho",
"gamma"}), the strikes of the vector forms ("strikes", strictly ascending, negative ones among them where β is below 1)
and the scalar forms' strikes ("points": 0, a negative strike, the forward and a strike beside it, where QuantLib
refuses some for β = 1); its outputs are the inspectors (nu the model's transformed one), "lognormalVolatility",
"normalVolatility" and "localVolatility" over the strikes, and "lognormalVolatilityAt", "normalVolatilityAt" and
"localVolatilityAt" at the points, and the call prices by finite differences on the Dupire equation, "fdPrice" over the
strikes' positive ones ("fdStrikes": QuantLib's grid starts at half the first) and "fdPriceAt" at the points, each
{"throws": true} where QuantLib refuses it. Left out: fullFdPrice, the two-dimensional equation's price, whose solve
stops at a relative 1e-8 and so fails the stability filter (the S7 plan's S7R6) at every strike
tools/vectors/gen/termstructures/volatility/zabrsmilesection.py tries. A "refusal" case holds the refused call. The
vector forms aren't given an empty vector, whose first element QuantLib reads (nanoql refuses it, a documented
divergence). tests/NanoQL.Tests/Vectors/Cxx/TermStructures/ZabrCxxVectorTests.cs reads the file back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def model(t, f, alpha, beta, nu, rho, gamma):
    return {"expiryTime": t, "forward": f, "alpha": alpha, "beta": beta, "nu": nu, "rho": rho, "gamma": gamma}


MODELS = [
    model(5.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0),
    model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 0.75),
    model(2.0, 0.025, 0.03, 0.5, 0.4, 0.2, 0.5),
    model(10.0, 0.04, 0.1, 0.8, 0.5, -0.5, 1.5),
    model(1.0, 0.03, 0.25, 1.0, 0.3, -0.2, 1.0),
    model(1.0, 0.03, 0.25, 1.0, 0.3, -0.2, 0.8),
    model(1.0, 0.03, 0.25, 0.9999999999, 0.3, -0.2, 1.0),
    model(3.0, 0.02, 0.008, 0.0, 0.3, 0.1, 1.0),
    model(3.0, 0.02, 0.008, 0.0, 0.3, 0.1, 0.0),
    model(0.5, 0.05, 0.05, 0.5, 0.0, 0.0, 1.0),
    model(0.5, 0.05, 0.05, 0.5, 0.0, 0.0, 0.6),
    model(2.0, 0.035, 0.05, 0.6, 0.6, 0.95, 1.0),
    model(2.0, 0.035, 0.05, 0.6, 0.6, -0.95, 1.2),
    model(0.05, 0.03, 0.04, 0.5, 0.8, -0.4, 1.0),
    model(30.0, 0.045, 0.02, 0.4, 0.25, 0.3, 0.9),
    model(4.0, 0.01, 0.02, 0.3, 0.7, -0.6, 1.1),
    model(1.5, 0.06, 0.3, 0.9, 1.5, 0.0, 0.3),
    model(1.0, 0.0, 0.05, 0.5, 0.3, -0.2, 1.0),
    model(1.0, 0.0, 0.05, 0.5, 0.3, -0.2, 0.7),
    model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0 + 4e-15),
    model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0 + 1e-13),
    model(7.0, 0.05, 0.2, 1.0, 0.4, 0.3, 1.25),
    model(0.25, 0.02, 0.6, 0.95, 2.0, -0.7, 1.9),
    model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 3.0),
]
# (label, model spec, method, argument)
REFUSALS = [
    ("alpha zero", model(1.0, 0.03, 0.0, 0.7, 0.2, -0.3, 1.0)),
    ("alpha NaN", model(1.0, 0.03, float("nan"), 0.7, 0.2, -0.3, 1.0)),
    ("beta above 1", model(1.0, 0.03, 0.08, 1.5, 0.2, -0.3, 1.0)),
    ("beta negative", model(1.0, 0.03, 0.08, -0.1, 0.2, -0.3, 1.0)),
    ("nu negative", model(1.0, 0.03, 0.08, 0.7, -0.2, -0.3, 1.0)),
    ("rho 1", model(1.0, 0.03, 0.08, 0.7, 0.2, 1.0, 1.0)),
    ("rho -1.5", model(1.0, 0.03, 0.08, 0.7, 0.2, -1.5, 1.0)),
    ("gamma negative", model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, -0.5)),
    ("gamma NaN", model(1.0, 0.03, 0.08, 0.7, 0.2, -0.3, float("nan"))),
    ("forward negative", model(1.0, -0.01, 0.08, 0.7, 0.2, -0.3, 1.0)),
    ("expiry zero", model(0.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0)),
    ("expiry negative", model(-1.0, 0.03, 0.08, 0.7, 0.2, -0.3, 1.0)),
    ("alpha and gamma bad", model(1.0, 0.03, -1.0, 0.7, 0.2, -0.3, -1.0)),
    ("forward and expiry bad", model(-1.0, -0.03, 0.08, 0.7, 0.2, -0.3, 1.0)),
]
# (label, model spec, strikes) for the vector members
STRIKE_REFUSALS = [
    ("not ascending", MODELS[0], [0.02, 0.03, 0.03, 0.04]),
    ("descending", MODELS[1], [0.04, 0.03]),
    ("NaN among them", MODELS[0], [0.02, float("nan"), 0.04]),
    ("zero first for beta 1", MODELS[4], [0.0, 0.03]),
    ("negative first for beta 1", MODELS[5], [-0.01, 0.03]),
]


def strikes(m):
    """Strictly ascending strikes around the forward: negative ones where β is below 1, the forward among them."""
    f = m["forward"] if m["forward"] > 0.0 else 0.02
    ks = [0.1 * f, 0.25 * f, 0.5 * f, 0.75 * f, 0.9 * f, m["forward"], 1.1 * f, 1.5 * f, 2.0 * f, 4.0 * f]
    if m["forward"] == 0.0:
        ks = [0.0] + [k for k in ks if k > 0.0]
    if m["beta"] < 1.0:
        ks = [-0.02, -0.005] + [k for k in ks if k > 0.0 or m["forward"] == 0.0]
    return ks


def points(m):
    f = m["forward"]
    return [0.0, -0.01, f, f * (1.0 + 1e-13) if f > 0.0 else 1e-13, 3.0 * f + 0.01]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 10, 2026))

        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        for k, m in enumerate(MODELS):
            ks, ps = strikes(m), points(m)
            inspectors = H.call("ZabrModel::ZabrModel", m)
            expected = {name: inspectors[name] for name in
                        ("forward", "expiryTime", "alpha", "beta", "nu", "rho", "gamma")}
            fd_ks = [k for k in ks if k > 0.0]
            for kind in ("lognormalVolatility", "normalVolatility", "localVolatility", "fdPrice"):
                expected[kind] = call(f"ZabrModel::{kind}(vector)", m, fd_ks if kind == "fdPrice" else ks)
                expected[kind + "At"] = [call(f"ZabrModel::{kind}(Real)", m, p) for p in ps]
            out.append({"name": f"model {k} / {m}",
                        "inputs": {"settings": st, "kind": "model", "model": v.enc(m), "strikes": ks,
                                   "fdStrikes": fd_ks, "points": v.enc(ps)},
                        "expected": v.enc(expected)})
        for label, m in REFUSALS:
            out.append({"name": f"refused / {label}",
                        "inputs": {"settings": st, "kind": "refusal", "model": v.enc(m)},
                        "expected": {"model": call("ZabrModel::ZabrModel", m)}})
        for label, m, ks in STRIKE_REFUSALS:
            out.append({"name": f"refused strikes / {label}",
                        "inputs": {"settings": st, "kind": "strikes", "model": v.enc(m), "strikes": v.enc(ks)},
                        "expected": {kind: call(f"ZabrModel::{kind}(vector)", m, ks)
                                     for kind in ("lognormalVolatility", "normalVolatility", "localVolatility")}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/zabr.hpp", cases)
