# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ImpliedTermStructure (ql/termstructures/yield/impliedtermstructure.hpp): the curve an
original implies at a later reference date, through the YieldTermStructure and TermStructure interface it inherits.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each implied at its own reference date, 1 year and 3 years later. A case is one original at one
implied date. Its outputs are the implied curve's reference date, day counter (its name), calendar (null, QuantLib's
empty calendar), maximum date and time, and whether it allows extrapolation; its discount at 17 times, from 0 to 25
years; and at 30 dates from its reference date, to 30 years after it or 25 to the original's last node and 5 past it,
its discount, its Actual/365 (Fixed) continuous zero rate, its Actual/365 (Fixed) semiannual forward rate over the six
months after the date and its instantaneous continuous forward rate. A refused value is {"throws": true}.

Left out (the S-sr plan's SSR6 (viii)): the date reads of the original on 30/360 (Bond Basis), whose year fractions
don't add, where QuantLib reads the original at the implied time plus the reference date's and misses the original's
ratio P(d)/P(ref) by date, which nanoql reads there (tests/NanoQL.Tests/Divergences/ImpliedTermStructureClockTests.cs);
its time reads stay, QuantLib's sum.

Each case's inputs hold its settings, the "original" (the zero-spreaded generator's spec), the "referenceDate", the
"times" and the "dates" (empty where left out). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/ImpliedTermStructureVectorTests.cs."""
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
fs = _load("forwardspreadedtermstructure", "termstructures/yield/forwardspreadedtermstructure.py")

OFFSETS = [ql.Period(0, ql.Days), ql.Period(1, ql.Years), ql.Period(3, ql.Years)]
TIMES = [0.0, 0.01, 0.1, 0.25, 0.5, 0.75, 1.0, 1.5, 2.0, 3.0, 4.0, 5.0, 7.0, 10.0, 15.0, 20.0, 25.0]


def query_dates(implied, spec):
    """30 dates from the implied reference date: to 30 years after it, or 25 to the last node and 5 past it."""
    reference = implied.referenceDate()
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
    for name, spec in zs.ORIGINALS:
        additive = spec["dayCounter"]["type"] != "Thirty360"
        for offset in OFFSETS:
            with v.case_settings(zs.TODAY) as st:
                base = zs.original(spec)
                reference = base.referenceDate() + offset
                implied = ql.ImpliedTermStructure(ql.YieldTermStructureHandle(base), reference)
                dates = query_dates(implied, spec) if additive else []
                expected = fs.curve_outputs(implied, dates)
                expected["discountAtTime"] = [v.attempt(lambda t=t: implied.discount(t)) for t in TIMES]
                out.append({"name": f"{name} / implied {offset}",
                            "inputs": {"settings": st, "original": spec, "referenceDate": v.enc(reference),
                                       "times": TIMES, "dates": v.enc(dates)},
                            "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/impliedtermstructure.hpp", cases)
