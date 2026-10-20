# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated hazard-rate curve (ql/termstructures/credit/interpolatedhazardratecurve.hpp), through the wheel's HazardRateCurve (backward-flat hazard rates).

- "curve" cases: seven nodes, rising, falling or with zero rates, on three day counters, and a single node: the dates, times, hazard rates and maximum date, and at the nodes, between them and 400
  days past the last (read with extrapolation) the survival probability, hazard rate and default density.
- "construction" cases: rates other than one per date, a negative rate, dates out of order: refused.

With no dates QuantLib reads the first one before its count check (tests/NanoQL.Tests/Divergences/
CreditCurveFirstDateTests.cs), so no case has none. A refused value is {"throws": true}. The cases are built by
tools/vectors/gen/termstructures/defaulttermstructure.py's interpolated_cases. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Credit/InterpolatedHazardRateCurveVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

TODAY = dts.TODAY
Y = ql.Years
NODES = [TODAY, TODAY + ql.Period(6, ql.Months), TODAY + ql.Period(2, Y), TODAY + ql.Period(5, Y),
         TODAY + ql.Period(7, Y), TODAY + ql.Period(10, Y), TODAY + ql.Period(20, Y)]
SETS = [
    ("rising", NODES, [0.01, 0.012, 0.015, 0.02, 0.022, 0.025, 0.03], dts.A365),
    ("falling, A360", NODES, [0.05, 0.045, 0.04, 0.03, 0.025, 0.02, 0.015], dts.A360),
    ("with zeros, 30/360", NODES, [0.0, 0.0, 0.01, 0.01, 0.0, 0.02, 0.02], dts.THIRTY360_BB),
    ("high", NODES[:4], [0.2, 0.25, 0.3, 0.4], dts.A365),
    ("one node", [TODAY], [0.02], dts.A365),
]
REFUSED = [
    ("rates short", NODES, [0.01] * 6),
    ("negative rate", NODES, [0.01, 0.012, -0.015, 0.02, 0.022, 0.025, 0.03]),
    ("dates out of order", [NODES[0], NODES[2], NODES[1]], [0.01, 0.01, 0.01]),
]


def make_cases():
    return dts.interpolated_cases("HazardRateCurve", SETS, REFUSED)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/interpolatedhazardratecurve.hpp", make_cases)
