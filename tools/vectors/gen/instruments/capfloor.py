# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapFloor, Cap, Floor and Collar (ql/instruments/capfloor.hpp), on the evaluation date 15
January 2025 and Euribor-style legs (tools/vectors/gen/cashflows/iborcoupon.py's IborLeg specs, on flat curves):
38 instruments and 47 implied volatilities.

The instruments are caps, floors and collars on 6M, 3M and 1Y legs of one to ten years, at, in and out of the money,
with one rate or several (the last extended to the leg's length, or more rates than coupons, which QuantLib keeps), a
collar whose cap is below its floor, gearings and spreads (a negative gearing among them), amortizing notionals, a leg
of 3M periods on the 6M index, indexed coupons (by the switch and by the leg), legs started in the past whose past
coupons read stored fixings, a coupon fixing on the evaluation date with and without a stored fixing, a leg paid in
full (expired) and an empty one, a negative-rate leg (which the Black formula refuses), a discount curve dated at the
spot date (its caplets paid before it skipped) and the refusals: no cap or floor rates, and a fixed-rate leg, which
builds and is refused at pricing ("non-FloatingRateCoupon given"). Each instrument's outputs are its type, its cap and
floor rates, its start and maturity dates, whether it has expired, its ATM rate on the discount curve, and its NPV on
the wheel's Black engine (a 20% volatility on Actual/365 (Fixed)) and on its Bachelier engine (0.8%); a refused value
is {"throws": true}. Amounts are compared at the leg's largest notional.

Each implied volatility is an instrument's at a price: the wheel engine's NPV at a chosen volatility (Black, with or
without a displacement, or Bachelier for the normal type), or a chosen price where none brackets; the outputs are the
price (nanoql's own engine's NPV at the case's "priceFrom" volatility) and the implied volatility, or {"throws": true};
a solve's "instrument" names the instrument's case, whose "capFloor" and "discountCurve" it reads, its own settings
being that case's. They cover guesses below, at and above the volatility, accuracies from QuantLib's default to 1e-12,
both volatility types, displacements (the normal type ignoring its own), the bracket's ends, a price no volatility
reaches, a guess outside the bracket, an empty bracket, too few evaluations, an expired instrument and a volatility type
outside the enum ("unknown VolatilityType"). QuantLib's helper solves with NewtonSafe on its own engine, whatever engine
the instrument holds. The stability filter runs on each solve: the solve is redone with the price moved one ulp up and
one down (the case's only moved input, so alone and together), and the implied volatility is written only where every
move leaves it within 1e-12 x max(|a|, |b|, 1), or throwing where it throws; the generator prints "dropped n of m
outputs" per solve and for the file.

An instrument spec is {"type", "leg", "legType", "capRates", "floorRates"}: the type QuantLib's integer, the leg an
IborLeg spec of cashflows/iborcoupon.py ("legType" "IborLeg"), a FixedRateLeg spec of cashflows/fixedratecoupon.py
with its "schedule" ("FixedRateLeg") or null for QuantLib's empty Leg() ("Empty"), built as the wheel's Cap(leg, capRates), Floor(leg, floorRates) or Collar(leg,
capRates, floorRates) (QuantLib-SWIG 1.44, SWIG/capfloor.i, the proxies of the C++ constructors). An engine is
{"vol", "dayCounter", "displacement"} for the Black engine on a volatility quote and {"vol"} for the Bachelier engine,
whose wheel constructor has no day counter (C++'s default, Actual/365 (Fixed)); the discount curve is "discountCurve", a
flat forward spec. The functions here build the specs and the instruments the cap generators share
(instruments/makecapfloor, pricingengines/capfloor/blackcapfloorengine and bacheliercapfloorengine, and their harness
counterparts import this file). Read back by tests/NanoQL.Tests/Vectors/Instruments/CapFloorVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CapFloorInputs.cs."""
import importlib.util
import math
import os
import random
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


ibc = _load("wheel_iborcoupon", "cashflows/iborcoupon.py")
irx, frc = ibc.irx, ibc.frc

TODAY = irx.TODAY
CAL = ql.TARGET()
TARGET = irx.TARGET
A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
D, M, Y = ql.Days, ql.Months, ql.Years
CAP, FLOOR, COLLAR = int(ql.CapFloor.Cap), int(ql.CapFloor.Floor), int(ql.CapFloor.Collar)
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
SPOT = CAL.advance(TODAY, 2, D)

FORECAST = irx.curve_spec(TODAY, 0.031)
DISCOUNT = irx.curve_spec(TODAY, 0.027)
SPOT_DISCOUNT = irx.curve_spec(SPOT, 0.027, A360, ql.Simple)
NEGATIVE = irx.curve_spec(TODAY, -0.004)
E6M = ibc.euribor((6, M), FORECAST)
E3M = ibc.euribor((3, M), FORECAST)
E1Y = ibc.euribor((1, Y), irx.curve_spec(TODAY, 0.034, A360, ql.Simple))
E6M_NEGATIVE = ibc.euribor((6, M), NEGATIVE)


def black_spec(vol=0.2, dc=A365, displacement=0.0):
    return {"vol": vol, "dayCounter": dc, "displacement": displacement}


def bachelier_spec(vol=0.008):
    return {"vol": vol}


def black_engine(curve_spec, spec):
    return ql.BlackCapFloorEngine(irx.handle(curve_spec), ql.QuoteHandle(ql.SimpleQuote(spec["vol"])),
                                  irx.day_counter(spec["dayCounter"]), spec["displacement"])


def bachelier_engine(curve_spec, spec):
    return ql.BachelierCapFloorEngine(irx.handle(curve_spec), ql.QuoteHandle(ql.SimpleQuote(spec["vol"])))


def schedule(start, length, tenor=ql.Period(6, M), rule=ql.DateGeneration.Forward, convention=ql.ModifiedFollowing):
    """capfloor.cpp's makeLeg schedule: from the start, the length on TARGET, the tenor, Forward."""
    end = CAL.advance(start, length, convention)
    return ibc.sched(start, end, tenor, convention=convention, rule=rule)


def ibor_leg(start, length, index=E6M, tenor=ql.Period(6, M), notionals=(1e6,), **kw):
    """capfloor.cpp's makeLeg: the index's day counter, ModifiedFollowing payments, two fixing days."""
    return ibc.leg_spec(schedule(start, length, tenor), index, notionals, payment_dc=index["dayCounter"],
                        payment_convention=ql.ModifiedFollowing, fixing_days=[2], **kw)


def instrument_spec(type_, leg, cap_rates=(), floor_rates=(), leg_type="IborLeg"):
    return {"type": type_, "leg": leg, "legType": leg_type, "capRates": list(cap_rates),
            "floorRates": list(floor_rates)}


def make_leg(spec):
    if spec["legType"] == "Empty":
        return ql.Leg()
    if spec["legType"] == "FixedRateLeg":
        return frc.fixed_rate_leg(spec["leg"])
    return ibc.leg(spec["leg"])


def make_instrument(spec):
    """The wheel's instrument for a spec, its leg built when this is called (reading the at-par switch)."""
    leg = make_leg(spec)
    if spec["type"] == CAP:
        return ql.Cap(leg, spec["capRates"])
    if spec["type"] == FLOOR:
        return ql.Floor(leg, spec["floorRates"])
    return ql.Collar(leg, spec["capRates"], spec["floorRates"])


def scale_of(spec):
    return max(spec["leg"]["notionals"]) if spec["leg"] is not None else 1.0


def past_fixings(spec, seed, through_today=False):
    """Fixings for the coupons of an Ibor leg that fix before the evaluation date (or on it, with through_today),
    drawn with the seed, as v.settings() takes them; None for a leg with none."""
    leg_spec = spec["leg"]
    index = irx.make_index(leg_spec["index"])
    rnd = random.Random(seed)
    pairs = []
    for cf in ibc.leg(leg_spec):
        c = ql.as_floating_rate_coupon(cf)
        d = c.fixingDate()
        if d < TODAY or (through_today and d == TODAY):
            pairs.append((d, round(rnd.uniform(0.025, 0.036), 6)))
    return [(index, pairs)] if pairs else None


def d(day, month, year):
    return ql.Date(day, month, year)


def instruments():
    """(name, instrument spec, discount curve spec, settings) for the 38 instruments; the settings are case_settings()'s
    keywords, the fixings a seed and whether today's fixing is stored."""
    out = []

    def add(name, spec, curve=DISCOUNT, **settings):
        out.append((name, spec, curve, settings))

    for length in (1, 3, 5, 10):
        add(f"{length}Y cap at 3%", instrument_spec(CAP, ibor_leg(SPOT, ql.Period(length, Y)), [0.03]))
        add(f"{length}Y floor at 3%", instrument_spec(FLOOR, ibor_leg(SPOT, ql.Period(length, Y)), (), [0.03]))
    add("3Y collar 3.5% / 2.5%", instrument_spec(COLLAR, ibor_leg(SPOT, ql.Period(3, Y)), [0.035], [0.025]))
    add("5Y cap in the money", instrument_spec(CAP, ibor_leg(SPOT, ql.Period(5, Y)), [0.015]))
    add("5Y cap far out of the money", instrument_spec(CAP, ibor_leg(SPOT, ql.Period(5, Y)), [0.09]))
    add("5Y floor far out of the money", instrument_spec(FLOOR, ibor_leg(SPOT, ql.Period(5, Y)), (), [0.004]))
    add("7Y quarterly cap on 3M", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(7, Y), E3M, ql.Period(3, M), (2.5e6,)), [0.032]))
    add("4Y cap, rates extended", instrument_spec(CAP, ibor_leg(SPOT, ql.Period(4, Y)), [0.025, 0.03]))
    add("2Y floor, more rates than coupons", instrument_spec(
        FLOOR, ibor_leg(SPOT, ql.Period(2, Y)), (), [0.02, 0.025, 0.03, 0.035, 0.04, 0.045]))
    add("3Y collar, cap below floor", instrument_spec(COLLAR, ibor_leg(SPOT, ql.Period(3, Y)), [0.02], [0.03]))
    add("4Y collar, several rates", instrument_spec(
        COLLAR, ibor_leg(SPOT, ql.Period(4, Y)), [0.04, 0.042, 0.045], [0.02, 0.019]))
    add("3Y cap, gearing 2, spread 1%", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(3, Y), gearings=[2.0], spreads=[0.01]), [0.09]))
    add("3Y floor, gearing 0.5, spread 0.2%", instrument_spec(
        FLOOR, ibor_leg(SPOT, ql.Period(3, Y), gearings=[0.5], spreads=[0.002]), (), [0.02]))
    add("3Y cap, gearing -1, spread 6%", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(3, Y), gearings=[-1.0], spreads=[0.06]), [0.03]))
    add("5Y cap, amortizing", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(5, Y), E1Y, ql.Period(1, Y), (5e6, 4e6, 3e6, 2e6, 1e6)), [0.035]))
    add("3Y cap on 3M periods of the 6M index", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(3, Y), E6M, ql.Period(3, M)), [0.03]))
    add("3Y cap, indexed coupons by the switch", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(3, Y), E6M, ql.Period(3, M)), [0.03]), use_at_par_coupons=False)
    add("3Y cap, indexed coupons by the leg", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(3, Y), E6M, ql.Period(3, M), indexed=True), [0.03]))
    seasoned = ibor_leg(d(19, 7, 2023), ql.Period(4, Y))
    add("seasoned cap, past fixings", instrument_spec(CAP, seasoned, [0.03]), fixings=(11, False))
    add("seasoned floor, past fixings", instrument_spec(FLOOR, seasoned, (), [0.03]), fixings=(11, False))
    add("seasoned collar, past fixings", instrument_spec(COLLAR, seasoned, [0.034], [0.028]), fixings=(11, False))
    fixing_today = ibor_leg(SPOT, ql.Period(2, Y))
    add("cap fixing today, none stored", instrument_spec(CAP, fixing_today, [0.025]))
    add("cap fixing today, stored", instrument_spec(CAP, fixing_today, [0.025]), fixings=(12, True))
    paid = ibor_leg(d(17, 1, 2022), ql.Period(2, Y))
    add("cap paid in full", instrument_spec(CAP, paid, [0.03]), fixings=(13, False))
    add("cap on an empty leg", instrument_spec(CAP, None, [0.03], (), "Empty"))
    add("4Y cap at -0.2% on negative rates", instrument_spec(
        CAP, ibor_leg(SPOT, ql.Period(4, Y), E6M_NEGATIVE), [-0.002]))
    add("4Y floor at 0% on negative rates", instrument_spec(
        FLOOR, ibor_leg(SPOT, ql.Period(4, Y), E6M_NEGATIVE), (), [0.0]))
    # its second coupon is paid on 16 January 2025, after the evaluation date and before the curve's reference date
    add("3Y cap on a spot-dated Actual/360 curve", instrument_spec(CAP, ibor_leg(d(16, 1, 2024), ql.Period(3, Y)),
                                                                    [0.03]), SPOT_DISCOUNT, fixings=(14, False))
    add("refused: a cap with no cap rates", instrument_spec(CAP, ibor_leg(SPOT, ql.Period(2, Y)), (), [0.03]))
    add("refused: a floor with no floor rates", instrument_spec(FLOOR, ibor_leg(SPOT, ql.Period(2, Y)), [0.03]))
    add("refused: a collar with no floor rates", instrument_spec(COLLAR, ibor_leg(SPOT, ql.Period(2, Y)), [0.05]))
    fixed = dict(frc.leg_spec(A365, [1e6], [0.03]), schedule=frc.schedule_spec(
        SPOT, CAL.advance(SPOT, 2, Y), ql.Period(6, M), TARGET, ql.ModifiedFollowing, ql.DateGeneration.Forward, False))
    add("refused at pricing: a fixed-rate leg", instrument_spec(CAP, fixed, [0.03], (), "FixedRateLeg"))
    return out


