# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ZeroSpreadedTermStructure (ql/termstructures/yield/zerospreadedtermstructure.hpp): a spread
added to the zero rates of an original curve, in a given compounding and frequency, through the YieldTermStructure and
TermStructure interface the spreaded curve inherits.

Six originals, under the evaluation date Monday 16 June 2025: a flat 3% continuous Actual/365 (Fixed) curve with a
fixed reference date; a flat 2.5% semiannual Actual/360 curve two TARGET business days after the evaluation date
(QuantLib's moving curve, which nanoql builds by FlatForward.Create from the context), with extrapolation enabled; a
log-linear discount curve to 10 years on Actual/365 (Fixed); the same nodes with two discount-factor jumps on TARGET,
with extrapolation enabled; a linear zero curve to 20 years on Actual/360, with extrapolation enabled; and a
backward-flat forward curve to 15 years on 30/360 (Bond Basis). Each is spread under 5 of 10 settings, (spread,
compounding, frequency): QuantLib's default, a 1% continuous spread, on every original, and 4 of the other 9 by
rotation, so each appears on two or three originals: a negative and a zero continuous spread, 1% compounded annually,
0.5% semiannually, -1% quarterly and 1% at QuantLib's OtherFrequency (999 a year), 0.75% simple, and two that QuantLib
builds and refuses at every query that reads a zero rate, compounded with no frequency ("frequency not allowed for
this interest rate") and simple then compounded ("... is not supported for direct calculations").

A case is one original under one setting. Its outputs are the spreaded curve's reference date, day counter (its
name), calendar (its name, or null for QuantLib's empty calendar), maximum date and time, and whether it allows
extrapolation (the original's flag, copied when the spreaded curve is built); and at 30 dates, from the reference date
(where the discount is 1 whatever the setting) to 30 years after it, or, for an original with a last node, 25 to the
node and 5 past it (which an original without extrapolation refuses), its discount, its Actual/365 (Fixed) continuous
zero rate, and its Actual/365 (Fixed) semiannual forward rate over the six months after the date. A refused value is
{"throws": true}.

Each case's inputs hold its settings, the "original" ({"type": "FlatForward", "referenceDate", "forward",
"dayCounter", "compounding", "frequency"}, the same with "settlementDays" and "calendar" in place of the reference
date, or {"type": "DiscountCurve" | "ZeroCurve" | "ForwardCurve", "dates", "values", "dayCounter", "calendar" (null
for QuantLib's empty one), "jumps", "jumpDates" (null for none)}, each with "extrapolate"), the "spread",
"compounding" and "frequency" as QuantLib's integers, and the "dates". The wheel's spreaded curve is built on a handle
of the original and a quote of the spread, positionally, without its fifth argument (the deprecated day counter,
which QuantLib ignores). Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/ZeroSpreadedTermStructureVectorTests.cs."""
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
THIRTY360_BB = {"type": "Thirty360", "convention": int(ql.Thirty360.BondBasis)}
TARGET = {"type": "TARGET"}

TODAY = ql.Date(16, ql.June, 2025)

# (spread, compounding, frequency); the first is QuantLib's default rule with a 1% spread.
SETTINGS = [(0.01, ql.Continuous, ql.NoFrequency), (-0.0025, ql.Continuous, ql.NoFrequency),
            (0.01, ql.Compounded, ql.Annual), (0.005, ql.Compounded, ql.Semiannual),
            (0.0075, ql.Simple, ql.Annual), (0.01, ql.Compounded, ql.OtherFrequency),
            (0.01, ql.Compounded, ql.NoFrequency), (0.0, ql.Continuous, ql.NoFrequency),
            (0.01, ql.SimpleThenCompounded, ql.Annual), (-0.01, ql.Compounded, ql.Quarterly)]


def day_counter(spec):
    t = spec["type"]
    if t == "Actual365Fixed":
        return ql.Actual365Fixed()
    if t == "Actual360":
        return ql.Actual360()
    if t == "Thirty360":
        return ql.Thirty360(spec["convention"])
    raise ValueError(f"unknown day counter {t}")


def calendar(spec):
    if spec["type"] == "TARGET":
        return ql.TARGET()
    raise ValueError(f"unknown calendar {spec['type']}")


def flat(forward, dc, compounding, frequency, extrapolate, reference=None, settlement_days=None, cal=None):
    out = {"type": "FlatForward", "forward": forward, "dayCounter": dc, "compounding": int(compounding),
           "frequency": int(frequency), "extrapolate": extrapolate}
    if reference is not None:
        out["referenceDate"] = v.enc(reference)
    else:
        out["settlementDays"] = settlement_days
        out["calendar"] = cal
    return out


def interpolated(kind, offsets, values, dc, extrapolate, cal=None, jumps=None, jump_dates=None):
    return {"type": kind, "dates": [v.enc(TODAY + n) for n in offsets], "values": values, "dayCounter": dc,
            "calendar": cal, "jumps": jumps, "jumpDates": None if jump_dates is None else v.enc(jump_dates),
            "extrapolate": extrapolate}


DISCOUNT_OFFSETS = [0, 91, 182, 365, 730, 1095, 1826, 2557, 3652]
DISCOUNTS = [1.0, 0.9925, 0.9851, 0.9702, 0.9405, 0.9108, 0.8512, 0.7930, 0.7105]

ORIGINALS = [
    ("flat 3% continuous Actual/365", flat(0.03, A365, ql.Continuous, ql.Annual, False, reference=TODAY)),
    ("flat 2.5% semiannual Actual/360, 2 TARGET days", flat(0.025, A360, ql.Compounded, ql.Semiannual, True,
                                                             settlement_days=2, cal=TARGET)),
    ("log-linear discount curve", interpolated("DiscountCurve", DISCOUNT_OFFSETS, DISCOUNTS, A365, False)),
    ("log-linear discount curve with jumps", interpolated("DiscountCurve", DISCOUNT_OFFSETS, DISCOUNTS, A365, True,
                                                           TARGET, [0.9995, 0.998],
                                                           [ql.Date(31, ql.December, 2025),
                                                            ql.Date(31, ql.December, 2028)])),
    ("linear zero curve", interpolated("ZeroCurve", [0, 182, 365, 1095, 1826, 3652, 7305],
                                       [0.021, 0.022, 0.0245, 0.029, 0.0315, 0.034, 0.0355], A360, True)),
    ("backward-flat forward curve", interpolated("ForwardCurve", [0, 365, 730, 1826, 3652, 5479],
                                                 [0.02, 0.023, 0.027, 0.031, 0.034, 0.036], THIRTY360_BB, False)),
]


def original(spec):
    """The wheel's original curve, its extrapolation enabled as the spec says."""
    dc = day_counter(spec["dayCounter"])
    if spec["type"] == "FlatForward":
        if "referenceDate" in spec:
            c = ql.FlatForward(ql.Date(spec["referenceDate"]), spec["forward"], dc, spec["compounding"],
                               spec["frequency"])
        else:
            c = ql.FlatForward(spec["settlementDays"], calendar(spec["calendar"]), spec["forward"], dc,
                               spec["compounding"], spec["frequency"])
    else:
        cls = {"DiscountCurve": ql.DiscountCurve, "ZeroCurve": ql.ZeroCurve, "ForwardCurve": ql.ForwardCurve}
        dates = [ql.Date(x) for x in spec["dates"]]
        if spec["jumps"] is None:
            assert spec["calendar"] is None
            c = cls[spec["type"]](dates, spec["values"], dc)
        else:
            c = cls[spec["type"]](dates, spec["values"], dc, calendar(spec["calendar"]),
                                  [ql.QuoteHandle(ql.SimpleQuote(x)) for x in spec["jumps"]],
                                  [ql.Date(x) for x in spec["jumpDates"]])
    if spec["extrapolate"]:
        c.enableExtrapolation()
    return c


def query_dates(curve, spec):
    """30 dates from the reference date: to 30 years after it, or 25 to the last node and 5 past it."""
    reference = curve.referenceDate()
    if spec["type"] == "FlatForward":
        offsets = [0, 1, 7, 30, 61, 91, 137, 182, 274, 365, 456, 548, 730, 913, 1095, 1461, 1826, 2191, 2557, 3287,
                   3652, 4383, 5479, 6574, 7305, 8036, 8766, 9131, 10227, 10957]
        return [reference + n for n in offsets]
    span = ql.Date(spec["dates"][-1]) - reference
    inside = [reference + int(round(span * (k / 24.0) ** 1.4)) for k in range(25)]
    past = [ql.Date(spec["dates"][-1]) + n for n in (1, 30, 182, 730, 1826)]
    return inside + past


def cases():
    out = []
    a365 = ql.Actual365Fixed()
    for k, (name, spec) in enumerate(ORIGINALS):
        chosen = [SETTINGS[0]] + [SETTINGS[1 + (4 * k + j) % 9] for j in range(4)]
        for spread, comp, freq in chosen:
            with v.case_settings(TODAY) as st:
                base = original(spec)
                spreaded = ql.ZeroSpreadedTermStructure(ql.YieldTermStructureHandle(base),
                                                        ql.QuoteHandle(ql.SimpleQuote(spread)), comp, freq)
                dates = query_dates(base, spec)
                cal = spreaded.calendar()
                expected = {
                    "referenceDate": v.attempt(spreaded.referenceDate),
                    "dayCounter": spreaded.dayCounter().name(),
                    "calendar": None if cal.empty() else cal.name(),
                    "maxDate": v.attempt(spreaded.maxDate),
                    "maxTime": v.attempt(spreaded.maxTime),
                    "allowsExtrapolation": spreaded.allowsExtrapolation(),
                    "discount": [v.attempt(lambda d=d: spreaded.discount(d)) for d in dates],
                    "zeroRate": [v.attempt(lambda d=d: spreaded.zeroRate(d, a365, ql.Continuous, ql.Annual).rate())
                                 for d in dates],
                    "forwardRate": [v.attempt(lambda d=d: spreaded.forwardRate(
                        d, d + ql.Period(6, ql.Months), a365, ql.Compounded, ql.Semiannual).rate()) for d in dates],
                }
                out.append({"name": f"{name} / {spread} {comp} {freq}",
                            "inputs": {"settings": st, "original": spec, "spread": spread, "compounding": int(comp),
                                       "frequency": int(freq), "dates": v.enc(dates)},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/zerospreadedtermstructure.hpp", cases)
