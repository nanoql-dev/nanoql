# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterestRate (ql/interestrate.hpp): 5 rates x 4 day counters x 5 compoundings x 7
frequencies, among them EveryFourthMonth and EveryFourthWeek, whose text QuantLib writes with hyphens.

- A rate whose compounding needs a frequency and gets NoFrequency fails to construct: its one output is
  "interestRate", which throws.
- Every other rate: its frequency (NoFrequency where the compounding takes none); compound and discount factors at
  six times; compound factors from one date to three others; three equivalent rates at one time; the semiannual
  equivalent rate from that date to the three others under another day counter (Actual/360, or Actual/365 (Fixed)
  for an Actual/360 rate); the rate implied
  by one compound factor at one time under the case's own day counter, compounding and frequency; and its text.
  Some of these throw (the mixed compoundings, a non-positive time).

Each case's inputs describe the rate: the value, the day counter ({"type": <class>}, with a "convention" integer for
Thirty360 and ActualActual), and the compounding and frequency as QuantLib's integers; and every argument the outputs
use. Read back by tests/NanoQL.Tests/Vectors/InterestRateVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

RATES = [0.0, 0.01, 0.05, -0.005, 0.25]
DAY_COUNTERS = [
    {"type": "Actual365Fixed"},
    {"type": "Actual360"},
    {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)},
    {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)},
]
COMPOUNDINGS = {"Simple": ql.Simple, "Compounded": ql.Compounded, "Continuous": ql.Continuous,
                "SimpleThenCompounded": ql.SimpleThenCompounded, "CompoundedThenSimple": ql.CompoundedThenSimple}
FREQUENCIES = {"Annual": ql.Annual, "Semiannual": ql.Semiannual, "Quarterly": ql.Quarterly, "Monthly": ql.Monthly,
               "NoFrequency": ql.NoFrequency, "EveryFourthMonth": ql.EveryFourthMonth,
               "EveryFourthWeek": ql.EveryFourthWeek}
TIMES = [0.0, 0.25, 0.5, 1.0, 2.5, 10.0]
START = ql.Date(15, ql.October, 2026)
ENDS = [ql.Date(15, ql.January, 2027), ql.Date(29, ql.February, 2028), ql.Date(15, ql.October, 2036)]
# (compounding, frequency) of the equivalent rates, all at one time
EQUIVALENTS = [(ql.Continuous, ql.Annual), (ql.Compounded, ql.Quarterly), (ql.Simple, ql.Annual)]
EQUIVALENT_TIME = 1.5
IMPLIED_COMPOUND, IMPLIED_TIME = 1.05, 2.0
EQUIVALENT_DATES = (ql.Compounded, ql.Semiannual)


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "Thirty360":
        return ql.Thirty360(spec["convention"])
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def other_day_counter(spec):
    """A result day counter other than the rate's own."""
    return {"type": "Actual365Fixed"} if spec["type"] == "Actual360" else {"type": "Actual360"}


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for r in RATES:
        for dcs in DAY_COUNTERS:
            dc = day_counter(dcs)
            for cn, c in COMPOUNDINGS.items():
                for fn, f in FREQUENCIES.items():
                    name = f"{r} {dc.name()} {cn} {fn}"
                    inputs = {"settings": st, "rate": r, "dayCounter": dcs, "compounding": int(c), "frequency": int(f)}
                    try:
                        ir = ql.InterestRate(r, dc, c, f)
                    except RuntimeError:
                        out.append({"name": name, "inputs": inputs, "expected": {"interestRate": dict(v.THROWS)}})
                        continue
                    inputs.update({"times": TIMES, "d1": v.enc(START), "d2": v.enc(ENDS),
                                   "equivalents": [[int(cc), int(ff)] for cc, ff in EQUIVALENTS],
                                   "equivalentTime": EQUIVALENT_TIME, "impliedCompound": IMPLIED_COMPOUND,
                                   "impliedTime": IMPLIED_TIME,
                                   "equivalentDates": {"dayCounter": other_day_counter(dcs),
                                                       "compounding": int(EQUIVALENT_DATES[0]),
                                                       "frequency": int(EQUIVALENT_DATES[1])}})
                    other = day_counter(other_day_counter(dcs))
                    out.append({"name": name, "inputs": inputs, "expected": {
                        "frequency": int(ir.frequency()),
                        "compoundFactor": [v.attempt(lambda t=t: ir.compoundFactor(t)) for t in TIMES],
                        "discountFactor": [v.attempt(lambda t=t: ir.discountFactor(t)) for t in TIMES],
                        "compoundFactorDates": [v.attempt(lambda d=d: ir.compoundFactor(START, d)) for d in ENDS],
                        "equivalentRate": [v.attempt(lambda cc=cc, ff=ff: ir.equivalentRate(cc, ff, EQUIVALENT_TIME)
                                                     .rate()) for cc, ff in EQUIVALENTS],
                        "equivalentRateDates": [v.attempt(lambda d=d: ir.equivalentRate(
                            other, EQUIVALENT_DATES[0], EQUIVALENT_DATES[1], START, d).rate()) for d in ENDS],
                        "impliedRate": v.attempt(lambda: ql.InterestRate.impliedRate(
                            IMPLIED_COMPOUND, dc, c, f, IMPLIED_TIME).rate()),
                        "toString": v.attempt(lambda: str(ir))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/interestrate.hpp", cases)
