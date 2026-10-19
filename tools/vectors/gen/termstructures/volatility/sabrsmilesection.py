# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's SabrSmileSection (ql/termstructures/volatility/sabrsmilesection.hpp, .cpp), with the
SmileSection interface it inherits (ql/termstructures/volatility/smilesection.hpp): 14 sections, 6 by time, 4 by date
with a reference date and 4 by date without one (QuantLib's floating section, whose reference date is the evaluation
date, which nanoql builds by SabrSmileSection.Create from the context); both volatility types, shifts from 0 to 0.03,
a negative forward under a shift, an exercise time of 0 by time and by date, and five parameters, the fifth ignored.

- "section" cases, one per section: its exerciseDate, referenceDate (refused for a section by time), exerciseTime,
  dayCounter (its name, null for the time form's empty one), volatilityType, shift, minStrike (minus the shift: -0.0
  for none), maxStrike (QL_MAX_REAL), atmLevel (the forward), alpha, beta, nu and rho; at 8 strikes (minus the shift,
  where a shifted lognormal price takes a standard deviation of 0.2 and the volatility is the floor's, then the forward
  plus the shift times 0.25 to 4, less the shift) variance, volatility, the call and put prices and digital prices with
  the case's discount, vega (refused for a normal section), density, and the volatility converted to a shifted
  lognormal one with the case's target shift and to a normal one; and the strike floor, the volatility and variance at
  6 strikes at or below 1e-5 minus the shift, where QuantLib's std::max(0.00001 - shift(), strike) gives the floor's
  volatility, a NaN strike among them.
- "refusal" cases: the sections QuantLib refuses to build: the forward plus the shift not positive, each of
  validateSabrParameters' checks (alpha, beta, nu, rho, a NaN alpha), a negative exercise time, and an exercise date
  before the reference date, by date and floating.

A refused value is {"throws": true}. Each case's input "section" is {"type": "SabrSmileSection", "form" ("date",
"floating" or "time"), "exerciseDate" and "referenceDate", or "exerciseDate", or "exerciseTime", "forward",
"sabrParameters", "dayCounter" (null for the time form), "volatilityType", "shift"}, the day counter as in
tools/vectors/gen/indexes/interestrateindex.py: the spec tools/cxx/src/termstructures/volatility/smilesectionspec.hpp
reads too. Fewer than four parameters, which QuantLib reads past (0xC0000005 for none), and a volatility type outside
the enum, which QuantLib reads as lognormal for the volatility and as normal for the prices, are never cases. The
wheel's SabrSmileSection is the proxy of the C++ class's two constructors (volatilities.i:795-814), positional only.
The section functions here are shared by tools/vectors/gen/cxx/termstructures/volatility/spreadedsmilesection.py.
Read back by tests/NanoQL.Tests/Vectors/TermStructures/SabrSmileSectionVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
NAN = float("nan")


def date_spec(exercise, reference, forward, params, dc, vol_type=SL, shift=0.0):
    return {"type": "SabrSmileSection", "form": "date", "exerciseDate": v.enc(exercise),
            "referenceDate": v.enc(reference), "forward": forward, "sabrParameters": params, "dayCounter": dc,
            "volatilityType": vol_type, "shift": shift}


def floating_spec(exercise, forward, params, dc, vol_type=SL, shift=0.0):
    return {"type": "SabrSmileSection", "form": "floating", "exerciseDate": v.enc(exercise), "forward": forward,
            "sabrParameters": params, "dayCounter": dc, "volatilityType": vol_type, "shift": shift}


def time_spec(time, forward, params, vol_type=SL, shift=0.0):
    return {"type": "SabrSmileSection", "form": "time", "exerciseTime": time, "forward": forward,
            "sabrParameters": params, "dayCounter": None, "volatilityType": vol_type, "shift": shift}


def make_section(spec):
    """The SabrSmileSection a spec describes, built with the wheel under the evaluation date the caller has set."""
    params, rest = spec["sabrParameters"], (spec["shift"], spec["volatilityType"])
    if spec["form"] == "time":
        return ql.SabrSmileSection(spec["exerciseTime"], spec["forward"], params, *rest)
    reference = ql.Date(spec["referenceDate"]) if spec["form"] == "date" else ql.Date()
    return ql.SabrSmileSection(ql.Date(spec["exerciseDate"]), spec["forward"], params, reference,
                               irx.day_counter(spec["dayCounter"]), *rest)


