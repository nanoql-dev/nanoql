# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BootstrapHelper and RelativeDateBootstrapHelper (ql/termstructures/bootstraphelper.hpp),
each output computed by the C++ harness: the five date inspectors' fallbacks, which every rate helper QuantLib-Python
wraps overrides by setting all five dates; the construction of a helper whose dates follow the evaluation date, which
no helper ported so far derives from; and operator<<(Pillar::Choice), which the wheel doesn't wrap.

- 32 cases, one per combination of the five dates given or left null (Date()): earliestDate 15 January 2024,
  latestDate 15 January 2026, maturityDate 15 January 2025, latestRelevantDate 15 July 2025 and pillarDate 15 October
  2025, each date apart, so that each fallback shows which date it took: the maturity date falls back on the latest
  relevant date, which falls back on the latest date, as the pillar date does, and the latest date on the stored pillar
  date. The helper is the harness's own BootstrapHelper of YieldTermStructure setting the dates it is given
  (termstructures/bootstraphelper.cpp), which tests/NanoQL.Tests/Vectors/Cxx/TermStructures/
  BootstrapHelperCxxVectorTests.cs defines the same way.
- 12 cases of the harness's own RelativeDateBootstrapHelper of YieldTermStructure, whose initializeDates(), called by
  its constructor, sets its earliest and pillar dates to offsets in days (2 and 92, or 0 and 366) from the evaluation
  date its base recorded, when its dates follow the evaluation date, or from a fixed date, 1 March 2030, when they
  don't, on three evaluation dates; the other dates fall back on the pillar date. tests/NanoQL.Tests/Vectors/Cxx/
  TermStructures/BootstrapHelperCxxVectorTests.cs defines the same helper on RelativeDateBootstrapHelper.
- One case: the text of each pillar choice, MaturityDate (0) to CustomDate (2), and of -1 and 3 just outside the
  enum, which QuantLib refuses ("unknown Pillar::Choice(n)").

Each date case's inputs hold its settings and "dates" ({"earliestDate", "latestDate", "maturityDate",
"latestRelevantDate", "pillarDate"}, a serial or null each), or "offsets" ({"updateDates", "fixedDate",
"earliestOffset", "pillarOffset"}) for a relative helper; its outputs are the five inspectors' dates."""
import itertools
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

FIELDS = ["earliestDate", "latestDate", "maturityDate", "latestRelevantDate", "pillarDate"]
GIVEN = {"earliestDate": ql.Date(15, 1, 2024), "latestDate": ql.Date(15, 1, 2026),
         "maturityDate": ql.Date(15, 1, 2025), "latestRelevantDate": ql.Date(15, 7, 2025),
         "pillarDate": ql.Date(15, 10, 2025)}
INSPECTORS = ["earliestDate", "maturityDate", "latestRelevantDate", "pillarDate", "latestDate"]
CHOICES = [-1, 0, 1, 2, 3]
RELATIVE_DATES = [ql.Date(15, 1, 2024), ql.Date(29, 2, 2024), ql.Date(30, 3, 2024)]
FIXED = ql.Date(1, 3, 2030)
OFFSETS = [(2, 92), (0, 366)]


def cases():
    out = []
    with cxx.connect() as H:
        for given in itertools.product([True, False], repeat=len(FIELDS)):
            dates = {f: v.enc(GIVEN[f]) if g else None for f, g in zip(FIELDS, given)}
            name = ", ".join(f for f, g in zip(FIELDS, given) if g) or "no date"
            with H.case_settings(v.enc(ql.Date(15, 1, 2024))) as st:
                expected = {f: H.call(f"BootstrapHelper<YieldTermStructure>::{f}", dates) for f in INSPECTORS}
            out.append({"name": f"given: {name}", "inputs": {"settings": st, "dates": dates}, "expected": expected})
        for today in RELATIVE_DATES:
            for update in (True, False):
                for earliest, pillar in OFFSETS:
                    offsets = {"updateDates": update, "fixedDate": v.enc(FIXED), "earliestOffset": earliest,
                               "pillarOffset": pillar}
                    with H.case_settings(v.enc(today)) as st:
                        expected = {f: H.call(f"RelativeDateBootstrapHelper<YieldTermStructure>::{f}", offsets)
                                    for f in INSPECTORS}
                    which = "following the evaluation date" if update else "from a fixed date"
                    out.append({"name": f"{today.ISO()} relative, {which}, offsets {earliest} and {pillar}",
                                "inputs": {"settings": st, "offsets": offsets}, "expected": expected})
        with H.case_settings(v.enc(ql.Date(15, 1, 2024))) as st:
            texts = [v.attempt(lambda c=c: H.call("operator<<(Pillar::Choice)", c)) for c in CHOICES]
        out.append({"name": "pillar choices -1 to 3", "inputs": {"settings": st, "choices": CHOICES},
                    "expected": {"toString": texts}})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # With no date at all, every inspector is null; with the pillar date alone, every fallback reaches it.
    assert set(by_name["given: no date"].values()) == {None}
    pillar = v.enc(GIVEN["pillarDate"])
    assert {k: x for k, x in by_name["given: pillarDate"].items() if k != "earliestDate"} == {
        "maturityDate": pillar, "latestRelevantDate": pillar, "pillarDate": pillar, "latestDate": pillar}
    following = by_name["2024-02-29 relative, following the evaluation date, offsets 2 and 92"]
    assert following["pillarDate"] == v.enc(ql.Date(29, 2, 2024) + 92) == following["latestDate"]
    fixed = by_name["2024-02-29 relative, from a fixed date, offsets 2 and 92"]
    assert fixed["earliestDate"] == v.enc(FIXED + 2)
    texts = by_name["pillar choices -1 to 3"]["toString"]
    assert texts[0] == v.THROWS and texts[-1] == v.THROWS
    assert texts[1:4] == ["MaturityPillarDate", "LastRelevantPillarDate", "CustomPillarDate"]


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/bootstraphelper.hpp", cases)
