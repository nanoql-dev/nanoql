# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MakeCapFloor (ql/instruments/makecapfloor.hpp), on Euribor-style indexes (the wheel's IborIndex
with Euribor's conventions, on flat curves): 37 builds, on the evaluation date 15 January 2025 unless a case says
otherwise.

The builds are caps and floors from 6M to 10Y, struck or at the money: with no strike, QuantLib strikes the instrument at
the leg's ATM rate on the discount curve of the Black or Bachelier engine given to withPricingEngine (a curve other than
the index's among them), and refuses without one ("cannot calculate ATM without a BlackCapFloorEngine or
BachelierCapFloorEngine"). They cover every member: the nominal, an effective date with the first caplet excluded or
kept, a forward start (which keeps the first caplet), both together, the leg's tenor, calendar, convention,
termination-date convention, rule, end of month, first and next-to-last dates and day counter, an optionlet (the last
coupon alone), both at-par settings (the leg reads the switch when it is built), an evaluation date on a weekend, a
6M cap on the 6M index (its one coupon excluded: an empty, expired cap, struck at the empty leg's ATM rate 0), and a
collar, which the instrument's constructor refuses, after the ATM rate when there is no strike. Each build's outputs
are the instrument's type and rates, its start and maturity dates (or {"throws": true} on an empty leg), its NPV on
the wheel's Black engine (20%, Actual/365 (Fixed)) on the case's "discountCurve", and per coupon of its leg the outputs of
cashflows/iborcoupon.py's leg_outputs; a refused build records only "built": {"throws": true}. Amounts are compared at
the nominal.

A build is {"type", "tenor", "index", "strike", "forwardStart", "setters", "engine"}: the tenor and forward start as
[length, units], the strike null for none, the setters as [name, value] pairs (an effective date [date, flag], a
calendar or day counter as its spec, a convention or rule as QuantLib's integer, a flag), and the engine null or
{"type": "BlackCapFloorEngine" | "BachelierCapFloorEngine", "vol", "curve"}, whose curve nanoql's builder takes
through WithDiscountingTermStructure. The wheel's MakeCapFloor is a Python function that builds QuantLib.QuantLib.
_MakeCapFloor(capFloorType, capFloorTenor, iborIndex, strike, forwardStart) and applies keywords, one argument each,
so withEffectiveDate, which takes two, can't be reached through it (QuantLib-SWIG 1.44, SWIG/capfloor.i): the
generator calls _MakeCapFloor directly, held in a variable (a chained temporary ends the process), and calls the
setters on it in order. That constructor is C++'s five-argument one, deprecated in 1.44 and not ported, which
makecapfloor.cpp:41-49 defines as the three-argument constructor followed by withStrike(strike) and
withForwardStart(forwardStart): nanoql's test builds the three-argument record and calls WithStrike and
WithForwardStart, then the setters, so this file is also the oracle for those two members, which the wheel doesn't
bind. Read back by tests/NanoQL.Tests/Vectors/Instruments/MakeCapFloorVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_capfloor", os.path.join(v.GEN_ROOT, "instruments", "capfloor.py"))
cfl = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cfl)
ibc, irx = cfl.ibc, cfl.irx

TODAY = cfl.TODAY
D, W, M, Y = ql.Days, ql.Weeks, ql.Months, ql.Years
CAP, FLOOR, COLLAR = cfl.CAP, cfl.FLOOR, cfl.COLLAR
E6M, E3M = cfl.E6M, cfl.E3M
E6M_LOW = ibc.euribor((6, M), irx.curve_spec(TODAY, 0.02))
DISCOUNT = cfl.DISCOUNT
ENGINE_CURVE = irx.curve_spec(TODAY, 0.02)
d = cfl.d


def build_spec(type_, tenor, index=E6M, strike=None, forward_start=(0, D), setters=(), engine=None):
    return {"type": type_, "tenor": [tenor[0], int(tenor[1])], "index": index, "strike": strike,
            "forwardStart": [forward_start[0], int(forward_start[1])], "setters": [list(s) for s in setters],
            "engine": engine}


def black(curve=DISCOUNT, vol=0.2):
    return {"type": "BlackCapFloorEngine", "vol": vol, "curve": curve}


def bachelier(curve=DISCOUNT, vol=0.008):
    return {"type": "BachelierCapFloorEngine", "vol": vol, "curve": curve}


def engine_of(spec):
    if spec["type"] == "BlackCapFloorEngine":
        return cfl.black_engine(spec["curve"], cfl.black_spec(spec["vol"]))
    return cfl.bachelier_engine(spec["curve"], cfl.bachelier_spec(spec["vol"]))


def setter_value(name, value):
    if name == "effectiveDate":
        return [ql.Date(value[0]), value[1]]
    if name in ("firstDate", "nextToLastDate"):
        return [ql.Date(value)]
    if name == "calendar":
        return [irx.calendar(value)]
    if name == "dayCount":
        return [irx.day_counter(value)]
    if name == "tenor":
        return [ql.Period(value[0], value[1])]
    return [value]


SETTERS = {"nominal": "withNominal", "effectiveDate": "withEffectiveDate", "tenor": "withTenor",
           "calendar": "withCalendar", "convention": "withConvention",
           "terminationDateConvention": "withTerminationDateConvention", "rule": "withRule",
           "endOfMonth": "withEndOfMonth", "firstDate": "withFirstDate", "nextToLastDate": "withNextToLastDate",
           "dayCount": "withDayCount", "asOptionlet": "asOptionlet"}


def make(spec):
    """The wheel's instrument for a build, through _MakeCapFloor held in a variable."""
    strike = ql.nullDouble() if spec["strike"] is None else spec["strike"]
    mv = ql.QuantLib._MakeCapFloor(spec["type"], ql.Period(*spec["tenor"]), irx.make_index(spec["index"]), strike,
                                   ql.Period(*spec["forwardStart"]))
    for name, value in spec["setters"]:
        getattr(mv, SETTERS[name])(*setter_value(name, value))
    if spec["engine"] is not None:
        mv.withPricingEngine(engine_of(spec["engine"]))
    return mv.makeCapFloor()


def builds():
    """(name, build spec, settings) for the 37 builds; the settings are case_settings()'s keywords and an evaluation
    date."""
    out = []

    def add(name, spec, **settings):
        out.append((name, spec, settings))

    add("2Y cap at 3%", build_spec(CAP, (2, Y), strike=0.03))
    add("2Y floor at 3%", build_spec(FLOOR, (2, Y), strike=0.03))
    add("5Y cap at the money, Black engine", build_spec(CAP, (5, Y), engine=black()))
    add("5Y floor at the money, Bachelier engine", build_spec(FLOOR, (5, Y), engine=bachelier()))
    add("10Y cap at the money", build_spec(CAP, (10, Y), engine=black()))
    add("3Y cap at the money, engine curve other than the index's", build_spec(CAP, (3, Y), engine=black(ENGINE_CURVE)))
    add("3Y cap at the money on a 2% index", build_spec(CAP, (3, Y), E6M_LOW, engine=bachelier(DISCOUNT)))
    add("refused: at the money without an engine", build_spec(CAP, (3, Y)))
    add("refused: a collar with a strike", build_spec(COLLAR, (3, Y), strike=0.03))
    add("refused: a collar at the money", build_spec(COLLAR, (3, Y), engine=black()))
    add("2Y cap, 1Y forward start", build_spec(CAP, (2, Y), strike=0.03, forward_start=(1, Y)))
    add("2Y cap, 1M forward start, at the money", build_spec(CAP, (2, Y), forward_start=(1, M), engine=black()))
    add("2Y cap, effective date, first caplet kept", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(17, 3, 2025)), False))]))
    add("2Y cap, effective date, first caplet excluded", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(17, 3, 2025)), True))]))
    add("2Y cap, forward start then effective date", build_spec(
        CAP, (2, Y), strike=0.03, forward_start=(1, M), setters=[("effectiveDate", (v.enc(d(18, 6, 2025)), True))]))
    add("3Y floor, nominal 5 million", build_spec(FLOOR, (3, Y), strike=0.028, setters=[("nominal", 5e6)]))
    add("3Y cap on 3M periods of the 6M index", build_spec(CAP, (3, Y), strike=0.03, setters=[("tenor", (3, M))]))
    # 25 August 2025, six months after 25 February, is a UK bank holiday and a TARGET business day
    add("2Y cap on a UK calendar", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(25, 2, 2025)), False)),
                                          ("calendar", irx.UK_EXCHANGE)]))
    add("2Y cap on TARGET, the UK case's dates", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(25, 2, 2025)), False))]))
    # from 30 May 2025: 30 November 2025 and 30 May 2027 are Sundays
    add("2Y cap, Following", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(30, 5, 2025)), False)),
                                          ("convention", int(ql.Following))]))
    add("2Y cap, modified following, the Following case's dates", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(30, 5, 2025)), False))]))
    add("2Y cap, unadjusted termination", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(30, 5, 2025)), False)),
                                          ("terminationDateConvention", int(ql.Unadjusted))]))
    add("2Y cap on 5M periods, forward rule", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("tenor", (5, M)), ("rule", int(ql.DateGeneration.Forward))]))
    add("2Y cap on 5M periods, backward rule", build_spec(
        CAP, (2, Y), strike=0.03, setters=[("tenor", (5, M)), ("rule", int(ql.DateGeneration.Backward))]))
    add("3Y cap, end of month", build_spec(
        CAP, (3, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(28, 2, 2025)), False)), ("endOfMonth", True)]))
    add("3Y cap, end of month off", build_spec(
        CAP, (3, Y), strike=0.03, setters=[("effectiveDate", (v.enc(d(28, 2, 2025)), False)), ("endOfMonth", False)]))
    add("3Y cap, first date", build_spec(
        CAP, (3, Y), strike=0.03, setters=[("rule", int(ql.DateGeneration.Forward)), ("firstDate", v.enc(d(17, 4, 2025)))]))
    add("3Y cap, next-to-last date", build_spec(
        CAP, (3, Y), strike=0.03, setters=[("nextToLastDate", v.enc(d(17, 9, 2027)))]))
    add("3Y cap, Actual/365 (Fixed) leg", build_spec(CAP, (3, Y), strike=0.03, setters=[("dayCount", irx.A365)]))
    add("5Y cap optionlet", build_spec(CAP, (5, Y), strike=0.03, setters=[("asOptionlet", True)]))
    add("5Y floor optionlet at the money", build_spec(FLOOR, (5, Y), setters=[("asOptionlet", True)], engine=black()))
    add("5Y cap, optionlet set and cleared", build_spec(
        CAP, (5, Y), strike=0.03, setters=[("asOptionlet", True), ("asOptionlet", False)]))
    add("6M cap on the 6M index at the money", build_spec(CAP, (6, M), engine=black()))
    add("3Y cap on 3M periods at the money, at par", build_spec(
        CAP, (3, Y), setters=[("tenor", (3, M))], engine=black()), use_at_par_coupons=True)
    add("3Y cap on 3M periods at the money, indexed", build_spec(
        CAP, (3, Y), setters=[("tenor", (3, M))], engine=black()), use_at_par_coupons=False)
    add("2Y cap on 3M, indexed", build_spec(CAP, (2, Y), E3M, strike=0.031), use_at_par_coupons=False)
    add("2Y cap at the money, evaluation date on a Saturday", build_spec(CAP, (2, Y), engine=black()),
        evaluation_date=d(18, 1, 2025))
    return out


def outputs(cap, scale):
    out = {"built": True, "type": cap.type(), "capRates": list(cap.capRates()), "floorRates": list(cap.floorRates()),
           "startDate": v.attempt(cap.startDate), "maturityDate": v.attempt(cap.maturityDate)}
    cap.setPricingEngine(cfl.black_engine(DISCOUNT, cfl.black_spec()))
    out["blackNPV"] = cfl.attempt_scaled(cap.NPV, scale)
    out.update(ibc.leg_outputs(cap.floatingLeg(), scale))
    return out


def cases():
    out = []
    for name, spec, settings in builds():
        kw = dict(settings)
        date = kw.pop("evaluation_date", TODAY)
        with v.case_settings(date, **kw) as st:
            scale = next((x for n, x in spec["setters"] if n == "nominal"), 1.0)
            try:
                cap = make(spec)
            except RuntimeError:
                expected = {"built": dict(v.THROWS)}
            else:
                expected = outputs(cap, scale)
            out.append({"name": name, "inputs": {"settings": st, "build": spec, "discountCurve": DISCOUNT},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/makecapfloor.hpp", cases)
