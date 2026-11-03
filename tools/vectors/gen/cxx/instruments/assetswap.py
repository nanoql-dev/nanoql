# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the asset swap's members the wheel hides (ql/instruments/assetswap.hpp), each output computed by the C++
harness (docs/porting-playbook.md §9.4), on tools/vectors/gen/instruments/assetswap.py's evaluation date, curve and
Euribor 6M index: 14 swaps on fixed-rate bonds of face 100, at par and at market, paying and receiving the bond's
coupons, with and without a spread and a non-par repayment, priced by DiscountingSwapEngine on the curve.

Each swap's outputs are its fair non-par repayment, its floating leg's NPV and BPS, its clean price, non-par repayment,
spread, par flag and coupon side, and its bond's settlement date. Generated and left out: on a bond redeemed above par
with no repayment given, the non-par repayment and the fair one, which QuantLib reads as 100 whatever the redemption,
where nanoql reads the redemption per 100 of notional
(tests/NanoQL.Tests/Divergences/AssetSwapNonParRepaymentTests.cs); on a face of 100 a repayment given is the same on
both readings, so the fix moves nothing here.

A case's inputs hold its settings and the method's arguments (the bond, an instruments/bondspec.hpp spec; the curve;
the coupon side, clean price, index, spread, par flag and repayment, null for none). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/AssetSwapCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


asw = _load("wheel_assetswap", "instruments/assetswap.py")
bd = asw.bd

D = ql.Date
TODAY = asw.TODAY


def bonds():
    """(name, bond spec) for the bonds: the wheel file's annual and semiannual ones, and the annual one redeemed at
    101."""
    annual = bd.schedule_spec(D(15, 3, 2024), D(15, 3, 2031), (1, ql.Years), asw.TARGET, ql.Unadjusted)
    return [(name, spec) for name, spec, face in asw.bonds()[:2]] + [
        ("annual 4% redeemed at 101", bd.fixed_bond_spec(2, 100.0, annual, [0.04], asw.ACT_ISMA, ql.Following, 101.0,
                                                         D(15, 3, 2024)))]


SWAPS = [("par, paying", True, 0.0, True, None), ("market, paying", True, 0.0, False, None),
         ("par, receiving, a spread", False, 0.003, True, None), ("market, receiving, a spread", False, 0.003, False,
                                                                  None),
         ("par, a repayment of 99", True, 0.0, True, 99.0)]

OUTPUTS = ["fairNonParRepayment", "floatingLegNPV", "floatingLegBPS", "cleanPrice", "nonParRepayment", "spread",
           "parSwap", "payBondCoupon", "bondSettlementDate"]


def cases():
    out = []
    with cxx.connect() as H:
        for bond_name, bond in bonds():
            with v.case_settings(TODAY):
                b = bd.make_bond(bond)
                b.setPricingEngine(ql.DiscountingBondEngine(asw.irx.handle(asw.CURVE)))
                clean = b.cleanPrice()
            for swap_name, pay, spread, par, repayment in SWAPS:
                if bond_name.endswith("101") and repayment is not None:
                    continue
                with H.case_settings(v.enc(TODAY)) as st:
                    args = [bond, asw.CURVE, pay, clean, asw.E6M, spread, par, repayment]
                    expected = dict(zip(OUTPUTS, H.call("AssetSwap", *args)))
                    if expected["fairNonParRepayment"] is None:
                        expected["fairNonParRepayment"] = dict(v.THROWS)
                    # QuantLib stores 100 whatever the redemption, where nanoql stores the redemption
                    if bond_name.endswith("101") and repayment is None:
                        del expected["fairNonParRepayment"], expected["nonParRepayment"]
                    out.append({"name": f"{bond_name} / {swap_name}", "inputs": {"settings": st, "arguments": args},
                                "expected": expected})
    assert len(out) == 14, len(out)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/assetswap.hpp", cases)
