# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MarketModelDiscounter (ql/models/marketmodels/discounter.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: the numeraire bonds a unit paid at a time is worth on an LMM curve
state of the rate times set on the forwards, in each bond as numeraire.

Payments on every rate time, before the first, after the last and inside the last period, on products/multistep/
multistepforwards.py's grids and on forwards rising 0.5% a period.

Left out (nanoql interpolates a payment's own period where QuantLib extrapolates from the next;
tests/NanoQL.Tests/Divergences/MarketModelDiscounterBracketTests.cs): payments strictly inside a period but the last.

The harness unit is tools/cxx/src/models/marketmodels/accountingengine.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/MarketModelDiscounterCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import vectors as v  # noqa: E402

R = 46322


def payments(times):
    return list(times) + [times[0] / 2.0, times[0] - 0.01, times[-1] + 0.4, (times[-2] + times[-1]) / 2.0,
                          times[-1] - 0.01]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        markets = [(label, times, mf.forwards_of(times)) for label, times in mf.GRIDS]
        markets.append(("rising forwards", mf.GRIDS[0][1], [0.03 + 0.005 * i for i in range(6)]))
        for label, times, fwds in markets:
            for numeraire in (len(times) - 1, 0, 2):
                for t in payments(times):
                    r = H.call("MarketModelDiscounter.numeraireBonds", t, times, fwds, numeraire)
                    out.append({"name": f"{label}: paid at {t!r}, bond {numeraire}",
                                "inputs": {"settings": st, "paymentTime": t, "rateTimes": times, "forwards": fwds,
                                           "numeraire": numeraire},
                                "expected": {"numeraireBonds": v.enc(r)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/discounter.hpp", cases)
