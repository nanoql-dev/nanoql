# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Swaption (ql/instruments/swaption.hpp), priced by the wheel's BlackSwaptionEngine and
BachelierSwaptionEngine on volatility quotes: 60 swaptions on the evaluation date 15 January 2025, and 40 implied
volatilities.

The swaptions are payers and receivers, on vanilla swaps (Euribor 6M and 3M) and overnight-indexed swaps (ESTR and
SOFR conventions), from 1M to 10Y options on 2W to 20Y swaps, struck from 1% to 6%, at nominals of 1 to 10 million,
with each of QuantLib's four consistent settlement pairs and the two inconsistent ones (physical with a cash method and
cash with a physical one, which the pricing refuses), both cash annuity models, floating spreads, a fixed schedule from
dates (whose par-yield annuity compounds annually) and fixed and floating schedules of different lengths (the swap
length is the floating schedule's), a swap shorter than half a month (its length floored at 1/12), zero and very large
volatilities, a displacement, a Bermudan exercise ("not a European option"), a swap starting before the exercise date,
an exercise date on the evaluation date (expired unless the reference date's events are included), an expired swaption,
indexed Ibor coupons, and testBlackEngineCaching's swaption (1Y on 1Y at 3%, 12%), whose NPV is that case's only value.

Each swaption's outputs are its settlement type and method, its type, whether it has expired, and on each engine
("black", "bachelier") the NPV and the results the wheel reads (vega, delta, annuity, forwardPrice, strike, atmForward,
swapLength and stdDev); a refused pricing is each output's {"throws": true}, as is an expired swaption's result, which
the engine never set. Amounts (NPV, vega, delta, annuity, forward price) are compared at the nominal.

Each implied volatility is that of a swaption at a price: the price is a wheel engine's at a chosen volatility (the spot
NPV or the forward price), or a chosen one where it can't be bracketed; its outputs are the implied volatility, or
{"throws": true}. They cover both volatility types, displacements, spot and forward prices, accuracies, bracket edges,
a guess outside the bracket, an empty bracket, an expired swaption, a Bermudan one, and QuantLib's helper skipping the
swaption's checks: a physical swaption with a cash method implies the volatility of its physical twin, and a cash
swaption with a physical method fails in the engine.

The swaption spec is {"swap", "exercise", "settlementType", "settlementMethod"}, the swap a spec of
instruments/fixedvsfloatingswap.py and the exercise one of tests/NanoQL.Tests/Vectors/OptionInputs.cs; an engine is
{"vol", "dayCounter", "displacement", "model"} (no displacement for the Bachelier engine), on the discount curve
"discountCurve", a flat forward spec. The wheel's Swaption, BlackSwaptionEngine and BachelierSwaptionEngine are the
proxies of the C++ classes' constructors; its vega(), delta() and the other result accessors read result<Real>(tag).
The functions here build the specs and the swaptions the swaption generators share (instruments/makeswaption,
pricingengines/swaption/blackswaptionengine and their harness counterparts import this file). Read back by
tests/NanoQL.Tests/Vectors/Instruments/SwaptionVectorTests.cs."""
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


fvf = _load("wheel_fixedvsfloatingswap", "instruments/fixedvsfloatingswap.py")
irx, ibc = fvf.irx, fvf.ibc

TODAY = irx.TODAY
TARGET_CAL = ql.TARGET()
TARGET, US_SOFR = irx.TARGET, irx.US_SOFR
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
D, W, M, Y = ql.Days, ql.Weeks, ql.Months, ql.Years
PAYER, RECEIVER = ql.Swap.Payer, ql.Swap.Receiver
PHYSICAL, CASH = int(ql.Settlement.Physical), int(ql.Settlement.Cash)
OTC, CLEARED = int(ql.Settlement.PhysicalOTC), int(ql.Settlement.PhysicalCleared)
COLLATERALIZED, PAR_YIELD = int(ql.Settlement.CollateralizedCashPrice), int(ql.Settlement.ParYieldCurve)
SWAP_RATE, DISCOUNT_CURVE = int(ql.BlackSwaptionEngine.SwapRate), int(ql.BlackSwaptionEngine.DiscountCurve)
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
SPOT, FORWARD = int(ql.Swaption.Spot), int(ql.Swaption.Forward)

DISCOUNT = fvf.curve(0.027)
FORECAST = fvf.curve(0.031)
E6M = ibc.euribor((6, M), FORECAST)
E3M = ibc.euribor((3, M), fvf.curve(0.045, A360, ql.Simple))
ESTR = irx.overnight_spec("ESTR", 0, TARGET, A360, FORECAST)
SOFR = irx.overnight_spec("SOFR", 0, US_SOFR, A360, fvf.curve(0.043, A360))

RESULTS = ["vega", "delta", "annuity", "forwardPrice", "strike", "atmForward", "swapLength", "stdDev"]
AMOUNTS = {"vega", "delta", "annuity", "forwardPrice"}


def european(date):
    return {"type": "EuropeanExercise", "date": v.enc(date)}


def bermudan(dates):
    return {"type": "BermudanExercise", "dates": v.enc(list(dates)), "payoffAtExpiry": False}


def make_exercise(spec):
    if spec["type"] == "EuropeanExercise":
        return ql.EuropeanExercise(ql.Date(spec["date"]))
    if spec["type"] == "BermudanExercise":
        return ql.BermudanExercise([ql.Date(x) for x in spec["dates"]], spec["payoffAtExpiry"])
    raise ValueError(f"unknown exercise {spec['type']}")


def swaption_spec(swap, exercise, settlement_type=PHYSICAL, settlement_method=OTC):
    return {"swap": swap, "exercise": exercise, "settlementType": settlement_type,
            "settlementMethod": settlement_method}


def make_swaption(spec):
    """The wheel's swaption for a spec, on a swap built by instruments/fixedvsfloatingswap.py's rules."""
    return ql.Swaption(fvf.make_swap(spec["swap"]), make_exercise(spec["exercise"]), spec["settlementType"],
                       spec["settlementMethod"])


def black_spec(vol, dc=A365, displacement=0.0, model=DISCOUNT_CURVE):
    return {"vol": vol, "dayCounter": dc, "displacement": displacement, "model": model}


def bachelier_spec(vol, dc=A365, model=DISCOUNT_CURVE):
    return {"vol": vol, "dayCounter": dc, "model": model}


def black_engine(curve_spec, spec):
    return ql.BlackSwaptionEngine(irx.handle(curve_spec), ql.QuoteHandle(ql.SimpleQuote(spec["vol"])),
                                  irx.day_counter(spec["dayCounter"]), spec["displacement"], spec["model"])


def bachelier_engine(curve_spec, spec):
    return ql.BachelierSwaptionEngine(irx.handle(curve_spec), ql.QuoteHandle(ql.SimpleQuote(spec["vol"])),
                                      irx.day_counter(spec["dayCounter"]), spec["model"])


def priced(swaption, scale, prefix):
    """The NPV and the results the wheel reads, with the engine set; a refused pricing is each output's throw."""
    out = {prefix + "NPV": fvf.ibc.scaled(swaption.NPV, scale)}
    for name in RESULTS:
        f = getattr(swaption, name)
        out[prefix + name[0].upper() + name[1:]] = (
            fvf.ibc.scaled(f, scale) if name in AMOUNTS else v.attempt(f))
    return out


def dates_of(exercise_tenor, length, lag=2):
    """The exercise date, the TARGET business day the tenor after the evaluation date, and the swap's start, two
    business days later, and its unadjusted maturity."""
    exercise = TARGET_CAL.advance(TODAY, exercise_tenor)
    start = TARGET_CAL.advance(exercise, lag, D)
    return exercise, start, start + length


def vanilla(swap_type, nominal, strike, start, maturity, index=E6M, spread=0.0, fixed_tenor=ql.Period(1, Y),
            fixed_dc=THIRTY360_BB, float_maturity=None, fixed_schedule=None, use_indexed=None):
    fixed = fixed_schedule or fvf.sched(start, maturity, fixed_tenor, TARGET, ql.ModifiedFollowing)
    floating = fvf.sched(start, float_maturity or maturity, ql.Period(index["tenor"][0], index["tenor"][1]), TARGET,
                         ql.ModifiedFollowing)
    return fvf.vanilla(swap_type, nominal, fixed, strike, fixed_dc, floating, index, spread, A360,
                       use_indexed=use_indexed)


def overnight(swap_type, nominal, strike, start, maturity, index=ESTR, spread=0.0, cal=TARGET):
    s = fvf.sched(start, maturity, ql.Period(1, Y), cal, ql.ModifiedFollowing)
    return fvf.ois("nominal", swap_type, [nominal], s, strike, A360, index, spread=spread)


def swaptions():
    """(name, swaption spec, black engine, bachelier engine, settings) for the 60 swaptions."""
    out = []

    def add(name, spec, black=None, bachelier=None, **settings):
        out.append((name, spec, black or black_spec(0.2), bachelier or bachelier_spec(0.008), settings))

    k = 0
    for exercise_tenor, length in [((1, Y), (5, Y)), ((2, Y), (2, Y)), ((5, Y), (10, Y)), ((6, M), (3, Y))]:
        exercise, start, maturity = dates_of(ql.Period(*exercise_tenor), ql.Period(*length))
        for swap_type in (PAYER, RECEIVER):
            for strike in (0.02, 0.031, 0.045):
                st, sm = [(PHYSICAL, OTC), (PHYSICAL, CLEARED), (CASH, COLLATERALIZED), (CASH, PAR_YIELD)][k % 4]
                model = DISCOUNT_CURVE if k % 3 else SWAP_RATE
                name = (f"{exercise_tenor[0]}{'MY'[exercise_tenor[1] == Y]} on {length[0]}Y "
                        f"{'payer' if swap_type == PAYER else 'receiver'} {strike} / {st}-{sm} / {k}")
                add(name, swaption_spec(vanilla(swap_type, 1e6, strike, start, maturity), european(exercise), st, sm),
                    black_spec(0.15 + 0.01 * (k % 7), model=model), bachelier_spec(0.006 + 0.0005 * (k % 5),
                                                                                   model=model))
                k += 1
    # 24 so far: the four consistent pairs over four tenors, payer and receiver, three strikes.
    exercise, start, maturity = dates_of(ql.Period(1, Y), ql.Period(5, Y))
    add("inconsistent physical, par yield curve", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity), european(exercise), PHYSICAL, PAR_YIELD))
    add("inconsistent physical, collateralized", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity), european(exercise), PHYSICAL, COLLATERALIZED))
    add("inconsistent cash, physical OTC", swaption_spec(
        vanilla(RECEIVER, 1e6, 0.03, start, maturity), european(exercise), CASH, OTC))
    add("inconsistent cash, physical cleared", swaption_spec(
        vanilla(RECEIVER, 1e6, 0.03, start, maturity), european(exercise), CASH, CLEARED))
    for spread, st, sm in [(0.001, PHYSICAL, OTC), (-0.0025, CASH, PAR_YIELD), (0.004, CASH, COLLATERALIZED)]:
        add(f"spread {spread} / {st}-{sm}", swaption_spec(
            vanilla(PAYER, 1e6, 0.03, start, maturity, spread=spread), european(exercise), st, sm))
    for model in (SWAP_RATE, DISCOUNT_CURVE):
        add(f"cash par yield on 3M Euribor, model {model}", swaption_spec(
            vanilla(RECEIVER, 5e6, 0.04, start, maturity, index=E3M, fixed_tenor=ql.Period(6, M), fixed_dc=A360),
            european(exercise), CASH, PAR_YIELD), black_spec(0.25, model=model), bachelier_spec(0.009, model=model))
    dated_fixed = fvf.dates_schedule([start, TARGET_CAL.advance(start, 1, Y), TARGET_CAL.advance(start, 2, Y),
                                      TARGET_CAL.advance(start, 5, Y, ql.ModifiedFollowing)])
    add("fixed schedule from dates, cash par yield", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity, fixed_schedule=dated_fixed), european(exercise), CASH, PAR_YIELD))
    add("fixed schedule from dates, physical", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity, fixed_schedule=dated_fixed), european(exercise)))
    add("floating schedule longer than the fixed one", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity, float_maturity=maturity + ql.Period(3, M)), european(exercise)))
    add("floating schedule shorter than the fixed one", swaption_spec(
        vanilla(RECEIVER, 1e6, 0.03, start, maturity + ql.Period(4, M), float_maturity=maturity),
        european(exercise), CASH, PAR_YIELD))
    short_exercise, short_start, _ = dates_of(ql.Period(3, M), ql.Period(2, W))
    add("2W swap, length floored at a month", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, short_start, short_start + ql.Period(2, W), fixed_tenor=ql.Period(1, Y),
                index=ibc.euribor((1, W), FORECAST)),
        european(short_exercise), CASH, PAR_YIELD))
    seven, seven_start, seven_mat = dates_of(ql.Period(7, Y), ql.Period(3, Y))
    add("7Y on 3Y receiver, physical cleared", swaption_spec(
        vanilla(RECEIVER, 1e6, 0.033, seven_start, seven_mat), european(seven), PHYSICAL, CLEARED))
    add("semiannual fixed leg, cash par yield", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity, fixed_tenor=ql.Period(6, M)), european(exercise), CASH, PAR_YIELD),
        black_spec(0.2, model=SWAP_RATE))
    one_m, one_m_start, one_m_mat = dates_of(ql.Period(1, M), ql.Period(1, Y))
    add("1M on 1Y", swaption_spec(vanilla(RECEIVER, 2e6, 0.031, one_m_start, one_m_mat), european(one_m)))
    long_e, long_s, long_m = dates_of(ql.Period(10, Y), ql.Period(20, Y))
    add("10Y on 20Y, nominal 10 million", swaption_spec(
        vanilla(PAYER, 1e7, 0.035, long_s, long_m), european(long_e), CASH, COLLATERALIZED))
    add("zero volatility", swaption_spec(vanilla(PAYER, 1e6, 0.025, start, maturity), european(exercise)),
        black_spec(0.0), bachelier_spec(0.0))
    add("large volatility", swaption_spec(vanilla(RECEIVER, 1e6, 0.025, start, maturity), european(exercise)),
        black_spec(3.0), bachelier_spec(0.2))
    add("displaced lognormal, negative strike", swaption_spec(
        vanilla(PAYER, 1e6, -0.005, start, maturity), european(exercise)), black_spec(0.3, displacement=0.02))
    add("negative strike without displacement", swaption_spec(
        vanilla(RECEIVER, 1e6, -0.005, start, maturity), european(exercise)))
    add("Actual/360 volatility", swaption_spec(vanilla(PAYER, 1e6, 0.03, start, maturity), european(exercise)),
        black_spec(0.2, A360), bachelier_spec(0.008, THIRTY360_BB))
    add("Bermudan exercise", swaption_spec(vanilla(PAYER, 1e6, 0.03, start, maturity),
                                           bermudan([exercise, TARGET_CAL.advance(exercise, 1, Y)])))
    add("swap starting before the exercise date", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, start, maturity), european(TARGET_CAL.advance(start, 1, D))))
    add("exercise on the evaluation date", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, TARGET_CAL.advance(TODAY, 2, D), TODAY + ql.Period(5, Y)), european(TODAY)))
    add("exercise on the evaluation date, reference date events included", swaption_spec(
        vanilla(PAYER, 1e6, 0.03, TARGET_CAL.advance(TODAY, 2, D), TODAY + ql.Period(5, Y)), european(TODAY)),
        include_reference_date_events=True)
    expired = TARGET_CAL.advance(TODAY, -1, M)
    add("expired", swaption_spec(vanilla(PAYER, 1e6, 0.03, TARGET_CAL.advance(expired, 2, D),
                                         TARGET_CAL.advance(expired, 2, D) + ql.Period(5, Y)), european(expired)))
    add("indexed coupons", swaption_spec(vanilla(PAYER, 1e6, 0.03, start, maturity), european(exercise)),
        use_at_par_coupons=False)
    add("indexed coupons by the swap", swaption_spec(
        vanilla(RECEIVER, 1e6, 0.03, start, maturity, use_indexed=True), european(exercise), CASH, PAR_YIELD))
    cache_e, cache_s, _ = dates_of(ql.Period(1, Y), ql.Period(1, Y))
    add("testBlackEngineCaching's swaption", swaption_spec(
        vanilla(PAYER, 1.0, 0.03, cache_s, TARGET_CAL.advance(cache_s, 1, Y, ql.ModifiedFollowing)),
        european(cache_e)), black_spec(0.12, model=SWAP_RATE))
    for st, sm, spread in [(PHYSICAL, OTC, 0.0), (CASH, PAR_YIELD, 0.0), (CASH, COLLATERALIZED, 0.0015),
                           (PHYSICAL, CLEARED, -0.001)]:
        add(f"overnight-indexed ESTR / {st}-{sm} / spread {spread}", swaption_spec(
            overnight(PAYER if spread >= 0 else RECEIVER, 1e6, 0.03, start, maturity, spread=spread),
            european(exercise), st, sm))
    sofr_e, sofr_s, sofr_m = dates_of(ql.Period(2, Y), ql.Period(3, Y))
    add("overnight-indexed SOFR", swaption_spec(
        overnight(RECEIVER, 3e6, 0.042, sofr_s, sofr_m, index=SOFR, cal=US_SOFR), european(sofr_e), CASH, PAR_YIELD),
        black_spec(0.18, model=SWAP_RATE), bachelier_spec(0.0075, model=SWAP_RATE))
    assert len(out) == 60, len(out)
    return out