def settings_of(spec, settings):
    """case_settings()'s keywords for an instrument's settings."""
    kw = {}
    if "use_at_par_coupons" in settings:
        kw["use_at_par_coupons"] = settings["use_at_par_coupons"]
    if "fixings" in settings:
        seed, through_today = settings["fixings"]
        kw["fixings"] = past_fixings(spec, v.SEED + seed, through_today)
    return kw


def attempt_scaled(f, scale):
    try:
        x = f()
    except RuntimeError:
        return dict(v.THROWS)
    return v.amount(x, scale)


def instrument_outputs(spec, curve_spec):
    try:
        c = make_instrument(spec)
    except RuntimeError:
        return {"built": dict(v.THROWS)}
    scale = scale_of(spec)
    curve = irx.handle(curve_spec).currentLink()

    def npv(engine):
        c.setPricingEngine(engine)
        return c.NPV()

    return {
        "built": True,
        "type": c.type(),
        "capRates": list(c.capRates()),
        "floorRates": list(c.floorRates()),
        "startDate": v.attempt(c.startDate),
        "maturityDate": v.attempt(c.maturityDate),
        "isExpired": c.isExpired(),
        "atmRate": v.attempt(lambda: c.atmRate(curve)),
        "blackNPV": attempt_scaled(lambda: npv(black_engine(curve_spec, black_spec())), scale),
        "bachelierNPV": attempt_scaled(lambda: npv(bachelier_engine(curve_spec, bachelier_spec())), scale),
    }


