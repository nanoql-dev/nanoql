# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ForwardRateAgreement (ql/instruments/forwardrateagreement.hpp, .cpp), which computes in the
instrument with no engine: 14 FRAs on the evaluation date 15 January 2025, long and short, on Euribor 3M and 6M and
Tibor 3M forecasting on flat curves; the value-date constructor, whose forward rate is the index fixing, and the
value-and-maturity one, whose forward rate is the par approximation from the forecasting curve, its maturity adjusted on
the index's calendar by its convention (a maturity on a weekend among them); with no discount curve, the index's curve
then discounting, and with another; strikes above, at and below the forward; a value date whose fixing date is past,
reading a stored fixing, and one whose fixing date is the evaluation date, with and without today's fixing stored; and
refused constructions: a notional that isn't positive ("notionalAmount must be positive") and a value date not before
the maturity ("valueDate must be earlier than maturityDate").

Each case's outputs are whether the FRA builds, QuantLib's refusal recorded as its throw; then its fixing date, whether
it has expired, the forward rate, the amount (the payoff on the value date) and the NPV, amounts compared at the
notional. Two expired FRAs, whose value dates are before the evaluation date, give their fixing date, expiry and NPV,
QuantLib's zero; left out: an expired FRA's amount, which QuantLib reads before computing it, and the forward rate and
NPV of an expired par FRA, which QuantLib computes from its curve before the curve's reference date and refuses
("negative time ... given"), all fixed in nanoql (Divergences/ForwardRateAgreementExpiredTests.cs).

Each case's inputs hold its settings, with the fixings it reads, and "fra" ({"index", "valueDate", "maturityDate", null
for the value-date constructor, "type" (QuantLib's Position::Type integer), "strike", "notional", "discountCurve", a
flat forward spec or null}): the index is a preset spec of instruments/fixedvsfloatingswap.py. The wheel's
ForwardRateAgreement is QuantLib-SWIG's proxy of the C++ class, its constructors QuantLib's two. Read back by
tests/NanoQL.Tests/Vectors/Instruments/ForwardRateAgreementVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_fixedvsfloatingswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "fixedvsfloatingswap.py"))
fvf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fvf)

TODAY = fvf.TODAY
D = fvf.d
LONG, SHORT = ql.Position.Long, ql.Position.Short
FORECAST = fvf.curve(0.031)
STEEP = fvf.curve(0.045, fvf.A360, ql.Simple)
DISCOUNT = fvf.curve(0.027)
EURIBOR6M = fvf.preset("Euribor6M", FORECAST)
EURIBOR3M = fvf.preset("Euribor3M", STEEP)
TIBOR3M = fvf.preset("Tibor", FORECAST, ql.Period(3, ql.Months))


def fra(index, value_date, position, strike, notional=1e6, maturity=None, discount=None):
    return {"index": index, "valueDate": v.enc(value_date), "maturityDate": v.enc(maturity), "type": int(position),
            "strike": strike, "notional": notional, "discountCurve": discount}


def make_fra(spec):
    index = fvf.make_index(spec["index"])
    handle = fvf.irx.handle(spec["discountCurve"])
    value_date = ql.Date(spec["valueDate"])
    if spec["maturityDate"] is None:
        return ql.ForwardRateAgreement(index, value_date, spec["type"], spec["strike"], spec["notional"], handle)
    return ql.ForwardRateAgreement(index, value_date, ql.Date(spec["maturityDate"]), spec["type"], spec["strike"],
                                   spec["notional"], handle)


def fras():
    """(name, spec, fixings, expired: None, "indexed" or "par") for the cases."""
    e6 = fvf.make_index(EURIBOR6M)
    e3 = fvf.make_index(EURIBOR3M)
    out = [
        ("indexed long, Euribor 6M", fra(EURIBOR6M, D(17, 7, 2025), LONG, 0.03), None, None),
        ("indexed short, Euribor 6M, at the money", fra(EURIBOR6M, D(17, 7, 2025), SHORT, 0.0315), None, None),
        ("indexed long, Euribor 3M, a discount curve", fra(EURIBOR3M, D(17, 3, 2025), LONG, 0.05, 2.5e6,
                                                         discount=DISCOUNT), None, None),
        ("indexed short, Tibor 3M", fra(TIBOR3M, D(15, 4, 2025), SHORT, 0.02, 1e8), None, None),
        ("indexed, 2 years forward", fra(EURIBOR6M, D(16, 1, 2027), LONG, 0.04), None, None),
        ("par long, Euribor 6M", fra(EURIBOR6M, D(17, 7, 2025), LONG, 0.03, maturity=D(19, 1, 2026)), None, None),
        ("par short, Euribor 3M, a discount curve", fra(EURIBOR3M, D(17, 3, 2025), SHORT, 0.044, 5e5,
                                                        maturity=D(17, 6, 2025), discount=DISCOUNT), None, None),
        ("par, maturity on a Saturday", fra(EURIBOR6M, D(17, 3, 2025), LONG, 0.03, maturity=D(13, 9, 2025)), None,
         None),
        ("par, a broken period", fra(TIBOR3M, D(3, 2, 2025), SHORT, 0.03, maturity=D(28, 5, 2025)), None, None),
        ("indexed, fixing date past", fra(EURIBOR6M, D(16, 1, 2025), LONG, 0.03),
         [(e6, [(D(14, 1, 2025), 0.0312)])], None),
        ("indexed, fixing today, stored", fra(EURIBOR3M, D(17, 1, 2025), SHORT, 0.03),
         [(e3, [(D(15, 1, 2025), 0.0298)])], None),
        ("indexed, fixing today, forecast", fra(EURIBOR3M, D(17, 1, 2025), SHORT, 0.03), None, None),
        ("indexed, expired", fra(EURIBOR6M, D(14, 1, 2025), LONG, 0.03),
         [(e6, [(D(10, 1, 2025), 0.0305)])], "indexed"),
        ("par, expired", fra(EURIBOR6M, D(14, 1, 2025), LONG, 0.03, maturity=D(14, 7, 2025)), None, "par"),
        ("a zero notional", fra(EURIBOR6M, D(17, 7, 2025), LONG, 0.03, 0.0), None, None),
        ("a value date at the maturity", fra(EURIBOR6M, D(17, 7, 2025), LONG, 0.03, maturity=D(17, 7, 2025)), None,
         None),
    ]
    return out


def cases():
    out = []
    for name, spec, fx, expired in fras():
        scale = abs(spec["notional"]) or 1.0
        with v.case_settings(TODAY, fixings=fx) as st:
            try:
                f = make_fra(spec)
            except RuntimeError:
                expected = {"build": dict(v.THROWS)}
            else:
                expected = {"fixingDate": v.enc(f.fixingDate()), "isExpired": f.isExpired()}
                if expired != "par":
                    expected["forwardRate"] = v.attempt(lambda: f.forwardRate().rate())
                    expected["NPV"] = v.amount(v.attempt(f.NPV), scale)
                if expired is None:
                    expected["amount"] = v.amount(v.attempt(f.amount), scale)
            out.append({"name": name, "inputs": {"settings": st, "fra": spec}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/forwardrateagreement.hpp", cases)
