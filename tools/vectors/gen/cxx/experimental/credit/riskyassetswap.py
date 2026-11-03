# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's risky asset swap and its bootstrap helper (ql/experimental/credit/riskyassetswap.hpp), which
the wheel doesn't expose, each output computed by the C++ harness (tools/cxx/src/experimental/credit/
riskyassetswap.cpp).

nanoql runs the fixed annuity over the fixed schedule and the recovery integral to each period's end
(tests/NanoQL.Tests/Divergences/RiskyAssetSwapTests.cs), so:

- "swap" cases read the NPV and the fair spread where the two schedules coincide at recovery 0, payer and receiver,
  at a given coupon or the par coupon, on flat and stepped curves, on TARGET and without a calendar; and the float
  annuity on every case, the fixed leg annual and the float quarterly among them, at recovery 40%.
- "bootstrap" cases: a backward-flat hazard-rate curve on asset swap helpers whose fixed and float legs coincide, at
  recovery 0: its dates and data, and its survival probability at sample times.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/RiskyAssetSwapCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds)
dts = cds.dts

TODAY = dts.TODAY
M, Y = int(ql.Months), int(ql.Years)
NULL_CALENDAR = {"type": "NullCalendar"}
NODES = [TODAY, TODAY + ql.Period(1, ql.Years), TODAY + ql.Period(3, ql.Years), TODAY + ql.Period(5, ql.Years)]
CURVES = [("flat 2%", dts.flat_spec(TODAY, 0.02)),
          ("stepped", dts.curve_spec("HazardRateCurve", NODES, [0.01, 0.01, 0.03, 0.06]))]
DISCOUNTS = [("5% A360", dts.discount_spec(TODAY, 0.05, dts.A360)), ("2%", dts.discount_spec(TODAY, 0.02))]


def schedule(months, cal, end_years=5):
    return cds.schedule_spec(TODAY, TODAY + ql.Period(end_years, ql.Years), (months, M), cal,
                             ql.DateGeneration.Forward)


def swap_args(payer, fixed, floating, spread, recovery, disc, credit, coupon, dc=dts.A360):
    return [payer, 100.0, fixed, floating, dc, dc, spread, recovery, disc, credit, coupon]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            for curve_label, credit in CURVES:
                for disc_label, disc in DISCOUNTS:
                    for cal_label, cal in (("TARGET", dts.TARGET), ("no calendar", NULL_CALENDAR)):
                        quarterly = schedule(3, cal)
                        for payer in (True, False):
                            for coupon in (None, 0.04):
                                args = swap_args(payer, quarterly, quarterly, 0.01, 0.0, disc, credit, coupon)
                                out.append({"name": f"swap {curve_label} {disc_label} {cal_label} payer {payer} "
                                                    f"coupon {coupon}",
                                            "inputs": {"settings": st, "kind": "swap", "args": args,
                                                       "annuityOnly": False},
                                            "expected": flat(call("RiskyAssetSwap::values", *args))})
                        args = swap_args(True, schedule(12, cal), quarterly, 0.01, 0.4, disc, credit, None)
                        values = call("RiskyAssetSwap::values", *args)
                        out.append({"name": f"annuity {curve_label} {disc_label} {cal_label} annual fixed",
                                    "inputs": {"settings": st, "kind": "swap", "args": args, "annuityOnly": True},
                                    "expected": {"floatAnnuity": values if "throws" in values
                                                 else values["floatAnnuity"]}})
            for disc_label, disc in DISCOUNTS:
                helpers = [[s, n, Y, 1, dts.TARGET, 3, M, int(ql.Following), dts.A360, 3, M, int(ql.Following),
                            dts.A360, 0.0, disc] for s, n in ((0.006, 1), (0.009, 3), (0.012, 5))]
                args = [helpers, v.enc(TODAY), [0.5, 2.0, 4.0, 6.0]]
                out.append({"name": f"bootstrap {disc_label}", "inputs": {"settings": st, "kind": "bootstrap",
                                                                          "args": args},
                            "expected": flat(call("AssetSwapHelper::bootstrap", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/riskyassetswap.hpp", cases)
