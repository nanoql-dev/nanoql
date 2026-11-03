# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's isotropic random walk, Levy flight distribution, Ziggurat generator and copula generators
(ql/experimental/math/isotropicrandomwalk.hpp, levyflightdistribution.hpp, zigguratrng.hpp, .cpp,
claytoncopularng.hpp, farliegumbelmorgensterncopularng.hpp, frankcopularng.hpp), which the wheel doesn't bind, each
output computed by the C++ harness (tools/cxx/src/experimental/math/randomwalksandgenerators.cpp).

- "walk" cases: the walk in one and two dimensions, its radius std::normal_distribution(0, 0.1) or the Levy flight
  distribution (x_m 0.5, alpha 1.5) on a std::mt19937 at seed 42, as MSVC 14.44 draws them, with and without an
  ellipsoid from bounds; 20 steps. From three dimensions nanoql's direction is Muller's, left out here.
- "levy" cases: the distribution's density at five points, 20 draws and its minimum.
- "ziggurat" case: 2,000 draws at seed 42 and five sequences of three from its traits' generator.
- "copula" cases: 50 pairs from each generator over a Mersenne Twister at seed 42, Clayton's and Frank's theta at or
  above 1e-2 in magnitude, where nanoql's forms are QuantLib's.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Math/RandomWalksAndGeneratorsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026
WALKS = [("normal", [0.0, 0.1]), ("levy", [0.5, 1.5])]
COPULAS = [("clayton", t) for t in (-0.5, 0.5, 2.0, 5.0, 0.05)] + [("frank", t) for t in (-5.0, -1.0, 1.0, 5.0, -0.02)] \
    + [("fgm", t) for t in (-1.0, -0.5, 0.0, 0.5, 1.0)]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for dist, p in WALKS:
            for dim, lower, upper in ((1, [], []), (2, [], []), (2, [0.0, 0.0], [1.0, 2.0])):
                args = [dist, p, dim, lower, upper, 42, 20]
                out.append({"name": f"walk: {dist}, dimension {dim}, bounds {lower} {upper}",
                            "inputs": {"settings": st, "args": args},
                            "expected": H.call("IsotropicRandomWalk::steps", *args)})
        for xm, alpha in ((0.5, 1.5), (1.0, 3.0)):
            args = [xm, alpha, [0.3, 0.5, 1.0, 2.0, 10.0], 7, 20]
            out.append({"name": f"levy: xm {xm}, alpha {alpha}", "inputs": {"settings": st, "args": args},
                        "expected": H.call("LevyFlightDistribution::values", *args)})
        args = [42, 2000, 3, 5]
        out.append({"name": "ziggurat: seed 42", "inputs": {"settings": st, "args": args},
                    "expected": H.call("ZigguratRng::draws", *args)})
        for copula, theta in COPULAS:
            args = [copula, theta, 42, 50]
            out.append({"name": f"copula: {copula}, theta {theta}", "inputs": {"settings": st, "args": args},
                        "expected": H.call("CopulaRng::draws", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/math/isotropicrandomwalk.hpp", cases)
