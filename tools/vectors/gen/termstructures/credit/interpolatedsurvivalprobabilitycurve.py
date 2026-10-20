# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated survival-probability curve (ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp), through the wheel's SurvivalProbabilityCurve (linear probabilities).

- "curve" cases: seven nodes, steadily falling, with a flat stretch, on three day counters, and two nodes: the dates, times, probabilities and maximum date, and at the nodes, between them and 400
  days past the last (read with extrapolation) the survival probability, hazard rate and default density.
- "construction" cases: a single node, probabilities other than one per date, a first probability other than 1, a zero probability, a rising one, dates out of order: refused.

With no dates QuantLib reads the first one before its count check (tests/NanoQL.Tests/Divergences/
CreditCurveFirstDateTests.cs), so no case has none. A refused value is {"throws": true}. The cases are built by
tools/vectors/gen/termstructures/defaulttermstructure.py's interpolated_cases. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Credit/InterpolatedSurvivalProbabilityCurveVectorTests.cs."""
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
    ("falling", NODES, [1.0, 0.995, 0.97, 0.92, 0.89, 0.84, 0.7], dts.A365),
    ("flat stretch, A360", NODES, [1.0, 0.99, 0.99, 0.95, 0.95, 0.9, 0.8], dts.A360),
    ("steep, 30/360", NODES, [1.0, 0.9, 0.7, 0.5, 0.4, 0.3, 0.1], dts.THIRTY360_BB),
    ("two nodes", NODES[:2], [1.0, 0.99], dts.A365),
]
REFUSED = [
    ("one node", [TODAY], [1.0]),
    ("probabilities short", NODES, [1.0, 0.99, 0.98, 0.97, 0.96, 0.95]),
    ("first not one", NODES, [0.99, 0.98, 0.97, 0.96, 0.95, 0.94, 0.93]),
    ("zero probability", NODES, [1.0, 0.99, 0.0, 0.0, 0.0, 0.0, 0.0]),
    ("rising probability", NODES, [1.0, 0.98, 0.99, 0.97, 0.96, 0.95, 0.94]),
    ("dates out of order", [NODES[0], NODES[2], NODES[1]], [1.0, 0.99, 0.98]),
]


def make_cases():
    return dts.interpolated_cases("SurvivalProbabilityCurve", SETS, REFUSED)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/credit/interpolatedsurvivalprobabilitycurve.hpp", make_cases)
