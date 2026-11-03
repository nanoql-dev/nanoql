# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the Italian bonds and the Rendistato calculator (ql/instruments/bonds/btp.hpp, .cpp), which no wheel
class exposes, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15
January 2025, a Euribor 6M index forecasting on a flat 3.1% curve and a flat 2.9% discount curve (Actual/365 (Fixed)).

- CCTEU: three bonds (a seasoned one from its start date, one whose start is QuantLib's default, whole years back from
  its maturity, and one with an issue date): the settlement date, the cash flows' dates and amounts, the accrued amount
  (rounded to five decimals) at the settlement date and three others, and the clean and dirty prices on
  DiscountingBondEngine on the discount curve. A coupon fixing before the evaluation date reads a stored Euribor 6M
  fixing, 3.05% plus a basis point per coupon.
- BTP: four bonds (from a start date, from QuantLib's default start, the legacy constructor's redemption of 99.999,
  and an issue date): the same outputs, and the yield of a clean price on the BTP conventions (Actual/Actual (ISMA),
  compounded annually) at the settlement date and at the last date given.
- RendistatoBasket: two baskets (its size, outstanding, outstandings and weights) and four refusals (empty, and the
  outstandings or the prices of another size than the bonds, a negative outstanding).
- RendistatoCalculator: three baskets, of short, medium and long BTPs, on the Euribor index and the discount curve: the
  basket's yield and duration, the bonds' yields and durations, the 15 swaps' lengths, rates, yields and durations, the
  equivalent swap's rate, yield, duration, length and spread, its fixed rate and maturity, and the two quotes' values;
  and one with no discount curve, refused. The calculator stops at the first swap whose duration exceeds the basket's:
  the later swaps' rates and durations are QuantLib's null, recorded as null, and their yields its starting guess, 5%,
  generated and left out ("solvedSwapYields" holds the computed ones): nanoql's are null
  (tests/NanoQL.Tests/Divergences/RendistatoUnsolvedSwapsTests.cs).

A case's inputs hold its settings and the method's arguments. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Instruments/BtpCxxVectorTests.cs."""
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


irx = _load("wheel_interestrateindex", "indexes/interestrateindex.py")

D = ql.Date
TODAY = irx.TODAY
EURIBOR_CURVE = irx.curve_spec(TODAY, 0.031)
DISCOUNT = irx.curve_spec(TODAY, 0.029)
EURIBOR_6M = [6, int(ql.Months)]


def btp(maturity, rate, redemption=None, start=None, issue=None):
    return {"maturity": v.enc(maturity), "rate": rate, "redemption": redemption, "start": v.enc(start),
            "issue": v.enc(issue)}


def ccteu_fixings(maturity, start):
    """The Euribor 6M fixings a CCTEU's coupons read before the evaluation date, 3.05% plus a basis point a coupon, by
    the harness's names: its schedule (6M, NullCalendar, Unadjusted, Backward, end of month) as QuantLib builds it."""
    ql.Settings.instance().evaluationDate = TODAY
    s = ql.Schedule(start or ql.Date(), maturity, ql.Period(6, ql.Months), ql.NullCalendar(), ql.Unadjusted,
                    ql.Unadjusted, ql.DateGeneration.Backward, True)
    target = ql.TARGET()
    dates = [target.advance(d, -2, ql.Days, ql.Preceding) for d in list(s.dates())[:-1]]
    pairs = [[v.enc(d), 0.0305 + 0.0001 * k] for k, d in enumerate(dates) if d < TODAY]
    return {"Euribor6M Actual/360": pairs} if pairs else None


CCTEUS = [
    ("seasoned, from its start date", D(15, 4, 2030), 0.01, D(15, 4, 2023), None),
    ("QuantLib's default start", D(15, 10, 2029), 0.0085, None, None),
    ("an issue date", D(1, 12, 2031), 0.012, D(1, 6, 2024), D(3, 6, 2024)),
]

BTPS = [
    ("from a start date", btp(D(1, 8, 2034), 0.039, None, D(1, 2, 2024)), 101.2),
    ("QuantLib's default start", btp(D(15, 3, 2030), 0.032), 99.5),
    ("the legacy redemption", btp(D(1, 5, 2037), 0.04, 99.999, D(1, 11, 2023), D(1, 11, 2023)), 102.0),
    ("an issue date", btp(D(1, 12, 2028), 0.03, None, D(1, 6, 2024), D(3, 6, 2024)), 100.4),
]

SHORT = [btp(D(1, 4, 2026), 0.025, None, D(1, 4, 2023)), btp(D(15, 9, 2026), 0.0285, None, D(15, 3, 2024))]
MEDIUM = [btp(D(1, 8, 2029), 0.03, None, D(1, 2, 2024)), btp(D(15, 11, 2030), 0.0335, None, D(15, 5, 2023)),
          btp(D(1, 6, 2031), 0.031, None, D(1, 12, 2023))]
LONG = [btp(D(1, 9, 2036), 0.0385, None, D(1, 3, 2024)), btp(D(1, 3, 2040), 0.041, None, D(1, 9, 2023)),
        btp(D(1, 10, 2044), 0.0435, None, D(1, 4, 2024))]

