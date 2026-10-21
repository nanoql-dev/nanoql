# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GBP LIBOR indexes (ql/indexes/ibor/gbplibor.hpp): GBPLibor at 1W, 1M, 3M, 6M and 1Y,
GBPLiborON, and DailyTenorGBPLibor at 1 settlement day, one case each, as tools/vectors/gen/indexes/ibor/euribor.py
writes them, with its DATES and index_case; and GBPLibor at a daily tenor, refused ("for daily tenors (1D) dedicated
DailyTenor constructor must be used"), its name's {"throws": true}.

The wheel's GBPLibor and GBPLiborON are QuantLib-SWIG's proxies of the C++ classes; it binds no DailyTenorGBPLibor,
so that case's expected values are the wheel's DailyTenorLibor built with DailyTenorGBPLibor's arguments
(gbplibor.hpp:53-59): "GBPLibor", the settlement days, GBP, the London exchange's calendar and Actual/365 (Fixed).
Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/GBPLiborVectorTests.cs."""
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
    out = [eur.index_case({"type": "GBPLibor", "tenor": eur.tenor_spec(t)}, lambda t=t: ql.GBPLibor(ql.Period(*t)))
           for t in [(1, ql.Weeks), (1, ql.Months), (3, ql.Months), (6, ql.Months), (1, ql.Years)]]
    out.append(eur.index_case({"type": "GBPLiborON"}, ql.GBPLiborON))
    out.append(eur.index_case(
        {"type": "DailyTenorGBPLibor", "settlementDays": 1},
        lambda: ql.DailyTenorLibor("GBPLibor", 1, ql.GBPCurrency(), ql.UnitedKingdom(ql.UnitedKingdom.Exchange),
                                   ql.Actual365Fixed())))
    out.append(eur.refused_case("GBPLibor at a daily tenor",
                                {"type": "GBPLibor", "tenor": eur.tenor_spec((1, ql.Days))},
                                lambda: ql.GBPLibor(ql.Period(1, ql.Days))))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/gbplibor.hpp", cases)
