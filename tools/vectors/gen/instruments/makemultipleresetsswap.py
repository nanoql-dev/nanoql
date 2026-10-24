# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's multiple-resets swap builder (ql/instruments/makemultipleresetsswap.hpp), through the wheel's
MakeMultipleResetsSwap, on instruments/multipleresetsswap.py's evaluation date, indexes and fixings.

Each "build" case is a builder on a tenor, an index and the resets per coupon, with its setters applied in order
(QuantLib-Python's keyword arguments): the fair rate on the index's forwarding curve or on a discounting curve, or a
given fixed rate; a payer or a receiver; settlement days, an effective date, a termination date, a forward start; the
fixed leg's frequency, day count and convention; a floating spread; the averaging method; a pricing engine. Its outputs
are the swap's inspectors (instruments/fixedvsfloatingswap.py's) with the full reset schedule's dates and the resets per
coupon, each leg's payment dates and amounts, and the NPV and fair rate on the engine QuantLib's builder sets on the
swap (the given engine, a DiscountingSwapEngine on a curve with QuantLib's default for the settlement date's flows;
else the given discounting curve's, or the index's forwarding curve's, without those flows). An effective date with
settlement days, and a fair rate on an index with no forwarding curve, are QuantLib's refusals ("build": {"throws":
true}).

A case's inputs hold its settings with the stored fixings and "build" ({"tenor", "index", "resetsPerCoupon",
"setters"}), each setter [name, value] with the wheel's keyword's name, a date a serial number, a period [length,
units], a day counter or curve a spec. Read back by
tests/NanoQL.Tests/Vectors/Instruments/MakeMultipleResetsSwapVectorTests.cs."""
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


mrs = _load("wheel_multipleresetsswap", "instruments/multipleresetsswap.py")
irx, cf, ibc, fvf = mrs.irx, mrs.cf, mrs.ibc, mrs.fvf

TODAY = mrs.TODAY
D = ql.Date
E1M, E3M = mrs.E1M, mrs.E3M
NO_CURVE = irx.ibor_spec("Euribor", (3, ql.Months), 2, irx.TARGET, ql.ModifiedFollowing, True, irx.A360)
DISCOUNT = mrs.DISCOUNT


def period(n, unit):
    return [n, int(unit)]


# (label, tenor, index, resets per coupon, setters)
BUILDS = [
    ("5Y on 1M, 3 resets, the fair rate", period(5, ql.Years), E1M, 3, []),
    ("5Y on 1M, 3 resets, a fixed rate, receiver", period(5, ql.Years), E1M, 3,
     [["receiveFixed", True], ["fixedRate", 0.03], ["nominal", 1e7]]),
    ("3Y on 3M, 4 resets, a discounting curve", period(3, ql.Years), E3M, 4,
     [["discountingTermStructure", DISCOUNT], ["nominal", 2.5e6]]),
    ("10Y on 3M, 2 resets, settlement days and a spread", period(10, ql.Years), E3M, 2,
     [["settlementDays", 0], ["floatingLegSpread", 0.0015], ["swapType", int(ql.Swap.Payer)]]),
    ("an effective and a termination date", period(2, ql.Years), E1M, 6,
     [["effectiveDate", D(20, ql.March, 2025).serialNumber()],
      ["terminationDate", D(20, ql.March, 2028).serialNumber()]]),
    ("a forward start, the fixed leg's frequency, day count and convention", period(4, ql.Years), E3M, 2,
     [["forwardStart", period(1, ql.Years)], ["fixedLegFrequency", int(ql.Annual)],
      ["fixedLegDayCount", cf.THIRTY360_BB], ["fixedLegConvention", int(ql.Following)]]),
    ("averaging", period(5, ql.Years), E1M, 3, [["averagingMethod", int(ql.RateAveraging.Simple)]]),
    ("a negative forward start", period(2, ql.Years), E3M, 4, [["forwardStart", period(-1, ql.Months)]]),
    ("an effective date with settlement days", period(5, ql.Years), E1M, 3,
     [["effectiveDate", D(20, ql.March, 2025).serialNumber()], ["settlementDays", 2]]),
    ("the fair rate with no forwarding curve", period(5, ql.Years), NO_CURVE, 2, []),
    ("a pricing engine on a discounting curve", period(5, ql.Years), E3M, 2,
     [["pricingEngine", DISCOUNT], ["nominal", 1e6]]),
]


def setter_value(name, value):
    if name in ("effectiveDate", "terminationDate"):
        return ql.Date(value)
    if name == "forwardStart":
        return ql.Period(value[0], value[1])
    if name == "fixedLegDayCount":
        return cf.day_counter(value)
    if name == "discountingTermStructure":
        return irx.handle(value)
    if name == "pricingEngine":
        return ql.DiscountingSwapEngine(irx.handle(value))
    return value


def cases():
    out = []
    for k, (label, tenor, index_spec, per_coupon, setters) in enumerate(BUILDS):
        with v.case_settings(TODAY, fixings=ibc.fixings_for(index_spec)) as st:
            index = irx.make_index(index_spec)
            inputs = {"settings": st, "build": {"tenor": tenor, "index": index_spec, "resetsPerCoupon": per_coupon,
                                                "setters": setters}}
            kwargs = {name: setter_value(name, value) for name, value in setters}
            try:
                sw = ql.MakeMultipleResetsSwap(ql.Period(tenor[0], tenor[1]), index, per_coupon, **kwargs)
            except RuntimeError:
                out.append({"name": f"build {label} / {k}", "inputs": inputs,
                            "expected": {"build": dict(v.THROWS)}})
                continue
            scale = max(abs(x) for x in sw.fixedNominals())
            expected = fvf.inspectors(sw)
            expected.update({"fullResetSchedule": fvf.schedule_dates(sw.fullResetSchedule()),
                             "resetsPerCoupon": sw.resetsPerCoupon()})
            expected.update(fvf.leg_outputs("fixedLeg", sw.fixedLeg(), scale))
            expected.update(fvf.leg_outputs("floatingLeg", sw.floatingLeg(), scale))
            expected.update({"NPV": v.amount(v.result(sw.NPV), scale), "fairRate": v.result(sw.fairRate)})
            out.append({"name": f"build {label} / {k}", "inputs": inputs, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makemultipleresetsswap.hpp", cases)
