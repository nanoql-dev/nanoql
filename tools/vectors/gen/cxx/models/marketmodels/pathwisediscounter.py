# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarketModelPathwiseDiscounter (ql/models/marketmodels/pathwisediscounter.hpp, .cpp), which
the wheel doesn't expose, each output computed by the C++ harness: the money-market units a unit paid at a time buys at
a step and their derivatives in each rate, from the step's discount factors.

Payments on every rate time, before the first, after the last and inside the last period, discounter.py's, on
products/multistep/multistepforwards.py's grids; two steps' discount factors, the grid's forwards and forwards 1% higher.

Left out (nanoql interpolates a payment's own period where QuantLib extrapolates from the next;
tests/NanoQL.Tests/Divergences/MarketModelDiscounterBracketTests.cs): payments strictly inside a period but the last.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelPathwiseDiscounterCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "multistep"))

import cxx  # noqa: E402
import discounter as dc  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322


def discount_rows(times):
    rows = []
    for shift in (0.0, 0.01):
        row = [1.0]
        for f, a in zip(mf.forwards_of(times), mf.accruals_of(times)):
            row.append(row[-1] / (1.0 + (f + shift) * a))
        rows.append(row)
    return rows


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            rows = discount_rows(times)
            for step in (0, 1):
                for t in dc.payments(times):
                    r = H.call("MarketModelPathwiseDiscounter.factors", t, times, rows, step)
                    out.append({"name": f"{label}: paid at {t!r}, step {step}",
                                "inputs": {"settings": st, "paymentTime": t, "rateTimes": times, "discounts": rows,
                                           "step": step},
                                "expected": {"factors": v.enc(r)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/pathwisediscounter.hpp", cases)
