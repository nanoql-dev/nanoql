# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BlackDeltaCalculator (ql/pricingengines/blackdeltacalculator.hpp, .cpp): deltas from strikes,
strikes from deltas, and the at-the-money strikes.

Calls and puts under each delta convention (spot, forward, and both premium-adjusted), on an FX-like and an equity-like
market (spot 1.3 and 100, two pairs of discount factors) at standard deviations of 5, 20 and 50%: the delta at strikes
of 80, 100 and 125% of the forward; the strike at deltas of 0.1, 0.25, 0.5 and 0.75 in size (signed by the type),
QuantLib's refusals of an incoherent or out-of-range delta recorded; and the strike at each ATM convention (QuantLib
refusing AtmNull, and AtmPutCall50 but under forward deltas).

The outputs nanoql fixes are left out (the S-rx plan's SRXR3 (xi)): no premium-adjusted put's delta exceeds the
non-adjusted range, the foreign discount factor (spot) or 1 (forward), where QuantLib refuses and nanoql solves
(tests/NanoQL.Tests/Divergences/PremiumAdjustedPutStrikeTests.cs).

**The stability filter** (the S7R6 filter of the S7 plan) on the premium-adjusted strikes, which QuantLib solves with
Brent: each is solved again with its delta moved one ulp up and one down (math.nextafter), and a delta is written only
where both solves give its strike within 1e-12 × max(1, |base|, |moved|). The generator prints the dropped deltas.

A case's inputs are the calculator's "optionType", "deltaType" (QuantLib's DeltaVolQuote::DeltaType: Spot 0, Fwd 1,
PaSpot 2, PaFwd 3), "spot", "dDiscount", "fDiscount" and "stdDev", and the "strikes" and "deltas" it reads; its outputs
the "deltaFromStrike" and "strikeFromDelta" at them and the "atmStrike" at each AtmType from AtmNull 0 to AtmPutCall50
6. The wheel's calls, by QuantLib-SWIG 1.44's: BlackDeltaCalculator(optionType, deltaType, spot, dDiscount, fDiscount,
stdDev) and its deltaFromStrike, strikeFromDelta and atmStrike. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BlackDeltaCalculatorVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TOLERANCE = 1e-12
DROPPED = []
MARKETS = [("FX", 1.3, 0.97, 0.99), ("equity", 100.0, 0.95, 0.98)]
DELTA_TYPES = [("spot", int(ql.DeltaVolQuote.Spot)), ("forward", int(ql.DeltaVolQuote.Fwd)),
               ("premium-adjusted spot", int(ql.DeltaVolQuote.PaSpot)),
               ("premium-adjusted forward", int(ql.DeltaVolQuote.PaFwd))]
PREMIUM_ADJUSTED = (int(ql.DeltaVolQuote.PaSpot), int(ql.DeltaVolQuote.PaFwd))


def stable(calc, delta, base):
    for moved in (math.nextafter(delta, math.inf), math.nextafter(delta, -math.inf)):
        try:
            m = calc.strikeFromDelta(moved)
        except RuntimeError:
            return False
        if abs(m - base) > TOLERANCE * max(1.0, abs(base), abs(m)):
            return False
    return True


def cases():
    st = v.settings(ql.Date(15, ql.October, 2026))
    out = []
    for mname, spot, d_df, f_df in MARKETS:
        forward = spot * f_df / d_df
        for dname, dt in DELTA_TYPES:
            for ot in (int(ql.Option.Call), int(ql.Option.Put)):
                for std in (0.05, 0.2, 0.5):
                    calc = ql.BlackDeltaCalculator(ot, dt, spot, d_df, f_df, std)
                    strikes = [forward * f for f in (0.8, 1.0, 1.25)]
                    deltas, strike_out = [], []
                    for size in (0.1, 0.25, 0.5, 0.75):
                        delta = size * ot
                        # SRXR3 (xi): a premium-adjusted put beyond the non-adjusted range is left out
                        bound = f_df if dt == int(ql.DeltaVolQuote.PaSpot) else 1.0
                        if dt in PREMIUM_ADJUSTED and ot < 0 and abs(delta) > bound:
                            continue
                        try:
                            base = calc.strikeFromDelta(delta)
                        except RuntimeError:
                            deltas.append(delta)
                            strike_out.append(dict(v.THROWS))
                            continue
                        if dt in PREMIUM_ADJUSTED and not stable(calc, delta, base):
                            DROPPED.append(f"{mname}, {dname}, {ot}, {std}: delta {delta!r}")
                            continue
                        deltas.append(delta)
                        strike_out.append(base)
                    # an incoherent delta, the opposite sign: QuantLib's refusal
                    deltas.append(-0.25 * ot)
                    strike_out.append(v.attempt(lambda: calc.strikeFromDelta(-0.25 * ot)))
                    out.append({"name": f"{mname}, {dname}, {'call' if ot > 0 else 'put'}, stdDev {std}",
                                "inputs": {"settings": st, "optionType": ot, "deltaType": dt, "spot": spot,
                                           "dDiscount": d_df, "fDiscount": f_df, "stdDev": std, "strikes": strikes,
                                           "deltas": deltas},
                                "expected": {"deltaFromStrike": [v.attempt(lambda k=k: calc.deltaFromStrike(k))
                                                                 for k in strikes],
                                             "strikeFromDelta": strike_out,
                                             "atmStrike": [v.attempt(lambda a=a: calc.atmStrike(a))
                                                           for a in range(7)]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/blackdeltacalculator.hpp", cases)
    print(f"dropped {len(DROPPED)} deltas")
    for d in DROPPED:
        print(" ", d)
