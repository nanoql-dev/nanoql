# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CPI bond helper (ql/termstructures/yield/bondhelpers.hpp), which the wheel doesn't bind:
QuantLib's CPIBondHelper builds its CPIBond and is a BondHelper on it, so its oracle is the wheel's BondHelper on the
wheel's CPIBond (tools/vectors/gen/instruments/bonds/cpibond.py's), its implied quote on a curve the bond's clean or
dirty price by the discounting bond engine on that curve, as BondHelper::impliedQuote prices it.

- "helper" cases: helpers on clean and dirty prices, for CPI bonds with and without a base CPI, on flat nominal
  curves: earliestDate, maturityDate, latestRelevantDate, pillarDate, latestDate, impliedQuote and quoteError.

A case's inputs hold its settings, the index spec and fixings (tools/vectors/gen/cashflows/indexedcashflow.py's),
the bond spec, the quoted price and its type (QuantLib's Bond::Price::Type integer), the curve the helper is bound to
(a flat forward spec, tools/vectors/gen/indexes/interestrateindex.py's). Read back by
tests/NanoQL.Tests/Vectors/TermStructures/CPIBondHelperVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_cpibond",
                                               os.path.join(v.GEN_ROOT, "instruments", "bonds", "cpibond.py"))
cpb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cpb)
icf = cpb.icf
irx = icf.irx

TODAY = icf.TODAY
CLEAN, DIRTY = int(ql.BondPrice.Clean), int(ql.BondPrice.Dirty)

# (label, bond, price, price type, curve)
HELPERS = [
    ("clean, base CPI", cpb.BONDS[0][1], 104.0, CLEAN, irx.curve_spec(TODAY, 0.04)),
    ("dirty, base CPI, linear", cpb.BONDS[1][1], 105.0, DIRTY, irx.curve_spec(TODAY, 0.035)),
    ("clean, no base", cpb.BONDS[2][1], 107.0, CLEAN, irx.curve_spec(TODAY, 0.045, cpb.cf.A360, ql.Simple)),
    ("clean, forward start", cpb.BONDS[4][1], 98.0, CLEAN, irx.curve_spec(TODAY, 0.04)),
]


def helper_cases():
    out = []
    for k, (label, spec, price, price_type, curve) in enumerate(HELPERS):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(icf.UKRPI, icf.UK_FIXINGS)
            bond = cpb.make_bond(spec, index)
            helper = ql.BondHelper(ql.QuoteHandle(ql.SimpleQuote(price)), bond, price_type)
            bond.setPricingEngine(ql.DiscountingBondEngine(irx.handle(curve)))
            implied = bond.cleanPrice() if price_type == CLEAN else bond.dirtyPrice()
            out.append({"name": f"helper {label} / {k}",
                        "inputs": {"settings": st, "index": icf.UKRPI, "fixings": icf.UK_FIXINGS, "bond": spec,
                                   "price": price, "priceType": price_type, "curve": curve},
                        "expected": {
                            "earliestDate": v.enc(helper.earliestDate()), "maturityDate": v.enc(helper.maturityDate()),
                            "latestRelevantDate": v.enc(helper.latestRelevantDate()),
                            "pillarDate": v.enc(helper.pillarDate()), "latestDate": v.enc(helper.latestDate()),
                            "impliedQuote": implied, "quoteError": price - implied}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/bondhelpers.hpp", helper_cases)
