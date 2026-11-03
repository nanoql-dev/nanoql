# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FX-resetting cash flows (ql/experimental/fx/fxresetcashflows.hpp) on the evaluation date
15 January 2025, a EUR constant leg against a USD resettable one:
- FxReset: its dates, and its refusals (a null date, a fixing after the value date);
- FxResetConvention: the reset a value date gives and the value date a fixing date gives, with no fixing lag, two
  days on TARGET and on the US settlement calendar, and the refusal of a lag without a calendar;
- DiscountingFxResetPricer's rates: past fixings from the exchange-rate registry (a direct rate, its inverse, a
  triangulation through GBP), a missing past fixing (refused), today's fixing stored, missing (forecast) and missing
  where the context enforces today's fixings (refused), and future resets projected from the spot, quoted either way,
  with and without a spot settlement date, and the refusal of a zero spot that the pricer inverts;
- FxResetCoupon: its nominal (refused before a pricer is set), rate, index fixing, amount, accrued amount and reset
  dates, over Ibor coupons, one of them seasoned (reading a stored fixing and a past FX fixing), and its refusal of a
  zero underlying nominal;
- FxResetNotionalExchange: its amount with a previous reset, a current one or both (refused before a pricer is set),
  and its refusal of neither;
- setFxResetPricer on a leg of coupons, exchanges and a plain cash flow, which it leaves alone. Left out: today's
non-positive fixing, which QuantLib forecasts over and nanoql refuses
(tests/NanoQL.Tests/Divergences/FxResetTodaysFixingTests.cs).

Each case's inputs hold its settings (with the exchange rates its fixings are) and "kind" with the kind's spec: "reset"
{"fixingDate", "valueDate"} gives "build" or the two dates; "convention" {"fixingDays", "calendar", "valueDate",
"fixingDate"} gives "reset" (its two dates) and "valueDate", each refused where QuantLib refuses; "rates" {"pricer",
"resets"} gives "fxRates"; "coupons" {"pricer", "coupons"} gives per coupon whether it builds, its nominal without a
pricer, nominal, rate, index fixing, amount, accrued amount at "accrualDate", constant-leg notional and reset dates;
"exchanges" {"pricer", "exchanges"} gives per exchange whether it builds, its amount without a pricer and its amount;
"leg" {"pricer", "coupons", "exchanges", "plainFlows"} gives the leg's dates and amounts after setFxResetPricer. A
pricer is {"constantLegCurrency", "resettableLegCurrency", "constantLegCurve", "resettableLegCurve", "spotFx",
"spotIsResettablePerConstant", "spotFxSettleDate"}; a coupon {"leg", "index", "constantLegNotional", "fxReset"}, the
underlying the index-th coupon of instruments/constnotionalcrosscurrencyswap.py's Ibor leg; an exchange {"paymentDate",
"constantLegNotional", "previousReset", "currentReset"}, a null reset none; a plain flow {"amount", "date"}. Amounts are
compared at 1e6. Read back by tests/NanoQL.Tests/Vectors/Experimental/FX/FxResetCashFlowsVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_constnotionalcrosscurrencyswap",
    os.path.join(v.GEN_ROOT, "instruments", "constnotionalcrosscurrencyswap.py"))
xcs = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(xcs)
irx = xcs.irx

TODAY, D, M, Y = xcs.TODAY, xcs.D, xcs.M, xcs.Y
SCALE = 1e6
US = {"type": "UnitedStates", "market": int(ql.UnitedStates.Settlement)}
EUR, USD, GBP = ql.EURCurrency(), ql.USDCurrency(), ql.GBPCurrency()
PAST, OLDER = D(10, 1, 2025), D(6, 1, 2025)


def date_or_null(serial):
    return ql.Date() if serial is None else ql.Date(serial)


def reset(fixing, value):
    return {"fixingDate": v.enc(fixing), "valueDate": v.enc(value)}


def make_reset(spec):
    return ql.FxReset(date_or_null(spec["fixingDate"]), date_or_null(spec["valueDate"]))


def pricer(spot=1.1, per_constant=True, settle=None):
    return {"constantLegCurrency": "EUR", "resettableLegCurrency": "USD", "constantLegCurve": xcs.EUR_DISCOUNT,
            "resettableLegCurve": xcs.USD_DISCOUNT, "spotFx": spot, "spotIsResettablePerConstant": per_constant,
            "spotFxSettleDate": v.enc(settle)}


def make_pricer(spec):
    return ql.DiscountingFxResetPricer(
        xcs.currency(spec["constantLegCurrency"]), xcs.currency(spec["resettableLegCurrency"]),
        irx.handle(spec["constantLegCurve"]), irx.handle(spec["resettableLegCurve"]),
        ql.QuoteHandle(ql.SimpleQuote(spec["spotFx"])), spec["spotIsResettablePerConstant"],
        date_or_null(spec["spotFxSettleDate"]))


def coupon(leg, index, notional, fx_reset):
    return {"leg": leg, "index": index, "constantLegNotional": notional, "fxReset": fx_reset}


