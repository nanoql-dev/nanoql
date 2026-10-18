# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for OptionletVolatilityStructure (ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp)
on QuantLib's ConstantOptionletVolatility, each output computed by the C++ harness.

- "periods" cases, one per surface of tools/vectors/gen/termstructures/volatility/optionlet/constantoptionletvol.py,
  whose functions build them: volatility and blackVariance on an option tenor, which the wheel doesn't expose, at 10
  tenors (0D to 30Y, and -1D, whose date is before the reference date) x 2 strikes, as [tenor][strike].
- "refusals" cases, on a fixed and a moving surface, each as built and with enableExtrapolation() called on it:
  volatility and blackVariance on a date (1 and 365 days before the reference date, which QuantLib refuses whatever
  the extrapolation; Date::maxDate, the last date in range) and on a time (-0.25 and NaN, which it refuses, the maximum
  time plus 1e-6 and plus 1, which it refuses unless extrapolating, and +Infinity, which QuantLib's close_enough
  accepts as the maximum time), each without and with the extrapolate argument, as [date or time][extrapolate]; and
  both at a date and at a time in range for 5 strikes (NaN and both infinities, outside the domain
  [QL_MIN_REAL, QL_MAX_REAL], which QuantLib refuses unless extrapolating, and the domain's ends), as
  [strike][extrapolate].
- "defaults" cases, on two of the harness's own structures, which override only what OptionletVolatilityStructure
  leaves abstract: volatilityType and displacement, the base's defaults (ShiftedLognormal and 0), and volatility and
  blackVariance on the same tenors and 2 strikes, through the base's default volatilityImpl on a date.

A refused value is {"throws": true}. Each case's inputs hold the surface spec ("surface", as in that generator), and
the tenors (each [length, units]) and strikes, or the dates, times, strikes, the in-range date, time and strike, and
the extrapolate arguments. The reference date and the maximum time that place the refusals' inputs are read from the
wheel's structure, built under the same evaluation date; every output is the harness's.
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/OptionletVolatilityStructureCxxVectorTests.cs reads it back."""
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
    "wheel_constantoptionletvol",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "optionlet", "constantoptionletvol.py"))
cov = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cov)

INF = float("inf")
NAN = float("nan")
MAX = 1.7976931348623157e308
D, W, M, Y = int(ql.Days), int(ql.Weeks), int(ql.Months), int(ql.Years)
TENORS = [[0, D], [1, D], [1, W], [1, M], [3, M], [6, M], [1, Y], [5, Y], [30, Y], [-1, D]]
# The harness's own structures, {"type": "BaseOptionletVolatility", "referenceDate", "calendar", "convention",
# "volatility", "dayCounter", "enableExtrapolation"}, and their strikes.
BASE_SURFACES = [
    (ql.Date(15, ql.January, 2025),
     {"type": "BaseOptionletVolatility", "referenceDate": v.enc(ql.Date(15, ql.January, 2025)), "calendar": cov.TARGET,
      "convention": int(ql.Following), "volatility": 0.2, "dayCounter": cov.A365, "enableExtrapolation": False},
     [0.01, 0.03]),
    (ql.Date(29, ql.February, 2024),
     {"type": "BaseOptionletVolatility", "referenceDate": v.enc(ql.Date(29, ql.February, 2024)),
      "calendar": cov.UK_EXCHANGE, "convention": int(ql.ModifiedFollowing), "volatility": 0.0125,
      "dayCounter": cov.ACT_ISDA, "enableExtrapolation": True},
     [-0.01, 0.02]),
]
# The surfaces the refusals use: a fixed one and a moving one.
REFUSAL_SURFACES = [1, 8]
EXTRAPOLATE = [False, True]
REFUSED_STRIKES = [NAN, INF, -INF, -MAX, MAX]

VOLATILITY = "OptionletVolatilityStructure::volatility"
BLACK_VARIANCE = "OptionletVolatilityStructure::blackVariance"


def period_case(H, k, evaluation_date, spec, strikes):
    with H.case_settings(v.enc(evaluation_date)) as st:
        ks = strikes[:2]
        expected = {}
        for output, method in (("volatility", VOLATILITY), ("blackVariance", BLACK_VARIANCE)):
            expected[output] = [[v.attempt(lambda p=p, x=x: H.call(method + "(Period)", spec, p, x, False))
                                 for x in ks] for p in TENORS]
        kind = "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"
        return {"name": f"periods {kind} {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "periods", "surface": spec, "tenors": TENORS, "strikes": ks},
                "expected": expected}


def refusal_case(H, k, evaluation_date, spec, strikes):
    with v.case_settings(evaluation_date):
        surface = cov.make_surface(spec)
        ref, max_time = surface.referenceDate(), surface.maxTime()
    with H.case_settings(v.enc(evaluation_date)) as st:
        dates = [v.enc(ref - 1), v.enc(ref - 365), v.enc(ql.Date.maxDate())]
        times = [-0.25, NAN, max_time + 1e-6, max_time + 1.0, INF]
        strike, date, time = strikes[1], v.enc(ref + 365), 1.0
        expected = {}
        for output, method in (("volatility", VOLATILITY), ("blackVariance", BLACK_VARIANCE)):
            expected[output + "AtDates"] = [
                [v.attempt(lambda d=d, e=e: H.call(method + "(Date)", spec, d, strike, e)) for e in EXTRAPOLATE]
                for d in dates]
            expected[output + "AtTimes"] = [
                [v.attempt(lambda t=t, e=e: H.call(method + "(Time)", spec, t, strike, e)) for e in EXTRAPOLATE]
                for t in times]
            expected[output + "AtStrikesOnDate"] = [
                [v.attempt(lambda x=x, e=e: H.call(method + "(Date)", spec, date, x, e)) for e in EXTRAPOLATE]
                for x in REFUSED_STRIKES]
            expected[output + "AtStrikesOnTime"] = [
                [v.attempt(lambda x=x, e=e: H.call(method + "(Time)", spec, time, x, e)) for e in EXTRAPOLATE]
                for x in REFUSED_STRIKES]
        kind = "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"
        extrapolating = " extrapolating" if spec["enableExtrapolation"] else ""
        return {"name": f"refusals {kind}{extrapolating} {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "refusals", "surface": spec, "dates": dates, "times": v.enc(times),
                           "strikes": v.enc(REFUSED_STRIKES), "strike": strike, "date": date, "time": time,
                           "extrapolate": EXTRAPOLATE},
                "expected": expected}


def defaults_case(H, k, evaluation_date, spec, strikes):
    with H.case_settings(v.enc(evaluation_date)) as st:
        expected = {"volatilityType": H.call("OptionletVolatilityStructure::volatilityType", spec),
                    "displacement": H.call("OptionletVolatilityStructure::displacement", spec)}
        for output, method in (("volatility", VOLATILITY), ("blackVariance", BLACK_VARIANCE)):
            expected[output] = [[v.attempt(lambda p=p, x=x: H.call(method + "(Period)", spec, p, x, False))
                                 for x in strikes] for p in TENORS]
        return {"name": f"defaults {evaluation_date.ISO()} / {k}",
                "inputs": {"settings": st, "kind": "defaults", "surface": spec, "tenors": TENORS, "strikes": strikes},
                "expected": expected}


def cases():
    with cxx.connect() as H:
        out = [period_case(H, k, ev, spec, strikes) for k, (ev, spec, strikes) in enumerate(cov.SURFACES)]
        for k in REFUSAL_SURFACES:
            ev, spec, strikes = cov.SURFACES[k]
            for enabled in (False, True):
                out.append(refusal_case(H, k, ev, dict(spec, enableExtrapolation=enabled), strikes))
        out += [defaults_case(H, k, ev, spec, strikes) for k, (ev, spec, strikes) in enumerate(BASE_SURFACES)]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/optionletvolatilitystructure.hpp", cases)
