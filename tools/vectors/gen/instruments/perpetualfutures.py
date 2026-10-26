# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's PerpetualFutures (ql/instruments/perpetualfutures.hpp, .cpp) priced by its
DiscountingPerpetualFuturesEngine (ql/pricingengines/futures/discountingperpetualfuturesengine.hpp, .cpp), on the
evaluation date 15 October 2026.

Linear and inverse futures funded on the previous and the current spot, quarterly, yearly, fortnightly, daily, every
eight hours and continuously, on flat and linear zero domestic and foreign curves (to 61 years), with one funding rate
and with a term of funding rates and interest-rate differences to the horizon (no case extrapolates a term)
interpolated piecewise constant, linearly and by a natural cubic spline, at horizons of 1 to 60 years (continuous
funding on a piecewise-constant term left out: QuantLib's trapezoid rule refines toward its 2^30 cap across each jump,
about two minutes a case): the NPV, and isExpired. Every curve is dated on the evaluation date and counted
on the futures' own day counter, Actual/Actual (ISDA), where QuantLib's grid times and nanoql's agree (the curves on
other dates or day counters are tests/NanoQL.Tests/Divergences/PerpetualFuturesMismatchedTimesTests.cs's); and every
tail converges, k + r_f - r_d > 0 continuously and (1 + k) e^(dt (r_f - r_d)) > 1 discretely on the last rates, each
case's margin asserted, since below it QuantLib returns the divergent series' continuation and nanoql refuses
(PerpetualFuturesDivergentTailTests.cs). Then QuantLib's constructor checks: the funding rates and the differences of
another length than the times.

