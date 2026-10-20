# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year inflation cap and floor (ql/instruments/inflationcapfloor.hpp), through the
wheel's YoYInflationCap, YoYInflationFloor and YoYInflationCollar on yoyInflationLeg's coupons, priced by the
year-on-year inflation cap and floor engines.

- "capfloor" cases: caps, floors and collars on legs of different lengths and strikes, each with each engine: NPV,
  optionletPrices and isExpired. The legs are observed flat, with no fixing days, on regular schedules, and every
  coupon fixes after the volatility's base date: there QuantLib's engines' forward, the index's curve at the fixing
  date, is the coupon's own fixing, which nanoql's engines read (tests/NanoQL.Tests/Divergences/
  YoYCapFloorEngineFixingTests.cs).
- "expired" cases: a cap whose every coupon has been paid: NPV and isExpired.
- "construction" cases: a cap with no cap rates, a floor with no floor rates: refused.

A refused value is {"throws": true}. A case's inputs hold its settings, the index spec and fixings
(tools/vectors/gen/cashflows/indexedcashflow.py's), the nominal curve, the instrument's {"type", "leg", "capRates",
"floorRates"} (QuantLib's integer, a leg spec of tools/vectors/gen/cashflows/yoyinflationcoupon.py), and the engine's
{"kind", "volatility"} ("Black", "UnitDisplacedBlack" or "Bachelier", a ConstantYoYOptionletVolatility spec). The
functions here are shared by tools/vectors/gen/pricingengines/inflation/inflationcapfloorengines.py, which imports
this file. Read back by tests/NanoQL.Tests/Vectors/Instruments/InflationCapFloorVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, *path):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *path))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


icf = _load("wheel_indexedcashflow", "cashflows", "indexedcashflow.py")
yoy = _load("wheel_yoyinflationcoupon", "cashflows", "yoyinflationcoupon.py")
vol = _load("wheel_yoyvol", "termstructures", "volatility", "inflation", "yoyinflationoptionletvolatilitystructure.py")
cf = icf.cf

TODAY = icf.TODAY
UK = cf.UK_SETTLEMENT
M = int(ql.Months)
CAP, FLOOR, COLLAR = int(ql.YoYInflationCapFloor.Cap), int(ql.YoYInflationCapFloor.Floor), int(
    ql.YoYInflationCapFloor.Collar)
ENGINES = {"Black": ql.YoYInflationBlackCapFloorEngine,
           "UnitDisplacedBlack": ql.YoYInflationUnitDisplacedBlackCapFloorEngine,
           "Bachelier": ql.YoYInflationBachelierCapFloorEngine}
INDEX = icf.YYUKRPI_RATIO


def leg(years, start=TODAY, notional=1e6, dc=cf.A365, gearings=(), spreads=()):
    schedule = cf.schedule_spec(start, start + ql.Period(years, ql.Years), ql.Period(1, ql.Years), UK, ql.Unadjusted,
                                ql.DateGeneration.Forward, False)
    return yoy.leg_spec(schedule, UK, ql.Period(2, ql.Months), ql.CPI.Flat, [notional], dc, ql.ModifiedFollowing, (0,),
                        gearings, spreads)


def instrument_spec(t, leg_spec, caps=(), floors=()):
    return {"type": t, "leg": leg_spec, "capRates": list(caps), "floorRates": list(floors)}


def engine_spec(kind, vol_spec):
    return {"kind": kind, "volatility": vol_spec}


VOLS = {"Black": vol.surface_spec(0.12, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly, False),
        "UnitDisplacedBlack": vol.surface_spec(0.01, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly,
                                               False),
        "Bachelier": vol.surface_spec(0.008, 0, UK, ql.ModifiedFollowing, cf.A365, [2, M], ql.Monthly, False)}


def make_instrument(spec, index):
    legs = yoy.make_leg(spec["leg"], index)
    if spec["type"] == CAP:
        return ql.YoYInflationCap(legs, spec["capRates"])
    if spec["type"] == FLOOR:
        return ql.YoYInflationFloor(legs, spec["floorRates"])
    return ql.YoYInflationCollar(legs, spec["capRates"], spec["floorRates"])


