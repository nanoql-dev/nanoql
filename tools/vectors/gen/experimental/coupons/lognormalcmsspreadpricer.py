# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's lognormal CMS spread pricer (ql/experimental/coupons/lognormalcmsspreadpricer.hpp, .cpp) on
QuantLib's cmsspread test's market (test-suite/cmsspread.cpp's TestData: 23 February 2018, a flat 2% curve, constant
swaption volatilities of 20% lognormal, 10% shifted by 1% and 0.75% normal, linear TSR pricers at a reversion of 1%).

- "coupon" cases: a CMS 10Y-2Y spread coupon of 10,000 fixing in ten years, priced on each volatility at correlations
  0.6, −0.4 and 0.9999 (the clamp's edge), at 32 and 16 integration points, with and without a coupon discount curve:
  the coupon's rate and price, and the pricer's caplet and floorlet rates and prices at five strikes, from −1% to 2%.
  Equal shifts only: QuantLib 1.44's pricer with unequal shifts is #2874's, nanoql's master's
  (Divergences/CmsSpreadUnequalShiftsTests.cs).
- "fixed" case: a coupon fixing on the evaluation date, its index fixings stored, the caplet and floorlet on them,
  rates and prices.
- "refusal" cases: an explicit volatility type on an at-the-money surface ("if only an atm surface is given, the
  volatility type must be inherited") and shifts with an inherited type, QuantLib's texts.

The wheel's pricer has no initialize: its option rates read the coupon last priced, as test-suite/cmsspread.cpp's
#2874 case reads them after coupon.rate(). Read back by
tests/NanoQL.Tests/Vectors/Experimental/Coupons/LognormalCmsSpreadPricerVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

REF = ql.Date(23, ql.February, 2018)
STRIKES = [-0.01, 0.0, 0.005, 0.01, 0.02]
VOLS = {"lognormal": (0.20, ql.ShiftedLognormal, 0.0), "shifted": (0.10, ql.ShiftedLognormal, 0.01),
        "normal": (0.0075, ql.Normal, 0.01)}


def market():
    yts2 = ql.YieldTermStructureHandle(ql.FlatForward(REF, 0.02, ql.Actual365Fixed()))
    cms10y = ql.EuriborSwapIsdaFixA(ql.Period(10, ql.Years), yts2, yts2)
    cms2y = ql.EuriborSwapIsdaFixA(ql.Period(2, ql.Years), yts2, yts2)
    return yts2, cms10y, cms2y, ql.SwapSpreadIndex("cms10y2y", cms10y, cms2y)


def cms_pricer(vol, yts2):
    level, kind, shift = VOLS[vol]
    sw = ql.SwaptionVolatilityStructureHandle(ql.ConstantSwaptionVolatility(
        REF, ql.TARGET(), ql.Following, level, ql.Actual365Fixed(), kind, shift))
    return ql.LinearTsrPricer(sw, ql.QuoteHandle(ql.SimpleQuote(0.01)), yts2)


def coupon(index, pay, start, end):
    return ql.CmsSpreadCoupon(pay, 10000.0, start, end, 2, index, 1.0, 0.0, ql.Date(), ql.Date(), ql.Actual360(),
                              False)


def cases():
    out = []
    ql.Settings.instance().evaluationDate = REF
    st = v.settings(REF)
    yts2, cms10y, cms2y, index = market()
    pay, start, end = ql.Date(23, ql.February, 2029), ql.Date(23, ql.February, 2028), ql.Date(23, ql.February, 2029)
    configs = [(vol, rho, points, discounted) for vol in VOLS for rho in (0.6, -0.4, 0.9999) for points in (32, 16)
               for discounted in (True, False) if points == 32 or (rho == 0.6 and not discounted)]
    for vol, rho, points, discounted in configs:
        pricer = ql.LognormalCmsSpreadPricer(
            cms_pricer(vol, yts2), ql.QuoteHandle(ql.SimpleQuote(rho)),
            yts2 if discounted else ql.YieldTermStructureHandle(), points)
        c = coupon(index, pay, start, end)
        c.setPricer(pricer)
        rate, price = c.rate(), c.price(yts2)
        out.append({"name": f"coupon: {vol}, rho {rho}, {points} points, discount curve {discounted}",
                    "inputs": {"settings": st, "vol": vol, "correlation": rho, "points": points,
                               "discounted": discounted, "strikes": STRIKES},
                    "expected": {"rate": rate, "price": price,
                                 "caplets": [pricer.capletRate(k) for k in STRIKES],
                                 "floorlets": [pricer.floorletRate(k) for k in STRIKES],
                                 "capletPrices": [pricer.capletPrice(k) for k in STRIKES],
                                 "floorletPrices": [pricer.floorletPrice(k) for k in STRIKES]}})
    # a coupon fixing on the evaluation date, on stored fixings
    fixed_st = v.settings(REF, fixings=[(cms10y, [(REF, 0.031)]), (cms2y, [(REF, 0.021)])])
    value_date = index.valueDate(REF)
    pricer = ql.LognormalCmsSpreadPricer(cms_pricer("lognormal", yts2), ql.QuoteHandle(ql.SimpleQuote(0.6)), yts2, 32)
    c = ql.CmsSpreadCoupon(value_date + ql.Period(1, ql.Years), 10000.0, value_date,
                           value_date + ql.Period(1, ql.Years), 2, index, 1.0, 0.0, ql.Date(), ql.Date(),
                           ql.Actual360(), False)
    c.setPricer(pricer)
    out.append({"name": "fixed: on the evaluation date",
                "inputs": {"settings": fixed_st, "strikes": STRIKES},
                "expected": {"rate": c.rate(), "caplets": [pricer.capletRate(k) for k in STRIKES],
                             "floorlets": [pricer.floorletRate(k) for k in STRIKES],
                             "capletPrices": [pricer.capletPrice(k) for k in STRIKES],
                             "floorletPrices": [pricer.floorletPrice(k) for k in STRIKES]}})
    ql.IndexManager.instance().clearHistories()
    out.append({"name": "refusal: an explicit type on an at-the-money surface",
                "inputs": {"settings": st},
                "expected": {"rate": v.attempt(lambda: _explicit(index, yts2, pay, start, end))}})
    out.append({"name": "refusal: shifts with an inherited type",
                "inputs": {"settings": st},
                "expected": {"construction": v.attempt(lambda: ql.LognormalCmsSpreadPricer(
                    cms_pricer("lognormal", yts2), ql.QuoteHandle(ql.SimpleQuote(0.6)), yts2, 16, None, 0.01,
                    0.01).correlation().value())}})
    return out


def _explicit(index, yts2, pay, start, end):
    pricer = ql.LognormalCmsSpreadPricer(cms_pricer("lognormal", yts2), ql.QuoteHandle(ql.SimpleQuote(0.6)), yts2, 16,
                                         ql.ShiftedLognormal, 0.01, 0.01)
    c = coupon(index, pay, start, end)
    c.setPricer(pricer)
    return c.rate()


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/coupons/lognormalcmsspreadpricer.hpp", cases)
