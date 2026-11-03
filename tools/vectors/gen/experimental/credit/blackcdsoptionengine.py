# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Black CDS-option engine (ql/experimental/credit/blackcdsoptionengine.hpp), through the
wheel's BlackCdsOptionEngine on CdsOption.

Each case is an option exercising 3, 9 or 12 months out on a swap starting a month after the exercise and running five
years, quarterly (TARGET, following, Actual/360): payer (buying protection) and receiver, knocking out or (the payer)
not, struck at the money, 20% below and 20% above, at volatilities 0, 10%, 25% and 60%, on a flat hazard rate of 1% with
a flat discount rate of 2% and one of 3% with 5%, recovery 40%, evaluated today and a month later; and one expired
option. Its outputs: the engine's NPV and risky annuity, the underlying swap priced by the mid-point engine on the same
curves, as the engine's delegate does.

A case's inputs hold its settings, the swap spec of tools/vectors/gen/instruments/creditdefaultswap.py, the exercise
date, "knocksOut", the volatility, the curve specs and the recovery. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Credit/BlackCdsOptionEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_creditdefaultswap",
                                               os.path.join(v.GEN_ROOT, "instruments", "creditdefaultswap.py"))
cds = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cds)
dts = cds.dts

TODAY = dts.TODAY
M = int(ql.Months)
RECOVERY = 0.4


def swap_spec(expiry, side, spread):
    start = ql.TARGET().advance(expiry, 1, ql.Months)
    maturity = ql.TARGET().advance(start, 5, ql.Years)
    return cds.cds_spec(side, 1e6, spread, cds.schedule_spec(start, maturity, (3, M), dts.TARGET,
                                                             ql.DateGeneration.Forward))


def option(spec, expiry, knocks_out, credit, disc):
    swap = cds.make_cds(spec)
    swap.setPricingEngine(ql.MidPointCdsEngine(dts.credit_handle(credit), RECOVERY, dts.discount_handle(disc)))
    return ql.CdsOption(swap, ql.EuropeanExercise(expiry), knocks_out)


def atm(expiry, credit, disc):
    spec = swap_spec(expiry, cds.SELLER, 0.01)
    swap = cds.make_cds(spec)
    swap.setPricingEngine(ql.MidPointCdsEngine(dts.credit_handle(credit), RECOVERY, dts.discount_handle(disc)))
    return swap.fairSpread()


def make_cases():
    out = []
    k = 0
    for ev in (TODAY, TODAY + ql.Period(1, ql.Months)):
        for months in (3, 9, 12):
            expiry = ql.TARGET().advance(TODAY, months, ql.Months)
            for h, r in ((0.01, 0.02), (0.03, 0.05)):
                with v.case_settings(ev) as st:
                    credit = dts.flat_spec(ev, h)
                    disc = dts.discount_spec(ev, r)
                    strike0 = atm(expiry, credit, disc)
                for side, knocks in ((cds.BUYER, True), (cds.BUYER, False), (cds.SELLER, True)):
                    for moneyness in (1.0, 0.8, 1.2):
                        for vol in (0.0, 0.1, 0.25, 0.6):
                            with v.case_settings(ev) as st:
                                spec = swap_spec(expiry, side, strike0 * moneyness)
                                opt = option(spec, expiry, knocks, credit, disc)
                                opt.setPricingEngine(ql.BlackCdsOptionEngine(
                                    dts.credit_handle(credit), RECOVERY, dts.discount_handle(disc),
                                    ql.QuoteHandle(ql.SimpleQuote(vol))))
                                expected = {"NPV": v.attempt(opt.NPV), "riskyAnnuity": v.attempt(opt.riskyAnnuity)}
                            out.append({"name": f"{ev.ISO()} {months}M h {h} r {r} side {side} knocks {knocks} "
                                                f"x{moneyness} vol {vol} / {k}",
                                        "inputs": {"settings": st, "swap": spec, "exercise": v.enc(expiry),
                                                   "knocksOut": knocks, "volatility": vol, "credit": credit,
                                                   "discount": disc, "recoveryRate": RECOVERY},
                                        "expected": expected})
                            k += 1
    # an expired option: its value and risky annuity 0
    ev = TODAY + ql.Period(6, ql.Months)
    expiry = ql.TARGET().advance(TODAY, 3, ql.Months)
    with v.case_settings(ev) as st:
        credit = dts.flat_spec(ev, 0.01)
        disc = dts.discount_spec(ev, 0.02)
        spec = swap_spec(expiry, cds.BUYER, 0.006)
        opt = option(spec, expiry, True, credit, disc)
        opt.setPricingEngine(ql.BlackCdsOptionEngine(dts.credit_handle(credit), RECOVERY, dts.discount_handle(disc),
                                                     ql.QuoteHandle(ql.SimpleQuote(0.25))))
        expected = {"NPV": v.attempt(opt.NPV), "riskyAnnuity": v.attempt(opt.riskyAnnuity)}
    out.append({"name": f"expired / {k}",
                "inputs": {"settings": st, "swap": spec, "exercise": v.enc(expiry), "knocksOut": True,
                           "volatility": 0.25, "credit": credit, "discount": disc, "recoveryRate": RECOVERY},
                "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/blackcdsoptionengine.hpp", make_cases)
