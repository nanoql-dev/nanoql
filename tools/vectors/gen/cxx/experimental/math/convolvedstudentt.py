# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CumulativeBehrensFisher and InverseCumulativeBehrensFisher
(ql/experimental/math/convolvedstudentt.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++
harness (tools/cxx/src/experimental/math/convolvedstudentt.cpp): the cumulative distribution and the density of
combinations of one to three t's of odd orders up to 171, the highest where QuantLib's factorial and power stay finite
(from 173 on its series reads NaN, which nanoql scales, the S-xcr plan's XR8 (xxvi), held in
Divergences/BehrensFisherHighOrderTests.cs), at values from -1e3 to 1e3; the inverse at probabilities from 1e-6 to
1 - 1e-6 at two accuracies, its failures past the bracket where they come; the inspectors; and the refused
combinations (sizes differing, an even order, a negative one). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/ConvolvedStudentTCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 38960
COMBINATIONS = [
    ("one t of order 1", [1], [1.0]),
    ("one t of order 5", [5], [0.8]),
    ("two t's of order 5", [5, 5], [0.4472135954999579, 0.7745966692414834]),
    ("orders 3 and 7", [3, 7], [0.6, -0.3]),
    ("orders 1, 3 and 9", [1, 3, 9], [0.2, 0.5, 0.3]),
    ("two t's of order 45", [45, 45], [0.5, 0.5]),
    ("two t's of order 99", [99, 99], [0.3, 0.9]),
    ("two t's of order 171", [171, 171], [0.6, 0.6]),
    ("one t of order 171", [171], [1.0]),
]
REFUSED = [([5, 5], [0.5]), ([4, 5], [0.5, 0.5]), ([-3, 5], [0.5, 0.5])]
XS = [-1e3, -20.0, -3.0, -1.0, -0.1, 0.0, 1e-8, 0.3, 1.0, 2.5, 7.0, 50.0, 1e3]
QS = [1e-6, 0.001, 0.05, 0.3, 0.5, 0.6, 0.95, 0.999, 1 - 1e-6]


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        for name, dfs, fs in COMBINATIONS:
            st = H.settings(R)
            out.append({"name": name,
                        "inputs": {"settings": st, "degreesFreedom": dfs, "factors": fs, "xs": XS, "qs": QS,
                                   "accuracies": [1e-6, 1e-10]},
                        "expected": {"inspect": call("CumulativeBehrensFisher::inspect", dfs, fs),
                                     "evaluate": call("CumulativeBehrensFisher::evaluate", dfs, fs, XS),
                                     "inverse": [call("InverseCumulativeBehrensFisher::evaluate", dfs, fs, a, QS)
                                                 for a in (1e-6, 1e-10)]}})
        st = H.settings(R)
        out.append({"name": "refused combinations", "inputs": {"settings": st, "refused": [[d, f] for d, f in REFUSED]},
                    "expected": {"inspect": [call("CumulativeBehrensFisher::inspect", d, f) for d, f in REFUSED]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/convolvedstudentt.hpp", cases)
