# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for IborIndex::forecastFixing(const Date&) (ql/indexes/iborindex.hpp) called directly, each output computed
by the C++ harness: the wheel reaches it only through fixing() on later dates. 7 Ibor indexes (tenors 2W to 1Y over
the four calendars of tools/vectors/gen/indexes/interestrateindex.py, a 0D index, whose forecast QuantLib refuses for
its period of no length, and an index with no curve, whose forecast QuantLib refuses) and 4 overnight indexes, each
forecasting from a flat curve with its reference date on 15 January 2025, at 30 dates from 10 days before it to 10
years after: weekend days and holidays, which aren't valid fixing dates, and dates whose value date is before the
curve's reference date, are refused. A refused value is {"throws": true}.

Each case's inputs hold the index spec ("index", as in that generator, whose functions build it) and the dates
("dates"). tests/NanoQL.Tests/Vectors/Cxx/Indexes/IborIndexCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

TODAY = irx.TODAY
OFFSETS = [-10, -3, -2, -1, 0, 1, 2, 3, 4, 5, 6, 7, 10, 14, 21, 30, 31, 45, 59, 60, 90, 120, 181, 182, 270, 365, 366,
           730, 1826, 3653]


def curve(forward, dc=irx.A365, compounding=ql.Continuous, frequency=ql.Annual):
    return irx.curve_spec(TODAY, forward, dc, compounding, frequency)


INDEXES = [
    irx.ibor_spec("Euribor", (1, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360, curve(0.031)),
    irx.ibor_spec("UsdIbor", (3, ql.Months), 2, irx.US_SOFR, ql.ModifiedFollowing, False, irx.A365,
                  curve(0.0475, irx.A360, ql.Simple)),
    irx.ibor_spec("GbpIbor", (6, ql.Months), 0, irx.UK_EXCHANGE, ql.Following, False, irx.THIRTY360_BB,
                  curve(-0.0035, irx.A365, ql.Compounded, ql.Quarterly)),
    irx.ibor_spec("NulIbor", (12, ql.Months), 1, irx.NULL_CALENDAR, ql.ModifiedFollowing, True, irx.A360,
                  curve(0.012)),
    irx.ibor_spec("Euribor", (2, ql.Weeks), 3, irx.TARGET, ql.Preceding, False, irx.A365, curve(0.0)),
    irx.ibor_spec("Zero", (0, ql.Days), 2, irx.TARGET, ql.ModifiedFollowing, False, irx.A360, curve(0.02)),
    irx.ibor_spec("Bare", (3, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, False, irx.A360, None),
    irx.overnight_spec("Sofr", 0, irx.US_SOFR, irx.A360, curve(0.043)),
    irx.overnight_spec("Estr", 1, irx.TARGET, irx.A360, curve(0.029, irx.A360, ql.Simple)),
    irx.overnight_spec("Sonia", 0, irx.UK_EXCHANGE, irx.A365, curve(0.047)),
    irx.overnight_spec("Nul", 2, irx.NULL_CALENDAR, irx.THIRTY360_BB, curve(-0.001, irx.A365, ql.Compounded)),
]


def cases():
    with cxx.connect() as H:
        out = []
        dates = [v.enc(TODAY + n) for n in OFFSETS]
        for k, spec in enumerate(INDEXES):
            with H.case_settings(v.enc(TODAY)) as st:
                name = H.call("Index::name", spec)
                out.append({"name": f"{name} / {k}", "inputs": {"settings": st, "index": spec, "dates": dates},
                            "expected": {"forecastFixing": [
                                v.attempt(lambda d=d: H.call("IborIndex::forecastFixing(Date)", spec, d))
                                for d in dates]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/iborindex.hpp", cases)
