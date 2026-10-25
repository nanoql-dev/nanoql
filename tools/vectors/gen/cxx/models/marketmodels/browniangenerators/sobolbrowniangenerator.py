# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SobolBrownianGeneratorBase's orderedIndices and transform
(ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp, .cpp), which the wheel doesn't bind, each output
computed by the C++ harness (docs/porting-playbook.md §9.4): the ordering's table of variate indices, and the
Brownian bridge's transform of given variates.

Each ordering (factors, steps, diagonal) on four (factors, steps) sets, one factor and eight steps, two factors and
five steps, three factors and four steps, four factors and two steps: the table, and the transform of seeded variates
over three paths.

The harness's calls are tools/cxx/src/models/marketmodels/browniangenerators/sobolbrowniangenerator.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SobolBrownianGeneratorCxxVectorTests.cs."""
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
ORDERINGS = [("factors", 0), ("steps", 1), ("diagonal", 2)]
SIZES = [(1, 8), (2, 5), (3, 4), (4, 2)]
PATHS = 3


def cases():
    rng = random.Random(20261025)
    with cxx.connect() as H:
        st = H.settings(R)
        out = []
        for oname, ordering in ORDERINGS:
            for factors, steps in SIZES:
                variates = [[rng.gauss(0.0, 1.0) for _ in range(PATHS)] for _ in range(factors * steps)]
                table = H.call("SobolBrownianGenerator::orderedIndices", factors, steps, ordering)
                transformed = H.call("SobolBrownianGenerator::transform", factors, steps, ordering, variates)
                out.append({"name": f"{oname}, {factors} factors, {steps} steps",
                            "inputs": {"settings": st, "factors": factors, "steps": steps, "ordering": ordering,
                                       "variates": variates},
                            "expected": {"orderedIndices": [[int(k) for k in row] for row in table],
                                         "transform": transformed}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/browniangenerators/sobolbrowniangenerator.hpp", cases)