def make_engine(spec, index):
    surface = ql.YoYOptionletVolatilitySurfaceHandle(vol.make_surface(spec["volatility"]))
    return ENGINES[spec["kind"]](index, surface, icf.nominal_curve(icf.NOMINAL))


def priced(instrument_spec_, engine_spec_, label):
    """A case pricing an instrument with an engine: NPV, optionletPrices and isExpired."""
    with v.case_settings(TODAY) as st:
        index = icf.index_with_fixings(INDEX, icf.UK_FIXINGS)
        instrument = make_instrument(instrument_spec_, index)
        instrument.setPricingEngine(make_engine(engine_spec_, index))
        return {"name": label,
                "inputs": {"settings": st, "kind": "capfloor", "index": INDEX, "fixings": icf.UK_FIXINGS,
                           "nominalCurve": icf.NOMINAL, "instrument": instrument_spec_, "engine": engine_spec_},
                "expected": {"NPV": v.attempt(instrument.NPV), "optionletPrices": list(instrument.optionletPrices()),
                             "isExpired": instrument.isExpired()}}


INSTRUMENTS = [
    ("cap, 1 year", instrument_spec(CAP, leg(1), caps=[0.031])),
    ("cap, 5 years", instrument_spec(CAP, leg(5), caps=[0.03])),
    ("floor, 5 years", instrument_spec(FLOOR, leg(5), floors=[0.029])),
    ("collar, 7 years", instrument_spec(COLLAR, leg(7), caps=[0.033], floors=[0.027])),
    ("cap, varying strikes", instrument_spec(CAP, leg(4), caps=[0.025, 0.03, 0.035])),
    ("floor, geared and spread", instrument_spec(FLOOR, leg(5, gearings=(1.2,), spreads=(0.001,)), floors=[0.03])),
    ("collar, Thirty/360, two million", instrument_spec(COLLAR, leg(3, notional=2e6, dc=cf.THIRTY360_BB),
                                                         caps=[0.035], floors=[0.025])),
]


def capfloor_cases():
    out = []
    k = 0
    for label, spec in INSTRUMENTS:
        for kind, vol_spec in VOLS.items():
            out.append(priced(spec, engine_spec(kind, vol_spec), f"capfloor {label}, {kind} / {k}"))
            k += 1
    return out


def expired_cases():
    out = []
    spec = instrument_spec(CAP, leg(2, start=ql.Date(15, ql.March, 2021)), caps=[0.02])
    with v.case_settings(TODAY) as st:
        index = icf.index_with_fixings(INDEX, icf.UK_FIXINGS)
        instrument = make_instrument(spec, index)
        engine = engine_spec("Black", VOLS["Black"])
        instrument.setPricingEngine(make_engine(engine, index))
        out.append({"name": "expired cap / 0",
                    "inputs": {"settings": st, "kind": "expired", "index": INDEX, "fixings": icf.UK_FIXINGS,
                               "nominalCurve": icf.NOMINAL, "instrument": spec, "engine": engine},
                    "expected": {"NPV": v.attempt(instrument.NPV), "isExpired": instrument.isExpired()}})
    return out


def construction_cases():
    out = []
    for k, (label, spec) in enumerate((("cap with no cap rates", instrument_spec(CAP, leg(2))),
                                       ("floor with no floor rates", instrument_spec(FLOOR, leg(2))))):
        with v.case_settings(TODAY) as st:
            index = icf.index_with_fixings(INDEX, icf.UK_FIXINGS)
            out.append({"name": f"construction {label} / {k}",
                        "inputs": {"settings": st, "kind": "construction", "index": INDEX, "fixings": icf.UK_FIXINGS,
                                   "nominalCurve": icf.NOMINAL, "instrument": spec},
                        "expected": {"construction": v.attempt(lambda: make_instrument(spec, index) and 0)}})
    return out


def make_cases():
    return capfloor_cases() + expired_cases() + construction_cases()


if __name__ == "__main__":
    v.main(__file__, "ql/instruments/inflationcapfloor.hpp", make_cases)
