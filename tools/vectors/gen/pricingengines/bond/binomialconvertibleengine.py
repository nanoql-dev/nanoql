# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BinomialConvertibleEngine (ql/pricingengines/bond/binomialconvertibleengine.hpp), with the
discretized convertible it rolls back (ql/pricingengines/bond/discretizedconvertible.hpp, .cpp) and the
Tsiveriotis-Fernandes lattice it rolls it back on (ql/methods/lattices/tflattice.hpp), on the evaluation date 15 October
2026.

Four convertibles of tools/vectors/gen/instruments/bonds/convertiblebonds.py (a zero-coupon bond under an American
exercise, and fixed-coupon and floating-rate bonds under European, American and Bermudan exercises, with calls, a soft
call and a put) on each of the seven binomial trees, at 100 and 101 steps, the Leisen-Reimer and Joshi trees at 101
only, with two fixed dividends and a credit spread of 1%, on the first process of
tools/vectors/gen/processes/blackscholesprocess.py (Merton, flat, constant); then the fixed-coupon bond under each
exercise on the Cox-Ross-Rubinstein tree at 101 steps on four more processes (zero curves, a variance curve and a
surface; the Black-Scholes and Black processes), with no dividends, with a dividend the day after the evaluation date
and one on a coupon date, at credit spreads of 0 and 5%; an expired bond, whose results are zero; and the refusals: no time step ("timeSteps must be positive, 0 not
allowed"), a dividend larger than the spot ("negative value after subtracting dividends"), a fractional dividend, whose
amount QuantLib can't compute without a nominal; and the fixed-coupon bond at QuantLib's regression case's volatility
of 2,168% and credit spread, priced on this process. The variance-curve and surface processes refuse the volatility at
the bonds' last exercise date, after their last date. Each case's outputs are the NPV and the settlement value, both the bond's value at its settlement
date.

Every structure shares the risk-free curve's day counter and reference date, where nanoql flattens a structure on
another clock from its discount or variance (tests/NanoQL.Tests/Divergences/ConvertibleClockTests.cs). Every bond
settles on the evaluation date (convertiblebonds.py's priced_at_evaluation: "settlementDays" 0, an issue date after the
evaluation date moved to it), where QuantLib's master's spot at the settlement date (#2752, taken ahead of the pin) is
QuantLib 1.44's, divided by a discount of 1; a lag, and a dividend inside it, are checked in
tests/NanoQL.Tests/Divergences/ConvertibleUpstreamFixesTests.cs. Generated and left out: the Leisen-Reimer and Joshi
trees at an even count, on which QuantLib rolls back a tree built for one more step (which nanoql's engine applies); and
an American exercise whose grid's last time rounds below the tree's maturity (#2878, convertiblebonds.py's test).

A case holds a bond spec of convertiblebonds.py, a process spec, the tree, the step count, the credit spread and the
dividends (tools/vectors/gen/pricingengines/vanilla/analyticdividendeuropeanengine.py's specs). The wheel's calls, by
QuantLib-SWIG 1.44's bonds.i: BinomialConvertibleEngine(process, tree, steps, creditSpread, dividends), QuantLib-Python's
factory of the named engines, on the bond; NPV and settlementValue the bond's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/BinomialConvertibleEngineVectorTests.cs."""
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


cvb = _load("wheel_convertiblebonds", "instruments/bonds/convertiblebonds.py")
bsp = cvb.bsp
R = cvb.R
CAL = cvb.CAL
TREES = [("jr", (100, 101)), ("crr", (100, 101)), ("eqp", (100, 101)), ("trigeorgis", (100, 101)),
         ("tian", (100, 101)), ("lr", (101,)), ("joshi4", (101,))]
LEFT_OUT_TREES = [("lr", 100), ("joshi4", 100)]


def fixed_dividend(amount, date):
    return {"type": "Fixed", "amount": amount, "date": v.enc(date)}


def make_dividends(specs):
    out = []
    for d in specs:
        if d["type"] == "Fixed":
            out.append(ql.FixedDividend(d["amount"], ql.Date(d["date"])))
        else:
            out.append(ql.FractionalDividend(d["rate"], ql.Date(d["date"])))
    return out


DIVIDENDS = [fixed_dividend(1.5, CAL.advance(R, 6, ql.Months)), fixed_dividend(1.5, CAL.advance(R, 18, ql.Months))]


def priced(bond_spec, pspec, tree, steps, spread, dividends):
    bond = cvb.make_bond(bond_spec)
    process = bsp.process(pspec)
    try:
        # QuantLib refuses a step count at construction
        bond.setPricingEngine(ql.BinomialConvertibleEngine(process, tree, steps, ql.QuoteHandle(ql.SimpleQuote(spread)),
                                                           make_dividends(dividends)))
        bond.NPV()
    except RuntimeError:
        return {"NPV": dict(v.THROWS), "settlementValue": dict(v.THROWS)}
    return {"NPV": v.result(bond.NPV), "settlementValue": v.result(bond.settlementValue)}


def short(bond_spec, pspec, steps):
    return cvb.american_grid_short(bond_spec, cvb.make_bond(bond_spec), bsp.process(pspec), steps)


def case(name, bond_spec, pspec, tree, steps, spread, dividends, st):
    return {"name": name,
            "inputs": {"settings": st, "bond": bond_spec, "process": pspec, "tree": tree, "timeSteps": steps,
                       "creditSpread": spread, "dividends": dividends},
            "expected": priced(bond_spec, pspec, tree, steps, spread, dividends)}


def cases():
    st = v.settings(R)
    bonds = {name: cvb.priced_at_evaluation(spec) for name, spec in cvb.bonds()}
    out = []
    p0 = bsp.PROCESSES[0][1]
    chosen = ["zero-coupon, American, callable", "fixed annual 4%, European, callable",
              "floating on Euribor 6M + 50bp, American, callable", "fixed annual 4%, Bermudan, callable"]
    for bname in chosen:
        spec = bonds[bname]
        for tree, counts in TREES:
            for steps in counts:
                if short(spec, p0, steps):
                    priced(spec, p0, tree, steps, cvb.CREDIT_SPREAD, DIVIDENDS)
                    continue
                out.append(case(f"{bname}, {tree} {steps}", spec, p0, tree, steps, cvb.CREDIT_SPREAD, DIVIDENDS, st))
        for tree, steps in LEFT_OUT_TREES:
            priced(spec, p0, tree, steps, cvb.CREDIT_SPREAD, DIVIDENDS)
    for ex in ("European", "American", "Bermudan"):
        spec = bonds[f"fixed annual 4%, {ex}, callable"]
        for i in (1, 2, 3, 5, 8):
            pname, pspec = bsp.PROCESSES[i]
            if not short(spec, pspec, 101):
                out.append(case(f"fixed, {ex}, {pname}", spec, pspec, "crr", 101, cvb.CREDIT_SPREAD, DIVIDENDS, st))
        before = [fixed_dividend(2.0, CAL.advance(R, 1, ql.Days))]
        on_coupon = [fixed_dividend(2.0, CAL.advance(cvb.ISSUE, 2, ql.Years))]
        for dname, dividends in (("no dividends", []), ("a dividend the day after the evaluation date", before),
                                 ("a dividend on a coupon date", on_coupon)):
            if not short(spec, p0, 101):
                out.append(case(f"fixed, {ex}, {dname}", spec, p0, "crr", 101, cvb.CREDIT_SPREAD, dividends, st))
        for spread in (0.0, 0.05):
            if not short(spec, p0, 101):
                out.append(case(f"fixed, {ex}, credit spread {spread}", spec, p0, "crr", 101, spread, DIVIDENDS, st))
    expired = cvb.bond_spec("ConvertibleFixedCouponBond", cvb.exercise_spec("European", [ql.Date(15, ql.March, 2026)]),
                            1.0, [], ql.Date(15, ql.March, 2021), 0,
                            cvb.schedule(ql.Date(15, ql.March, 2021), ql.Date(15, ql.March, 2026), (1, ql.Years)),
                            coupons=[0.04])
    out.append(case("expired", expired, p0, "crr", 101, cvb.CREDIT_SPREAD, [], st))
    fixed = bonds["fixed annual 4%, European, callable"]
    out.append(case("refused: no time step", fixed, p0, "crr", 0, cvb.CREDIT_SPREAD, [], st))
    out.append(case("refused: a dividend larger than the spot", fixed, p0, "crr", 101, cvb.CREDIT_SPREAD,
                    [fixed_dividend(150.0, CAL.advance(R, 6, ql.Months))], st))
    out.append(case("refused: a fractional dividend", fixed, p0, "crr", 101, cvb.CREDIT_SPREAD,
                    [{"type": "Fractional", "rate": 0.02, "date": v.enc(CAL.advance(R, 6, ql.Months))}], st))
    hot = dict(p0, volatility=bsp.bvts.vol_spec_constant(R, 21.685235548092248))
    out.append(case("a volatility of 2,168%, QuantLib's regression case's", fixed, hot, "crr", 600, 0.11498700678012874, [], st))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/bond/binomialconvertibleengine.hpp", cases)
