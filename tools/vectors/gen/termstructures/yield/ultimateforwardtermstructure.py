# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's UltimateForwardTermStructure (ql/termstructures/yield/ultimateforwardtermstructure.hpp): an
original curve to a first smoothing point, and past it a forward rate extrapolated from a last liquid forward rate
towards an ultimate forward rate, its zero rates optionally rounded in a given compounding.

The six originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py, under its evaluation date
Monday 16 June 2025, each under four settings (LLFR, UFR, first smoothing point, α, rounding digits, compounding,
frequency): 3%, 3.6%, 20 years, 0.1, none, annually compounded, QuantLib's defaults for the last three; 2.5%, 4.2%,
20 years, 0.15, 3 digits, annually compounded; 2%, 2.3%, 20 years, 0.2, 5 digits, continuous; 3.5%, 3%, 30 years,
0.05, 4 digits, semiannually compounded. On the two originals that don't extrapolate, the first smoothing point is 5
years, inside their range.

A case is one original under one setting. Its outputs are the curve's reference date, day counter (its name), calendar
(its name, or null), maximum date and time, and whether it allows extrapolation; and at 32 dates from the reference
date to a hundred years after it its discount, Actual/365 (Fixed) continuous zero rate, semiannual forward rate over the
six months after the date and instantaneous continuous forward rate. A refused value is {"throws": true}.

Left out: α = 0 (the S-sr plan's SSR6 (xviii)), where QuantLib's kernel (1 − e^(−αx))/(αx) is 0/0 and nanoql's is its
limit (tests/NanoQL.Tests/Divergences/UfrSmallAlphaTests.cs); and every read past an original that doesn't
extrapolate (SSR6 (xxi)), where QuantLib reads it without the extrapolation flag and nanoql with
(tests/NanoQL.Tests/Divergences/DerivedCurveExtrapolationTests.cs). The kernel is upstream's after the pin,
−expm1(−αx)/(αx), so the values agree within R7, not bit for bit.

Each case's inputs hold its settings, the "original" (the zero-spreaded generator's spec), the "llfr", "ufr",
"firstSmoothingPoint" ([length, units]), "alpha", "roundingDigits" (null for none), "compounding" and "frequency" as
QuantLib's integers, and the "dates". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/UltimateForwardTermStructureVectorTests.cs."""
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

SETTINGS = [
    (0.03, 0.036, (20, ql.Years), 0.1, None, ql.Compounded, ql.Annual),
    (0.025, 0.042, (20, ql.Years), 0.15, 3, ql.Compounded, ql.Annual),
    (0.02, 0.023, (20, ql.Years), 0.2, 5, ql.Continuous, ql.NoFrequency),
    (0.035, 0.03, (30, ql.Years), 0.05, 4, ql.Compounded, ql.Semiannual),
]
YEARS = [0, 0.25, 0.5, 1, 2, 3, 4, 5, 6, 7, 8, 10, 12, 15, 18, 19, 20, 21, 22, 25, 28, 30, 31, 35, 40, 45, 50, 60, 70, 80,
         90, 100]


def cases():
    out = []
    for name, spec in zs.ORIGINALS:
        inside = spec["extrapolate"] or spec["type"] == "FlatForward"
        for llfr, ufr, fsp, alpha, digits, comp, freq in SETTINGS:
            if not inside:
                fsp = (5, ql.Years)
            with v.case_settings(zs.TODAY) as st:
                base = zs.original(spec)
                curve = ql.UltimateForwardTermStructure(ql.YieldTermStructureHandle(base),
                                                        ql.QuoteHandle(ql.SimpleQuote(llfr)),
                                                        ql.QuoteHandle(ql.SimpleQuote(ufr)), ql.Period(*fsp), alpha,
                                                        digits, comp, freq)
                reference = base.referenceDate()
                dates = [reference + int(round(y * 365.25)) for y in YEARS]
                if not inside:
                    dates = [d for d in dates if d <= base.maxDate()]
                out.append({"name": f"{name} / {llfr} {ufr} {fsp[0]}Y {alpha} {digits} {comp} {freq}",
                            "inputs": {"settings": st, "original": spec, "llfr": llfr, "ufr": ufr,
                                       "firstSmoothingPoint": [fsp[0], int(fsp[1])], "alpha": alpha,
                                       "roundingDigits": digits, "compounding": int(comp), "frequency": int(freq),
                                       "dates": v.enc(dates)},
                            "expected": fs.curve_outputs(curve, dates)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/ultimateforwardtermstructure.hpp", cases)
