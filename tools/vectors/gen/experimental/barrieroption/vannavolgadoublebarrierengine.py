# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's VannaVolgaDoubleBarrierEngine (ql/experimental/barrieroption/vannavolgadoublebarrierengine.hpp),
on the evaluation date 15 October 2026: FX double barriers priced by the Vanna-Volga method over the analytic
(Ikeda-Kunitomo) and Suo-Wang engines, QuantLib-SWIG's two templates.

A spot of 1.30265 and two smiles (25-delta put, at-the-money delta-neutral and 25-delta call, forward deltas): QuantLib's
case's one-year smile and a steeper one; two corridors, [1.1, 1.5] and [1.0, 1.6]; one and two years; equal domestic and
foreign rates of 0.5% and 3% on Actual/365F curves from the evaluation date; each knock-in and knock-out a call and a
put at 1.2, 1.3 and 1.4, with the vanilla price from the smile and with QuantLib's vanilla-delta adaptation at a given
price; the NPV. Then the spot beyond each barrier, an expired option and the KIKO type ("Only same type barrier
supported").

The outputs nanoql fixes are left out: the domestic and foreign rates are equal, so the domestic discount nanoql passes
the smile's interpolation equals the foreign one QuantLib passes twice; the curves and the engine's volatility share
Actual/365F and the evaluation date, so reading each curve at the quotes' instant is reading it at their time; every
rebate is 0; and no spot lies on a barrier, where QuantLib's strict tests send it to the inner engine's "barrier
touched". The corridors are wide against the vols, so both inner engines' series converge in QuantLib's terms.

A case's inputs: the quotes {"put25", "atm", "call25", "maturity"}, the spot, the rates {"domestic", "foreign"}, the
barrier (the analytic double-barrier generator's spec), the exercise's date, the payoffs (bc's specs), "adaptVanDelta"
and "bsPriceWithSmile", and "inner" ("analytic" or "suowang"). The wheel's calls, by QuantLib-SWIG 1.44's
barrieroptions.i: DeltaVolQuote(quote, Fwd, maturity, AtmDeltaNeutral), DeltaVolQuote(delta, quote, maturity, Fwd),
VannaVolgaIKDoubleBarrierEngine and VannaVolgaWODoubleBarrierEngine (atmVol, vol25Put, vol25Call, spotFX, domesticTS,
foreignTS, adaptVanDelta, bsPriceWithSmile), each VannaVolgaDoubleBarrierEngine<AnalyticDoubleBarrierEngine> and
<SuoWangDoubleBarrierEngine>, and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/Experimental/BarrierOption/VannaVolgaDoubleBarrierEngineVectorTests.cs."""
import importlib.util
import math
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


adbe = _load("analyticdoublebarrierengine", "pricingengines/barrier/analyticdoublebarrierengine.py")
bc = adbe.bc
R = adbe.R
CALL, PUT = adbe.CALL, adbe.PUT
KNOCK_IN, KNOCK_OUT, KIKO = adbe.KNOCK_IN, adbe.KNOCK_OUT, adbe.KIKO
SPOT = 1.30265
SMILES = [("QuantLib's smile", (0.10087, 0.08925, 0.08463)), ("a steeper smile", (0.13, 0.10, 0.115))]
CORRIDORS = [(1.1, 1.5), (1.0, 1.6)]
YEARS = [1, 2]
RATES = [0.005, 0.03]
STRIKES = [1.2, 1.3, 1.4]
INNER = [("analytic", ql.VannaVolgaIKDoubleBarrierEngine), ("suowang", ql.VannaVolgaWODoubleBarrierEngine)]
A365 = ql.Actual365Fixed()


def quotes_spec(smile, years):
    return {"put25": smile[0], "atm": smile[1], "call25": smile[2], "maturity": float(years)}


def price(q, spot, rates, barrier, ex_date, payoff, adapt, bs, cls):
    atm = ql.DeltaVolQuoteHandle(ql.DeltaVolQuote(ql.QuoteHandle(ql.SimpleQuote(q["atm"])), ql.DeltaVolQuote.Fwd,
                                                  q["maturity"], ql.DeltaVolQuote.AtmDeltaNeutral))
    put25 = ql.DeltaVolQuoteHandle(ql.DeltaVolQuote(-0.25, ql.QuoteHandle(ql.SimpleQuote(q["put25"])), q["maturity"],
                                                    ql.DeltaVolQuote.Fwd))
    call25 = ql.DeltaVolQuoteHandle(ql.DeltaVolQuote(0.25, ql.QuoteHandle(ql.SimpleQuote(q["call25"])), q["maturity"],
                                                     ql.DeltaVolQuote.Fwd))
    domestic = ql.YieldTermStructureHandle(ql.FlatForward(R, rates["domestic"], A365))
    foreign = ql.YieldTermStructureHandle(ql.FlatForward(R, rates["foreign"], A365))
    o = ql.DoubleBarrierOption(barrier["barrierType"], barrier["barrierLo"], barrier["barrierHi"], barrier["rebate"],
                               bc.payoff(payoff), ql.EuropeanExercise(ql.Date(ex_date)))
    o.setPricingEngine(cls(atm, put25, call25, ql.QuoteHandle(ql.SimpleQuote(spot)), domestic, foreign, adapt, bs))
    return v.attempt(o.NPV)


def cases():
    st = v.settings(R)
    out = []

    def add(name, q, spot, rates, barrier, ex_date, specs, adapt, bs, inner, cls):
        out.append({"name": name,
                    "inputs": {"settings": st, "quotes": q, "spot": spot, "rates": rates, "barrier": barrier,
                               "exerciseDate": ex_date, "payoffs": specs, "adaptVanDelta": adapt,
                               "bsPriceWithSmile": bs, "inner": inner},
                    "expected": {"NPV": [price(q, spot, rates, barrier, ex_date, p, adapt, bs, cls) for p in specs]}})

    for sname, smile in SMILES:
        for years in YEARS:
            q = quotes_spec(smile, years)
            ex_date = (R + 365 * years).serialNumber()
            for rate in RATES:
                rates = {"domestic": rate, "foreign": rate}
                for lo, hi in CORRIDORS:
                    for tname, t in adbe.TYPES:
                        barrier = adbe.barrier_spec(t, lo, hi)
                        for kind, option_type in (("call", CALL), ("put", PUT)):
                            specs = [bc.payoff_spec("PlainVanilla", option_type, k) for k in STRIKES]
                            for adapt in (False, True):
                                # the vanilla price QuantLib's case passes: Black's at the smile's at-the-money vol
                                bs = 0.0
                                if adapt:
                                    fwd = SPOT
                                    bs = ql.blackFormula(option_type, 1.3, fwd, smile[1] * math.sqrt(years),
                                                         math.exp(-rate * years))
                                for inner, cls in INNER:
                                    add(f"{sname}, {years}y, rate {rate:g}, {tname} on [{lo:g}, {hi:g}], {kind}"
                                        f"{', adapted' if adapt else ''}, {inner}", q, SPOT, rates, barrier, ex_date,
                                        specs, adapt, bs, inner, cls)
    q = quotes_spec(SMILES[0][1], 1)
    ex_date = (R + 365).serialNumber()
    rates = {"domestic": 0.01, "foreign": 0.01}
    specs = [bc.payoff_spec("PlainVanilla", CALL, 1.3)]
    for spot in (1.05, 1.55):
        for tname, t in adbe.TYPES:
            add(f"the spot at {spot:g}, beyond a barrier, {tname}", q, spot, rates, adbe.barrier_spec(t, 1.1, 1.5),
                ex_date, specs, False, 0.0, "analytic", ql.VannaVolgaIKDoubleBarrierEngine)
    add("expired", q, SPOT, rates, adbe.barrier_spec(KNOCK_OUT, 1.1, 1.5), (R - 1).serialNumber(), specs, False, 0.0,
        "analytic", ql.VannaVolgaIKDoubleBarrierEngine)
    add("a KIKO type", q, SPOT, rates, adbe.barrier_spec(KIKO, 1.1, 1.5), ex_date, specs, False, 0.0, "analytic",
        ql.VannaVolgaIKDoubleBarrierEngine)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/barrieroption/vannavolgadoublebarrierengine.hpp", cases)
