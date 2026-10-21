# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VarianceSwap (ql/instruments/varianceswap.hpp, .cpp): its inspectors and its expiry.

Long and short swaps at two strikes and notionals, started on, before and after the evaluation date, maturing before,
on and after it: the position, strike, notional, start and maturity dates, and isExpired under evaluation dates on
either side of the maturity and on it, with the reference date's events included and not. Its pricing is
tools/vectors/gen/pricingengines/forward/replicatingvarianceswapengine.py's.

A case holds one swap {"position" (QuantLib's Long 0, Short 1), "strike", "notional", "startDate", "maturityDate"}, its
isExpired outputs one per evaluation setting. The wheel's calls, by QuantLib-SWIG 1.44's varianceswaps.i:
VarianceSwap(position, strike, notional, startDate, maturityDate), its inspectors and isExpired. Read back by
tests/NanoQL.Tests/Vectors/Instruments/VarianceSwapVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(15, ql.October, 2026)
EVALUATIONS = [(-30, False), (0, False), (0, True), (30, False), (89, False), (90, False), (90, True), (200, False)]


def cases():
    out = []
    long_, short = int(ql.Position.Long), int(ql.Position.Short)
    swaps = [(long_, 0.04, 50000.0, 0, 90), (short, 0.0625, 1.0, -60, 90), (long_, 0.09, 1e6, 30, 365)]
    settings = [v.settings(R + d, include_reference_date_events=inc) for d, inc in EVALUATIONS]
    for position, strike, notional, start, end in swaps:
        spec = {"position": position, "strike": strike, "notional": notional, "startDate": (R + start).serialNumber(),
                "maturityDate": (R + end).serialNumber()}
        s = ql.VarianceSwap(position, strike, notional, R + start, R + end)
        expired = []
        for d, inc in EVALUATIONS:
            ql.Settings.instance().evaluationDate = R + d
            ql.Settings.instance().includeReferenceDateEvents = inc
            expired.append(s.isExpired())
        ql.Settings.instance().includeReferenceDateEvents = False
        out.append({"name": f"{'long' if position == long_ else 'short'} {strike}, {start} to {end} days",
                    "inputs": {"swap": spec, "settings": settings},
                    "expected": {"position": int(s.position()), "strike": s.strike(), "notional": s.notional(),
                                 "startDate": v.enc(s.startDate()), "maturityDate": v.enc(s.maturityDate()),
                                 "isExpired": expired}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/varianceswap.hpp", cases)