A case holds one futures {"payoffType" (QuantLib's Linear 0, Inverse 1), "fundingType" (FundingWithPreviousSpot 0,
FundingWithCurrentSpot 1), "fundingFrequency" {"length", "units"}, "dayCounter"}, the engine {"domestic", "foreign"
(blackscholesprocess.py's curve specs), "spot", "fundingTimes", "fundingRates", "interestRateDiffs",
"interpolation" (PiecewiseConstant 0, Linear 1, CubicSpline 2), "maxT"}. The wheel's calls, by QuantLib-SWIG 1.44's
futures.i: PerpetualFutures(payoffType, fundingType, fundingFrequency, NullCalendar(), dayCounter),
DiscountingPerpetualFuturesEngine(domestic, foreign, spot, fundingTimes, fundingRates, interestRateDiffs,
interpolation, maxT); NPV and isExpired the instrument's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/PerpetualFuturesVectorTests.cs."""
import importlib.util
import math
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bsp.R
ISDA = bsp.bvts.ACT_ISDA
LINEAR, INVERSE = int(ql.PerpetualFutures.Linear), int(ql.PerpetualFutures.Inverse)
PREVIOUS, CURRENT = int(ql.PerpetualFutures.FundingWithPreviousSpot), int(ql.PerpetualFutures.FundingWithCurrentSpot)
INTERPOLATIONS = [("piecewise constant", int(ql.DiscountingPerpetualFuturesEngine.PiecewiseConstant)),
                  ("linear", int(ql.DiscountingPerpetualFuturesEngine.Linear)),
                  ("cubic spline", int(ql.DiscountingPerpetualFuturesEngine.CubicSpline))]

MARKETS = [
    ("flat 4% and 2%", bsp.flat_spec(R, 0.04, ISDA), bsp.flat_spec(R, 0.02, ISDA)),
    ("flat 1% and 3%", bsp.flat_spec(R, 0.01, ISDA), bsp.flat_spec(R, 0.03, ISDA)),
    ("zero curves", bsp.zero_spec(R, [0, 365, 730, 1826, 3652, 10957, 22281],
                                  [0.030, 0.034, 0.037, 0.040, 0.042, 0.043, 0.043],
                                  ISDA),
     bsp.zero_spec(R, [0, 365, 1096, 3652, 10957, 22281], [0.020, 0.022, 0.025, 0.028, 0.030, 0.030],
                     ISDA)),
]

# (name, length, units, period in years, the funding rate per period, the horizon)
FREQUENCIES = [
    ("quarterly", 3, ql.Months, 0.25, 0.01, 60.0),
    ("yearly", 1, ql.Years, 1.0, 0.04, 60.0),
    ("fortnightly", 2, ql.Weeks, 14.0 / 365.0, 0.002, 10.0),
    ("daily", 1, ql.Days, 1.0 / 365.0, 0.0003, 3.0),
    ("eight-hourly", 8, ql.Hours, 8.0 / 8760.0, 0.0001, 1.0),
    ("continuous", 0, ql.Months, 0.0, 0.06, 60.0),
]
# a term's times as fractions of the horizon, its rates and differences as multiples of the period's rate
TERM_TIMES = [0.0, 0.1, 0.2, 0.4, 1.0]
TERM_RATES = [1.2, 1.1, 1.0, 1.05, 1.05]
TERM_DIFFS = [0.4, 0.5, 0.5, 0.45, 0.45]
INTERPOLATORS = [ql.BackwardFlatInterpolation, ql.LinearInterpolation, ql.CubicNaturalSpline]


def futures_spec(payoff, funding, length, units):
    return {"payoffType": payoff, "fundingType": funding, "fundingFrequency": {"length": length, "units": int(units)},
            "dayCounter": ISDA}


def engine_spec(dom, fore, times, rates, diffs, interpolation, max_t, spot=10000.0):
    return {"domestic": dom, "foreign": fore, "spot": spot, "fundingTimes": list(times), "fundingRates": list(rates),
            "interestRateDiffs": list(diffs), "interpolation": interpolation, "maxT": max_t}


def converges(payoff, dom, fore, period, espec):
    """The tail's convergence on the last rates, with a margin; the current spot's adjustment moves k by e^(dt dr)."""
    max_t = espec["maxT"]
    times, rates = espec["fundingTimes"], espec["fundingRates"]
    k = rates[0] if len(times) == 1 else INTERPOLATORS[espec["interpolation"]](times, rates)(max_t, True)
    d, f = (dom, fore) if payoff == LINEAR else (fore, dom)
    rd = bsp.curve(d).forwardRate(max_t, max_t, ql.Continuous, ql.NoFrequency).rate()
    rf = bsp.curve(f).forwardRate(max_t, max_t, ql.Continuous, ql.NoFrequency).rate()
    if period == 0.0:
        return k + rf - rd > 0.1 * k
    k_eff = k * math.exp(period * (rd - rf))
    return (1.0 + k_eff) * math.exp(period * (rf - rd)) > 1.0 + 0.1 * k


def priced(fspec, espec):
    fr = fspec["fundingFrequency"]
    f = ql.PerpetualFutures(fspec["payoffType"], fspec["fundingType"], ql.Period(fr["length"], fr["units"]),
                            ql.NullCalendar(), bsp.bvts.day_counter(fspec["dayCounter"]))
    dom = ql.YieldTermStructureHandle(bsp.curve(espec["domestic"]))
    fore = ql.YieldTermStructureHandle(bsp.curve(espec["foreign"]))
    spot = ql.QuoteHandle(ql.SimpleQuote(espec["spot"]))
    try:
        f.setPricingEngine(ql.DiscountingPerpetualFuturesEngine(
            dom, fore, spot, espec["fundingTimes"], espec["fundingRates"], espec["interestRateDiffs"],
            espec["interpolation"], espec["maxT"]))
    except RuntimeError:
        return {"NPV": dict(v.THROWS), "isExpired": f.isExpired()}
    return {"NPV": f.NPV(), "isExpired": f.isExpired()}


def cases():
    st = v.settings(R)
    out = []
    for mname, dom, fore in MARKETS:
        for fname, length, units, period, k, max_t in FREQUENCIES:
            for payoff, pname in [(LINEAR, "linear"), (INVERSE, "inverse")]:
                fundings = [(PREVIOUS, "previous spot")] + ([(CURRENT, "current spot")] if period > 0.0 else [])
                for funding, gname in fundings:
                    fspec = futures_spec(payoff, funding, length, units)
                    terms = [("one rate", engine_spec(dom, fore, [0.0], [k], [0.4 * k], INTERPOLATIONS[0][1], max_t))]
                    for iname, interpolation in INTERPOLATIONS[1:] if period == 0.0 else INTERPOLATIONS:
                        terms.append((f"a term, {iname}", engine_spec(
                            dom, fore, [max_t * x for x in TERM_TIMES], [k * x for x in TERM_RATES],
                            [k * x for x in TERM_DIFFS], interpolation, max_t)))
                    for tname, espec in terms:
                        assert converges(payoff, dom, fore, period, espec), (mname, fname, pname, tname)
                        out.append({"name": f"{mname}, {fname}, {pname}, {gname}, {tname}",
                                    "inputs": {"settings": st, "futures": fspec, "engine": espec},
                                    "expected": priced(fspec, espec)})
    dom, fore = MARKETS[0][1], MARKETS[0][2]
    fspec = futures_spec(LINEAR, PREVIOUS, 3, ql.Months)
    for name, espec in [
            ("funding rates of another length", engine_spec(dom, fore, [0.0, 1.0], [0.01], [0.004, 0.004], 0, 60.0)),
            ("differences of another length", engine_spec(dom, fore, [0.0, 1.0], [0.01, 0.01], [0.004], 0, 60.0))]:
        out.append({"name": name, "inputs": {"settings": st, "futures": fspec, "engine": espec},
                    "expected": priced(fspec, espec)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/perpetualfutures.hpp", cases)
