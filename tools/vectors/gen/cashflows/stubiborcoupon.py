# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's StubIborCoupon and StubIndexSelection (ql/cashflows/stubiborcoupon.hpp), on the evaluation
date 15 January 2025 and Euribor-style 1W, 1M, 3M, 6M and 1Y indexes, each on a flat curve of its own (2.5% to 3.8%),
the candidates of every selection.

24 stub periods, each under both conventions (ClosestIndex and Interpolated): the irregular first or last period of an
IborLeg built with indexed coupons and the selection, short and long front and back stubs from a week to over a year,
fixing before the evaluation date (fixings stored for every candidate, or for some only, which QuantLib then refuses as
missing), on it (a fixing stored for every candidate, or for some, or none; and the same three again with
enforcesTodaysHistoricFixings, under which the stub has fixed and a missing candidate fixing is refused), and after it;
and a leg with front and back stubs whose front period has a zero gearing, a fixed-rate coupon the selection leaves
alone, so that the stub coupon is the back one. A stub that no two maturities bracket is refused under Interpolated
("leg": {"throws": true}). Each case's outputs are, per cash flow, whether it is a stub
coupon, and the stub coupon's accrual dates, fixing date, hasFixed, rate, amount, its index's name (each candidate's
name and weight, written with four decimals) and its index's fixing on the fixing date. Six more cases build a
StubIborCoupon directly: two coupons, and its constructor's refusals of an empty selection, an in-arrears coupon, a
start that isn't the candidates' value date, and two candidates of one maturity ("coupon": {"throws": true}).

A refused value is {"throws": true}; amounts are compared at the leg's notional. Each case's inputs hold the leg spec
("leg", as iborcoupon.py's functions build it, with its "stubIndexSelection") or the coupon spec ("coupon": a
"StubIborCoupon" with "paymentDate", "nominal", "startDate", "endDate", "fixingDays", "stubIndexSelection",
"dayCounter", "isInArrears" and "pricer"). Read back by tests/NanoQL.Tests/Vectors/Coupons/StubIborCouponVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_iborcoupon", os.path.join(v.GEN_ROOT, "cashflows", "iborcoupon.py"))
ibc = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(ibc)

irx = ibc.irx
TODAY = ibc.TODAY
d = ibc.d

TENORS = [(1, ql.Weeks), (1, ql.Months), (3, ql.Months), (6, ql.Months), (1, ql.Years)]
CANDIDATES = [ibc.euribor(t, irx.curve_spec(TODAY, f)) for t, f in zip(TENORS, (0.025, 0.028, 0.031, 0.034, 0.038))]
E6M = CANDIDATES[3]
SIX_M = ibc.SIX_M
CONVENTIONS = [ql.StubIndexSelection.ClosestIndex, ql.StubIndexSelection.Interpolated]


def selection(convention, indices=None):
    return {"convention": int(convention), "indices": CANDIDATES if indices is None else indices}


