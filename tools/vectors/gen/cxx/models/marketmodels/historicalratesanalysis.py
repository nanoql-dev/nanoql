# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's historicalRatesAnalysis (ql/models/marketmodels/historicalratesanalysis.hpp, .cpp), which the
wheel doesn't expose, each output computed by the C++ harness: the relative changes of Ibor indexes' fixings between
successive dates, on fixing histories the case stores, read through the dates skipped and their messages and the
statistics' size, samples, weight sum, means and covariance.

Three Ibor indexes (3, 6 and 12 months on TARGET) with histories on every TARGET business day of September and October
2026, a random walk from 3%, 3.2% and 3.5%; weekly and three-day steps; one history with gaps (the dates skipped) and
one index alone.

The harness unit is tools/cxx/src/models/marketmodels/historicalratesanalysis.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/HistoricalRatesAnalysisCxxVectorTests.cs."""
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.join(_dir, "gen", "indexes"))

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import interestrateindex as iri  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(16, ql.November, 2026)
START = ql.Date(1, ql.September, 2026)
END = ql.Date(30, ql.October, 2026)


def ibor(months):
    return iri.ibor_spec("Test", (months, ql.Months), 2, iri.TARGET, ql.ModifiedFollowing, False, iri.A360)


def histories(rnd, specs, gaps):
    """Each index's fixings on every TARGET business day from a week before the start to the end, a random walk; the
    dates in gaps left out of the first."""
    cal = ql.TARGET()
    out = {}
    for k, (spec, level) in enumerate(specs):
        name = iri.make_index(spec).name()
        d, x, pairs = START - 7, level, []
        while d <= END + 7:
            if cal.isBusinessDay(d):
                x *= 1.0 + rnd.gauss(0.0, 0.01)
                if not (k == 0 and d in gaps):
                    pairs.append([d.serialNumber(), x])
            d += 1
        out[name] = pairs
    return out


def statistics(r):
    s = r["statistics"]
    return {"size": s["size"], "samples": s["samples"], "weightSum": v.enc(s["weightSum"]),
            "mean": v.enc(s["mean"]), "covariance": v.enc(s["covariance"])}


def cases():
    out = []
    rnd = random.Random(v.SEED)
    specs = [(ibor(3), 0.03), (ibor(6), 0.032), (ibor(12), 0.035)]
    gaps = [ql.Date(9, ql.September, 2026), ql.Date(30, ql.September, 2026), ql.Date(21, ql.October, 2026)]
    with cxx.connect() as H:
        for name, used, missing, step in (
                ("three indexes, weekly", specs, [], [1, int(ql.Weeks)]),
                ("three indexes, every three days", specs, [], [3, int(ql.Days)]),
                ("a history with gaps, weekly", specs, gaps, [1, int(ql.Weeks)]),
                ("one index, every two weeks", specs[:1], [], [2, int(ql.Weeks)])):
            fixings = histories(rnd, used, missing)
            indexes = [s for s, _ in used]
            with H.case_settings(TODAY.serialNumber(), fixings=fixings) as st:
                r = H.call("historicalRatesAnalysis", START.serialNumber(), END.serialNumber(), step, indexes)
            expected = {"skippedDates": r["skippedDates"], "skippedDatesErrorMessage": r["skippedDatesErrorMessage"]}
            expected.update(statistics(r))
            out.append({"name": name,
                        "inputs": {"settings": st, "startDate": START.serialNumber(), "endDate": END.serialNumber(),
                                   "step": step, "indexes": indexes},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/historicalratesanalysis.hpp", cases)