def strikes(spec):
    """8 strikes: minus the shift, then a quarter to four times the forward (plus the shift, then less it, for a
    forward that isn't positive)."""
    f, s = spec["forward"], spec["shift"]
    m = f + s
    return [-s] + [x * m - s for x in (0.25, 0.5, 0.9, 1.0, 1.1, 2.0, 4.0)]


def floor_strikes(spec):
    """6 strikes at or below the floor 1e-5 minus the shift: the floor, below it by 1e-9 and by 0.01, minus the shift,
    a large negative strike and NaN."""
    s = spec["shift"]
    return [0.00001 - s, 0.00001 - s - 1e-9, 0.00001 - s - 0.01, -s, -1.0, NAN]


def day_counter_name(s):
    dc = s.dayCounter()
    return None if dc.empty() else dc.name()


def inspectors(s):
    """The inspectors the section answers without a strike."""
    return {"exerciseDate": v.enc(s.exerciseDate()),
            "referenceDate": v.attempt(lambda: s.referenceDate()),
            "exerciseTime": s.exerciseTime(),
            "dayCounter": day_counter_name(s),
            "volatilityType": int(s.volatilityType()),
            "shift": s.shift(),
            "minStrike": s.minStrike(),
            "maxStrike": s.maxStrike(),
            "atmLevel": v.enc(s.atmLevel())}


def at_strikes(s, ks, discount, target_shift):
    """The members at each strike, as lists in the order of ks."""
    def each(f):
        return [v.attempt(lambda x=x: f(x)) for x in ks]

    return {"variance": each(s.variance),
            "volatility": each(s.volatility),
            "callPrice": each(lambda x: s.optionPrice(x, ql.Option.Call, discount)),
            "putPrice": each(lambda x: s.optionPrice(x, ql.Option.Put, discount)),
            "digitalCallPrice": each(lambda x: s.digitalOptionPrice(x, ql.Option.Call, discount)),
            "digitalPutPrice": each(lambda x: s.digitalOptionPrice(x, ql.Option.Put, discount)),
            "vega": each(lambda x: s.vega(x, discount)),
            "density": each(lambda x: s.density(x, discount)),
            "lognormalVolatility": each(lambda x: s.volatility(x, ql.ShiftedLognormal, target_shift)),
            "normalVolatility": each(lambda x: s.volatility(x, ql.Normal, 0.0))}


