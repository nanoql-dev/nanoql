# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CLV models (ql/experimental/models/normalclvmodel.hpp, .cpp; squarerootclvmodel.hpp, .cpp),
which the wheel doesn't bind, each output computed by the C++ harness (tools/cxx/src/experimental/models/clvmodels.cpp).

- "normal" cases: the normal kernel of QuantLib's Monte Carlo case (speed 2.3, σ 0.35, x₀ and level 100) collocated at
  six months and a year, at Lagrange orders 8 and 4, the second with pMax 0.99: the collocation points X and Y at both
  maturities, the cdf at each Y point, and the mapping at three points between and at the maturities.
- "squareRoot" cases: the square-root kernel of QuantLib's vanilla case (θ 0.06, κ 1, σ 0.2, x₀ 0.09) collocated at
  three and six months, at Lagrange orders 3, 4 and 5 with QuantLib's bounds 1e-14 and 1 − 1e-14, where QuantLib's
  double nodes are within the tolerance of the exact ones nanoql's polynomial computes: the same outputs.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Models/CLVModelsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        normal = [(8, None), (4, 0.99)]
        for order, p_max in normal:
            args = [REFERENCE, 100.0, 0.10, 0.04, 0.25, [2.3, 0.35, 100.0, 100.0], [182, 365], order, p_max, None,
                    [182, 365], [[182 / 365, 95.0], [0.75, 100.0], [1.0, 108.0]]]
            out.append({"name": f"normal: order {order}, pMax {p_max}",
                        "inputs": {"settings": st, "model": "normal", "args": args},
                        "expected": H.call("NormalCLVModel::values", *args)})
        for order in (3, 4, 5):
            args = [REFERENCE, 100.0, 0.08, 0.03, 0.3, [0.06, 1.0, 0.2, 0.09], [91, 182], order, 1 - 1e-14, 1e-14,
                    [91, 182], [[91 / 365, 0.08], [0.4, 0.1], [182 / 365, 0.12]]]
            out.append({"name": f"squareRoot: order {order}",
                        "inputs": {"settings": st, "model": "squareRoot", "args": args},
                        "expected": H.call("SquareRootCLVModel::values", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/models/normalclvmodel.hpp", cases)
