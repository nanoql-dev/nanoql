# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Gaussian and Student t copula policies (ql/experimental/math/gaussiancopulapolicy.hpp,
tcopulapolicy.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/math/tcopulapolicy.cpp).

Gaussian policies on one and two factors, and Student t policies on odd orders from 3 to 171 (the convolution takes odd
orders only), each policy's members: its number of factors; the cumulative and inverse cumulative distributions of
every variable, of the idiosyncratic and of each systemic factor at a sample of values and probabilities (the tails
1e-12 and 1 - 1e-12 among them); the systemic factors' density on samples; the all-factor inversion of uniform
samples; and, for the T policy, its orders and variance factors. The refused policies (a weight norm of 1 or more,
orders not one more than the factors, an order of 2 or less). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/CopulaPolicyCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960
GAUSSIAN = [("one factor", [[0.3], [0.7], [0.0]]), ("two factors", [[0.3, 0.2], [0.5, 0.1], [0.2, 0.6]])]
T = [("orders 5, 5", [[0.3], [0.5]], [5, 5]),
     ("orders 3, 7", [[0.6], [0.1], [0.4]], [3, 7]),
     ("orders 3, 5, 9", [[0.3, 0.2], [0.1, 0.5]], [3, 5, 9]),
     ("orders 45, 45", [[0.55]], [45, 45]),
     ("orders 171, 171", [[0.3], [0.8]], [171, 171]),
     ("orders 99, 3", [[0.2]], [99, 3])]
REFUSED_G = [[[0.8, 0.7]], [[1.0]]]
REFUSED_T = [([[0.3, 0.2]], [5, 5]), ([[0.3]], [2, 5]), ([[0.9, 0.5]], [5, 5, 5]), ([[0.3]], [5, 5, 5])]
VALUES = [-6.0, -2.0, -0.5, 0.0, 0.25, 1.0, 3.0]
PROBS = [1e-12, 0.001, 0.2, 0.5, 0.7, 0.999, 1 - 1e-12]


def samples(total):
    base = [-1.5, 0.3, 0.8, -0.2, 2.1, 0.0, -0.7]
    return [[base[(j + s) % len(base)] for j in range(total)] for s in range(3)]


def uniforms(total):
    base = [0.05, 0.5, 0.93, 0.31, 0.77, 0.12]
    return [[base[(j + s) % len(base)] for j in range(total)] for s in range(3)]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, w in GAUSSIAN:
            st = H.settings(R)
            total = len(w) + len(w[0])
            s, u = samples(total), uniforms(total)
            out.append({"name": "Gaussian, " + name,
                        "inputs": {"settings": st, "factorWeights": w, "values": VALUES, "probabilities": PROBS,
                                   "samples": s, "uniforms": u},
                        "expected": {"members": call("GaussianCopulaPolicy::members", w, VALUES, PROBS, s, u)}})
        for name, w, orders in T:
            st = H.settings(R)
            total = len(w) + len(w[0])
            s, u = samples(total), uniforms(total)
            out.append({"name": "Student t, " + name,
                        "inputs": {"settings": st, "factorWeights": w, "tOrders": orders, "values": VALUES,
                                   "probabilities": PROBS, "samples": s, "uniforms": u},
                        "expected": {"members": call("TCopulaPolicy::members", w, orders, VALUES, PROBS, s, u)}})
        st = H.settings(R)
        out.append({"name": "refused policies",
                    "inputs": {"settings": st, "gaussian": REFUSED_G, "t": [[w, o] for w, o in REFUSED_T]},
                    "expected": {"gaussian": [call("GaussianCopulaPolicy::members", w, [], [], [], []) for w in REFUSED_G],
                                 "t": [call("TCopulaPolicy::members", w, o, [], [], [], []) for w, o in REFUSED_T]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/tcopulapolicy.hpp", cases)
