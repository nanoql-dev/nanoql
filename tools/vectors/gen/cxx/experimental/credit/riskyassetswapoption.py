# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's option on a risky asset swap (ql/experimental/credit/riskyassetswapoption.hpp), which the
wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/riskyassetswap.cpp):
payer and receiver swaps of riskyassetswap.py's form, the fixed leg annual and the float quarterly, expiring a month
and a year out and today (expired), at market spreads below, at and above the swap's, at volatilities from 1e-9 to
50%. At a volatility of 0 QuantLib reads NaN where nanoql reads the intrinsic value
(tests/NanoQL.Tests/Divergences/RiskyAssetSwapOptionTests.cs), so no case asks for it. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RiskyAssetSwapOptionCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("cxx_riskyassetswap",
                                               os.path.join(v.GEN_ROOT, "cxx", "experimental", "credit",
                                                            "riskyassetswap.py"))
asw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(asw)
dts = asw.dts

TODAY = asw.TODAY


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            credit = dts.flat_spec(TODAY, 0.02)
            disc = dts.discount_spec(TODAY, 0.05, dts.A360)
            for payer in (True, False):
                swap = asw.swap_args(payer, asw.schedule(12, dts.TARGET), asw.schedule(3, dts.TARGET), 0.01, 0.4,
                                     disc, credit, None)
                for expiry_label, expiry in (("a month", TODAY + ql.Period(1, ql.Months)),
                                             ("a year", TODAY + ql.Period(1, ql.Years)), ("today", TODAY)):
                    for market in (0.008, 0.01, 0.012):
                        for vol in (1e-9, 0.002, 0.05, 0.5):
                            args = swap + [v.enc(expiry), market, vol]
                            out.append({"name": f"payer {payer} {expiry_label} market {market} vol {vol}",
                                        "inputs": {"settings": st, "args": args},
                                        "expected": {"NPV": call("RiskyAssetSwapOption::NPV", *args)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/riskyassetswapoption.hpp", cases)
