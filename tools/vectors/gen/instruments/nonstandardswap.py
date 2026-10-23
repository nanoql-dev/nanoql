# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's NonstandardSwap (ql/instruments/nonstandardswap.hpp, .cpp), each output computed by the wheel
on swaps built under an evaluation date of 15 January 2025, on instruments/fixedvsfloatingswap.py's curves and indexes:
a swap of constant terms; amortizing nominals with step-up rates, per-coupon gearings and spreads, a zero gearing among
them; the same with the intermediate and the final capital exchanges, and with the final alone; floating nominals other
than the fixed; a receiver on a semiannual fixed leg; and an overnight index (SOFR's conventions) paid two days after
each period's end. On each, the inspectors (the nominals, the fixed rates, the gearings and spreads as init leaves them,
the payment terms), each leg's cash flows (dates and amounts, a redemption's among them) and the discounting engine's
NPV, leg NPVs and BPSs, on the 2.7% discount curve; amounts compared at the largest nominal.

The wheel binds the per-coupon constructor only, with its payment convention and lag required; QuantLib's conversion
from a fixed-versus-floating swap is checked by QuantLib's own cases (testSofrSwaptionPaymentLag,
testNonstandardSwapConversionPreservesObservationConventions). Each case's input "swap" is the spec make() reads;
tests/NanoQL.Tests/Vectors/Instruments/NonstandardSwapVectorTests.cs reads it back."""
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
irx = fvf.irx
TODAY = fvf.TODAY
TARGET = fvf.TARGET
A360, A365 = fvf.A360, fvf.A365
Y, M, D = ql.Years, ql.Months, ql.Days
TARGET_CAL = ql.TARGET()
E6M = fvf.preset("Euribor", fvf.FORECAST, ql.Period(6, M))
SOFR = fvf.SOFR
DISCOUNT = fvf.DISCOUNT
US = fvf.US_SOFR


def nonstandard(swap_type, fixed_nominal, floating_nominal, fixed_schedule, fixed_rate, fixed_dc, floating_schedule,
                index, gearing, spread, floating_dc, intermediate=False, final=False, convention=ql.Following, lag=0,
                calendar=None):
    return {"kind": "NonstandardSwap", "type": int(swap_type), "fixedNominal": list(fixed_nominal),
            "floatingNominal": list(floating_nominal), "fixedSchedule": fixed_schedule, "fixedRate": list(fixed_rate),
            "fixedDayCount": fixed_dc, "floatingSchedule": floating_schedule, "index": index,
            "gearing": list(gearing), "spread": list(spread), "floatingDayCount": floating_dc,
            "intermediateCapitalExchange": intermediate, "finalCapitalExchange": final,
            "paymentConvention": int(convention), "paymentLag": lag, "paymentCalendar": calendar}


def make(spec, index=None):
    """The wheel's swap: its per-coupon constructor, QuantLib's empty payment calendar where the spec has none."""
    index = index if index is not None else fvf.make_index(spec["index"])
    args = [spec["type"], spec["fixedNominal"], spec["floatingNominal"], fvf.schedule(spec["fixedSchedule"]),
            spec["fixedRate"], irx.day_counter(spec["fixedDayCount"]), fvf.schedule(spec["floatingSchedule"]), index,
            spec["gearing"], spec["spread"], irx.day_counter(spec["floatingDayCount"]),
            spec["intermediateCapitalExchange"], spec["finalCapitalExchange"], spec["paymentConvention"],
            spec["paymentLag"]]
    if spec["paymentCalendar"] is not None:
        args.append(irx.calendar(spec["paymentCalendar"]))
    return ql.NonstandardSwap(*args)


def scale_of(spec):
    return max([abs(x) for x in spec["fixedNominal"] + spec["floatingNominal"]] + [1.0])


def annual(start, years, tenor=ql.Period(1, Y), cal=TARGET):
    return fvf.sched(start, TARGET_CAL.advance(start, years, Y), tenor, cal, ql.ModifiedFollowing)


def constant(swap_type, nominal, rate, start, years, index=E6M, fixed_tenor=ql.Period(1, Y)):
    """A swap of constant terms: the per-coupon form with every coupon's terms equal."""
    fixed = annual(start, years, fixed_tenor)
    floating = annual(start, years, ql.Period(6, M))
    n, m = len(fvf.schedule(fixed)) - 1, len(fvf.schedule(floating)) - 1
    return nonstandard(swap_type, [nominal] * n, [nominal] * m, fixed, [rate] * n, fvf.THIRTY360_BB, floating, index,
                       [1.0] * m, [0.0] * m, A360)


