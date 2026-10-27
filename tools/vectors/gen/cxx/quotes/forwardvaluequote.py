# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardValueQuote (ql/quotes/forwardvaluequote.hpp, .cpp), which the wheel doesn't expose,
each output computed by the C++ harness: the index's fixing at a date.

On Euribor indexes of 3 months, 6 months and 1 year (TARGET, 2 fixing days, modified following, end of month,
Actual/360) forecast on flat curves of 3% and 5% (Actual/365, continuous), on Wednesday 15 January 2025: fixing dates
one week, one month, one year and five years after the evaluation date, adjusted to TARGET business days. Each quote's
value. A case's inputs are the "index" (a spec of tools/vectors/gen/indexes/interestrateindex.py with its curve) and
the "fixingDate". The harness unit is tools/cxx/src/quotes/forwardvaluequote.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Quotes/QuoteCxxVectorTests.cs."""
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

TODAY = ql.Date(15, ql.January, 2025)


def cases():
    out = []
    cal = ql.TARGET()
    with cxx.connect() as H:
        st = H.settings(TODAY.serialNumber())
        for tenor in ((3, ql.Months), (6, ql.Months), (1, ql.Years)):
            for rate in (0.03, 0.05):
                index = irx.ibor_spec("Euribor", tenor, 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360,
                                      irx.curve_spec(TODAY, rate))
                for offset in ((1, ql.Weeks), (1, ql.Months), (1, ql.Years), (5, ql.Years)):
                    fixing = cal.adjust(TODAY + ql.Period(*offset))
                    out.append({"name": f"Euribor {tenor[0]} {tenor[1]} at {rate} fixing {fixing.ISO()}",
                                "inputs": {"settings": st, "index": index, "fixingDate": fixing.serialNumber()},
                                "expected": {"value": v.enc(H.call("ForwardValueQuote", index, fixing.serialNumber()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/quotes/forwardvaluequote.hpp", cases)