def stubs():
    """(name, schedule, fixings for every candidate (True), for the first two only ("some"), or none (False)[, the leg's
    other settings and whether the case enforces today's historic fixings])."""
    s = ibc.sched
    fwd = ql.DateGeneration.Forward
    return [
        ("a 5-month front stub", s(d(20, 2, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 4-month front stub", s(d(17, 3, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 3-month front stub", s(d(17, 4, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 6-week front stub", s(d(3, 6, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 2-week front stub", s(d(3, 7, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 1-week front stub", s(d(10, 7, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 3-day front stub", s(d(14, 7, 2025), d(17, 7, 2027), SIX_M), False),
        ("a 10-month long front stub", s(d(17, 9, 2025), d(17, 1, 2028), SIX_M, rule=fwd, first=d(17, 7, 2026)),
         False),
        ("a 14-month long front stub", s(d(17, 5, 2025), d(17, 1, 2028), SIX_M, rule=fwd, first=d(17, 7, 2026)),
         False),
        ("a 2-month back stub", s(d(17, 7, 2025), d(17, 3, 2027), SIX_M, rule=fwd), False),
        ("a 5-month back stub", s(d(17, 7, 2025), d(17, 6, 2027), SIX_M, rule=fwd), False),
        ("a 1-week back stub", s(d(17, 7, 2025), d(25, 1, 2027), SIX_M, rule=fwd), False),
        ("a past 5-month front stub, every fixing stored", s(d(19, 8, 2024), d(17, 1, 2027), SIX_M), True),
        ("a past 2-month front stub, every fixing stored", s(d(18, 11, 2024), d(17, 7, 2026), SIX_M), True),
        ("a past stub, some fixings stored", s(d(19, 8, 2024), d(17, 1, 2027), SIX_M), "some"),
        ("a past stub, no fixing stored", s(d(18, 11, 2024), d(17, 7, 2026), SIX_M), False),
        ("a stub fixing today, every fixing stored", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M, first=d(17, 3, 2025),
                                                         rule=fwd), True),
        ("a stub fixing today, some fixings stored", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M, first=d(17, 3, 2025),
                                                        rule=fwd), "some"),
        ("a stub fixing today, none stored", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M, first=d(17, 3, 2025),
                                               rule=fwd), False),
        ("a stub fixing tomorrow", s(d(20, 1, 2025), d(17, 7, 2027), SIX_M), False),
        ("front and back stubs, the front one fixed", s(d(3, 2, 2025), d(28, 9, 2027), SIX_M, first=d(17, 4, 2025),
                                                        next_to_last=d(17, 4, 2027)), False,
         {"leg": {"gearings": [0.0, 1.0], "spreads": [0.0015], "payment_dc": ibc.A360}}),
        ("a stub fixing today, every fixing stored, enforced", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M,
                                                                   first=d(17, 3, 2025), rule=fwd), True,
         {"enforce": True}),
        ("a stub fixing today, some fixings stored, enforced", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M,
                                                                  first=d(17, 3, 2025), rule=fwd), "some",
         {"enforce": True}),
        ("a stub fixing today, none stored, enforced", s(d(17, 1, 2025), d(17, 7, 2027), SIX_M, first=d(17, 3, 2025),
                                                         rule=fwd), False, {"enforce": True}),
    ]


def fixings_for(schedule_spec, which):
    if not which:
        return None
    sched = ibc.schedule(schedule_spec)
    cal = irx.calendar(ibc.TARGET)
    fixing = cal.advance(sched[0], -2, ql.Days)
    out = []
    for k, spec in enumerate(CANDIDATES if which is True else CANDIDATES[:2]):
        pairs = ibc.history(spec, fixing - 3, min(fixing + 3, TODAY), v.SEED + k)
        out.append((irx.make_index(spec), pairs))
    return out


def stub_coupons():
    """(name, coupon spec) built with StubIborCoupon's constructor."""
    def c(start, end, sel, in_arrears=False):
        return {"type": "StubIborCoupon", "paymentDate": v.enc(end), "nominal": 1e6, "startDate": v.enc(start),
                "endDate": v.enc(end), "fixingDays": 2, "stubIndexSelection": sel, "dayCounter": ibc.A360,
                "isInArrears": in_arrears,
                "pricer": ibc.pricer_spec(use_indexed=True)}
    interpolated = selection(ql.StubIndexSelection.Interpolated)
    return [
        ("a 4-month stub", c(d(17, 3, 2025), d(17, 7, 2025), interpolated)),
        ("a 4-month stub on the closest index", c(d(17, 3, 2025), d(17, 7, 2025),
                                                  selection(ql.StubIndexSelection.ClosestIndex))),
        ("an empty selection", c(d(17, 3, 2025), d(17, 7, 2025), None)),
        ("in arrears", c(d(17, 3, 2025), d(17, 7, 2025), interpolated, True)),
        ("a start that isn't the value date", c(d(15, 3, 2025), d(17, 7, 2025), interpolated)),
        ("two candidates of one maturity", c(d(17, 3, 2025), d(17, 7, 2025),
                                             selection(ql.StubIndexSelection.Interpolated, [E6M, E6M]))),
    ]


def stub_coupon(spec):
    sel = ibc.stub_selection(spec["stubIndexSelection"])
    # The wheel's constructor takes its arguments by position only, and can't build QuantLib's empty day counter.
    c = ql.StubIborCoupon(ql.Date(spec["paymentDate"]), spec["nominal"], ql.Date(spec["startDate"]),
                          ql.Date(spec["endDate"]), spec["fixingDays"], sel, 1.0, 0.0, ql.Date(), ql.Date(),
                          irx.day_counter(spec["dayCounter"]), spec["isInArrears"])
    c.setPricer(ibc.pricer(spec["pricer"]))
    return c


def coupon_outputs(c, scale):
    return {"accrualStartDate": v.enc(c.accrualStartDate()), "accrualEndDate": v.enc(c.accrualEndDate()),
            "fixingDate": v.enc(c.fixingDate()), "hasFixed": c.hasFixed(), "rate": v.attempt(c.rate),
            "amount": ibc.scaled(c.amount, scale), "indexName": c.index().name(),
            "indexFixing": v.attempt(lambda: c.index().fixing(c.fixingDate()))}


def cases():
    out = []
    for k, (name, sched, which, *extra) in enumerate(stubs()):
        settings = extra[0] if extra else {}
        for convention in CONVENTIONS:
            spec = ibc.leg_spec(sched, E6M, [1e6], indexed=True, stub=selection(convention),
                                **settings.get("leg", {}))
            with v.case_settings(TODAY, enforces_todays_historic_fixings=settings.get("enforce", False),
                                 fixings=fixings_for(sched, which)) as st:
                label = f"{k}: {name}, {'interpolated' if convention else 'closest index'}"
                inputs = {"settings": st, "leg": spec}
                try:
                    cashflows = ibc.leg(spec)
                except RuntimeError:
                    out.append({"name": label, "inputs": inputs, "expected": {"leg": dict(v.THROWS)}})
                    continue
                stubbed = [ql.as_stub_ibor_coupon(cf) for cf in cashflows]
                expected = {"isStub": [s is not None for s in stubbed]}
                stub = next(s for s in stubbed if s is not None)
                expected.update(coupon_outputs(stub, 1e6))
                out.append({"name": label, "inputs": inputs, "expected": expected})
    for name, spec in stub_coupons():
        with v.case_settings(TODAY) as st:
            inputs = {"settings": st, "coupon": spec}
            try:
                c = stub_coupon(spec)
            except RuntimeError:
                out.append({"name": f"constructor: {name}", "inputs": inputs, "expected": {"coupon": dict(v.THROWS)}})
                continue
            out.append({"name": f"constructor: {name}", "inputs": inputs, "expected": coupon_outputs(c, 1e6)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/stubiborcoupon.hpp", cases)
