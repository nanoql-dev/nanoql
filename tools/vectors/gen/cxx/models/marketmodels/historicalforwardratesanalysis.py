# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's historicalForwardRatesAnalysis (ql/models/marketmodels/historicalforwardratesanalysis.hpp),
which the wheel doesn't expose, each output computed by the C++ harness: the relative changes of a 3-month index's
forward rates at fixed periods ahead, read off a curve bootstrapped on each date from Ibor deposits' fixings, read
through the dates skipped and failed, their messages, the fixing periods and the statistics' size, samples, weight sum,
means and covariance.

Deposits of 1, 3, 6 and 12 months on TARGET with historicalratesanalysis.py's random-walk histories; forwards at 1 and
4 months ahead (an initial gap of 1 month and a horizon of 6), weekly; on a discount curve interpolated log-linearly
and a zero curve interpolated linearly, each to QuantLib's accuracy 1e-12; one history with gaps (the dates skipped);
and a horizon of 12 months, whose last forward ends past the curve on every date (the dates failed).

nanoql bootstraps each date's curve afresh, where QuantLib's single curve restarts each date's bootstrap from the last
date's solution (the S-mmp plan's SMD9): the forwards agree within the bootstrap's accuracy, and R7's tolerance on the
statistics' outputs holds them.

Left out: the failed dates' messages, the curve's range check, whose text prints the times to QuantLib's six digits
and nanoql's in full (TermStructure's check); their number is written.

The harness unit is tools/cxx/src/models/marketmodels/historicalratesanalysis.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/HistoricalForwardRatesAnalysisCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import historicalratesanalysis as hra  # noqa: E402
import vectors as v  # noqa: E402


def cases():
    out = []
    rnd = random.Random(v.SEED + 1)
    deposits = [(hra.ibor(m), 0.028 + 0.001 * m) for m in (1, 3, 6, 12)]
    fwd = hra.ibor(3)
    gaps = [ql.Date(9, ql.September, 2026), ql.Date(14, ql.October, 2026)]
    weekly = [1, int(ql.Weeks)]
    with cxx.connect() as H:
        for name, curve, missing, horizon in (
                ("log-linear discounts", "Discount/LogLinear", [], [6, int(ql.Months)]),
                ("linear zero rates", "ZeroYield/Linear", [], [6, int(ql.Months)]),
                ("a deposit's history with gaps", "Discount/LogLinear", gaps, [6, int(ql.Months)]),
                ("a horizon past the curve", "Discount/LogLinear", [], [12, int(ql.Months)])):
            fixings = hra.histories(rnd, deposits, missing)
            ibors = [s for s, _ in deposits]
            args = [curve, hra.START.serialNumber(), hra.END.serialNumber(), weekly, fwd, [1, int(ql.Months)],
                    horizon, ibors, {"type": "Actual365Fixed"}, 1e-12]
            with H.case_settings(hra.TODAY.serialNumber(), fixings=fixings) as st:
                r = H.call("historicalForwardRatesAnalysis", *args)
            expected = {"skippedDates": r["skippedDates"], "skippedDatesErrorMessage": r["skippedDatesErrorMessage"],
                        "failedDates": r["failedDates"], "failedDatesErrorMessages": len(r["failedDatesErrorMessage"]),
                        "fixingPeriods": r["fixingPeriods"]}
            expected.update(hra.statistics(r))
            out.append({"name": name,
                        "inputs": {"settings": st, "curve": curve, "startDate": hra.START.serialNumber(),
                                   "endDate": hra.END.serialNumber(), "step": weekly, "fwdIndex": fwd,
                                   "initialGap": [1, int(ql.Months)], "horizon": horizon, "iborIndexes": ibors,
                                   "yieldCurveDayCounter": {"type": "Actual365Fixed"}, "yieldCurveAccuracy": 1e-12},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/historicalforwardratesanalysis.hpp", cases)