def make_coupon(spec):
    underlying = ql.as_floating_rate_coupon(xcs.make_leg(spec["leg"])[spec["index"]])
    return ql.FxResetCoupon(underlying, spec["constantLegNotional"], make_reset(spec["fxReset"]))


def exchange(payment, notional, previous, current):
    return {"paymentDate": v.enc(payment), "constantLegNotional": notional, "previousReset": previous,
            "currentReset": current}


def make_exchange(spec):
    previous = None if spec["previousReset"] is None else make_reset(spec["previousReset"])
    current = None if spec["currentReset"] is None else make_reset(spec["currentReset"])
    return ql.FxResetNotionalExchange(date_or_null(spec["paymentDate"]), spec["constantLegNotional"], previous,
                                      current)


def rates_registry():
    """The past fixings: EUR/USD directly on 10 January, its inverse on 6 January, and EUR/GBP and GBP/USD on
    13 January (a triangulation); today's EUR/USD where a case stores it."""
    return [(ql.ExchangeRate(EUR, USD, 1.0832), PAST, PAST), (ql.ExchangeRate(USD, EUR, 0.9251), OLDER, OLDER),
            (ql.ExchangeRate(EUR, GBP, 0.8417), D(13, 1, 2025), D(13, 1, 2025)),
            (ql.ExchangeRate(GBP, USD, 1.2861), D(13, 1, 2025), D(13, 1, 2025))]


TODAYS = (ql.ExchangeRate(EUR, USD, 1.0915), TODAY, TODAY)


def coupon_outputs(spec, p):
    try:
        cpn = make_coupon(spec)
    except RuntimeError:
        return {"build": dict(v.THROWS)}
    out = {"build": True, "nominalWithoutPricer": v.attempt(cpn.nominal)}
    cpn.setFxResetPricer(p)
    accrual = cpn.accrualStartDate() + 20
    out.update({"nominal": xcs.fvf.ibc.scaled(cpn.nominal, SCALE), "rate": v.attempt(cpn.rate),
                "indexFixing": v.attempt(cpn.indexFixing), "amount": xcs.fvf.ibc.scaled(cpn.amount, SCALE),
                "accruedAmount": xcs.fvf.ibc.scaled(lambda: cpn.accruedAmount(accrual), SCALE),
                "accrualDate": accrual.serialNumber(), "constantLegNotional": cpn.constantLegNotional(),
                "fxResetDate": cpn.fxResetDate().serialNumber(),
                "fxResetValueDate": cpn.fxResetValueDate().serialNumber()})
    return out


def exchange_outputs(spec, p):
    try:
        x = make_exchange(spec)
    except RuntimeError:
        return {"build": dict(v.THROWS)}
    out = {"build": True, "amountWithoutPricer": v.attempt(x.amount)}
    x.setFxResetPricer(p)
    out["amount"] = xcs.fvf.ibc.scaled(x.amount, SCALE)
    return out


def table(rows):
    """Per-item outputs as one list per output name, an item without the output (one that didn't build) null."""
    names = []
    for r in rows:
        names += [n for n in r if n not in names]
    return {n + "s" if not n.endswith("s") else n: [r.get(n) for r in rows] for n in names}


