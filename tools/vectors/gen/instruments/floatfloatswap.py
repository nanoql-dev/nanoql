# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FloatFloatSwap (ql/instruments/floatfloatswap.hpp, .cpp), through its per-coupon constructor,
the one the wheel binds: swaps on the evaluation date 15 January 2025, payer and receiver, of Euribor 3M against 6M and
of Euribor against a CMS (EUR swap) index; spreads, gearings (a zero gearing, which QuantLib makes its machine epsilon),
amortizing nominals with intermediate and final capital exchanges, payment conventions, caps on an Ibor leg, a swap that
has expired, and the constructor's refusals: nominals that don't match a schedule, gearings that don't match the
nominals, and caps given for some coupons only ("cappedRate 1 must be null for all or none entry ...").

Each case's outputs are whether the swap builds, QuantLib's refusal recorded as its throw; then each leg's payment
dates, whether each cash flow is a floating-rate coupon (a zero gearing made QuantLib's machine epsilon keeps its
coupons floating), and amounts, a coupon's amount refused where QuantLib refuses it (a capped coupon or a CMS coupon
without a pricer, which wait for batch 3); and DiscountingSwapEngine's results on a flat curve: the NPV, the per-leg NPV
and BPS and the two fair spreads, null where QuantLib refuses them, or the refused pricing, the one output "calculate".
NPVs and amounts are compared at the largest nominal, BPS at it times 1e-4. Left out (the S-rx plan's SRXR3 (ix)): a
schedule of one date, on which QuantLib reads an empty vector and the process dies.

Each case's inputs hold its settings and "swap" ({"type", "nominal1", "nominal2", "schedule1", "index1", "dayCount1",
"schedule2", "index2", "dayCount2", "intermediateCapitalExchange", "finalCapitalExchange", "gearing1", "spread1",
"cappedRate1", "flooredRate1", "gearing2", "spread2", "cappedRate2", "flooredRate2", "paymentConvention1",
"paymentConvention2"}): an index is {"ibor": <instruments/fixedvsfloatingswap.py's spec>} or {"swap":
<indexes/swapindex.py's spec>}; a null rate in a caps or floors list is QuantLib's Null<Real>(); schedules are
fixedvsfloatingswap.py's specs; each day counter is given, the wheel building no empty one. The wheel's constructor
takes a payment convention for each leg, Following by default, where QuantLib's takes none and uses the schedule's. Read
back by tests/NanoQL.Tests/Vectors/Instruments/FloatFloatSwapVectorTests.cs."""
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


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
swi = _load("wheel_swapindex", "indexes/swapindex.py")
irx = fvf.irx

TODAY = fvf.TODAY
D = fvf.d
M, Y = ql.Months, ql.Years
MF, F, P = ql.ModifiedFollowing, ql.Following, ql.Preceding
PAYER, RECEIVER = ql.Swap.Payer, ql.Swap.Receiver
FORECAST = fvf.curve(0.031)
DISCOUNT = fvf.curve(0.027)
E3M = {"ibor": fvf.preset("Euribor3M", FORECAST)}
E6M = {"ibor": fvf.preset("Euribor6M", fvf.curve(0.033))}
CMS10Y = {"swap": swi.swap_index("EurSwap", (10, Y), 2, "EUR", irx.TARGET, (1, Y), MF, irx.THIRTY360_BB,
                                 fvf.preset("Euribor6M", FORECAST))}
SPOT = D(17, 1, 2025)


def sched(start, end, tenor, convention=MF):
    return fvf.sched(start, end, ql.Period(*tenor), irx.TARGET, convention)


def swap(swap_type, n1, n2, s1, i1, s2, i2, dc1=irx.A360, dc2=irx.A360, intermediate=False, final=False, gearing1=(),
         spread1=(), caps1=(), floors1=(), gearing2=(), spread2=(), caps2=(), floors2=(), pc1=F, pc2=F):
    return {"type": int(swap_type), "nominal1": list(n1), "nominal2": list(n2), "schedule1": s1, "index1": i1,
            "dayCount1": dc1, "schedule2": s2, "index2": i2, "dayCount2": dc2,
            "intermediateCapitalExchange": intermediate, "finalCapitalExchange": final,
            "gearing1": list(gearing1), "spread1": list(spread1), "cappedRate1": list(caps1),
            "flooredRate1": list(floors1), "gearing2": list(gearing2), "spread2": list(spread2),
            "cappedRate2": list(caps2), "flooredRate2": list(floors2), "paymentConvention1": int(pc1),
            "paymentConvention2": int(pc2)}


def make_index(spec):
    return fvf.make_index(spec["ibor"]) if "ibor" in spec else swi.make_swap_index(spec["swap"])


def rates(values):
    return [ql.nullDouble() if x is None else x for x in values]


def make_swap(spec):
    return ql.FloatFloatSwap(spec["type"], spec["nominal1"], spec["nominal2"], fvf.schedule(spec["schedule1"]),
                             make_index(spec["index1"]), irx.day_counter(spec["dayCount1"]),
                             fvf.schedule(spec["schedule2"]), make_index(spec["index2"]),
                             irx.day_counter(spec["dayCount2"]),
                             spec["intermediateCapitalExchange"], spec["finalCapitalExchange"],
                             spec["gearing1"], spec["spread1"], rates(spec["cappedRate1"]),
                             rates(spec["flooredRate1"]), spec["gearing2"], spec["spread2"],
                             rates(spec["cappedRate2"]), rates(spec["flooredRate2"]), spec["paymentConvention1"],
                             spec["paymentConvention2"])


def swaps():
    """(name, spec) for the cases."""
    two3, two6 = sched(SPOT, D(17, 1, 2027), (3, M)), sched(SPOT, D(17, 1, 2027), (6, M))
    five6, five1 = sched(SPOT, D(17, 1, 2030), (6, M)), sched(SPOT, D(17, 1, 2030), (1, Y))
    one3, one6 = sched(SPOT, D(17, 1, 2026), (3, M)), sched(SPOT, D(17, 1, 2026), (6, M))
    a360 = irx.A360
    return [
        ("payer, Euribor 3M against 6M", swap(PAYER, [100.0] * 8, [100.0] * 4, two3, E3M, two6, E6M)),
        ("receiver, spreads and a gearing", swap(RECEIVER, [1e6] * 8, [1e6] * 4, two3, E3M, two6, E6M,
                                                 gearing1=[0.9] * 8, spread1=[0.001] * 8,
                                                 spread2=[0.002, 0.0021, 0.0022, 0.0023])),
        ("day counters and Preceding payments", swap(PAYER, [100.0] * 8, [100.0] * 4, two3, E3M, two6, E6M,
                                                     dc1=a360, dc2=irx.A365, pc1=P, pc2=MF)),
        ("a zero gearing", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M, gearing1=[0.0] * 4)),
        ("amortizing, capital exchanged", swap(RECEIVER, [100.0, 100.0, 80.0, 80.0, 60.0, 60.0, 40.0, 40.0],
                                               [100.0, 80.0, 60.0, 40.0], two3, E3M, two6, E6M, intermediate=True,
                                               final=True)),
        ("final exchange only", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M, final=True)),
        ("caps and floors on Euribor 3M", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M,
                                               caps1=[0.04] * 4, floors1=[0.0] * 4)),
        ("Euribor 6M against CMS 10Y", swap(PAYER, [100.0] * 10, [100.0] * 5, five6, E6M, five1, CMS10Y)),
        ("CMS 10Y capped against Euribor 6M", swap(RECEIVER, [100.0] * 5, [100.0] * 10, five1, CMS10Y, five6, E6M,
                                                   caps1=[0.05] * 5)),
        ("expired", swap(PAYER, [100.0] * 4, [100.0] * 2, sched(D(15, 1, 2020), D(15, 1, 2021), (3, M)), E3M,
                         sched(D(15, 1, 2020), D(15, 1, 2021), (6, M)), E6M)),
        ("nominal1 not matching its schedule", swap(PAYER, [100.0] * 3, [100.0] * 2, one3, E3M, one6, E6M)),
        ("nominal2 not matching its schedule", swap(PAYER, [100.0] * 4, [100.0] * 3, one3, E3M, one6, E6M)),
        ("gearing2 not matching the nominals", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M,
                                                    gearing2=[1.0] * 3)),
        ("caps given for some coupons", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M,
                                             caps1=[0.04, None, 0.04, 0.04])),
        ("caps missing for the first coupon", swap(PAYER, [100.0] * 4, [100.0] * 2, one3, E3M, one6, E6M,
                                                   caps2=[None, 0.04])),
    ]


def leg_outputs(prefix, leg, scale):
    return {prefix + "Dates": [cf.date().serialNumber() for cf in leg],
            prefix + "Floating": [ql.as_floating_rate_coupon(cf) is not None for cf in leg],
            prefix + "Amounts": [fvf.ibc.scaled(cf.amount, scale) for cf in leg]}


def results(sw, scale):
    try:
        sw.NPV()
    except RuntimeError:
        return {"calculate": dict(v.THROWS)}
    r = v.result
    bps = scale * 1e-4
    return {"NPV": v.amount(r(sw.NPV), scale),
            "legNPV": [v.amount(r(lambda j=j: sw.legNPV(j)), scale) for j in range(2)],
            "legBPS": [v.amount(r(lambda j=j: sw.legBPS(j)), bps) for j in range(2)],
            "fairSpread1": r(sw.fairSpread1),
            "fairSpread2": r(sw.fairSpread2)}


def cases():
    out = []
    for name, spec in swaps():
        scale = max([abs(x) for x in spec["nominal1"] + spec["nominal2"]] + [1.0])
        with v.case_settings(TODAY) as st:
            try:
                sw = make_swap(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                expected = leg_outputs("leg1", sw.leg(0), scale)
                expected.update(leg_outputs("leg2", sw.leg(1), scale))
                sw.setPricingEngine(fvf.engine(DISCOUNT))
                expected.update(results(sw, scale))
            out.append({"name": name, "inputs": {"settings": st, "swap": spec, "discountCurve": DISCOUNT},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/floatfloatswap.hpp", cases)