def swaption_case(name, spec, black, bachelier, settings):
    with v.case_settings(TODAY, **settings) as st:
        swaption = make_swaption(spec)
        scale = fvf.scale_of(spec["swap"])
        expected = {"settlementType": int(swaption.settlementType()),
                    "settlementMethod": int(swaption.settlementMethod()), "type": int(swaption.type()),
                    "isExpired": swaption.isExpired()}
        swaption.setPricingEngine(black_engine(DISCOUNT, black))
        expected.update(priced(swaption, scale, "black"))
        swaption.setPricingEngine(bachelier_engine(DISCOUNT, bachelier))
        expected.update(priced(swaption, scale, "bachelier"))
        return {"name": name,
                "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "black": black,
                           "bachelier": bachelier},
                "expected": expected}


def implied_spec(price, guess, accuracy=1e-4, max_evaluations=100, min_vol=1e-7, max_vol=4.0, vol_type=SL,
                 displacement=0.0, price_type=SPOT):
    return {"price": price, "guess": guess, "accuracy": accuracy, "maxEvaluations": max_evaluations,
            "minVol": min_vol, "maxVol": max_vol, "type": vol_type, "displacement": displacement,
            "priceType": price_type}


def price_of(spec, vol, vol_type, displacement, price_type, settings):
    """The price an engine on a quote gives, the spot NPV or the forward price."""
    with v.case_settings(TODAY, **settings):
        swaption = make_swaption(spec)
        engine = (black_engine(DISCOUNT, black_spec(vol, displacement=displacement)) if vol_type == SL
                  else bachelier_engine(DISCOUNT, bachelier_spec(vol)))
        swaption.setPricingEngine(engine)
        return swaption.NPV() if price_type == SPOT else swaption.forwardPrice()


