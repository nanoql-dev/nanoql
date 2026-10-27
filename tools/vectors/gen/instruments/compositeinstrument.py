# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CompositeInstrument (ql/instruments/compositeinstrument.hpp, .cpp): the sum of its components'
values, each times its multiplier.

Composites of European options on one underlying (spot 100, a 3% risk-free and 1% dividend flat curve and a 20% flat
volatility, Actual/365, on the evaluation date of 15 January 2025), each priced by the analytic European engine:
an empty composite; one call; a call spread (a call added, another subtracted); a straddle with multipliers 2 and 0.5;
a butterfly of three calls with multipliers 1, -2 and 1 through add and subtract; a composite with an expired option
among live ones; and one of expired options only. Each composite's NPV and whether it has expired. A case's inputs are
the "components", each {"type", "strike", "maturityDays" (from the evaluation date, negative for an expired option),
"multiplier", "subtract"}, an option added with its multiplier or subtracted with it. The wheel's CompositeInstrument
takes add(instrument, multiplier) and subtract(instrument, multiplier), each option carrying its engine. Read back by
tests/NanoQL.Tests/Vectors/Instruments/CompositeInstrumentVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def component(option_type, strike, days, multiplier=1.0, subtract=False):
    return {"type": option_type, "strike": strike, "maturityDays": days, "multiplier": multiplier,
            "subtract": subtract}


COMPOSITES = [
    ("empty", []),
    ("one call", [component(CALL, 100.0, 180)]),
    ("call spread", [component(CALL, 95.0, 180), component(CALL, 110.0, 180, subtract=True)]),
    ("straddle", [component(CALL, 100.0, 365, 2.0), component(PUT, 100.0, 365, 0.5)]),
    ("butterfly", [component(CALL, 90.0, 90), component(CALL, 100.0, 90, 2.0, True), component(CALL, 110.0, 90)]),
    ("an expired option among live ones", [component(PUT, 105.0, -10), component(CALL, 100.0, 30, 3.0)]),
    ("expired options only", [component(CALL, 100.0, -1), component(PUT, 95.0, -30, 2.0, True)]),
]


def cases():
    out = []
    dc = ql.Actual365Fixed()
    with v.case_settings(TODAY) as st:
        process = ql.BlackScholesMertonProcess(
            ql.QuoteHandle(ql.SimpleQuote(100.0)),
            ql.YieldTermStructureHandle(ql.FlatForward(TODAY, 0.01, dc)),
            ql.YieldTermStructureHandle(ql.FlatForward(TODAY, 0.03, dc)),
            ql.BlackVolTermStructureHandle(ql.BlackConstantVol(TODAY, ql.NullCalendar(), 0.2, dc)))
        engine = ql.AnalyticEuropeanEngine(process)
        for name, components in COMPOSITES:
            composite = ql.CompositeInstrument()
            keep = []
            for c in components:
                option = ql.EuropeanOption(ql.PlainVanillaPayoff(c["type"], c["strike"]),
                                           ql.EuropeanExercise(TODAY + c["maturityDays"]))
                option.setPricingEngine(engine)
                keep.append(option)
                if c["subtract"]:
                    composite.subtract(option, c["multiplier"])
                else:
                    composite.add(option, c["multiplier"])
            out.append({"name": name, "inputs": {"settings": st, "components": components},
                        "expected": {"NPV": composite.NPV(), "isExpired": composite.isExpired()}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/compositeinstrument.hpp", cases)