def cases():
    q1 = xcs.sched(xcs.SPOT, D(17, 1, 2026), (3, M))
    seasoned = xcs.sched(D(17, 10, 2024), D(17, 10, 2025), (3, M))
    leg = xcs.ibor_leg(q1, 1.1e6, xcs.USD3M, exchanges=False)
    old_leg = xcs.ibor_leg(seasoned, 1.1e6, xcs.USD3M, 0.001, exchanges=False)
    zero_leg = xcs.ibor_leg(q1, 0.0, xcs.USD3M, exchanges=False)
    later, end = D(17, 4, 2025), D(17, 7, 2025)
    out = []

    def add(name, kind, spec, expected, **settings):
        with v.case_settings(TODAY, **settings) as st:
            out.append({"name": name, "inputs": {"settings": st, "kind": kind, kind: spec}, "expected": expected()})

    for name, spec in [("a reset", reset(TODAY, xcs.SPOT)), ("a reset on its value date", reset(later, later)),
                       ("a null fixing date", reset(None, later)), ("a null value date", reset(later, None)),
                       ("a fixing after the value date", reset(later + 1, later))]:
        def built(spec=spec):
            try:
                r = make_reset(spec)
            except RuntimeError:
                return {"build": dict(v.THROWS)}
            return {"build": True, "fixingDate": r.fixingDate().serialNumber(),
                    "valueDate": r.valueDate().serialNumber()}
        add(name, "reset", spec, built)

    for name, days, cal in [("no fixing lag", 0, None), ("no fixing lag on TARGET", 0, irx.TARGET),
                            ("two days on TARGET", 2, irx.TARGET), ("two days on the US calendar", 2, US),
                            ("a lag without a calendar", 2, None)]:
        spec = {"fixingDays": days, "calendar": cal, "valueDate": v.enc(D(21, 1, 2025)),
                "fixingDate": v.enc(D(17, 1, 2025))}

        def converted(spec=spec):
            c = ql.FxResetConvention(spec["fixingDays"], irx.calendar(spec["calendar"])) \
                if spec["calendar"] is not None else ql.FxResetConvention(spec["fixingDays"])
            r = v.attempt(lambda: [c.reset(ql.Date(spec["valueDate"])).fixingDate(),
                                   c.reset(ql.Date(spec["valueDate"])).valueDate()])
            return {"reset": r, "valueDate": v.attempt(lambda: c.valueDate(ql.Date(spec["fixingDate"])))}
        add(name, "convention", spec, converted)

    resets = [reset(PAST, PAST + 2), reset(OLDER, OLDER + 2), reset(D(13, 1, 2025), xcs.SPOT), reset(D(9, 1, 2025),
              D(13, 1, 2025)), reset(TODAY, xcs.SPOT), reset(later - 2, later), reset(end, end), reset(D(15, 1, 2027),
              D(19, 1, 2027))]
    for name, p, todays, enforce in [
            ("the spot per constant", pricer(), False, False),
            ("the spot per resettable, inverted", pricer(1 / 1.1, False), False, False),
            ("a spot settlement date", pricer(settle=xcs.SPOT), False, False),
            ("today's fixing stored", pricer(), True, False),
            ("today's fixing enforced and stored", pricer(), True, True),
            ("today's fixing enforced and missing", pricer(), False, True),
            ("a zero spot, inverted", pricer(0.0, False), False, False)]:
        spec = {"pricer": p, "resets": resets}
        registry = rates_registry() + ([TODAYS] if todays else [])

        def rated(spec=spec):
            pr = make_pricer(spec["pricer"])
            return {"fxRates": [v.attempt(lambda r=r: pr.fxRate(make_reset(r))) for r in spec["resets"]]}
        add(name, "rates", spec, rated, exchange_rates=registry, enforces_todays_historic_fixings=enforce)

    coupons = [coupon(leg, 0, 1e6, reset(TODAY, xcs.SPOT)), coupon(leg, 2, 1e6, reset(D(15, 7, 2025), D(17, 7, 2025))),
               coupon(leg, 3, 0.9e6, reset(D(15, 10, 2025), D(17, 10, 2025))),
               coupon(old_leg, 0, 1e6, reset(D(15, 10, 2024), D(17, 10, 2024))),
               coupon(zero_leg, 1, 1e6, reset(D(15, 4, 2025), D(17, 4, 2025)))]
    for name, p in [("coupons, the spot per constant", pricer()), ("coupons, a spot settlement date",
                                                                     pricer(settle=xcs.SPOT))]:
        spec = {"pricer": p, "coupons": coupons}

        def couponed(spec=spec):
            pr = make_pricer(spec["pricer"])
            return table([coupon_outputs(c, pr) for c in spec["coupons"]])
        add(name, "coupons", spec, couponed, exchange_rates=rates_registry() + [
            (ql.ExchangeRate(EUR, USD, 1.0721), D(15, 10, 2024), D(15, 10, 2024))],
            fixings=[(xcs.make_index(xcs.USD3M), [(D(15, 10, 2024), 0.0471)])])

    exchanges = [exchange(later, 1e6, reset(TODAY, xcs.SPOT), reset(later - 2, later)),
                 exchange(xcs.SPOT, 1e6, None, reset(TODAY, xcs.SPOT)),
                 exchange(D(19, 1, 2026), 1e6, reset(D(15, 1, 2026), D(17, 1, 2026)), None),
                 exchange(later, 1e6, reset(PAST, PAST + 2), reset(later - 2, later)),
                 exchange(later, 1e6, None, None), exchange(None, 1e6, reset(TODAY, xcs.SPOT), None)]
    spec = {"pricer": pricer(), "exchanges": exchanges}

    def exchanged(spec=spec):
        pr = make_pricer(spec["pricer"])
        return table([exchange_outputs(x, pr) for x in spec["exchanges"]])
    add("notional exchanges", "exchanges", spec, exchanged, exchange_rates=rates_registry())

    spec = {"pricer": pricer(), "coupons": coupons[:3], "exchanges": exchanges[:3],
            "plainFlows": [{"amount": 1e6, "date": v.enc(later)}]}

    def legged(spec=spec):
        flows = [make_coupon(c) for c in spec["coupons"]] + [make_exchange(x) for x in spec["exchanges"]] + [
            ql.SimpleCashFlow(f["amount"], ql.Date(f["date"])) for f in spec["plainFlows"]]
        leg_ = ql.Leg(flows)
        ql.setFxResetPricer(leg_, make_pricer(spec["pricer"]))
        return {"dates": [cf.date().serialNumber() for cf in leg_],
                "amounts": [xcs.fvf.ibc.scaled(cf.amount, SCALE) for cf in leg_]}
    add("a leg, setFxResetPricer", "leg", spec, legged, exchange_rates=rates_registry())
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/fx/fxresetcashflows.hpp", cases)