def implied_volatilities():
    """(name, swaption spec, implied-volatility spec, settings) for the 40 implied volatilities."""
    out = []
    exercise, start, maturity = dates_of(ql.Period(1, Y), ql.Period(5, Y))
    e2, s2, m2 = dates_of(ql.Period(5, Y), ql.Period(10, Y))
    payer = swaption_spec(vanilla(PAYER, 1e6, 0.03, start, maturity), european(exercise))
    receiver = swaption_spec(vanilla(RECEIVER, 1e6, 0.035, start, maturity), european(exercise), CASH, PAR_YIELD)
    long_payer = swaption_spec(vanilla(PAYER, 1e6, 0.025, s2, m2, spread=0.002), european(e2), CASH, COLLATERALIZED)
    ois_payer = swaption_spec(overnight(PAYER, 1e6, 0.03, start, maturity), european(exercise))

    def add(name, spec, implied, settings=None):
        out.append((name, spec, implied, settings or {}))

    for k, (vol, vol_type, displacement, price_type) in enumerate(
            [(0.2, SL, 0.0, SPOT), (0.2, SL, 0.0, FORWARD), (0.45, SL, 0.0, SPOT), (0.05, SL, 0.0, FORWARD),
             (0.15, SL, 0.01, SPOT), (0.3, SL, 0.02, FORWARD), (0.008, N, 0.0, SPOT), (0.008, N, 0.0, FORWARD),
             (0.012, N, 0.0, SPOT), (0.003, N, 0.0, FORWARD)]):
        for which, spec in (("payer", payer), ("receiver, cash", receiver)):
            price = price_of(spec, vol, vol_type, displacement, price_type, {})
            guess = 0.1 if vol_type == SL else 0.005
            add(f"{which} at {vol} / type {vol_type} / displacement {displacement} / price type {price_type} / {k}",
                spec, implied_spec(price, guess, 1e-10, 100, 1e-7, 4.0, vol_type, displacement, price_type))
    # 20 so far.
    for vol in (0.1, 0.6):
        price = price_of(long_payer, vol, SL, 0.0, SPOT, {})
        add(f"long payer with a spread at {vol}", long_payer, implied_spec(price, 0.2, 1e-8))
    price = price_of(ois_payer, 0.22, SL, 0.0, SPOT, {})
    add("overnight-indexed at 0.22", ois_payer, implied_spec(price, 0.1, 1e-10))
    price = price_of(ois_payer, 0.0095, N, 0.0, FORWARD, {})
    add("overnight-indexed normal, forward price", ois_payer, implied_spec(price, 0.005, 1e-10, vol_type=N,
                                                                           price_type=FORWARD))
    base = price_of(payer, 0.2, SL, 0.0, SPOT, {})
    add("default accuracy", payer, implied_spec(base, 0.1))
    add("few evaluations", payer, implied_spec(base, 0.1, 1e-14, 3))
    add("price above the bracket", payer, implied_spec(1e9, 0.1))
    add("zero price", payer, implied_spec(0.0, 0.1))
    add("guess below the bracket", payer, implied_spec(base, 1e-8))
    add("guess above the bracket", payer, implied_spec(base, 5.0))
    add("empty bracket", payer, implied_spec(base, 0.1, min_vol=0.5, max_vol=0.5))
    add("non-positive accuracy", payer, implied_spec(base, 0.1, accuracy=0.0))
    add("narrow bracket", payer, implied_spec(base, 0.2, 1e-10, 100, 0.19, 0.21))
    add("bracket just around the volatility", payer, implied_spec(base, 0.2, 1e-12, 100, 0.1999, 0.2001))
    physical_twin = swaption_spec(payer["swap"], payer["exercise"], PHYSICAL, PAR_YIELD)
    add("physical with a cash method, priced as physical", physical_twin, implied_spec(base, 0.1, 1e-10))
    cash_twin = swaption_spec(payer["swap"], payer["exercise"], CASH, OTC)
    add("cash with a physical method, refused by the engine", cash_twin, implied_spec(base, 0.1, 1e-10))
    expired = TARGET_CAL.advance(TODAY, -1, M)
    add("expired", swaption_spec(vanilla(PAYER, 1e6, 0.03, TARGET_CAL.advance(expired, 2, D),
                                         TARGET_CAL.advance(expired, 2, D) + ql.Period(5, Y)), european(expired)),
        implied_spec(base, 0.1))
    add("Bermudan", swaption_spec(payer["swap"], bermudan([exercise, TARGET_CAL.advance(exercise, 1, Y)])),
        implied_spec(base, 0.1))
    add("indexed coupons", payer, implied_spec(price_of(payer, 0.2, SL, 0.0, SPOT, {"use_at_par_coupons": False}),
                                               0.1, 1e-10), {"use_at_par_coupons": False})
    add("normal type, forward price on the cash receiver", receiver,
        implied_spec(price_of(receiver, 0.0065, N, 0.0, FORWARD, {}), 0.01, 1e-12, 100, 1e-7, 0.05, N, 0.0, FORWARD))
    assert len(out) == 40, len(out)
    return out


