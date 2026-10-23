# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardSpreadedTermStructure (ql/termstructures/yield/forwardspreadedtermstructure.hpp): a
spread added to the instantaneous forward rate of an original curve, through the YieldTermStructure and TermStructure
interface the spreaded curve inherits.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each spread by 1%, -0.5% and 0. A case is one original under one spread. Its outputs are the
spreaded curve's reference date, day counter (its name), calendar (its name, or null for QuantLib's empty calendar),
maximum date and time, and whether it allows extrapolation; and at the zero-spreaded generator's 30 dates its discount,
its Actual/365 (Fixed) continuous zero rate, its Actual/365 (Fixed) semiannual forward rate over the six months after
the date, and its instantaneous Actual/365 (Fixed) continuous forward rate. A refused value is {"throws": true}.

Each case's inputs hold its settings, the "original" (the zero-spreaded generator's spec), the "spread" and the
"dates". Read back by tests/NanoQL.Tests/Vectors/TermStructures/Yield/ForwardSpreadedTermStructureVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


zs = _load("zerospreadedtermstructure", "termstructures/yield/zerospreadedtermstructure.py")

SPREADS = [0.01, -0.005, 0.0]


def curve_outputs(curve, dates):
    """A curve's inspectors and its reads at the dates, as the derived-curve generators record them."""
    a365 = ql.Actual365Fixed()
    cal = curve.calendar()
    return {
        "referenceDate": v.attempt(curve.referenceDate),
        "dayCounter": curve.dayCounter().name(),
        "calendar": None if cal.empty() else cal.name(),
        "maxDate": v.attempt(curve.maxDate),
        "maxTime": v.attempt(curve.maxTime),
        "allowsExtrapolation": curve.allowsExtrapolation(),
        "discount": [v.attempt(lambda d=d: curve.discount(d)) for d in dates],
        "zeroRate": [v.attempt(lambda d=d: curve.zeroRate(d, a365, ql.Continuous, ql.Annual).rate()) for d in dates],
        "forwardRate": [v.attempt(lambda d=d: curve.forwardRate(
            d, d + ql.Period(6, ql.Months), a365, ql.Compounded, ql.Semiannual).rate()) for d in dates],
        "instantaneousForward": [v.attempt(lambda d=d: curve.forwardRate(
            d, d, a365, ql.Continuous, ql.NoFrequency).rate()) for d in dates],
    }


def cases():
    out = []
    for name, spec in zs.ORIGINALS:
        for spread in SPREADS:
            with v.case_settings(zs.TODAY) as st:
                base = zs.original(spec)
                spreaded = ql.ForwardSpreadedTermStructure(ql.YieldTermStructureHandle(base),
                                                           ql.QuoteHandle(ql.SimpleQuote(spread)))
                dates = zs.query_dates(base, spec)
                out.append({"name": f"{name} / {spread}",
                            "inputs": {"settings": st, "original": spec, "spread": spread, "dates": v.enc(dates)},
                            "expected": curve_outputs(spreaded, dates)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/forwardspreadedtermstructure.hpp", cases)