# The stability filter: a calibrated output is kept only where every move of its inputs leaves it within this.
STABLE = 1e-12


def stable(base, moved):
    if isinstance(base, dict) or isinstance(moved, dict):
        return base == moved
    return abs(base - moved) <= STABLE * max(abs(base), abs(moved), 1.0)


def moves(inputs):
    """The moves of a case's inputs: every input together one ulp up, then down, and each alone up and down, as lists of
    input values; a case with one input has two moves."""
    out = [[math.nextafter(x, math.inf) for x in inputs], [math.nextafter(x, -math.inf) for x in inputs]]
    for i in range(len(inputs)):
        for direction in (math.inf, -math.inf):
            moved = list(inputs)
            moved[i] = math.nextafter(inputs[i], direction)
            if moved not in out:
                out.append(moved)
    return out


class Dropped:
    """The filter's counts, per case and for the file."""

    def __init__(self):
        self.dropped = 0
        self.outputs = 0

    def case(self, name, dropped, outputs):
        self.dropped += dropped
        self.outputs += outputs
        print(f"{name}: dropped {dropped} of {outputs} outputs")

    def file(self, label):
        print(f"{label}: dropped {self.dropped} of {self.outputs} outputs")


def solves():
    """(name, instrument name, price spec, solve arguments) for the 47 implied volatilities. A price spec is
    ("black", vol, displacement) or ("bachelier", vol), the wheel engine's NPV at that volatility, or ("price", p)."""
    out = []

    def add(name, instrument, price, guess=0.1, accuracy=1e-4, max_evaluations=100, min_vol=1e-7, max_vol=4.0,
            vol_type=SL, displacement=0.0):
        out.append((name, instrument, price, {"guess": guess, "accuracy": accuracy, "maxEvaluations": max_evaluations,
                                              "minVol": min_vol, "maxVol": max_vol, "type": vol_type,
                                              "displacement": displacement}))

    for instrument in ("3Y cap at 3%", "3Y floor at 3%", "3Y collar 3.5% / 2.5%", "10Y cap at 3%",
                       "7Y quarterly cap on 3M", "seasoned cap, past fixings"):
        for vol in (0.05, 0.2, 0.6):
            add(f"{instrument}, Black {vol}", instrument, ("black", vol, 0.0), accuracy=1e-8)
    for guess in (0.01, 0.25, 1.0, 3.5):
        add(f"5Y cap in the money, guess {guess}", "5Y cap in the money", ("black", 0.25, 0.0), guess=guess)
    for accuracy in (1e-4, 1e-6, 1e-10, 1e-12):
        add(f"3Y cap at 3%, accuracy {accuracy}", "3Y cap at 3%", ("black", 0.18, 0.0), accuracy=accuracy)
    for vol in (0.004, 0.008, 0.02):
        add(f"3Y cap at 3%, normal {vol}", "3Y cap at 3%", ("bachelier", vol), guess=0.01, vol_type=N,
            accuracy=1e-10)
        add(f"4Y floor at 0% on negative rates, normal {vol}", "4Y floor at 0% on negative rates",
            ("bachelier", vol), guess=0.005, vol_type=N, accuracy=1e-10)
    add("3Y cap at 3%, normal with a displacement ignored", "3Y cap at 3%", ("bachelier", 0.008), guess=0.01,
        vol_type=N, displacement=0.5, accuracy=1e-10)
    for displacement in (0.01, 0.03):
        add(f"4Y cap at -0.2%, displaced {displacement}", "4Y cap at -0.2% on negative rates",
            ("black", 0.15, displacement), displacement=displacement, accuracy=1e-10)
    add("5Y cap far out of the money, Black 0.3", "5Y cap far out of the money", ("black", 0.3, 0.0), accuracy=1e-8)
    add("5Y floor far out of the money, Black 0.4", "5Y floor far out of the money", ("black", 0.4, 0.0),
        accuracy=1e-8)
    add("4Y collar, several rates, Black 0.22", "4Y collar, several rates", ("black", 0.22, 0.0), accuracy=1e-8)
    add("3Y cap, gearing 2, Black 0.3", "3Y cap, gearing 2, spread 1%", ("black", 0.3, 0.0), accuracy=1e-8)
    add("bracket's upper end", "3Y cap at 3%", ("black", 0.2, 0.0), max_vol=0.2, accuracy=1e-8)
    add("bracket's lower end", "3Y cap at 3%", ("black", 0.2, 0.0), min_vol=0.2, guess=0.3, accuracy=1e-8)
    add("unreachable price", "3Y cap at 3%", ("price", 1e6))
    add("guess above the bracket", "3Y cap at 3%", ("black", 0.2, 0.0), guess=5.0)
    add("empty bracket", "3Y cap at 3%", ("black", 0.2, 0.0), min_vol=0.5, max_vol=0.3, guess=0.4)
    add("too few evaluations", "3Y cap at 3%", ("black", 0.2, 0.0), max_evaluations=2, accuracy=1e-12)
    add("expired", "cap paid in full", ("price", 100.0))
    add("volatility type outside the enum", "3Y cap at 3%", ("black", 0.2, 0.0), vol_type=7)
    return out