BASKETS = [
    ("three bonds", MEDIUM, [12.0, 18.0, 6.0], [99.1, 101.4, 99.8]),
    ("one bond", SHORT[:1], [20.0], [99.9]),
    ("refused / empty", [], [], []),
    ("refused / outstandings of another size", MEDIUM, [12.0, 18.0], [99.1, 101.4, 99.8]),
    ("refused / prices of another size", MEDIUM, [12.0, 18.0, 6.0], [99.1, 101.4]),
    ("refused / a negative outstanding", MEDIUM, [12.0, -18.0, 6.0], [99.1, 101.4, 99.8]),
]

CALCULATORS = [
    ("short bonds", SHORT, [10.0, 15.0], [99.6, 100.2], DISCOUNT),
    ("medium bonds", MEDIUM, [12.0, 18.0, 6.0], [99.1, 101.4, 99.8], DISCOUNT),
    ("long bonds", LONG, [8.0, 5.0, 3.0], [103.5, 104.9, 106.0], DISCOUNT),
    ("refused / no discount curve", MEDIUM, [12.0, 18.0, 6.0], [99.1, 101.4, 99.8], None),
]

BOND_OUTPUTS = ["settlementDate", "cashflowDates", "cashflowAmounts", "accruedAmounts", "cleanPrice", "dirtyPrice"]
CALCULATOR_OUTPUTS = ["yield", "duration", "yields", "durations", "swapLengths", "swapRates", "swapYields",
                      "swapDurations", "equivalentSwapRate", "equivalentSwapYield", "equivalentSwapDuration",
                      "equivalentSwapLength", "equivalentSwapSpread", "equivalentSwapFixedRate",
                      "equivalentSwapMaturity", "lengthQuote", "spreadQuote"]


def nulls_refused(values):
    return [dict(v.THROWS) if x is None else x for x in values]


def bond_expected(out):
    e = dict(zip(BOND_OUTPUTS, out))
    e["cashflowAmounts"] = nulls_refused(e["cashflowAmounts"])
    e["accruedAmounts"] = nulls_refused(e["accruedAmounts"])
    return e


def cases():
    out = []
    with cxx.connect() as H:
        for name, maturity, spread, start, issue in CCTEUS:
            fixings = ccteu_fixings(maturity, start)
            with H.case_settings(v.enc(TODAY), **({"fixings": fixings} if fixings else {})) as st:
                dates = [None, v.enc(TODAY + 30), v.enc(D(1, 6, 2025)), v.enc(D(20, 11, 2025))]
                args = [v.enc(maturity), spread, EURIBOR_CURVE, v.enc(start), v.enc(issue), dates]
                out.append({"name": f"CCTEU / {name}", "inputs": {"settings": st, "method": "CCTEU", "arguments": args},
                            "expected": bond_expected(H.call("CCTEU", *args))})
        for name, spec, clean in BTPS:
            with H.case_settings(v.enc(TODAY)) as st:
                dates = [None, v.enc(TODAY + 45), v.enc(D(2, 5, 2025)), v.enc(D(20, 1, 2025))]
                args = [spec, DISCOUNT, clean, dates]
                r = H.call("BTP", *args)
                e = bond_expected(r[:6])
                e["yield"], e["yieldAtLastDate"] = r[6], r[7]
                out.append({"name": f"BTP / {name}", "inputs": {"settings": st, "method": "BTP", "arguments": args},
                            "expected": e})
        for name, bonds, outstandings, prices in BASKETS:
            with H.case_settings(v.enc(TODAY)) as st:
                args = [bonds, outstandings, prices]
                r = v.attempt(lambda: H.call("RendistatoBasket", *args))
                e = ({"construct": r} if r == v.THROWS else
                     dict(zip(["size", "outstanding", "outstandings", "weights"], r)))
                out.append({"name": f"RendistatoBasket / {name}",
                            "inputs": {"settings": st, "method": "RendistatoBasket", "arguments": args}, "expected": e})
        for name, bonds, outstandings, prices, discount in CALCULATORS:
            with H.case_settings(v.enc(TODAY)) as st:
                args = [bonds, outstandings, prices, EURIBOR_6M, EURIBOR_CURVE, discount]
                r = v.attempt(lambda: H.call("RendistatoCalculator", *args))
                if r == v.THROWS:
                    e = {"construct": r}
                else:
                    e = dict(zip(CALCULATOR_OUTPUTS, r))
                    # the unsolved swaps' yields are QuantLib's starting guess: left out
                    solved = [y for y, rate in zip(e.pop("swapYields"), e["swapRates"]) if rate is not None]
                    e["solvedSwapYields"] = solved
                out.append({"name": f"RendistatoCalculator / {name}",
                            "inputs": {"settings": st, "method": "RendistatoCalculator", "arguments": args},
                            "expected": e})
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    for c in out:
        assert ("refused" in c["name"]) == ("construct" in c["expected"]), c["name"]
    medium = by_name["RendistatoCalculator / medium bonds"]
    assert None in medium["swapRates"] and len(medium["solvedSwapYields"]) < 15
    assert by_name["BTP / the legacy redemption"]["cashflowAmounts"][-1] == 99.999


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/bonds/btp.hpp", cases)
