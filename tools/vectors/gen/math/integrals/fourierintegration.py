# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for FourierIntegration (ql/math/integrals/fourierintegration.hpp, .cpp) as far as the wheel exposes it:
andersenPiterbargIntegrationLimit over decay rates, tolerances, initial variances and maturities, a zero initial
variance among them, whose second root can't be bracketed and which falls back to the first; isAdaptiveIntegration of
each factory nanoql ports (the quadratures' at four orders, Gauss-Lobatto, Simpson, the trapezoid and Gauss-Kronrod);
numberOfEvaluations of the quadratures, their order; and gaussLaguerre's refusal of an order above 192. The wheel
doesn't expose calculate, which the Heston engine's vectors reach (tools/vectors/gen/pricingengines/vanilla/
analytichestonengine.py), nor an integrator's count before a call, which QuantLib leaves uninitialized. The wheel's
class is a SWIG wrapper of the C++ class, with no logic of its own. Read back by
tests/NanoQL.Tests/Vectors/Numerics/Integrals/FourierIntegrationVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

C_INFS = [0.1, 0.5, 1.0, 5.0]
EPSILONS = [1e-4, 1e-8, 1e-12]
V0S = [0.0, 0.01, 0.04, 0.25]
TIMES = [0.1, 1.0, 5.0]
ORDERS = [1, 64, 128, 192]
QUADRATURES = {"gaussLaguerre": ql.FourierIntegration.gaussLaguerre,
               "gaussLegendre": ql.FourierIntegration.gaussLegendre,
               "gaussChebyshev": ql.FourierIntegration.gaussChebyshev,
               "gaussChebyshev2nd": ql.FourierIntegration.gaussChebyshev2nd}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for c_inf in C_INFS:
        rows = [(eps, v0, t) for eps in EPSILONS for v0 in V0S for t in TIMES]
        out.append({"name": f"Andersen-Piterbarg limit, c_inf {c_inf!r}",
                    "inputs": {"settings": st, "c_inf": c_inf,
                               "limits": [{"epsilon": eps, "v0": v0, "t": t} for eps, v0, t in rows]},
                    "expected": {"limit": [v.attempt(
                        lambda eps=eps, v0=v0, t=t: ql.FourierIntegration.andersenPiterbargIntegrationLimit(
                            c_inf, eps, v0, t)) for eps, v0, t in rows]}})
    for name, factory in QUADRATURES.items():
        for n in ORDERS:
            f = factory(n)
            out.append({"name": f"{name}({n})", "inputs": {"settings": st, "factory": name, "order": n},
                        "expected": {"isAdaptiveIntegration": f.isAdaptiveIntegration(),
                                     "numberOfEvaluations": f.numberOfEvaluations()}})
    adaptive = {"gaussLobatto": lambda: ql.FourierIntegration.gaussLobatto(1e-8, 1e-8),
                "simpson": lambda: ql.FourierIntegration.simpson(1e-8),
                "trapezoid": lambda: ql.FourierIntegration.trapezoid(1e-8),
                "gaussKronrod": lambda: ql.FourierIntegration.gaussKronrod(1e-8)}
    for name, factory in adaptive.items():
        out.append({"name": name, "inputs": {"settings": st, "factory": name},
                    "expected": {"isAdaptiveIntegration": factory().isAdaptiveIntegration()}})
    out.append({"name": "gaussLaguerre(193)", "inputs": {"settings": st, "factory": "gaussLaguerre", "order": 193},
                "expected": {"numberOfEvaluations": v.attempt(
                    lambda: ql.FourierIntegration.gaussLaguerre(193).numberOfEvaluations())}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/integrals/fourierintegration.hpp", cases)