def implied_case(name, spec, implied, settings):
    with v.case_settings(TODAY, **settings) as st:
        swaption = make_swaption(spec)
        value = v.attempt(lambda: swaption.impliedVolatility(
            implied["price"], irx.handle(DISCOUNT), implied["guess"], implied["accuracy"],
            implied["maxEvaluations"], implied["minVol"], implied["maxVol"], implied["type"],
            implied["displacement"], implied["priceType"]))
        return {"name": "implied volatility / " + name,
                "inputs": {"settings": st, "swaption": spec, "discountCurve": DISCOUNT, "impliedVolatility": implied},
                "expected": {"impliedVolatility": value}}


def cases():
    out = [swaption_case(*row) for row in swaptions()]
    out += [implied_case(*row) for row in implied_volatilities()]
    check_rows(out)
    return out


def check_rows(out):
    by_name = {c["name"]: c["expected"] for c in out}
    # The inconsistent pairs are refused when priced, not when built.
    assert by_name["inconsistent physical, par yield curve"]["blackNPV"] == v.THROWS
    assert by_name["inconsistent physical, par yield curve"]["settlementMethod"] == PAR_YIELD
    # QuantLib's helper skips the swaption's checks: the physical twin implies the physical volatility.
    twin = by_name["implied volatility / physical with a cash method, priced as physical"]["impliedVolatility"]
    assert abs(twin - 0.2) < 1e-8, twin
    assert by_name["implied volatility / cash with a physical method, refused by the engine"][
        "impliedVolatility"] == v.THROWS
    # An expired swaption's results were never set.
    assert by_name["expired"]["isExpired"] and by_name["expired"]["blackVega"] == v.THROWS
    assert by_name["expired"]["blackNPV"]["value"] == 0.0
    # The short swap's length is floored at a month.
    assert by_name["2W swap, length floored at a month"]["blackSwapLength"] == 1.0 / 12.0


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/swaption.hpp", cases)
