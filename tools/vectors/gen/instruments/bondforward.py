# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BondForward (ql/instruments/bondforward.hpp) and its base Forward
(ql/instruments/forward.hpp): 29 forwards on 6 bonds on the evaluation date 15 January 2025, a flat 2.6% curve on
Actual/365 (Fixed) discounting the bonds, the contracts and their income.

The bonds are tools/vectors/gen/instruments/bond.py's specs: annual and semiannual fixed-rate bonds of face 100, a
zero-coupon bond, a floating-rate bond on Euribor 6M with its current coupon's fixing stored, an amortizing fixed-rate
bond and QuantLib's test-suite bond on its face of 1e5 (bondforward.cpp's), each settling on the evaluation date: the
spot value, the dirty price at the bond's settlement date, is then a reference-date value, as QuantLib's forward reads
it (on a bond settling later QuantLib grows the settlement-date price from the reference date,
tests/NanoQL.Tests/Divergences/BondForwardSpotDateTests.cs). Each forward is long or short, struck at 0 or at 100,
settling in 2 TARGET business days on Actual/Actual (ISDA), delivering before the bond's next coupon, after one or two
coupons, on a coupon date, or before the evaluation date (expired: an NPV of 0, its other results never computed).

Each forward's outputs are its settlement date, whether it has expired, the spot value (the bond's dirty price on
DiscountingBondEngine on the curve), the spot income on the curve, the forward value, the forward price, the clean
forward price, the NPV, and the implied yield of the spot value and the forward value from the settlement date,
continuous and compounded annually on Actual/365 (Fixed). The forwards with an income on a bond whose notional at its
settlement date isn't 100, the 1e5 face and the amortizing bond's 50, are generated and left out but their dates and
spot value: QuantLib's income is the flows' amounts, where the spot value is per 100 of that notional
(tests/NanoQL.Tests/Divergences/BondForwardIncomeTests.cs). Amounts are per 100 of notional.

A case's inputs hold its settings, the bond (bond.py's spec), the curve, and the forward's value date, delivery date,
position, strike and settlement days. The wheel's BondForward is the proxy of the C++ constructor, every argument
passed; its engine is the forward's own, priced through the bond's. Read back by
tests/NanoQL.Tests/Vectors/Instruments/BondForwardVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_bond", os.path.join(v.GEN_ROOT, "instruments", "bond.py"))
bd = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bd)

D = ql.Date
M, Y = ql.Months, ql.Years
TODAY = D(15, 1, 2025)
TARGET = bd.TARGET
CURVE = bd.curve_spec(TODAY, 0.026)
LONG, SHORT = int(ql.Position.Long), int(ql.Position.Short)
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
EURIBOR6M = bd.irx.ibor_spec("Euribor", (6, M), 2, TARGET, ql.ModifiedFollowing, True, bd.A360,
                             bd.irx.curve_spec(TODAY, 0.028))
FLOATING_FIXING = (D(16, 10, 2024), 0.0291)


def bonds():
    """(name, bond spec, face) for the 6 bonds."""
    annual = bd.schedule_spec(D(20, 3, 2022), D(20, 3, 2032), (1, Y), TARGET, ql.Following)
    semiannual = bd.schedule_spec(D(15, 5, 2023), D(15, 5, 2030), (6, M), TARGET, ql.ModifiedFollowing)
    floating = bd.schedule_spec(D(18, 4, 2024), D(18, 4, 2029), (6, M), TARGET, ql.ModifiedFollowing)
    suite = bd.schedule_spec(D(15, 8, 2015), D(15, 8, 2046), (1, Y), TARGET, ql.Following)
    return [
        ("annual 3%", bd.fixed_bond_spec(0, 100.0, annual, [0.03], ACT_ISDA), 100.0),
        ("semiannual 2.25%", bd.fixed_bond_spec(0, 100.0, semiannual, [0.0225], bd.THIRTY360_US), 100.0),
        ("zero coupon", bd.zero_bond_spec(0, TARGET, 100.0, D(15, 6, 2028)), 100.0),
        ("floating on Euribor 6M", bd.floating_bond_spec(0, 100.0, floating, EURIBOR6M, bd.A360), 100.0),
        ("amortizing 4%", bd.amortizing_bond_spec(0, [100.0, 75.0, 50.0, 25.0], annual, [0.04], ACT_ISDA), 100.0),
        ("the test suite's 2.5% on 1e5", bd.fixed_bond_spec(0, 1e5, suite, [0.025], ACT_ISDA), 1e5),
    ]


