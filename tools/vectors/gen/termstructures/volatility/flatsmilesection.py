# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FlatSmileSection (ql/termstructures/volatility/flatsmilesection.hpp), through the
SmileSection interface it inherits (ql/termstructures/volatility/smilesection.hpp): 40 sections, 14 by date with a
reference date, 8 by date without one (QuantLib's floating section, whose reference date is the evaluation date, which
nanoql builds by FlatSmileSection.Create from the context) and 18 by time; both volatility types, shifts from 0 to
0.05, with and without an ATM level, over four day counters, an exercise time of 0 among them.

- "section" cases, one per section: its exerciseDate, referenceDate (refused for a section by time), exerciseTime,
  dayCounter (its name), volatilityType, shift, minStrike, maxStrike and atmLevel (null for none); and at 10 strikes
  around the ATM level (minus the shift, where a shifted lognormal price takes a standard deviation of 0.2, a strike
  below it, which Black's formula refuses, and 0 among them), variance, volatility, the call and put prices and
  digital prices with the case's discount, vega, density, and the volatility converted to a shifted lognormal one with
  the case's target shift and to a normal one. Without an ATM level the prices, vega and converted volatilities are
  refused ("smile section must provide atm level ..."), as is a normal section's vega.
- "refusals" cases: the sections QuantLib refuses to build, an exercise date before the reference date (by date and
  floating) and a negative or NaN exercise time.

A refused value is {"throws": true}. Each case's input "section" is the spec tools/cxx/src/termstructures/volatility/
smilesectionspec.hpp reads: {"type": "FlatSmileSection", "form" ("date", "floating" or "time"), "exerciseDate" and
"referenceDate", or "exerciseDate", or "exerciseTime", "volatility", "dayCounter", "atmLevel", "volatilityType",
"shift"}, the day counter as in tools/vectors/gen/indexes/interestrateindex.py, the ATM level null for none. The
wheel's FlatSmileSection is the proxy of the C++ class's constructors (volatilities.i), positional only, so a type or
shift without an ATM level passes QuantLib's Null<Real>() for it, as the default does. The section functions here are
shared by the harness generators of cxx/termstructures/volatility/, which import this file. The case's "skip" input lists the strikes whose
normal volatility QuantLib's master's Bachelier inversion moves (#2924, taken ahead of the pin as the upstream sweep's
SWD14 and SWI3 rule; tools/vectors/gen/pricingengines/blackformula.py's smile_normal_moved): QuantLib 1.44's 0, null in
"normalVolatility", which the reader nulls too. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/FlatSmileSectionVectorTests.cs."""
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
_spec = importlib.util.spec_from_file_location("wheel_blackformula",
                                               os.path.join(v.GEN_ROOT, "pricingengines", "blackformula.py"))
bf = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(bf)

A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def date_spec(exercise, reference, vol, dc, atm=None, vol_type=SL, shift=0.0):
    return {"type": "FlatSmileSection", "form": "date", "exerciseDate": v.enc(exercise),
            "referenceDate": v.enc(reference), "volatility": vol, "dayCounter": dc, "atmLevel": atm,
            "volatilityType": vol_type, "shift": shift}


def floating_spec(exercise, vol, dc, atm=None, vol_type=SL, shift=0.0):
    return {"type": "FlatSmileSection", "form": "floating", "exerciseDate": v.enc(exercise), "volatility": vol,
            "dayCounter": dc, "atmLevel": atm, "volatilityType": vol_type, "shift": shift}


def time_spec(time, vol, dc, atm=None, vol_type=SL, shift=0.0):
    return {"type": "FlatSmileSection", "form": "time", "exerciseTime": time, "volatility": vol, "dayCounter": dc,
            "atmLevel": atm, "volatilityType": vol_type, "shift": shift}


def make_section(spec):
    """The FlatSmileSection a spec describes, built with the wheel under the evaluation date the caller has set."""
    dc = irx.day_counter(spec["dayCounter"])
    atm = v.NULL_REAL if spec["atmLevel"] is None else spec["atmLevel"]
    rest = (atm, spec["volatilityType"], spec["shift"])
    if spec["form"] == "time":
        return ql.FlatSmileSection(spec["exerciseTime"], spec["volatility"], dc, *rest)
    reference = ql.Date(spec["referenceDate"]) if spec["form"] == "date" else ql.Date()
    return ql.FlatSmileSection(ql.Date(spec["exerciseDate"]), spec["volatility"], dc, reference, *rest)


def strikes(spec):
    """10 strikes around the ATM level (0.03 where the section has none): for a shifted lognormal section, minus the
    shift and a strike below it; for a normal one, two negative strikes."""
    f = 0.03 if spec["atmLevel"] is None else spec["atmLevel"]
    s = spec["shift"]
    low = [-s, -s - 0.005] if spec["volatilityType"] == SL else [-0.02, -0.005]
    return low + [0.0, 0.25 * f, 0.5 * f, 0.9 * f, f, 1.1 * f, 2.0 * f, 4.0 * f]


E1 = ql.Date(15, ql.January, 2025)
E2 = ql.Date(29, ql.February, 2024)
E3 = ql.Date(30, ql.October, 2026)
# (evaluation date, spec, discount, target shift of the converted lognormal volatility)
SECTIONS = [
    # by date, with a reference date
    (E1, date_spec(ql.Date(15, ql.January, 2026), E1, 0.2, A365, 0.03), 1.0, 0.0),
    (E1, date_spec(ql.Date(15, ql.July, 2025), E1, 0.35, A360, 0.025, SL, 0.01), 0.97, 0.01),
    (E1, date_spec(ql.Date(16, ql.January, 2030), E1, 0.15, ACT_ISDA, 0.04, SL, 0.02), 1.0, 0.03),
    (E1, date_spec(ql.Date(15, ql.January, 2025), E1, 0.2, A365, 0.03), 1.0, 0.0),
    (E2, date_spec(ql.Date(28, ql.February, 2025), E2, 0.008, A365, 0.03, N, 0.0), 1.0, 0.0),
    (E2, date_spec(ql.Date(31, ql.May, 2024), E2, 0.012, THIRTY360_BB, 0.02, N, 0.01), 0.95, 0.02),
    (E2, date_spec(ql.Date(1, ql.March, 2034), ql.Date(1, ql.March, 2024), 0.005, ACT_ISDA, 0.01, N, 0.0), 1.0,
     0.03),
    (E1, date_spec(ql.Date(15, ql.January, 2027), E1, 0.25, A365), 1.0, 0.0),
    (E1, date_spec(ql.Date(15, ql.January, 2027), E1, 0.01, A365, None, N), 1.0, 0.0),
    (E3, date_spec(ql.Date(31, ql.October, 2027), ql.Date(31, ql.October, 2026), 0.45, THIRTY360_BB, 0.05, SL,
                   0.05), 0.9, 0.0),
    (E3, date_spec(ql.Date(30, ql.April, 2027), E3, 0.6, A360, 0.001, SL, 0.0), 1.0, 0.0),
    (E3, date_spec(ql.Date(30, ql.October, 2036), E3, 0.0, A365, 0.03, SL, 0.0), 1.0, 0.0),
    (E3, date_spec(ql.Date(2, ql.November, 2026), E3, 0.0095, A360, -0.004, N, 0.0), 1.0, 0.01),
    (E3, date_spec(ql.Date(30, ql.October, 2028), E3, 0.3, ACT_ISDA, None, SL, 0.03), 1.0, 0.03),
    # by date, floating
    (E1, floating_spec(ql.Date(15, ql.January, 2026), 0.2, A365, 0.03), 1.0, 0.0),
    (E1, floating_spec(ql.Date(16, ql.January, 2028), 0.3, A360, 0.02, SL, 0.015), 0.96, 0.0),
    (E1, floating_spec(ql.Date(15, ql.January, 2025), 0.25, A365, 0.035), 1.0, 0.01),
    (E2, floating_spec(ql.Date(28, ql.February, 2026), 0.007, ACT_ISDA, 0.025, N, 0.0), 1.0, 0.0),
    (E2, floating_spec(ql.Date(29, ql.August, 2024), 0.011, THIRTY360_BB, 0.0, N, 0.02), 1.0, 0.02),
    (E3, floating_spec(ql.Date(30, ql.October, 2031), 0.18, A365, None, SL, 0.0), 1.0, 0.0),
    (E3, floating_spec(ql.Date(30, ql.January, 2027), 0.4, A360, 0.01, SL, 0.03), 0.99, 0.03),
    (E3, floating_spec(ql.Date(30, ql.October, 2027), 0.006, A365, None, N, 0.0), 1.0, 0.0),
    # by time
    (E1, time_spec(1.0, 0.2, A365, 0.03), 1.0, 0.0),
    (E1, time_spec(0.25, 0.5, A360, 0.02, SL, 0.01), 1.0, 0.0),
    (E1, time_spec(5.0, 0.15, ACT_ISDA, 0.04, SL, 0.02), 0.85, 0.02),
    (E1, time_spec(30.0, 0.1, THIRTY360_BB, 0.05), 1.0, 0.01),
    (E1, time_spec(0.0, 0.2, A365, 0.03), 1.0, 0.0),
    (E1, time_spec(1e-6, 0.3, A365, 0.03), 1.0, 0.0),
    (E1, time_spec(2.0, 0.009, A365, 0.03, N), 1.0, 0.0),
    (E1, time_spec(0.5, 0.004, A360, -0.002, N, 0.01), 0.98, 0.01),
    (E1, time_spec(10.0, 0.012, ACT_ISDA, 0.035, N, 0.0), 1.0, 0.04),
    (E1, time_spec(1.0, 0.2, A365), 1.0, 0.0),
    (E1, time_spec(1.0, 0.008, A365, None, N, 0.0), 1.0, 0.0),
    (E1, time_spec(3.0, 0.25, A365, 0.03, SL, 0.05), 1.0, 0.05),
    (E2, time_spec(0.75, 1.2, A360, 0.025), 1.0, 0.0),
    (E2, time_spec(7.5, 0.05, THIRTY360_BB, 0.06, SL, 0.0), 1.0, 0.0),
    (E2, time_spec(1.5, 0.0, A365, 0.02, N, 0.0), 1.0, 0.0),
    (E3, time_spec(4.0, 0.22, ACT_ISDA, 0.0, SL, 0.02), 1.0, 0.02),
    (E3, time_spec(20.0, 0.0065, A365, 0.015, N, 0.0), 0.7, 0.0),
    (E3, time_spec(0.1, 0.33, A360, 0.045, SL, 0.0), 1.0, 0.025),
]
REFUSALS = [
    (E1, date_spec(ql.Date(14, ql.January, 2025), E1, 0.2, A365, 0.03)),
    (E1, date_spec(ql.Date(15, ql.January, 2024), ql.Date(15, ql.January, 2025), 0.2, A365, 0.03, SL, 0.01)),
    (E2, floating_spec(ql.Date(28, ql.February, 2024), 0.01, A360, 0.03, N)),
    (E1, time_spec(-0.25, 0.2, A365, 0.03)),
    (E1, time_spec(-1e-12, 0.2, A365)),
    (E1, time_spec(float("nan"), 0.2, A365, 0.03, N)),
]


def section_case(k, evaluation_date, spec, discount, target_shift):
    st = v.settings(evaluation_date)
    s = make_section(spec)
    ks = strikes(spec)

    def at_strikes(f):
        return [v.attempt(lambda x=x: f(x)) for x in ks]

    expected = {
        "exerciseDate": v.enc(s.exerciseDate()),
        "referenceDate": v.attempt(lambda: s.referenceDate()),
        "exerciseTime": s.exerciseTime(),
        "dayCounter": s.dayCounter().name(),
        "volatilityType": int(s.volatilityType()),
        "shift": s.shift(),
        "minStrike": s.minStrike(),
        "maxStrike": s.maxStrike(),
        "atmLevel": v.enc(s.atmLevel()),
        "variance": at_strikes(s.variance),
        "volatility": at_strikes(s.volatility),
        "callPrice": at_strikes(lambda x: s.optionPrice(x, ql.Option.Call, discount)),
        "putPrice": at_strikes(lambda x: s.optionPrice(x, ql.Option.Put, discount)),
        "digitalCallPrice": at_strikes(lambda x: s.digitalOptionPrice(x, ql.Option.Call, discount)),
        "digitalPutPrice": at_strikes(lambda x: s.digitalOptionPrice(x, ql.Option.Put, discount)),
        "vega": at_strikes(lambda x: s.vega(x, discount)),
        "density": at_strikes(lambda x: s.density(x, discount)),
        "lognormalVolatility": at_strikes(lambda x: s.volatility(x, ql.ShiftedLognormal, target_shift)),
        "normalVolatility": at_strikes(lambda x: s.volatility(x, ql.Normal, 0.0))}
    skip = [j for j, x in enumerate(ks) if bf.smile_normal_moved(s, x)]
    for j in skip:
        expected["normalVolatility"][j] = None
    return {"name": f"{spec['form']} {evaluation_date.ISO()} "
                    f"{'Normal' if spec['volatilityType'] == N else 'ShiftedLognormal'} {spec['shift']!r} / {k}",
            "inputs": {"settings": st, "kind": "section", "section": v.enc(spec), "strikes": ks,
                       "discount": discount, "targetShift": target_shift, "skip": skip},
            "expected": expected}


def refusal_case(k, evaluation_date, spec):
    st = v.settings(evaluation_date)
    return {"name": f"refusal {spec['form']} {evaluation_date.ISO()} / {k}",
            "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
            "expected": {"section": v.attempt(lambda: make_section(spec).exerciseTime())}}


def cases():
    out = [section_case(k, *row) for k, row in enumerate(SECTIONS)]
    out += [refusal_case(k, *row) for k, row in enumerate(REFUSALS)]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/flatsmilesection.hpp", cases)
