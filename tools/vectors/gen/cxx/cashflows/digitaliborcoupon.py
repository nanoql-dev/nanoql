# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's DigitalIborLeg and DigitalIborCoupon (ql/cashflows/digitaliborcoupon.hpp, .cpp), which the
wheel doesn't bind, each output computed by the C++ harness, on the evaluation date of
tools/vectors/gen/cashflows/iborcoupon.py (15 January 2025) and its curve: two-year semiannual legs on its Euribor 6M,
priced by the Black pricer on its lognormal caplet volatility: cash calls, asset puts per coupon, short collars on the
sub-replication in arrears, naked options, and a zero gearing with its spread given, the leg building a fixed coupon at
it.

Each case's outputs are, per coupon, tools/cxx/src/cashflows/digitalspec.hpp's leg outputs, flattened into a list per
name over the coupons: "digital", "date", the digital inspectors and the rates, null for a fixed coupon but its rate, a
refused value {"throws": true}. Each case's inputs hold its settings and the leg ("leg": digitalspec.hpp's leg spec,
always setting the naked option flag, which QuantLib's builder leaves uninitialized otherwise, the S-cms plan's CMR5
(viii)). tests/NanoQL.Tests/Vectors/Cxx/Coupons/DigitalIborCouponCxxVectorTests.cs reads it back."""
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


cms = _load("cxx_digitalcmscoupon", "cxx/cashflows/digitalcmscoupon.py")
ibc = cms.ibc

TODAY = ibc.TODAY
INDEX = ibc.E6M
PRICER = ibc.pricer_spec(ibc.VOL_LN)


def legs(index, pricer):
    """The CMS generator's legs on the Ibor index, from 17 January 2025."""
    out = []
    for name, spec in cms.legs(index, pricer):
        spec["schedule"] = ibc.sched(ql.Date(17, ql.January, 2025), ql.Date(18, ql.January, 2027), ql.Period(6, ql.Months))
        out.append((name, spec))
    return out


def cases():
    return cms.run("DigitalIborLeg", TODAY, legs(INDEX, PRICER))


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/digitaliborcoupon.hpp", cases)