def deliveries(bond):
    """(label, delivery date) around the bond's coupons after the evaluation date."""
    coupons = sorted({cf.date() for cf in bond.cashflows() if cf.date() > TODAY + 3})
    first = coupons[0]
    out = [("before the next coupon", TARGET_CAL.advance(first, -5, ql.Days)),
           ("on a coupon date", first)]
    if len(coupons) > 2:
        out.append(("after a coupon", TARGET_CAL.advance(first, 10, ql.Days)))
        out.append(("after two coupons", TARGET_CAL.advance(coupons[1], 10, ql.Days)))
    return out


TARGET_CAL = ql.TARGET()


def forward(spec, bond, h, delivery, position, strike, value_date=TODAY):
    return ql.BondForward(value_date, delivery, position, strike, 2, ql.ActualActual(ql.ActualActual.ISDA),
                          ql.TARGET(), ql.Following, bond, h, h)


def outputs(f, h, keep_income):
    out = {"settlementDate": v.enc(f.settlementDate()), "isExpired": f.isExpired()}
    if f.isExpired():
        # QuantLib's forward value reads its spot value and income, which an expired forward never computes
        out["NPV"] = v.attempt(f.NPV)
        return out
    out["spotValue"] = f.spotValue()
    if keep_income:
        out.update({"spotIncome": f.spotIncome(h), "forwardValue": f.forwardValue(), "forwardPrice": f.forwardPrice(),
                    "cleanForwardPrice": f.cleanForwardPrice(), "NPV": f.NPV()})
        for name, comp in (("impliedYieldContinuous", ql.Continuous), ("impliedYieldCompounded", ql.Compounded)):
            # a zero forward (a zero-coupon bond delivered at its redemption) has no implied yield
            out[name] = v.attempt(lambda comp=comp: f.impliedYield(f.spotValue(), f.forwardValue(), f.settlementDate(),
                                                                   comp, ql.Actual365Fixed()).rate())
    return out


def cases():
    out = []
    for name, spec, face in bonds():
        fixings = None
        if spec["type"] == "FloatingRateBond":
            fixings = [(bd.irx.make_index(spec["index"]), [FLOATING_FIXING])]
        with v.case_settings(TODAY, fixings=fixings) as st:
            bond = bd.make_bond(spec)
            h = bd.irx.handle(CURVE)
            bond.setPricingEngine(ql.DiscountingBondEngine(h))
            rows = [(label, d, LONG, 0.0) for label, d in deliveries(bond)]
            rows.append(("short struck at 100", deliveries(bond)[0][1], SHORT, 100.0))
            for label, delivery, position, strike in rows:
                f = forward(spec, bond, h, delivery, position, strike)
                # QuantLib's income is the flows' amounts, its spot value per 100 of the notional, where nanoql's are
                # at the bond's notional
                keep = bond.notional(bond.settlementDate()) == 100.0 or f.spotIncome(h) == 0.0
                out.append({"name": f"{name} / {label} / {'long' if position == LONG else 'short'} {strike:g}",
                            "inputs": {"settings": st, "bond": spec, "curve": CURVE, "valueDate": v.enc(TODAY),
                                       "deliveryDate": v.enc(delivery), "position": position, "strike": strike,
                                       "settlementDays": 2},
                            "expected": outputs(f, h, keep)})
    # an expired forward: delivered before the evaluation date
    name, spec, face = bonds()[0]
    with v.case_settings(TODAY) as st:
        bond = bd.make_bond(spec)
        h = bd.irx.handle(CURVE)
        bond.setPricingEngine(ql.DiscountingBondEngine(h))
        delivery = D(10, 1, 2025)
        f = forward(spec, bond, h, delivery, LONG, 0.0, D(2, 1, 2025))
        out.append({"name": f"{name} / expired",
                    "inputs": {"settings": st, "bond": spec, "curve": CURVE, "valueDate": v.enc(D(2, 1, 2025)),
                               "deliveryDate": v.enc(delivery), "position": LONG, "strike": 0.0, "settlementDays": 2},
                    "expected": outputs(f, h, True)})
    check_rows(out)
    return out


def check_rows(out):
    left_out = [c for c in out if "spotIncome" not in c["expected"] and not c["expected"]["isExpired"]]
    assert left_out and all("1e5" in c["name"] or "amortizing" in c["name"] for c in left_out), [
        c["name"] for c in left_out]
    assert any(c["expected"].get("spotIncome", 0.0) > 0.0 for c in out)
    assert out[-1]["expected"]["isExpired"] and out[-1]["expected"]["NPV"] == 0.0


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bondforward.hpp", cases)
