# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation-coupon pricers' members (ql/cashflows/inflationcouponpricer.hpp),
each output computed by the C++ harness: the wheel reaches the pricers only through a capped or floored coupon's rate.

- "member" cases: tools/vectors/gen/cxx/cashflows/inflationcoupon.py's year-on-year coupons, each with the base pricer
  (whose live optionlets QuantLib refuses), the Black, unit-displaced Black and Bachelier pricers, each built with and
  without a volatility and a nominal curve, initialized on the coupon: swapletPrice, swapletRate, capletPrice,
  capletRate, floorletPrice and floorletRate at three strikes, and whether capletVolatility and nominalTermStructure
  are set. Without a volatility every optionlet is refused, a fixed one too: QuantLib reads the volatility's base date
  first.
- "setCapletVolatility" cases: a pricer given a volatility, then an empty handle (refused): whether its volatility is
  set afterwards.
- "initialize" cases: a pricer initialized on a CPI coupon: refused.

A case's inputs hold its settings (UK RPI's fixings stored by name), the index, coupon and pricer specs, as
tools/cxx/src/cashflows/inflationspec.hpp reads them, and the strikes. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Coupons/InflationCouponPricerCxxVectorTests.cs."""
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
UK = ic.UK
A365 = ic.A365
M = ic.M
NAME = "YoYInflationCouponPricer::"
STRIKES = [0.01, 0.03, 0.05]
VOLS = {"YoYInflationCouponPricer": ic.YOY_VOL, "BlackYoYInflationCouponPricer": ic.BLACK_VOL,
        "UnitDisplacedBlackYoYInflationCouponPricer": ic.YOY_VOL,
        "BachelierYoYInflationCouponPricer": ic.yoyvol.surface_spec(0.008, 2, UK, ql.Following, ic.THIRTY360_BB,
                                                                    [2, M], ql.Monthly, True)}


def member_cases(H):
    out = []
    k = 0
    for label, coupon in ic.YOY_COUPONS:
        for kind, vol in VOLS.items():
            for p in (ic.pricer(kind), ic.pricer(kind, vol, None), ic.pricer(kind, vol, ic.NOMINAL)):
                with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
                    def call(member, argument=None):
                        return v.attempt(lambda: H.call(NAME + "member", ic.RATIO, coupon, p, member, argument))

                    expected = {"swapletPrice": call("swapletPrice"), "swapletRate": call("swapletRate"),
                                "capletVolatility": call("capletVolatility"),
                                "nominalTermStructure": call("nominalTermStructure")}
                    for member in ("capletPrice", "capletRate", "floorletPrice", "floorletRate"):
                        expected[member] = [call(member, x) for x in STRIKES]
                    out.append({"name": f"member {label}, {kind}, volatility {p['volatility'] is not None}, nominal "
                                        f"curve {p['nominalCurve'] is not None} / {k}",
                                "inputs": {"settings": st, "kind": "member", "index": ic.RATIO, "coupon": coupon,
                                           "pricer": p, "strikes": STRIKES},
                                "expected": expected})
                k += 1
    return out


def set_caplet_volatility_cases(H):
    out = []
    k = 0
    for kind in VOLS:
        for label, vol in (("a volatility", ic.YOY_VOL), ("an empty handle", None)):
            p = ic.pricer(kind, None, ic.NOMINAL)
            with H.case_settings(v.enc(TODAY)) as st:
                out.append({"name": f"setCapletVolatility {kind}, {label} / {k}",
                            "inputs": {"settings": st, "kind": "setCapletVolatility", "pricer": p, "volatility": vol},
                            "expected": {"setCapletVolatility": v.attempt(
                                lambda: H.call(NAME + "setCapletVolatility", p, vol))}})
            k += 1
    return out


def initialize_cases(H):
    out = []
    label, coupon = ic.CPI_COUPONS[0]
    p = ic.pricer("BlackYoYInflationCouponPricer", ic.BLACK_VOL, ic.NOMINAL)
    with H.case_settings(v.enc(TODAY), fixings=ic.harness_fixings()) as st:
        out.append({"name": f"initialize on a {label} coupon / 0",
                    "inputs": {"settings": st, "kind": "initialize", "index": ic.UKRPI, "coupon": coupon,
                               "pricer": p},
                    "expected": {"initialize": v.attempt(
                        lambda: H.call(NAME + "initialize(CPICoupon)", ic.UKRPI, coupon, p))}})
    return out


def cases():
    with cxx.connect() as H:
        return member_cases(H) + set_caplet_volatility_cases(H) + initialize_cases(H)


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/inflationcouponpricer.hpp", cases)