E1 = ql.Date(15, ql.January, 2025)
E2 = ql.Date(29, ql.February, 2024)
E3 = ql.Date(30, ql.October, 2026)
# (evaluation date, spec, discount, target shift of the converted lognormal volatility)
SECTIONS = [
    # by time
    (E1, time_spec(1.0, 0.03, [0.03, 0.5, 0.4, 0.0]), 1.0, 0.0),
    (E1, time_spec(5.0, 0.04, [0.05, 0.7, 0.3, -0.25], SL, 0.01), 0.95, 0.01),
    (E1, time_spec(0.5, 0.025, [0.01, 0.0, 0.35, 0.2], N), 1.0, 0.0),
    (E1, time_spec(2.0, -0.002, [0.0462, 0.5, 0.3, -0.2], N, 0.03), 0.97, 0.03),
    (E1, time_spec(0.0, 0.03, [0.03, 0.5, 0.4, 0.0]), 1.0, 0.0),
    (E1, time_spec(1.5, 0.035, [0.04, 0.6, 0.5, -0.4, 0.9]), 1.0, 0.02),
    # by date, with a reference date
    (E1, date_spec(ql.Date(15, ql.January, 2027), E1, 0.03, [0.025, 0.4, 0.45, -0.3], A365, SL, 0.02), 1.0, 0.02),
    (E1, date_spec(ql.Date(15, ql.July, 2025), E1, 0.02, [0.009, 0.0, 0.3, 0.1], A360, N), 0.99, 0.0),
    (E1, date_spec(E1, E1, 0.03, [0.03, 0.5, 0.4, 0.0], A365), 1.0, 0.0),
    (E2, date_spec(ql.Date(1, ql.March, 2034), ql.Date(1, ql.March, 2024), 0.05, [0.12, 0.8, 0.25, 0.5],
                   THIRTY360_BB, SL, 0.0), 0.9, 0.01),
    # by date, floating
    (E1, floating_spec(ql.Date(16, ql.January, 2028), 0.045, [0.06, 0.65, 0.35, 0.15], A360, SL, 0.005), 1.0, 0.0),
    (E3, floating_spec(ql.Date(30, ql.April, 2027), 0.012, [0.0102, 0.25, 0.4, -0.1], A365, N, 0.01), 0.96, 0.01),
    (E2, floating_spec(ql.Date(28, ql.February, 2029), 0.025, [0.18, 0.9, 0.6, -0.6], ACT_ISDA), 1.0, 0.0),
    (E3, floating_spec(E3, 0.03, [0.03, 0.5, 0.4, 0.0], A360, N), 1.0, 0.0),
]
REFUSALS = [
    (E1, time_spec(1.0, -0.01, [0.03, 0.5, 0.4, 0.0], SL, 0.01)),
    (E1, time_spec(1.0, -0.02, [0.03, 0.5, 0.4, 0.0], N, 0.01)),
    (E1, time_spec(1.0, 0.03, [0.0, 0.5, 0.4, 0.0])),
    (E1, time_spec(1.0, 0.03, [NAN, 0.5, 0.4, 0.0])),
    (E1, time_spec(1.0, 0.03, [0.03, 1.1, 0.4, 0.0])),
    (E1, time_spec(1.0, 0.03, [0.03, 0.5, -0.1, 0.0], N)),
    (E1, time_spec(1.0, 0.03, [0.03, 0.5, 0.4, 1.0])),
    (E1, time_spec(-1.0, 0.03, [0.03, 0.5, 0.4, 0.0])),
    (E1, date_spec(ql.Date(14, ql.January, 2025), E1, 0.03, [0.03, 0.5, 0.4, 0.0], A365)),
    (E1, floating_spec(ql.Date(14, ql.January, 2025), 0.03, [0.03, 0.5, 0.4, 0.0], A360)),
    (E1, date_spec(ql.Date(15, ql.January, 2026), E1, 0.0, [0.03, 0.5, 0.4, 0.0], A365)),
]


def form_name(spec):
    return f"{spec['form']} {'Normal' if spec['volatilityType'] == N else 'ShiftedLognormal'} {spec['shift']!r}"


def section_case(k, evaluation_date, spec, discount, target_shift):
    st = v.settings(evaluation_date)
    s = make_section(spec)
    ks, fs = strikes(spec), floor_strikes(spec)
    expected = inspectors(s)
    expected.update({"alpha": s.alpha(), "beta": s.beta(), "nu": s.nu(), "rho": s.rho()})
    expected.update(at_strikes(s, ks, discount, target_shift))
    expected["flooredVolatility"] = [v.attempt(lambda x=x: s.volatility(x)) for x in fs]
    expected["flooredVariance"] = [v.attempt(lambda x=x: s.variance(x)) for x in fs]
    return {"name": f"{form_name(spec)} {evaluation_date.ISO()} / {k}",
            "inputs": {"settings": st, "kind": "section", "section": v.enc(spec), "strikes": ks,
                       "floorStrikes": v.enc(fs), "discount": discount, "targetShift": target_shift},
            "expected": expected}


def refusal_case(k, evaluation_date, spec):
    st = v.settings(evaluation_date)
    return {"name": f"refusal {form_name(spec)} / {k}",
            "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
            "expected": {"section": v.attempt(lambda: make_section(spec).exerciseTime())}}


def cases():
    out = [section_case(k, *row) for k, row in enumerate(SECTIONS)]
    out += [refusal_case(k, *row) for k, row in enumerate(REFUSALS)]
    for c in out[len(SECTIONS):]:
        assert c["expected"]["section"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/sabrsmilesection.hpp", cases)
