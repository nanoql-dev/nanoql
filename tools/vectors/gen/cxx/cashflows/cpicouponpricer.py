# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI coupon pricer (ql/cashflows/cpicouponpricer.hpp), each output computed by the C++
harness: the wheel builds the pricer with no arguments and binds none of its members.

- "member" cases: tools/vectors/gen/cxx/cashflows/inflationcoupon.py's CPI coupons, each with a pricer built with and
  without a volatility and a nominal curve, initialized on the coupon: swapletPrice, swapletRate, capletPrice,
  capletRate, floorletPrice and floorletRate at two strikes, accruedRate at two dates, and whether capletVolatility
  and nominalTermStructure are set. A live optionlet is refused, missing the volatility or, with one, by the base
  pricer's optionletPriceImp; a fixed coupon's optionlets are left out, its intrinsic value read on the index's level
  where nanoql reads the index ratio (tests/NanoQL.Tests/Divergences/CpiPricerFixedOptionletTests.cs).
- "setCapletVolatility" cases: a pricer given a volatility, then an empty handle (refused): whether its volatility is
  set afterwards.

A case's inputs hold its settings (UK RPI's fixings stored by name), the index, coupon and pricer specs, as
tools/cxx/src/cashflows/inflationspec.hpp reads them, and the strikes and dates. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/CPICouponPricerCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("cxx_inflationcoupon",
                                               os.path.join(v.GEN_ROOT, "cxx", "cashflows", "inflationcoupon.py"))
ic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ic)

TODAY = ic.TODAY
NAME = "CPICouponPricer::"
STRIKES = [0.02, 1.05]
PRICERS = [ic.pricer("CPICouponPricer"), ic.pricer("CPICouponPricer", None, ic.NOMINAL),
           ic.pricer("CPICouponPricer", ic.CPI_VOL, None), ic.pricer("CPICouponPricer", ic.CPI_VOL, ic.NOMINAL)]


def member_cases(H):
    out = []
    k = 0
    for label, coupon in ic.CPI_COUPONS:
        live = ql.Date(coupon["endDate"]) > TODAY
        strikes = STRIKES if live else []
        start, end = coupon["startDate"], coupon["endDate"]
        dates = [start + 60, end]
        for p in PRICERS:
            with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                def call(member, argument=None):
                    return v.attempt(lambda: H.call(NAME + "member", ic.UKRPI, coupon, p, member, argument))

                expected = {"swapletPrice": call("swapletPrice"), "swapletRate": call("swapletRate"),
                            "accruedRate": [call("accruedRate", d) for d in dates],
                            "capletVolatility": call("capletVolatility"),
                            "nominalTermStructure": call("nominalTermStructure")}
                for member in ("capletPrice", "capletRate", "floorletPrice", "floorletRate"):
                    expected[member] = [call(member, x) for x in strikes]
                out.append({"name": f"member {label}, volatility {p['volatility'] is not None}, nominal curve "
                                    f"{p['nominalCurve'] is not None} / {k}",
                            "inputs": {"settings": st, "kind": "member", "index": ic.UKRPI, "coupon": coupon,
                                       "pricer": p, "strikes": strikes, "dates": dates},
                            "expected": expected})
            k += 1
    return out


def set_caplet_volatility_cases(H):
    out = []
    for k, (label, vol) in enumerate((("a volatility", ic.CPI_VOL), ("an empty handle", None))):
        p = PRICERS[1]
        with H.case_settings(v.enc(TODAY)) as st:
            out.append({"name": f"setCapletVolatility {label} / {k}",
                        "inputs": {"settings": st, "kind": "setCapletVolatility", "pricer": p, "volatility": vol},
                        "expected": {"setCapletVolatility": v.attempt(
                            lambda: H.call(NAME + "setCapletVolatility", p, vol))}})
    return out


def cases():
    with cxx.connect() as H:
        return member_cases(H) + set_caplet_volatility_cases(H)


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/cpicouponpricer.hpp", cases)
