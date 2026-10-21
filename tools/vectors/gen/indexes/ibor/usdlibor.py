# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's USD LIBOR indexes (ql/indexes/ibor/usdlibor.hpp): USDLibor at 1W, 1M, 3M, 6M and 1Y,
USDLiborON, and DailyTenorUSDLibor at 1 settlement day, one case each, as tools/vectors/gen/indexes/ibor/euribor.py
writes them, with its DATES and index_case; and USDLibor at a daily tenor, refused ("for daily tenors (1D) dedicated
DailyTenor constructor must be used"), its name's {"throws": true}.

The wheel's USDLibor and USDLiborON are QuantLib-SWIG's proxies of the C++ classes; it binds no DailyTenorUSDLibor,
so that case's expected values are the wheel's DailyTenorLibor built with DailyTenorUSDLibor's arguments
(usdlibor.hpp:55-61): "USDLibor", the settlement days, USD, the US LIBOR-impact calendar and Actual/360. Read back by
tests/NanoQL.Tests/Vectors/Indexes/Ibor/USDLiborVectorTests.cs."""
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
    out = [eur.index_case({"type": "USDLibor", "tenor": eur.tenor_spec(t)}, lambda t=t: ql.USDLibor(ql.Period(*t)))
           for t in [(1, ql.Weeks), (1, ql.Months), (3, ql.Months), (6, ql.Months), (1, ql.Years)]]
    out.append(eur.index_case({"type": "USDLiborON"}, ql.USDLiborON))
    out.append(eur.index_case(
        {"type": "DailyTenorUSDLibor", "settlementDays": 1},
        lambda: ql.DailyTenorLibor("USDLibor", 1, ql.USDCurrency(), ql.UnitedStates(ql.UnitedStates.LiborImpact),
                                   ql.Actual360())))
    out.append(eur.refused_case("USDLibor at a daily tenor",
                                {"type": "USDLibor", "tenor": eur.tenor_spec((1, ql.Days))},
                                lambda: ql.USDLibor(ql.Period(1, ql.Days))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/usdlibor.hpp", cases)
