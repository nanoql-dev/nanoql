# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's bivariate cumulative normal distributions (ql/math/distributions/bivariatenormaldistribution.hpp,
.cpp), on the evaluation date 15 October 2026, which nothing reads: Drezner's 1978 algorithm and West's 2004 one at
every pair of bounds in {-5, -2, -1, -0.3, 0, 0.3, 1, 2, 5} and correlations from -0.99 to 0.99, and the refusals of a
correlation outside [-1, 1]. Correlations of ±1 and infinite bounds are left out: at ±1 Drezner's takes QuantLib's fix
after v1.44 (#2880, tests/NanoQL.Tests/Divergences/BivariateDr78DegenerateCorrelationsTests.cs), and at an infinite
bound West's takes its limit (BivariateInfiniteArgumentTests.cs). #2880's hypot forms move the last bits of Drezner's
recursion where a·b·ρ > 0. The wheel's calls, by QuantLib-SWIG 1.44's distributions.i:
BivariateCumulativeNormalDistributionDr78(rho) and BivariateCumulativeNormalDistributionWe04DP(rho), their __call__ on
(a, b). Read back by tests/NanoQL.Tests/Vectors/Numerics/Distributions/BivariateNormalDistributionVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

BOUNDS = [-5.0, -2.0, -1.0, -0.3, 0.0, 0.3, 1.0, 2.0, 5.0]
RHOS = [-0.99, -0.9, -0.6, -0.3, 0.0, 0.3, 0.6, 0.9, 0.99]
CLASSES = {"Dr78": ql.BivariateCumulativeNormalDistributionDr78, "We04DP": ql.BivariateCumulativeNormalDistributionWe04DP}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for kind, cls in CLASSES.items():
        for rho in RHOS:
            f = cls(rho)
            values = [f(a, b) for a in BOUNDS for b in BOUNDS]
            out.append({"name": f"{kind}, rho {rho}", "inputs": {"settings": st, "kind": kind, "rho": rho, "bounds": BOUNDS},
                        "expected": {"values": v.enc(values)}})
        for rho in (-1.5, 1.5):
            out.append({"name": f"{kind}, rho {rho} (refused)",
                        "inputs": {"settings": st, "kind": kind, "rho": rho, "bounds": BOUNDS},
                        "expected": {"values": v.attempt(lambda: cls(rho))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/math/distributions/bivariatenormaldistribution.hpp", cases)
