# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DigitalCmsSpreadLeg and DigitalCmsSpreadCoupon
(ql/experimental/coupons/digitalcmsspreadcoupon.hpp, .cpp), which the wheel doesn't bind, each output computed by the
C++ harness: the legs' structure alone, since their rates need a CMS spread coupon pricer, whose lognormal
implementation waits for S-xrt. Legs on a 10Y-2Y swap spread index over tools/vectors/gen/cashflows/conundrumpricer.py's
EUR swap indexes (16 June 2025, a flat 5%), as tools/vectors/gen/cxx/cashflows/digitalcmscoupon.py's legs.

Each case's outputs are, per coupon, tools/cxx/src/cashflows/digitalspec.hpp's leg outputs without rates, flattened into
a list per name over the coupons: "digital", "date" and the digital inspectors, null for a fixed coupon, and a fixed
coupon's "rate". Each case's inputs hold its settings and the leg ("leg": digitalspec.hpp's leg spec with "index1" and
"index2" for the index, no pricer). tests/NanoQL.Tests/Vectors/Cxx/Experimental/Coupons/
DigitalCmsSpreadCouponCxxVectorTests.cs reads it back."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "cxx_digitalcmscoupon", os.path.join(v.GEN_ROOT, "cxx", "cashflows", "digitalcmscoupon.py"))
cms = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cms)
svc, cp = cms.svc, cms.cp

INDEX2 = svc.euribor([2, svc.Y], cp.CURVE)
NAMES = [n for n in cms.NAMES if n not in ("callOptionRate", "putOptionRate")]


def legs():
    out = []
    for name, spec in cms.legs(cms.INDEX, None):
        spec = dict(spec)
        del spec["index"], spec["pricer"]
        spec["index1"], spec["index2"] = cms.INDEX, INDEX2
        # the spread's strikes, a tenth of the rates'
        for key in ("callStrikes", "putStrikes"):
            spec[key] = [k / 10 for k in spec[key]]
        out.append((name, spec))
    return out


def cases():
    out = cms.run("DigitalCmsSpreadLeg", cms.TODAY, legs())
    for c in out:
        if "leg" not in c["expected"]:
            c["expected"] = {name: c["expected"][name] for name in NAMES}
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/coupons/digitalcmsspreadcoupon.hpp", cases)
