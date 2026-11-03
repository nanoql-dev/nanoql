# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AssetSwap (ql/instruments/assetswap.hpp): asset swaps on eight bonds on the evaluation date
15 January 2025, a flat 3% curve on Actual/365 (Fixed) discounting the bonds and the swaps, and a Euribor 6M index
forecasting on a flat 3.1% one.

The bonds are tools/vectors/gen/instruments/bond.py's specs, each of face 100 but the last: annual and semiannual
fixed-rate bonds, a floating-rate bond on Euribor 6M with a spread, a CMS-rate bond on a 10-year swap index (priced by
cmsratebond.py's pricer), a zero-coupon bond, a bond built from a face amount and its flows, an amortizing fixed-rate
bond, and a fixed-rate bond on a face of 1e6. Every fixing a bond's or a swap's coupon reads before the evaluation date
is stored with the case's settings, 3% plus a basis point each in date order. Each bond
is swapped at par and at market, paying its coupons, and at par receiving them with a spread of 0.3%, at its clean
price on DiscountingBondEngine on the curve. On the annual bond, besides: a market price, a given quarterly floating
schedule on Euribor 3M, an Actual/365 (Fixed) floating day count, a gearing of 0.8, a non-par repayment of 99, a deal
maturity before the bond's, an ESTR-style overnight index (its floating schedule given, as QuantLib requires), the
engine given a settlement date and an NPV date (and the settlement date's flows), and a seasoned deal, its floating
schedule starting before the evaluation date (its fair clean price refused, "not available for seasoned deal").

A swap's outputs are its NPV, fair spread and fair clean price, each leg's NPV, BPS and start and end discounts, the NPV
date's discount, and each leg's payment dates and amounts. The swap on the 1e6 face with a repayment of 100 is generated
and its outputs that read the repayment left out (the NPV, fair spread, fair clean price, the legs' NPVs and the bond
leg's amounts): QuantLib pays the number given as an amount, where nanoql pays it per 100 of the notional
(tests/NanoQL.Tests/Divergences/AssetSwapNonParRepaymentTests.cs). The fair non-par repayment, the floating leg's NPV
and BPS and the swap's terms, which the wheel hides, are in tools/vectors/gen/cxx/instruments/assetswap.py. Amounts are
compared at the bond's face.

A case's inputs hold its settings, the bond (bond.py's spec), the curve, and the swap ({"payBondCoupon", "cleanPrice",
"index", "spread", "floatSchedule", "floatingDayCount", "parSwap", "gearing", "nonParRepayment", "dealMaturity"}, a
null schedule, day count (the index's), repayment or maturity QuantLib's default, an index a spec of indexes/interestrateindex.py)
and the engine ({"includeSettlementDateFlows", "settlementDate", "npvDate"}, nulls the engine's defaults). The wheel's
AssetSwap is the proxy of the C++ constructor, every argument passed by position. Read back by
tests/NanoQL.Tests/Vectors/Instruments/AssetSwapVectorTests.cs."""
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


cms = _load("wheel_cmsratebond", "instruments/bonds/cmsratebond.py")
bd = cms.bd
irx = bd.irx

D = ql.Date
M, Y = ql.Months, ql.Years
TODAY = D(15, 1, 2025)
TARGET = bd.TARGET
A360, A365, THIRTY360_BB = bd.A360, bd.A365, cms.THIRTY360_BB
CURVE = bd.curve_spec(TODAY, 0.03)
FORECAST = irx.curve_spec(TODAY, 0.031)
E6M = irx.ibor_spec("Euribor", (6, M), 2, TARGET, ql.ModifiedFollowing, True, A360, FORECAST)
E3M = irx.ibor_spec("Euribor", (3, M), 2, TARGET, ql.ModifiedFollowing, True, A360, FORECAST)
ESTR = irx.overnight_spec("ESTR", 0, TARGET, A360, FORECAST)
ACT_ISMA = bd.ACT_ISMA


def bonds():
    """(name, bond spec, face) for the 8 bonds."""
    annual = bd.schedule_spec(D(15, 3, 2024), D(15, 3, 2031), (1, Y), TARGET, ql.Unadjusted)
    semiannual = bd.schedule_spec(D(10, 6, 2023), D(10, 6, 2030), (6, M), TARGET, ql.ModifiedFollowing)
    floating = bd.schedule_spec(D(20, 10, 2024), D(20, 10, 2029), (6, M), TARGET, ql.ModifiedFollowing)
    cms_schedule = bd.schedule_spec(D(20, 2, 2025), D(20, 2, 2032), (1, Y), TARGET, ql.Unadjusted)
    flows = bd.leg_spec(bd.fixed(annual, [100.0], [0.035], ACT_ISMA), extra=[bd.flow("Redemption", 100.0,
                                                                                   D(17, 3, 2031))])
    return [
        ("annual 4%", bd.fixed_bond_spec(2, 100.0, annual, [0.04], ACT_ISMA, ql.Following, 100.0, D(15, 3, 2024)),
         100.0),
        ("semiannual 2.5%", bd.fixed_bond_spec(2, 100.0, semiannual, [0.025], THIRTY360_BB), 100.0),
        ("floating on Euribor 6M", bd.floating_bond_spec(2, 100.0, floating, E6M, A360, spreads=[0.004]), 100.0),
        ("CMS on the 10-year rate", bd.cms_bond_spec(2, 100.0, cms_schedule, cms.swap_index((10, Y), TODAY),
                                                     THIRTY360_BB, bd.cms_pricer_spec(TODAY), fixing_days=2,
                                                     caps=[0.06], floors=[0.01]), 100.0),
        ("zero coupon", bd.zero_bond_spec(2, TARGET, 100.0, D(15, 6, 2030), ql.Following, 100.0, D(15, 6, 2020)),
         100.0),
        ("from a face amount and flows", bd.face_bond_spec(2, TARGET, 100.0, D(17, 3, 2031), D(15, 3, 2024), flows),
         100.0),
        ("amortizing 3.5%", bd.amortizing_bond_spec(2, [100.0, 80.0, 60.0, 40.0, 20.0], annual, [0.035], ACT_ISMA),
         100.0),
        ("annual 4% on 1e6", bd.fixed_bond_spec(2, 1e6, annual, [0.04], ACT_ISMA, ql.Following, 100.0,
                                                D(15, 3, 2024)), 1e6),
    ]


def swap_spec(clean, index=E6M, pay=True, spread=0.0, par=True, schedule=None, dc=None, gearing=1.0, repayment=None,
              maturity=None):
    return {"payBondCoupon": pay, "cleanPrice": clean, "index": index, "spread": spread, "floatSchedule": schedule,
            "floatingDayCount": dc, "parSwap": par, "gearing": gearing, "nonParRepayment": repayment,
            "dealMaturity": v.enc(maturity)}


ENGINE = {"includeSettlementDateFlows": None, "settlementDate": None, "npvDate": None}


def make_swap(bond, spec):
    schedule = ql.Schedule() if spec["floatSchedule"] is None else bd.make_schedule(spec["floatSchedule"])
    index = irx.make_index(spec["index"])
    # QuantLib's empty day counter, which the wheel can't build, gives the floating coupons the index's
    dc = index.dayCounter() if spec["floatingDayCount"] is None else bd.day_counter(spec["floatingDayCount"])
    repayment = ql.nullDouble() if spec["nonParRepayment"] is None else spec["nonParRepayment"]
    maturity = ql.Date() if spec["dealMaturity"] is None else ql.Date(spec["dealMaturity"])
    return ql.AssetSwap(spec["payBondCoupon"], bond, spec["cleanPrice"], index, spec["spread"], schedule, dc,
                        spec["parSwap"], spec["gearing"], repayment, maturity)


def make_engine(spec):
    h = irx.handle(CURVE)
    if spec["settlementDate"] is None:
        return ql.DiscountingSwapEngine(h)
    return ql.DiscountingSwapEngine(h, spec["includeSettlementDateFlows"], ql.Date(spec["settlementDate"]),
                                    ql.Date(spec["npvDate"]))


def outputs(a, face):
    out = {"NPV": v.amount(a.NPV(), face), "fairSpread": v.attempt(a.fairSpread),
           "fairCleanPrice": v.attempt(a.fairCleanPrice),
           "legNPV": [v.amount(a.legNPV(j), face) for j in (0, 1)],
           "legBPS": [v.amount(a.legBPS(j), face) for j in (0, 1)],
           "startDiscounts": [v.attempt(lambda j=j: a.startDiscounts(j)) for j in (0, 1)],
           "endDiscounts": [v.attempt(lambda j=j: a.endDiscounts(j)) for j in (0, 1)],
           "npvDateDiscount": a.npvDateDiscount()}
    for j, name in ((0, "bondLeg"), (1, "floatingLeg")):
        out[name + "Dates"] = [v.enc(cf.date()) for cf in a.leg(j)]
        out[name + "Amounts"] = [v.amount(cf.amount(), face) for cf in a.leg(j)]
    return out


def rows():
    """(name, bond spec, face, swap spec builder, engine spec, fixings) for the swaps; a swap spec builder takes the
    bond's clean price."""
    out = []
    for name, spec, face in bonds():
        for label, kw in (("par, paying the coupons", {}), ("market, paying the coupons", {"par": False}),
                          ("par, receiving the coupons, a spread", {"pay": False, "spread": 0.003})):
            if face != 100.0 and label != "par, paying the coupons":
                continue
            out.append((f"{name} / {label}", spec, face, lambda clean, kw=kw: swap_spec(clean, **kw), ENGINE))
    name, spec, face = bonds()[0]
    quarterly = bd.schedule_spec(D(17, 1, 2025), D(17, 3, 2031), (3, M), TARGET, ql.ModifiedFollowing)
    overnight = bd.schedule_spec(D(17, 1, 2025), D(17, 3, 2031), (1, Y), TARGET, ql.ModifiedFollowing)
    seasoned = bd.schedule_spec(D(15, 9, 2024), D(15, 3, 2031), (6, M), TARGET, ql.ModifiedFollowing)
    out += [
        (f"{name} / a market price", spec, face, lambda clean: swap_spec(98.5), ENGINE),
        (f"{name} / market, a market price", spec, face, lambda clean: swap_spec(98.5, par=False), ENGINE),
        (f"{name} / a quarterly floating schedule on Euribor 3M", spec, face,
         lambda clean: swap_spec(clean, E3M, schedule=quarterly), ENGINE),
        (f"{name} / an Actual/365 floating day count", spec, face, lambda clean: swap_spec(clean, dc=A365), ENGINE),
        (f"{name} / a gearing of 0.8", spec, face, lambda clean: swap_spec(clean, gearing=0.8), ENGINE),
        (f"{name} / a repayment of 99", spec, face, lambda clean: swap_spec(clean, repayment=99.0), ENGINE),
        (f"{name} / market, a repayment of 99", spec, face, lambda clean: swap_spec(clean, par=False, repayment=99.0),
         ENGINE),
        (f"{name} / a deal maturity before the bond's", spec, face,
         lambda clean: swap_spec(clean, maturity=D(17, 3, 2028)), ENGINE),
        (f"{name} / an ESTR-style index", spec, face, lambda clean: swap_spec(clean, ESTR, schedule=overnight),
         ENGINE),
        (f"{name} / settlement and NPV dates", spec, face, lambda clean: swap_spec(clean),
         {"includeSettlementDateFlows": True, "settlementDate": v.enc(D(17, 1, 2025)),
          "npvDate": v.enc(D(17, 1, 2025))}),
        (f"{name} / a seasoned deal", spec, face, lambda clean: swap_spec(clean, schedule=seasoned), ENGINE),
    ]
    name, spec, face = bonds()[-1]
    out.append((f"{name} / a repayment of 100", spec, face, lambda clean: swap_spec(clean, repayment=100.0), ENGINE))
    # a bond that matured before the evaluation date, on a floating schedule given: an expired swap, whose fair
    # values QuantLib derives from its zeroed results (0/0); the matured bond's clean price isn't read
    matured = bd.fixed_bond_spec(2, 100.0, bd.schedule_spec(D(15, 3, 2022), D(15, 3, 2024), (1, Y), TARGET,
                                                            ql.Unadjusted), [0.03], ACT_ISMA)
    past = bd.schedule_spec(D(17, 3, 2022), D(15, 3, 2024), (6, M), TARGET, ql.ModifiedFollowing)
    out.append(("matured 3% / an expired swap", matured, 100.0, lambda clean: swap_spec(99.0, schedule=past),
                ENGINE))
    return out


def stored_fixings(spec, swap):
    """The fixings the bond's coupons and then the swap's floating coupons read before the evaluation date, 3% plus a
    basis point each in date order: two passes, since the swap's upfront reads the bond's accrued amount."""
    found = {}

    def collect(cashflows):
        for cf in cashflows:
            frc = ql.as_floating_rate_coupon(cf)
            if frc is not None and frc.fixingDate() < TODAY:
                found.setdefault(frc.index().name(), (frc.index(), set()))[1].add(frc.fixingDate())

    def as_list():
        return [(index, [(d, 0.03 + 0.0001 * k) for k, d in enumerate(sorted(dates))])
                for index, dates in found.values()] or None

    with v.case_settings(TODAY):
        collect(bd.make_bond(spec).cashflows())
    with v.case_settings(TODAY, fixings=as_list()):
        collect(make_swap(bd.make_bond(spec), swap(100.0)).leg(1))
    return as_list()


# QuantLib pays a given repayment as an amount: the outputs that read it, on a face other than 100
REPAYMENT_OUTPUTS = ("NPV", "fairSpread", "fairCleanPrice", "legNPV", "bondLegAmounts")


def cases():
    out = []
    for name, spec, face, swap, engine in rows():
        with v.case_settings(TODAY, fixings=stored_fixings(spec, swap)) as st:
            bond = bd.make_bond(spec)
            bond.setPricingEngine(ql.DiscountingBondEngine(irx.handle(CURVE)))
            sw = swap(bond.cleanPrice() if not bond.isExpired() else None)
            a = make_swap(bond, sw)
            a.setPricingEngine(make_engine(engine))
            expected = outputs(a, face)
            if face != 100.0 and sw["nonParRepayment"] is not None:
                for key in REPAYMENT_OUTPUTS:
                    del expected[key]
            out.append({"name": name, "inputs": {"settings": st, "bond": spec, "curve": CURVE, "swap": sw,
                                                 "engine": engine},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    assert by_name["annual 4% / a seasoned deal"]["fairCleanPrice"] == v.THROWS
    assert "NPV" not in by_name["annual 4% on 1e6 / a repayment of 100"]
    expired = by_name["matured 3% / an expired swap"]
    assert expired["NPV"]["value"] == 0.0 and expired["legBPS"][1]["value"] == 0.0, expired


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/assetswap.hpp", cases)
