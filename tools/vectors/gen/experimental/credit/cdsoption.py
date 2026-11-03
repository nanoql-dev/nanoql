# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CDS option (ql/experimental/credit/cdsoption.hpp), through the wheel's CdsOption.

- "option" cases: payer and receiver options exercising 3 or 12 months out on swaps of
  tools/vectors/gen/experimental/credit/blackcdsoptionengine.py's form, at the money and 20% out, evaluated today,
  a month later and after the exercise: whether the option has expired, its at-the-money rate (the swap's fair spread,
  the swap priced by the mid-point engine) and, on a live option, the Black volatility implied by its value at 10%, 25%
  and 60%.
- "refused" cases: a receiver option that doesn't knock out, and an underlying with an upfront.

The implied volatility is solved, so the stability filter runs on it: it is solved again with the target value, the
discount rate and the recovery moved one ulp up, then down, together and each alone, and written only where every move
leaves it within 1e-12 x max(|a|, |b|, 1); a dropped one is written null and listed in the case's inputs "dropped".

A case's inputs hold its settings, the swap spec, the exercise date, "knocksOut", the curve specs and the recovery.
Read back by tests/NanoQL.Tests/Vectors/Experimental/Credit/CdsOptionVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_blackcdsoptionengine",
                                               os.path.join(v.GEN_ROOT, "experimental", "credit",
                                                            "blackcdsoptionengine.py"))
eng = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(eng)
cds = eng.cds
dts = eng.dts

TODAY = dts.TODAY
RECOVERY = eng.RECOVERY
VOLS = (0.1, 0.25, 0.6)


def implied(spec, expiry, knocks, ev, target, rate, recovery, h):
    with v.case_settings(ev):
        credit = dts.flat_spec(ev, h)
        disc = dts.discount_spec(ev, rate)
        opt = eng.option(spec, expiry, knocks, credit, disc)
        opt.setPricingEngine(ql.BlackCdsOptionEngine(dts.credit_handle(credit), recovery, dts.discount_handle(disc),
                                                     ql.QuoteHandle(ql.SimpleQuote(0.2))))
        return v.attempt(lambda: opt.impliedVolatility(target, dts.discount_handle(disc), dts.credit_handle(credit),
                                                       recovery))


def option_cases():
    out = []
    total = [0, 0]
    k = 0
    h, r = 0.02, 0.03
    for months in (3, 12):
        expiry = ql.TARGET().advance(TODAY, months, ql.Months)
        for ev in (TODAY, TODAY + ql.Period(1, ql.Months), expiry + ql.Period(1, ql.Months)):
            with v.case_settings(ev):
                strike0 = eng.atm(expiry, dts.flat_spec(ev, h), dts.discount_spec(ev, r))
            for side in (cds.BUYER, cds.SELLER):
                for moneyness in (1.0, 1.2):
                    with v.case_settings(ev) as st:
                        credit = dts.flat_spec(ev, h)
                        disc = dts.discount_spec(ev, r)
                        spec = eng.swap_spec(expiry, side, strike0 * moneyness)
                        opt = eng.option(spec, expiry, True, credit, disc)
                        expected = {"isExpired": opt.isExpired(), "atmRate": v.attempt(opt.atmRate)}
                        targets = []
                        if not opt.isExpired():
                            for vol in VOLS:
                                opt.setPricingEngine(ql.BlackCdsOptionEngine(
                                    dts.credit_handle(credit), RECOVERY, dts.discount_handle(disc),
                                    ql.QuoteHandle(ql.SimpleQuote(vol))))
                                targets.append(opt.NPV())
                    inputs = {"settings": st, "swap": spec, "exercise": v.enc(expiry), "knocksOut": True,
                              "credit": credit, "discount": disc, "recoveryRate": RECOVERY, "targets": targets}
                    dropped = []
                    vols = []
                    for i, target in enumerate(targets):
                        base = implied(spec, expiry, True, ev, target, r, RECOVERY, h)
                        moved = [implied(spec, expiry, True, ev, m[0], m[1], m[2], h)
                                 for m in cds.moves([target, r, RECOVERY])]
                        if not all(cds.stable(base, m) for m in moved):
                            dropped.append(i)
                            base = None
                        vols.append(base)
                    total[0] += len(dropped)
                    total[1] += len(targets)
                    expected["impliedVolatility"] = vols
                    inputs["dropped"] = dropped
                    out.append({"name": f"option {months}M {ev.ISO()} side {side} x{moneyness} / {k}",
                                "inputs": inputs, "expected": expected})
                    k += 1
    print(f"experimental/credit/cdsoption: dropped {total[0]} of {total[1]} implied volatilities")
    return out, k


def refused_cases(k):
    out = []
    expiry = ql.TARGET().advance(TODAY, 3, ql.Months)
    with v.case_settings(TODAY) as st:
        spec = eng.swap_spec(expiry, cds.SELLER, 0.01)
        swap = cds.make_cds(spec)
        expected = {"construction": v.attempt(lambda: ql.CdsOption(swap, ql.EuropeanExercise(expiry), False)
                                              .isExpired())}
    out.append({"name": f"refused receiver without knock-out / {k}",
                "inputs": {"settings": st, "swap": spec, "exercise": v.enc(expiry), "knocksOut": False},
                "expected": expected})
    with v.case_settings(TODAY) as st:
        spec = eng.swap_spec(expiry, cds.BUYER, 0.01)
        spec = dict(spec, upfront=0.02)
        swap = cds.make_cds(spec)
        expected = {"construction": v.attempt(lambda: ql.CdsOption(swap, ql.EuropeanExercise(expiry), True)
                                              .isExpired())}
    out.append({"name": f"refused upfront / {k + 1}",
                "inputs": {"settings": st, "swap": spec, "exercise": v.enc(expiry), "knocksOut": True},
                "expected": expected})
    return out


def make_cases():
    cases, k = option_cases()
    return cases + refused_cases(k)


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/cdsoption.hpp", make_cases)
