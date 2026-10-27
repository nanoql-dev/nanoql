# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MultiProductComposite (ql/models/marketmodels/products/multiproductcomposite.hpp, .cpp) and
the composite base it derives (compositeproduct.hpp, .cpp), which the wheel doesn't expose, each output computed by
the C++ harness: composites stepped on set LMM curve states, read per step as multistepforwards.py reads the forwards,
each part's cash flows scaled by its multiplier and their time indices into the merged cash-flow times.

Composites of forwards, optionlets and a product of nothing on multistepforwards.py's grids, added with multipliers 1,
-2 (a subtraction) and 0.5, every part finishing at the last step; and forwards beside optionlets paid off the grid,
the merged cash-flow times sorted.

Left out (nanoql records a finished part, where QuantLib's composite steps it again and ends early; the S-mmp plan's
MMPR4 (vii); tests/NanoQL.Tests/Divergences/MarketModelCompositeEndTests.cs): composites whose parts finish before the
last step.

The harness unit is tools/cxx/src/models/marketmodels/products/products.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MultiProductCompositeCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
TYPE = "MultiProductComposite"


def composites(times):
    n = len(times) - 1
    f0 = mf.forwards_of(times)
    fwd = mf.forwards_spec(times, f0)
    opt = mf.optionlets_spec(times, [[1, f + 0.002] for f in f0])
    nothing = {"type": "MultiStepNothing", "rateTimes": times, "evolutionTimes": times[:-1], "numberOfProducts": 2,
               "doneIndex": n}
    off = mf.optionlets_spec(times, [[-1, f] for f in f0], payment_times=[t - 0.1 for t in times[1:]])
    return [("forwards and optionlets", [(fwd, 1.0), (opt, 1.0)]),
            ("a subtraction and nothing", [(fwd, 0.5), (opt, -2.0), (nothing, 1.0)]),
            ("optionlets paid off the grid", [(fwd, 1.0), (off, 1.0)])]


def cases_for(type_name):
    out = []
    rnd = random.Random(v.SEED)
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            fs = mf.states(rnd, times, len(times) - 1)
            for name, parts in composites(times):
                spec = {"type": type_name, "parts": [{"product": p, "multiplier": m} for p, m in parts]}
                out.append(mf.product_case(H, st, f"{label}: {name}", spec, fs))
    return out


def cases():
    return cases_for(TYPE)


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/products/multiproductcomposite.hpp", cases)
