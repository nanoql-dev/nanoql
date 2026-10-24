# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of ql/cashflows/conundrumpricer.hpp and lineartsrpricer.hpp that the wheel doesn't bind,
each output computed by the C++ harness, on the evaluation date Monday 16 June 2025, the curve, coupons and
volatilities those of tools/vectors/gen/cashflows/conundrumpricer.py.

- MeanRevertingPricer::meanReversion(): the numeric and analytic Hagan pricers and the linear TSR pricer, each at a
  reversion of its own.
- NumericHaganPricer's upperLimit(), lowerLimit() and stdDeviations() after a caplet's (a call's) or a floorlet's (a
  put's) price on a fresh pricer, which prices the optionlet that sets them: the default limits and limits of 0.1% and
  50%, on the lognormal and the normal matrix, at strikes of 4% and 6%, on three coupons whose fixings are in the
  future.
- MarketQuotedOptionPricer's price: a forward of 5% on a 10-year swap expiring in 1 and 5 years, calls and puts at
  three strikes and a deflator of 2.5, on the lognormal and the normal matrix, and the shifted matrix, which it refuses
  ("VanillaOptionPricer: a normal or a zero-shift lognormal volatility is required").
- GFunctionFactory's g-functions: the standard model's at frequencies 1 and 2, payment fractions 0.5 and 1 and swap
  lengths 2 and 10, and the exact-yield and shift models' on the coupons, the shifts at reversions 0 and 3%, each with
  its first and second derivatives at swap rates of 1%, 3%, 5% and 8%.

A refused value is {"throws": true}. Each case's inputs hold its settings and the call's arguments by name.
tests/NanoQL.Tests/Vectors/Cxx/Coupons/ConundrumPricerCxxVectorTests.cs reads it back."""
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

_spec = importlib.util.spec_from_file_location(
    "wheel_conundrumpricer", os.path.join(v.GEN_ROOT, "cashflows", "conundrumpricer.py"))
cp = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cp)
svc, mat = cp.svc, cp.mat

TODAY = cp.TODAY
LOGNORMAL = svc.atm_floating()
NORMAL = svc.atm_floating(svc.scaled(mat.ATM_VOLS, 0.05), svc.N)
SHIFTED = svc.atm_floating(shifts=[[0.01] * len(mat.ATM_SWAPS) for _ in mat.ATM_OPTIONS])
MODELS = {name: int(m) for name, m in cp.MODELS.items()}
XS = [0.01, 0.03, 0.05, 0.08]


def pricer(kind, vol, model="Standard", m=0.0, lower=0.0, upper=1.0, precision=1e-6):
    spec = {"type": kind, "meanReversion": m, "volatility": vol}
    if kind != "LinearTsrPricer":
        spec["model"] = MODELS[model]
    if kind == "NumericHaganPricer":
        spec.update({"lowerLimit": lower, "upperLimit": upper, "precision": precision})
    return spec


def cases():
    out = []
    coupons = cp.coupons()[:3]
    with cxx.connect() as H:
        for name, p in (("numeric, NonParallelShifts",
                         pricer("NumericHaganPricer", LOGNORMAL, "NonParallelShifts", 0.03)),
                        ("analytic, Standard", pricer("AnalyticHaganPricer", LOGNORMAL, "Standard", -0.01)),
                        ("linear TSR", pricer("LinearTsrPricer", LOGNORMAL, m=0.02))):
            with H.case_settings(v.enc(TODAY)) as st:
                out.append({"name": f"meanReversion: {name}", "inputs": {"settings": st, "pricer": p},
                            "expected": {"meanReversion": v.attempt(
                                lambda p=p: H.call("MeanRevertingPricer::meanReversion", p))}})
        for vol_name, vol in (("lognormal", LOGNORMAL), ("normal", NORMAL)):
            for limits in ((0.0, 1.0), (0.001, 0.5)):
                p = pricer("NumericHaganPricer", vol, "Standard", 0.0, *limits)
                with H.case_settings(v.enc(TODAY)) as st:
                    rows = []
                    for c in coupons:
                        for t in (int(ql.Option.Call), int(ql.Option.Put)):
                            for k in (0.04, 0.06):
                                rows.append(v.attempt(lambda c=c, t=t, k=k: H.call(
                                    "NumericHaganPricer::integrationLimits", p, c, t, k)))
                    out.append({"name": f"integration limits: {vol_name}, limits {limits!r}",
                                "inputs": {"settings": st, "pricer": p, "coupons": coupons, "strikes": [0.04, 0.06]},
                                "expected": {"limits": rows}})
        for vol_name, vol in (("lognormal", LOGNORMAL), ("normal", NORMAL), ("shifted lognormal", SHIFTED)):
            with H.case_settings(v.enc(TODAY)) as st:
                cal = ql.TARGET()
                expiries = [cal.advance(TODAY, n, ql.Years).serialNumber() for n in (1, 5)]
                rows = []
                for e in expiries:
                    for t in (int(ql.Option.Call), int(ql.Option.Put)):
                        for k in (0.03, 0.05, 0.07):
                            rows.append(v.attempt(lambda e=e, t=t, k=k: H.call(
                                "MarketQuotedOptionPricer::operator()", 0.05, e, [10, int(ql.Years)], vol, k, t, 2.5)))
                out.append({"name": f"MarketQuotedOptionPricer: {vol_name}",
                            "inputs": {"settings": st, "forward": 0.05, "expiries": expiries,
                                       "swapTenor": [10, int(ql.Years)], "volatility": vol,
                                       "strikes": [0.03, 0.05, 0.07], "deflator": 2.5},
                            "expected": {"prices": rows}})
        with H.case_settings(v.enc(TODAY)) as st:
            rows, specs = [], []
            for q in (1, 2):
                for delta in (0.5, 1.0):
                    for length in (2, 10):
                        specs.append([q, delta, length])
                        rows.append(v.attempt(lambda q=q, delta=delta, length=length: H.call(
                            "GFunctionFactory::newGFunctionStandard", q, delta, length, XS)))
            out.append({"name": "newGFunctionStandard", "inputs": {"settings": st, "specs": specs, "xs": XS},
                        "expected": {"values": rows}})
        with H.case_settings(v.enc(TODAY)) as st:
            rows = [v.attempt(lambda c=c: H.call("GFunctionFactory::newGFunctionExactYield", c, XS)) for c in coupons]
            out.append({"name": "newGFunctionExactYield", "inputs": {"settings": st, "coupons": coupons, "xs": XS},
                        "expected": {"values": rows}})
        for m in (0.0, 0.03):
            with H.case_settings(v.enc(TODAY)) as st:
                rows = [v.attempt(lambda c=c: H.call("GFunctionFactory::newGFunctionWithShifts", c, m, XS))
                        for c in coupons]
                out.append({"name": f"newGFunctionWithShifts, reversion {m!r}",
                            "inputs": {"settings": st, "coupons": coupons, "meanReversion": m, "xs": XS},
                            "expected": {"values": rows}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/cashflows/conundrumpricer.hpp", cases)
