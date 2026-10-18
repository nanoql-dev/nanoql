# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Tibor preset (ql/indexes/ibor/tibor.hpp), the Tokyo Interbank Offered Rate, an Ibor index
fixed on the Japanese calendar, at 1M, 3M and 6M: one case per index, as tools/vectors/gen/indexes/ibor/euribor.py
writes them, with its DATES and index_case: the name, family name, tenor, fixing days, currency code, fixing calendar's
name, day counter's name, business-day convention and end-of-month flag, and fixingDate, valueDate and maturityDate at
the 30 dates, valueDate refusing a date that isn't a fixing date; and two refused constructions, at 1 hour and 30
minutes, which QuantLib's InterestRateIndex refuses through tenor_ == 1*Days ("unknown time unit"), recorded as their
names' {"throws": true}.

The wheel's Tibor is QuantLib-SWIG's proxy of the C++ class of that name, its constructor QuantLib's with the default
empty forwarding handle. Read back by tests/NanoQL.Tests/Vectors/Indexes/Ibor/TiborVectorTests.cs."""
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
    out = [eur.index_case({"type": "Tibor", "tenor": eur.tenor_spec(t)}, lambda t=t: ql.Tibor(ql.Period(*t)))
           for t in [(1, ql.Months), (3, ql.Months), (6, ql.Months)]]
    out += [eur.refused_case(f"Tibor at {label}", {"type": "Tibor", "tenor": eur.tenor_spec(t)},
                             lambda t=t: ql.Tibor(ql.Period(*t)))
            for label, t in [("1 hour", (1, ql.Hours)), ("30 minutes", (30, ql.Minutes))]]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/indexes/ibor/tibor.hpp", cases)