def target_price(instrument, curve_spec, price_spec):
    kind = price_spec[0]
    if kind == "price":
        return price_spec[1]
    engine = (black_engine(curve_spec, black_spec(price_spec[1], A365, price_spec[2])) if kind == "black"
              else bachelier_engine(curve_spec, bachelier_spec(price_spec[1])))
    instrument.setPricingEngine(engine)
    return instrument.NPV()


def price_input(price_spec):
    """The price's source as the case records it: {"engine": "black", "vol", "displacement"} or {"engine":
    "bachelier", "vol"}, the wheel engine's NPV, or {"engine": null}, a chosen price."""
    if price_spec[0] == "black":
        return {"engine": "black", "vol": price_spec[1], "displacement": price_spec[2]}
    if price_spec[0] == "bachelier":
        return {"engine": "bachelier", "vol": price_spec[1]}
    return {"engine": None}


def solve_case(dropped, name, row, price_spec, args):
    inst_name, spec, curve_spec, settings = row
    with v.case_settings(TODAY, **settings_of(spec, settings)) as st:
        c = make_instrument(spec)
        price = target_price(c, curve_spec, price_spec)
        handle = irx.handle(curve_spec)

        def solve(p):
            return v.attempt(lambda: c.impliedVolatility(p, handle, args["guess"], args["accuracy"],
                                                         args["maxEvaluations"], args["minVol"], args["maxVol"],
                                                         args["type"], args["displacement"]))

        base = solve(price)
        expected = {"price": v.amount(price, scale_of(spec))}
        kept = all(stable(base, solve(p[0])) for p in moves([price]))
        if kept:
            expected["impliedVolatility"] = base
        dropped.case(f"implied volatility: {name}", 0 if kept else 1, 1)
        return {"name": f"implied volatility: {name}",
                "inputs": {"settings": st, "instrument": inst_name, "priceFrom": price_input(price_spec), "price": price,
                           **args},
                "expected": expected}


def cases():
    out = []
    rows = instruments()
    for name, spec, curve_spec, settings in rows:
        with v.case_settings(TODAY, **settings_of(spec, settings)) as st:
            out.append({"name": name, "inputs": {"settings": st, "capFloor": spec, "discountCurve": curve_spec},
                        "expected": instrument_outputs(spec, curve_spec)})
    by_name = {row[0]: row for row in rows}
    dropped = Dropped()
    for name, instrument, price_spec, args in solves():
        out.append(solve_case(dropped, name, by_name[instrument], price_spec, args))
    dropped.file("instruments/capfloor")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/capfloor.hpp", cases)