def swaps():
    start = TARGET_CAL.advance(TODAY, 1, Y)
    fixed = annual(start, 5)
    floating = annual(start, 5, ql.Period(6, M))
    amortizing = [1e6, 8e5, 6e5, 4e5, 2e5]
    floating_amortizing = [n for n in amortizing for _ in range(2)]
    rates = [0.025, 0.0275, 0.03, 0.0325, 0.035]
    gearings = [1.0, 1.0, 0.8, 0.8, 0.0, 1.2, 1.2, 1.0, 1.0, 0.9]
    spreads = [0.0, 0.001, 0.001, 0.002, 0.0, 0.0, -0.001, 0.0005, 0.0, 0.0015]
    sofr_schedule = fvf.sched(start, TARGET_CAL.advance(start, 3, Y), ql.Period(1, Y), US, ql.ModifiedFollowing)
    out = [
        ("constant terms, payer", constant(ql.Swap.Payer, 1e6, 0.03, start, 5)),
        ("amortizing, step-up, gearings and spreads", nonstandard(
            ql.Swap.Payer, amortizing, floating_amortizing, fixed, rates, fvf.THIRTY360_BB, floating, E6M, gearings,
            spreads, A360)),
        ("the same with both capital exchanges", nonstandard(
            ql.Swap.Payer, amortizing, floating_amortizing, fixed, rates, fvf.THIRTY360_BB, floating, E6M, gearings,
            spreads, A360, intermediate=True, final=True)),
        ("the same with the final exchange alone, a receiver", nonstandard(
            ql.Swap.Receiver, amortizing, floating_amortizing, fixed, rates, fvf.THIRTY360_BB, floating, E6M, gearings,
            spreads, A360, final=True)),
        ("floating nominals other than the fixed", nonstandard(
            ql.Swap.Receiver, amortizing, [1e6] * 10, fixed, rates, fvf.THIRTY360_BB, floating, E6M, [1.0] * 10,
            [0.0] * 10, A360, intermediate=True)),
        ("a semiannual fixed leg, modified following payments", constant(
            ql.Swap.Receiver, 5e6, 0.028, start, 3, fixed_tenor=ql.Period(6, M))),
        ("SOFR, paid two days late", nonstandard(
            ql.Swap.Payer, [1e6, 1e6, 1e6], [1e6, 9e5, 8e5], sofr_schedule, [0.04] * 3, A360, sofr_schedule, SOFR,
            [1.0] * 3, [0.0, 0.0005, 0.001], A360, convention=ql.Following, lag=2, calendar=US)),
    ]
    out[5][1]["paymentConvention"] = int(ql.ModifiedFollowing)
    return out


def leg(prefix, cashflows, scale):
    return {prefix + "Dates": [cf.date().serialNumber() for cf in cashflows],
            prefix + "Amounts": [fvf.ibc.scaled(cf.amount, scale) for cf in cashflows]}


def swap_case(name, spec):
    with v.case_settings(TODAY) as st:
        scale = scale_of(spec)
        sw = make(spec)
        expected = {
            "type": int(sw.type()),
            "fixedNominal": list(sw.fixedNominal()),
            "floatingNominal": list(sw.floatingNominal()),
            "fixedRate": list(sw.fixedRate()),
            "gearings": list(sw.gearings()),
            "spreads": list(sw.spreads()),
            "paymentConvention": int(sw.paymentConvention()),
            "paymentLag": sw.paymentLag(),
        }
        expected.update(leg("fixed", sw.fixedLeg(), scale))
        expected.update(leg("floating", sw.floatingLeg(), scale))
        sw.setPricingEngine(fvf.engine(DISCOUNT))
        r = v.result
        expected.update({"NPV": v.amount(r(sw.NPV), scale),
                         "legNPV": [v.amount(r(lambda j=j: sw.legNPV(j)), scale) for j in range(2)],
                         "legBPS": [v.amount(r(lambda j=j: sw.legBPS(j)), scale * 1e-4) for j in range(2)]})
        return {"name": name, "inputs": {"settings": st, "swap": spec, "discount": DISCOUNT}, "expected": expected}


def cases():
    return [swap_case(name, spec) for name, spec in swaps()]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/nonstandardswap.hpp", cases)
