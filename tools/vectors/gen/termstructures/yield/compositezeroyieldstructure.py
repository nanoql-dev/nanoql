# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CompositeZeroYieldStructure (ql/termstructures/yield/compositezeroyieldstructure.hpp): a
zero curve whose rate is a function of two curves' zero rates, in a given compounding and frequency.

Pairs of curves sharing their reference date and day counter, under the evaluation date Monday 16 June 2025, from the
originals of tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py and three flat curves beside them:
the flat 3% Actual/365 (Fixed) curve with the log-linear discount curve, either way round, and with the discount curve
with jumps; the linear zero curve on Actual/360 with a flat 2% Actual/360 curve; the backward-flat forward curve on
30/360 (Bond Basis) with a flat 2.5% 30/360 curve. Each pair under four functions of the two rates (their sum, their
difference, their mean and their maximum), named in the case, and two settings of compounding and frequency, by
rotation from continuous, annually compounded, semiannually compounded and simple. A case is one pair under one
function and one setting. Its outputs are the composite's reference date, day counter (its name), calendar (the first
curve's name, or null), maximum date and time, and whether it allows extrapolation; and at the first curve's 30 query
dates (the zero-spreaded generator's), its discount, its Actual/365 (Fixed) continuous zero rate, its Actual/365
(Fixed) semiannual forward rate over the six months after the date and its instantaneous continuous forward rate. A
refused value is {"throws": true}.

Left out (the S-sr plan's SSR6 (xv)): composites of two curves on different clocks, where QuantLib reads the second
curve at the first's time and nanoql at the instant it denotes (tests/NanoQL.Tests/Divergences/CompositeClockTests.cs).

Each case's inputs hold its settings, the two curves' specs ("curve1", "curve2", the zero-spreaded generator's), the
"function", the "compounding" and "frequency" as QuantLib's integers, and the "dates". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/CompositeZeroYieldStructureVectorTests.cs."""
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

FUNCTIONS = {"add": lambda a, b: a + b, "sub": lambda a, b: a - b, "mean": lambda a, b: (a + b) / 2.0,
             "max": lambda a, b: max(a, b)}
SETTINGS = [(ql.Continuous, ql.NoFrequency), (ql.Compounded, ql.Annual), (ql.Compounded, ql.Semiannual),
            (ql.Simple, ql.Annual)]

ORIGINALS = dict(zs.ORIGINALS)
FLAT_A360 = zs.flat(0.02, zs.A360, ql.Continuous, ql.Annual, False, reference=zs.TODAY)
FLAT_30360 = zs.flat(0.025, zs.THIRTY360_BB, ql.Continuous, ql.Annual, False, reference=zs.TODAY)
PAIRS = [
    ("flat 3% / log-linear discount", ORIGINALS["flat 3% continuous Actual/365"],
     ORIGINALS["log-linear discount curve"]),
    ("log-linear discount / flat 3%", ORIGINALS["log-linear discount curve"],
     ORIGINALS["flat 3% continuous Actual/365"]),
    ("discount with jumps / flat 3%", ORIGINALS["log-linear discount curve with jumps"],
     ORIGINALS["flat 3% continuous Actual/365"]),
    ("linear zero / flat 2% Actual/360", ORIGINALS["linear zero curve"], FLAT_A360),
    ("backward-flat forward / flat 2.5% 30/360", ORIGINALS["backward-flat forward curve"], FLAT_30360),
]


def cases():
    out = []
    for k, (name, spec1, spec2) in enumerate(PAIRS):
        for j, fname in enumerate(FUNCTIONS):
            for comp, freq in (SETTINGS[(k + j) % 4], SETTINGS[(k + j + 1) % 4]):
                with v.case_settings(zs.TODAY) as st:
                    c1, c2 = zs.original(spec1), zs.original(spec2)
                    composite = ql.CompositeZeroYieldStructure(ql.YieldTermStructureHandle(c1),
                                                               ql.YieldTermStructureHandle(c2), FUNCTIONS[fname],
                                                               comp, freq)
                    dates = zs.query_dates(c1, spec1)
                    out.append({"name": f"{name} / {fname} {comp} {freq}",
                                "inputs": {"settings": st, "curve1": spec1, "curve2": spec2, "function": fname,
                                           "compounding": int(comp), "frequency": int(freq), "dates": v.enc(dates)},
                                "expected": fs.curve_outputs(composite, dates)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/compositezeroyieldstructure.hpp", cases)
