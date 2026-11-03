# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SingleProductComposite (ql/models/marketmodels/products/singleproductcomposite.hpp, .cpp),
which the wheel doesn't expose, each output computed by the C++ harness: multiproductcomposite.py's composites as one
product, every part's cash flows its one product's.

Left out: its maximum number of cash flows a step, which nanoql counts per part's product where QuantLib sums the parts'
maxima (tests/NanoQL.Tests/Divergences/SingleProductCompositeBufferTests.cs), and multiproductcomposite.py's left-out
composites.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/SingleProductCompositeCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import multiproductcomposite as mc  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    out = mc.cases_for("SingleProductComposite")
    for c in out:
        c["expected"].pop("maxNumberOfCashFlowsPerProductPerStep")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/singleproductcomposite.hpp", cases)
