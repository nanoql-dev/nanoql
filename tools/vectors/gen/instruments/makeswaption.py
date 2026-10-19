# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeSwaption (ql/instruments/makeswaption.hpp): 41 builds on the evaluation date 15 January
2025 (a few on Saturday 18 January, which the builder adjusts to the next business day before advancing the option
tenor), on Euribor swap presets (EuriborSwapIsdaFixA, EuriborSwapIsdaFixB, EuriborSwapIfrFix, one with exogenous
discounting), swap indexes of indexes/swapindex.py (on Euribor, a US dollar and a pound Ibor index, a null calendar,
exogenous discounting, an index with no forwarding curve) and overnight-indexed swap indexes (compounded and averaged,
with telescopic value dates or not).

The builds use both constructors, an option tenor and a fixing date, at the money (no strike) and at given strikes, and
each setter: the nominal, the settlement type and method (an inconsistent pair builds: QuantLib checks it when pricing),
the option convention, the exercise date (on or before the fixing date, and after it, refused), the exercise calendar,
the underlying type, indexed and at-par coupons (and the context's at-par switch, both ways), and a pricing engine,
which changes nothing. The refusals: at the money on an index with no forwarding curve ("null term structure set to this
instance of ..."), at the money on QuantLib's empty discounting handle, a fixing date that isn't a valid fixing date,
and an exercise date after the fixing date.

Each build's outputs are whether it builds, a refusal its one output {"throws": true}; then the swaption's exercise
dates, settlement type and method and type, and its underlying swap's fixed rate (the at-the-money strike where none was
given), nominal, start and maturity dates, both schedules' dates, and its NPV on a DiscountingSwapEngine on a flat 2.7%
curve without the settlement date's flows, which shows the floating coupons' choice, compared at the nominal.

The builds call QuantLib-Python's MakeSwaption, a Python function that builds the C++ QuantLib::MakeSwaption from the
index, the option tenor or the fixing date (by the argument's type) and the strike or Null<Rate>(), calls each keyword's
setter in the order given, skipping None, and converts the builder to a Swaption (QuantLib-SWIG 1.44, SWIG/swaption.i).
Each case's inputs hold its settings and its "build": {"index", "form" ("tenor" or "date"), "optionTenor" or
"fixingDate", "strike" (null for none), "setters"}, the setters as [name, value] pairs in order, an exercise calendar a
calendar spec and a pricing engine a Black engine on a volatility of instruments/swaption.py; the index a spec of
indexes/swapindex.py or a preset, {"type", "tenor", "forwarding"} with "discounting" for the exogenous constructor.
Read back by tests/NanoQL.Tests/Vectors/Instruments/MakeSwaptionVectorTests.cs."""
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


swp = _load("wheel_swaption", "instruments/swaption.py")
sidx = _load("wheel_swapindex", "indexes/swapindex.py")
fvf, irx = swp.fvf, swp.irx

TODAY = irx.TODAY
SATURDAY = ql.Date(18, ql.January, 2025)
TARGET, US_SOFR, UK_EXCHANGE, NULL_CALENDAR = irx.TARGET, irx.US_SOFR, irx.UK_EXCHANGE, irx.NULL_CALENDAR
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
D, M, Y = ql.Days, ql.Months, ql.Years
NPV_CURVE = fvf.curve(0.027)
FORWARDING = fvf.curve(0.029)
DISCOUNTING = fvf.curve(0.024, A360, ql.Simple)
PRESETS = {"EuriborSwapIsdaFixA": ql.EuriborSwapIsdaFixA, "EuriborSwapIsdaFixB": ql.EuriborSwapIsdaFixB,
           "EuriborSwapIfrFix": ql.EuriborSwapIfrFix}


def preset(name, tenor, forwarding=FORWARDING, discounting=False):
    spec = {"type": name, "tenor": [tenor[0], int(tenor[1])], "forwarding": forwarding}
    if discounting is not False:
        spec["discounting"] = discounting
    return spec


def make_index(spec):
    if spec["type"] in PRESETS:
        tenor = ql.Period(*spec["tenor"])
        if "discounting" in spec:
            return PRESETS[spec["type"]](tenor, irx.handle(spec["forwarding"]), irx.handle(spec["discounting"]))
        return PRESETS[spec["type"]](tenor, irx.handle(spec["forwarding"]))
    return sidx.make_swap_index(spec)


def build(index, form, when, strike=None, **setters):
    out = {"index": index, "form": form, "strike": strike,
           "setters": [[k, v.enc(x) if not isinstance(x, dict) else x] for k, x in setters.items()]}
    if form == "tenor":
        out["optionTenor"] = [when[0], int(when[1])]
    else:
        out["fixingDate"] = v.enc(when)
    return out


def wheel_value(key, x):
    if key == "exerciseDate":
        return ql.Date(x)
    if key == "exerciseCalendar":
        return irx.calendar(x)
    if key == "pricingEngine":
        return swp.black_engine(swp.DISCOUNT, x)
    return x


def make(spec):
    index = make_index(spec["index"])
    when = ql.Period(*spec["optionTenor"]) if spec["form"] == "tenor" else ql.Date(spec["fixingDate"])
    kwargs = {k: wheel_value(k, x) for k, x in spec["setters"]}
    return ql.MakeSwaption(index, when, spec["strike"], **kwargs)


def builds():
    """(name, build spec, evaluation date, at-par switch or None) for the 41 builds."""
    E5 = preset("EuriborSwapIsdaFixA", (5, Y))
    E1 = preset("EuriborSwapIsdaFixA", (1, Y))
    estr = irx.overnight_spec("Estr", 0, TARGET, A360, fvf.curve(0.028, A360))
    sofr = irx.overnight_spec("Sofr", 0, US_SOFR, A360, fvf.curve(0.043, A360))
    sonia = irx.overnight_spec("Sonia", 0, UK_EXCHANGE, A365, fvf.curve(0.047))
    by_name = dict(sidx.indexes())
    ois_compound = sidx.ois_index("EstrSwap", (5, Y), 2, "EUR", estr)
    ois_telescopic = sidx.ois_index("EstrSwap", (2, Y), 2, "EUR", estr, True)
    ois_averaged = sidx.ois_index("SofrSwap", (3, Y), 2, "USD", sofr, False, ql.RateAveraging.Simple)
    ois_telescopic_averaged = sidx.ois_index("SofrSwap", (5, Y), 2, "USD", sofr, True, ql.RateAveraging.Simple)
    ois_uk = sidx.ois_index("SoniaSwap", (1, Y), 0, "GBP", sonia)
    engine = swp.black_spec(0.2)
    out = [
        ("1Y on 5Y at the money", build(E5, "tenor", (1, Y)), TODAY, None),
        ("1Y on 5Y at 3%", build(E5, "tenor", (1, Y), 0.03), TODAY, None),
        ("3M on 1Y at the money", build(E1, "tenor", (3, M)), TODAY, None),
        ("5Y on 5Y at the money, receiver", build(E5, "tenor", (5, Y), underlyingType=int(ql.Swap.Receiver)), TODAY,
         None),
        ("10Y on 10Y at 2.5%", build(preset("EuriborSwapIsdaFixA", (10, Y)), "tenor", (10, Y), 0.025), TODAY, None),
        ("IsdaFixB 2Y on 5Y at the money", build(preset("EuriborSwapIsdaFixB", (5, Y)), "tenor", (2, Y)), TODAY, None),
        ("IfrFix 1Y on 7Y at the money", build(preset("EuriborSwapIfrFix", (7, Y)), "tenor", (1, Y)), TODAY, None),
        ("exogenous preset at the money", build(preset("EuriborSwapIsdaFixA", (5, Y), FORWARDING, DISCOUNTING), "tenor",
                                                (1, Y)), TODAY, None),
        ("exogenous preset on an empty handle at the money",
         build(preset("EuriborSwapIsdaFixA", (5, Y), FORWARDING, None), "tenor", (1, Y)), TODAY, None),
        ("exogenous preset on an empty handle at 3%",
         build(preset("EuriborSwapIsdaFixA", (5, Y), FORWARDING, None), "tenor", (1, Y), 0.03), TODAY, None),
        ("fixing date form at the money", build(E5, "date", ql.Date(15, ql.January, 2026)), TODAY, None),
        ("fixing date form at 4%, nominal", build(E5, "date", ql.Date(13, ql.July, 2026), 0.04, nominal=2.5e6),
         TODAY, None),
        ("fixing date on a Saturday, refused", build(E5, "date", ql.Date(17, ql.January, 2026)), TODAY, None),
        ("nominal and settlement", build(E5, "tenor", (2, Y), 0.031, nominal=1e7,
                                         settlementType=swp.CASH, settlementMethod=swp.PAR_YIELD), TODAY, None),
        ("collateralized cash", build(E5, "tenor", (2, Y), None, settlementType=swp.CASH,
                                      settlementMethod=swp.COLLATERALIZED), TODAY, None),
        ("inconsistent pair builds", build(E5, "tenor", (2, Y), 0.03, settlementMethod=swp.PAR_YIELD), TODAY, None),
        ("physical cleared", build(E5, "tenor", (1, Y), 0.03, settlementMethod=swp.CLEARED), TODAY, None),
        ("option convention Following", build(E5, "tenor", (1, M), None, optionConvention=int(ql.Following)),
         ql.Date(30, ql.January, 2025), None),
        ("option convention Preceding", build(E5, "tenor", (1, M), None, optionConvention=int(ql.Preceding)),
         ql.Date(30, ql.January, 2025), None),
        ("exercise date before the fixing date", build(E5, "tenor", (1, Y), 0.03,
                                                      exerciseDate=ql.Date(15, ql.December, 2025)), TODAY, None),
        ("exercise date after the fixing date, refused", build(E5, "tenor", (1, Y), 0.03,
                                                              exerciseDate=ql.Date(15, ql.February, 2026)),
         TODAY, None),
        ("exercise calendar", build(E5, "tenor", (1, Y), None, exerciseCalendar=US_SOFR),
         ql.Date(9, ql.October, 2025), None),
        ("on a Saturday", build(E5, "tenor", (6, M)), SATURDAY, None),
        ("on a Saturday, exercise calendar", build(E5, "tenor", (6, M), 0.03, exerciseCalendar=UK_EXCHANGE),
         SATURDAY, None),
        ("indexed coupons", build(E5, "tenor", (1, Y), 0.03, indexedCoupons=True), TODAY, None),
        ("at-par coupons false", build(E5, "tenor", (1, Y), 0.03, atParCoupons=False), TODAY, None),
        ("the context's switch off", build(E5, "tenor", (1, Y)), TODAY, False),
        ("the context's switch off, at-par coupons", build(E5, "tenor", (1, Y), None, atParCoupons=True), TODAY,
         False),
        ("pricing engine changes nothing", build(E5, "tenor", (1, Y), None, pricingEngine=engine), TODAY, None),
        ("swap index on Euribor 6M", build(by_name["2Y on Euribor 6M"], "tenor", (1, Y)), TODAY, None),
        ("swap index, semiannual fixed leg", build(by_name["18M, semiannual fixed leg"], "tenor", (6, M)), TODAY,
         None),
        ("swap index, US dollar exogenous", build(by_name["10Y US dollar exogenous"], "tenor", (2, Y)), TODAY, None),
        ("swap index, pound", build(by_name["5Y pound"], "tenor", (1, Y), None,
                                    underlyingType=int(ql.Swap.Receiver)), TODAY, None),
        ("swap index, null calendar", build(by_name["5Y null calendar, unadjusted"], "tenor", (1, Y)), TODAY, None),
        ("swap index with no curve at the money, refused", build(by_name["5Y on an index with no curve"], "tenor",
                                                                 (1, Y)), TODAY, None),
        ("swap index with no curve at 3%", build(by_name["5Y on an index with no curve"], "tenor", (1, Y), 0.03),
         TODAY, None),
        ("overnight compounded at the money", build(ois_compound, "tenor", (1, Y)), TODAY, None),
        ("overnight telescopic at the money", build(ois_telescopic, "tenor", (6, M)), TODAY, None),
        ("overnight averaged at the money", build(ois_averaged, "tenor", (1, Y), None, nominal=1e6), TODAY, None),
        ("overnight telescopic, averaged at the money, cash", build(
            ois_telescopic_averaged, "tenor", (2, Y), None, settlementType=swp.CASH,
            settlementMethod=swp.PAR_YIELD), TODAY, None),
    ]
    out.append(("overnight pound at 4.5%, receiver", build(ois_uk, "date", ql.Date(15, ql.July, 2025), 0.045,
                                                           underlyingType=int(ql.Swap.Receiver)), TODAY, None))
    assert len(out) == 41, len(out)
    return out


def outputs(swaption, scale):
    u = swaption.underlying()
    sw_engine = fvf.engine(NPV_CURVE, False)
    u.setPricingEngine(sw_engine)
    return {"exerciseDates": v.enc(list(swaption.exercise().dates())),
            "settlementType": int(swaption.settlementType()),
            "settlementMethod": int(swaption.settlementMethod()), "type": int(swaption.type()),
            "fixedRate": u.fixedRate(), "nominal": u.nominal(), "startDate": v.enc(u.startDate()),
            "maturityDate": v.enc(u.maturityDate()), "fixedSchedule": fvf.schedule_dates(u.fixedSchedule()),
            "floatingSchedule": fvf.schedule_dates(u.floatingSchedule()),
            "underlyingNPV": fvf.ibc.scaled(u.NPV, scale)}


def cases():
    out = []
    for name, spec, evaluation_date, at_par in builds():
        settings = {} if at_par is None else {"use_at_par_coupons": at_par}
        with v.case_settings(evaluation_date, **settings) as st:
            nominal = dict(spec["setters"]).get("nominal", 1.0)
            try:
                swaption = make(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                expected = outputs(swaption, nominal)
            out.append({"name": name, "inputs": {"settings": st, "build": spec, "npvCurve": NPV_CURVE},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for refused in ("fixing date on a Saturday, refused", "exercise date after the fixing date, refused",
                    "swap index with no curve at the money, refused",
                    "exogenous preset on an empty handle at the money"):
        assert by_name[refused] == {"build": v.THROWS}, refused
    # A given strike is the fixed rate; an engine set on the builder changes nothing.
    assert by_name["1Y on 5Y at 3%"]["fixedRate"] == 0.03
    assert by_name["pricing engine changes nothing"] == by_name["1Y on 5Y at the money"]
    # Indexed coupons change the underlying's floating leg.
    assert by_name["indexed coupons"]["underlyingNPV"] != by_name["1Y on 5Y at 3%"]["underlyingNPV"]


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makeswaption.hpp", cases)
