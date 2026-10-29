# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's quanto Black pricer, proxy index and stripped capped/floored coupon
(ql/experimental/coupons/quantocouponpricer.hpp, proxyibor.hpp, strippedcapflooredcoupon.hpp and their .cpp files),
which the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/coupons/iborcouponextensions.cpp).

- "quanto" cases: a Euribor 6M coupon on the quanto pricer, at correlations 0, 0.3 and −0.5, on a shifted lognormal
  volatility of 20% (shift 0 and 1%) and a normal one of 0.8%: its swaplet, caplet and floorlet rates.
- "proxy" cases: a proxy of Euribor 6M at gearing 2 and spread 0.001: its name, its fixing, value and maturity dates at
  the coupon's fixing, and a past fixing read back. Its forecast, gearing × proxied × spread on QuantLib, is left out
  (the S-xrt plan's XRR5 (xxi); Divergences/ProxyIborTests.cs).
- "stripped" cases: capped, floored and collared coupons on QuantLib's Black pricer at a 20% optionlet volatility: the
  underlying's, the capped coupon's and the stripped coupon's rates, a three-coupon stripped leg's amounts, and the
  stripped coupon's cap, floor, effective cap and floor, kind and convexity adjustment.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Coupons/IborCouponExtensionsCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

REFERENCE = 46311  # Friday 16 October 2026


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(REFERENCE)
        for normal, vol, shift in ((False, 0.2, 0.0), (False, 0.2, 0.01), (True, 0.008, 0.0)):
            for rho in (0.0, 0.3, -0.5):
                args = [REFERENCE, 0.03, vol, normal, shift, 0.1, rho, 0.035, 0.025]
                out.append({"name": f"quanto: {'normal' if normal else 'lognormal'} {vol}, shift {shift}, rho {rho}",
                            "inputs": {"settings": st, "args": args},
                            "expected": H.call("BlackIborQuantoCouponPricer::rates", *args)})
        args = [REFERENCE, 0.03, 2.0, 0.001, 0.0275]
        out.append({"name": "proxy: gearing 2, spread 0.001", "inputs": {"settings": st, "args": args},
                    "expected": H.call("ProxyIbor::conventions", *args)})
        for cap, floor in ((0.035, None), (None, 0.025), (0.035, 0.025)):
            args = [REFERENCE, 0.03, 0.2, cap, floor]
            out.append({"name": f"stripped: cap {cap}, floor {floor}", "inputs": {"settings": st, "args": args},
                        "expected": H.call("StrippedCappedFlooredCoupon::rates", *args)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/coupons/quantocouponpricer.hpp", cases)
