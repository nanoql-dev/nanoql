# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JPY LIBOR indexes (ql/indexes/ibor/jpylibor.hpp): JPYLibor at 1W, 1M, 3M, 6M and 1Y, and
DailyTenorJPYLibor at 0 and 2 settlement days, one case each, as tools/vectors/gen/indexes/ibor/euribor.py writes
them, with its DATES and index_case; and JPYLibor at a daily tenor, refused ("for daily tenors (1D) dedicated
DailyTenor constructor must be used"), its name's {"throws": true}.

The wheel's JPYLibor is QuantLib-SWIG's proxy of the C++ class; it binds no DailyTenorJPYLibor, so those cases'
expected values are the wheel's DailyTenorLibor built with DailyTenorJPYLibor's arguments (jpylibor.hpp:57-63):
"JPYLibor", the settlement days, JPY, the Japanese calendar and Actual/360. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/JPYLiborVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_euribor",
                                               os.path.join(v.GEN_ROOT, "indexes", "ibor", "euribor.py"))
eur = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eur)


def cases():
    out = [eur.index_case({"type": "JPYLibor", "tenor": eur.tenor_spec(t)}, lambda t=t: ql.JPYLibor(ql.Period(*t)))
           for t in [(1, ql.Weeks), (1, ql.Months), (3, ql.Months), (6, ql.Months), (1, ql.Years)]]
    for days in (0, 2):
        out.append(eur.index_case(
            {"type": "DailyTenorJPYLibor", "settlementDays": days},
            lambda d=days: ql.DailyTenorLibor("JPYLibor", d, ql.JPYCurrency(), ql.Japan(), ql.Actual360())))
    out.append(eur.refused_case("JPYLibor at a daily tenor",
                                {"type": "JPYLibor", "tenor": eur.tenor_spec((1, ql.Days))},
                                lambda: ql.JPYLibor(ql.Period(1, ql.Days))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/jpylibor.hpp", cases)
