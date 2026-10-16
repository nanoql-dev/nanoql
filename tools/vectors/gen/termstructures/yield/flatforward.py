# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FlatForward (ql/termstructures/yield/flatforward.hpp), through the YieldTermStructure and
TermStructure interface it inherits.

- "fixed" cases: 144 curves with a fixed reference date (2 reference dates x 4 forward rates x 3 day counters x
  3 compoundings x 2 frequencies). The reference and maximum dates; the discount, a continuous Actual/365 zero rate
  and a semiannual Actual/360 zero rate at seven dates, among them the reference date (the zero rate's t = 0 branch)
  and the day before it (which throws); simple Actual/365 forward rates over two periods and at one date (the
  instantaneous branch); a semiannual zero rate at four times, t = 0 among them; and simple forward rates between
  four pairs of times, one pair equal and one reversed (which throws).
- "settlementDays" cases: a curve whose reference date is 0 or 2 TARGET business days after each of three
  evaluation dates (QuantLib's moving curve, which nanoql builds by FlatForward.Create from the context): the
  reference date and the discount at two dates.
- "range" cases on four curves: the maximum time; the discount at it, 1e-13 past it (inside QuantLib's close_enough)
  and 1e-6 past it (which throws), without and with the extrapolate argument; and whether the curve allows
  extrapolation, then whether it does, and the discount 1e-6 past the maximum time, after enableExtrapolation, and
  again after disableExtrapolation. QuantLib switches the curve in place; nanoql's switches return a copy, and the
  test applies each switch to the curve the previous one returned.

Each case's inputs describe its curve: the reference date (or the settlement days and the calendar), the forward
rate, the day counter ({"type": <class>}, with a "convention" integer for ActualActual), and the compounding and
frequency as QuantLib's integers; and every argument the outputs use. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/FlatForwardVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

A365 = {"type": "Actual365Fixed"}
A360 = {"type": "Actual360"}
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
TARGET = {"type": "TARGET"}

REFERENCE_DATES = [ql.Date(15, ql.October, 2026), ql.Date(29, ql.February, 2024)]
FORWARDS = [0.0, 0.03, -0.01, 0.12]
DAY_COUNTERS = [("Actual365Fixed", A365), ("Actual360", A360), ("ActualActual.ISDA", ACT_ISDA)]
COMPOUNDINGS = [("Continuous", ql.Continuous), ("Compounded", ql.Compounded), ("Simple", ql.Simple)]
FREQUENCIES = [("Annual", ql.Annual), ("Semiannual", ql.Semiannual)]
NAMES = {**{("c", c): n for n, c in COMPOUNDINGS}, **{("f", f): n for n, f in FREQUENCIES}}
DATE_OFFSETS = [0, 1, 90, 365, 3653, 18262, -1]
FORWARD_OFFSETS = [(0, 180), (365, 730), (100, 100)]
ZERO_CONTINUOUS = {"dayCounter": A365, "compounding": int(ql.Continuous), "frequency": int(ql.Annual)}
ZERO_SEMIANNUAL = {"dayCounter": A360, "compounding": int(ql.Compounded), "frequency": int(ql.Semiannual)}
FORWARD_RATE = {"dayCounter": A365, "compounding": int(ql.Simple), "frequency": int(ql.Annual)}
TIMES = [0.0, 0.5, 2.0, 10.0]
ZERO_RATE_TIMES = {"compounding": int(ql.Compounded), "frequency": int(ql.Semiannual)}
TIME_PAIRS = [(0.0, 0.5), (1.0, 3.0), (2.0, 2.0), (3.0, 1.0)]
FORWARD_RATE_TIMES = {"compounding": int(ql.Simple), "frequency": int(ql.Annual)}

EVALUATION_DATES = [ql.Date(15, ql.October, 2026), ql.Date(31, ql.December, 2026), ql.Date(29, ql.February, 2028)]
SETTLEMENT_DAYS = [0, 2]
MOVING_OFFSETS = [30, 400]

RANGE_CURVES = [(ql.Date(15, ql.October, 2026), 0.03, A365, ql.Continuous, ql.Annual),
                (ql.Date(29, ql.February, 2024), -0.01, A360, ql.Compounded, ql.Semiannual),
                (ql.Date(15, ql.October, 2026), 0.12, ACT_ISDA, ql.Simple, ql.Annual),
                (ql.Date(29, ql.February, 2024), 0.0, A365, ql.Compounded, ql.Annual)]
PAST_MAX_TIME = [0.0, 1e-13, 1e-6]


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "ActualActual":
        return ql.ActualActual(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def calendar(spec):
    if spec["type"] == "TARGET":
        return ql.TARGET()
    raise ValueError(f"unknown calendar {spec['type']}")


def rate_of(f, spec):
    """The rate of an InterestRate that f returns with the spec's day counter, compounding and frequency."""
    return v.attempt(lambda: f(day_counter(spec["dayCounter"]), spec["compounding"], spec["frequency"]).rate())


def fixed_cases():
    out = []
    for ref in REFERENCE_DATES:
        st = v.settings(ref)
        for r in FORWARDS:
            for dcn, dcs in DAY_COUNTERS:
                for cn, c in COMPOUNDINGS:
                    for fn, f in FREQUENCIES:
                        ff = ql.FlatForward(ref, r, day_counter(dcs), c, f)
                        ds = [ref + k for k in DATE_OFFSETS]
                        pairs = [(ref + a, ref + b) for a, b in FORWARD_OFFSETS]
                        out.append({
                            "name": f"fixed {ref.ISO()} {r} {dcn} {cn} {fn}",
                            "inputs": {"settings": st, "kind": "fixed",
                                       "curve": {"referenceDate": v.enc(ref), "forward": r, "dayCounter": dcs,
                                                 "compounding": int(c), "frequency": int(f)},
                                       "dates": v.enc(ds), "zeroRateContinuous": ZERO_CONTINUOUS,
                                       "zeroRateSemiannual": ZERO_SEMIANNUAL,
                                       "forwardRate": dict(FORWARD_RATE, dates=[v.enc(list(p)) for p in pairs]),
                                       "zeroRateTimes": dict(ZERO_RATE_TIMES, times=TIMES),
                                       "forwardRateTimes": dict(FORWARD_RATE_TIMES,
                                                                times=[list(p) for p in TIME_PAIRS])},
                            "expected": {
                                "referenceDate": v.enc(ff.referenceDate()),
                                "maxDate": v.enc(ff.maxDate()),
                                "discount": [v.attempt(lambda x=x: ff.discount(x)) for x in ds],
                                "zeroRateContinuous": [rate_of(lambda dc, cc, fq, x=x: ff.zeroRate(x, dc, cc, fq),
                                                               ZERO_CONTINUOUS) for x in ds],
                                "zeroRateSemiannual": [rate_of(lambda dc, cc, fq, x=x: ff.zeroRate(x, dc, cc, fq),
                                                               ZERO_SEMIANNUAL) for x in ds],
                                "forwardRate": [rate_of(lambda dc, cc, fq, a=a, b=b: ff.forwardRate(a, b, dc, cc, fq),
                                                        FORWARD_RATE) for a, b in pairs],
                                "zeroRateTimes": [v.attempt(lambda t=t: ff.zeroRate(
                                    t, ZERO_RATE_TIMES["compounding"], ZERO_RATE_TIMES["frequency"]).rate())
                                                  for t in TIMES],
                                "forwardRateTimes": [v.attempt(lambda a=a, b=b: ff.forwardRate(
                                    a, b, FORWARD_RATE_TIMES["compounding"], FORWARD_RATE_TIMES["frequency"]).rate())
                                                     for a, b in TIME_PAIRS]}})
    return out


def settlement_days_cases():
    out = []
    for ev in EVALUATION_DATES:
        st = v.settings(ev)
        for sd in SETTLEMENT_DAYS:
            ff = ql.FlatForward(sd, calendar(TARGET), 0.03, day_counter(A365), ql.Continuous, ql.Annual)
            ds = [ev + k for k in MOVING_OFFSETS]
            out.append({
                "name": f"settlementDays {ev.ISO()} {sd}",
                "inputs": {"settings": st, "kind": "settlementDays",
                           "curve": {"settlementDays": sd, "calendar": TARGET, "forward": 0.03, "dayCounter": A365,
                                     "compounding": int(ql.Continuous), "frequency": int(ql.Annual)},
                           "dates": v.enc(ds)},
                "expected": {"referenceDate": v.enc(ff.referenceDate()),
                             "discount": [v.attempt(lambda x=x: ff.discount(x)) for x in ds]}})
    return out


def range_cases():
    out = []
    for ref, r, dcs, c, f in RANGE_CURVES:
        st = v.settings(ref)
        ff = ql.FlatForward(ref, r, day_counter(dcs), c, f)
        t_max = ff.maxTime()
        ts = [t_max + dt for dt in PAST_MAX_TIME]
        expected = {"maxTime": t_max,
                    "discountAtTimes": [v.attempt(lambda t=t: ff.discount(t)) for t in ts],
                    "discountAtTimesExtrapolating": [v.attempt(lambda t=t: ff.discount(t, True)) for t in ts],
                    "allowsExtrapolation": ff.allowsExtrapolation()}
        ff.enableExtrapolation()
        expected["allowsAfterEnable"] = ff.allowsExtrapolation()
        expected["discountAfterEnable"] = v.attempt(lambda: ff.discount(ts[-1]))
        ff.disableExtrapolation()
        expected["allowsAfterDisable"] = ff.allowsExtrapolation()
        expected["discountAfterDisable"] = v.attempt(lambda: ff.discount(ts[-1]))
        out.append({
            "name": f"range {ref.ISO()} {r} {ff.dayCounter().name()} {NAMES['c', c]} {NAMES['f', f]}",
            "inputs": {"settings": st, "kind": "range",
                       "curve": {"referenceDate": v.enc(ref), "forward": r, "dayCounter": dcs, "compounding": int(c),
                                 "frequency": int(f)},
                       "times": ts},
            "expected": expected})
    return out


def cases():
    return fixed_cases() + settlement_days_cases() + range_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/flatforward.hpp", cases)
