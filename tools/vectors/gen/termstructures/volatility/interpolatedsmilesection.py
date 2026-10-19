# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's InterpolatedSmileSection (ql/termstructures/volatility/interpolatedsmilesection.hpp)
through the wheel's four instantiations, LinearInterpolatedSmileSection, CubicInterpolatedSmileSection,
MonotonicCubicInterpolatedSmileSection and SplineCubicInterpolatedSmileSection, with the SmileSection interface they
inherit (ql/termstructures/volatility/smilesection.hpp): each class by time, by date with a reference date, and by date
without one (QuantLib's floating section, whose reference date is the evaluation date, which nanoql builds by the
classes' Create from the context), each with and without flat strike extrapolation; then Cubic with two interpolators
of its own (Fritsch-Butland's, and a spline with first-derivative ends), normal and shifted sections, more standard
deviations than strikes (the extra ones ignored), a smile whose extrapolation goes negative (floored at 0) and an
exercise time of 0 (NaN volatilities, the standard deviations over a zero root).

- "section" cases, one per section: its exerciseDate, referenceDate (refused for a section by time), exerciseTime,
  dayCounter (its name), volatilityType, shift, minStrike, maxStrike and atmLevel; and at 10 strikes, inside the
  strikes, at them and outside them on both sides, variance, volatility and the call and put prices with the case's
  discount.
- "refusal" cases: the sections QuantLib refuses to build: unsorted strikes, a single strike (fewer than an
  interpolation takes), a negative exercise time, and an exercise date before the reference date, by date and
  floating.

A refused value is {"throws": true}. Each case's input "section" is {"type" (the class), "form" ("date", "floating"
or "time"), "exerciseDate" and "referenceDate", or "exerciseDate", or "exerciseTime", "strikes", "stdDevs",
"atmLevel", "dayCounter", "volatilityType", "shift", "flatStrikeExtrapolation", "cubic"}, the day counter as in
tools/vectors/gen/indexes/interestrateindex.py, and "cubic" CubicInterpolatedSmileSection's interpolator,
{"derivativeApprox", "monotonic", "leftCondition", "leftConditionValue", "rightCondition", "rightConditionValue"}
as QuantLib's enums and values, or null for the default Cubic() (and for the other classes). More strikes than
standard deviations, which QuantLib's interpolation reads past, and a volatility type outside the enum are never
cases. The wheel's classes are SWIG's %template instantiations of the C++ class (volatilities.i:1032-1087), over
Linear, Cubic and the SWIG-side MonotonicCubic and SplineCubic, Cubic(Spline, true or false, SecondDerivative, 0,
SecondDerivative, 0) (interpolation.i:144-158), positional only; they take the value forms, standard deviations and
an ATM level as numbers. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/InterpolatedSmileSectionVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_interestrateindex",
                                               os.path.join(v.GEN_ROOT, "indexes", "interestrateindex.py"))
irx = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(irx)

A360, A365, THIRTY360_BB = irx.A360, irx.A365, irx.THIRTY360_BB
ACT_ISDA = {"type": "ActualActual", "convention": int(ql.ActualActual.ISDA)}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
CLASSES = ["LinearInterpolatedSmileSection", "CubicInterpolatedSmileSection", "MonotonicCubicInterpolatedSmileSection",
           "SplineCubicInterpolatedSmileSection"]
INTERPOLATORS = {"LinearInterpolatedSmileSection": ql.Linear,
                 "MonotonicCubicInterpolatedSmileSection": ql.MonotonicCubic,
                 "SplineCubicInterpolatedSmileSection": ql.SplineCubic}
FRITSCH_BUTLAND = {"derivativeApprox": int(ql.CubicInterpolation.FritschButland), "monotonic": False,
                   "leftCondition": int(ql.CubicInterpolation.SecondDerivative), "leftConditionValue": 0.0,
                   "rightCondition": int(ql.CubicInterpolation.SecondDerivative), "rightConditionValue": 0.0}
CLAMPED_SPLINE = {"derivativeApprox": int(ql.CubicInterpolation.Spline), "monotonic": False,
                  "leftCondition": int(ql.CubicInterpolation.FirstDerivative), "leftConditionValue": -2.0,
                  "rightCondition": int(ql.CubicInterpolation.FirstDerivative), "rightConditionValue": 1.5}

STRIKES = [0.01, 0.02, 0.03, 0.04, 0.05]
VOLS = [0.3, 0.25, 0.2, 0.22, 0.26]


def std_devs(vols, t):
    return [x * math.sqrt(t) for x in vols]


def section(cls, form, when, strikes, sds, atm, dc, vol_type=SL, shift=0.0, flat=False, reference=None, cubic=None):
    spec = {"type": cls, "form": form}
    if form == "time":
        spec["exerciseTime"] = when
    else:
        spec["exerciseDate"] = v.enc(when)
        if form == "date":
            spec["referenceDate"] = v.enc(reference)
    spec.update({"strikes": strikes, "stdDevs": sds, "atmLevel": atm, "dayCounter": dc, "volatilityType": vol_type,
                 "shift": shift, "flatStrikeExtrapolation": flat, "cubic": cubic})
    return spec


def interpolator(spec):
    if spec["type"] == "CubicInterpolatedSmileSection":
        c = spec["cubic"]
        if c is None:
            return ql.Cubic()
        return ql.Cubic(c["derivativeApprox"], c["monotonic"], c["leftCondition"], c["leftConditionValue"],
                        c["rightCondition"], c["rightConditionValue"])
    return INTERPOLATORS[spec["type"]]()


def make_section(spec):
    """The section a spec describes, built with the wheel under the evaluation date the caller has set."""
    cls = getattr(ql, spec["type"])
    dc = irx.day_counter(spec["dayCounter"])
    rest = (spec["volatilityType"], spec["shift"], spec["flatStrikeExtrapolation"])
    head = (spec["strikes"], spec["stdDevs"], spec["atmLevel"])
    if spec["form"] == "time":
        return cls(spec["exerciseTime"], *head, interpolator(spec), dc, *rest)
    reference = ql.Date(spec["referenceDate"]) if spec["form"] == "date" else ql.Date()
    return cls(ql.Date(spec["exerciseDate"]), *head, dc, interpolator(spec), reference, *rest)


def strikes(spec):
    """10 strikes: two below the first strike, the first, between the nodes and at one, the last, and two above."""
    ks = spec["strikes"]
    lo, hi = ks[0], ks[-1]
    span = hi - lo
    return [lo - 0.5 * span, lo - 0.1 * span, lo, lo + 0.15 * span, ks[len(ks) // 2], lo + 0.55 * span,
            lo + 0.9 * span, hi, hi + 0.1 * span, hi + span]


E1 = ql.Date(15, ql.January, 2025)
E2 = ql.Date(29, ql.February, 2024)
E3 = ql.Date(30, ql.October, 2026)


def sections():
    """(evaluation date, spec, discount)."""
    out = []
    for i, cls in enumerate(CLASSES):
        dc = [A365, A360, ACT_ISDA, THIRTY360_BB][i]
        for flat in (False, True):
            out.append((E1, section(cls, "time", 1.5, STRIKES, std_devs(VOLS, 1.5), 0.03, dc, flat=flat), 1.0))
            out.append((E1, section(cls, "date", ql.Date(15, ql.July, 2026), STRIKES,
                                    std_devs(VOLS, 1.4), 0.032, dc, flat=flat, reference=E1), 0.97))
            out.append((E3, section(cls, "floating", ql.Date(30, ql.April, 2028), STRIKES,
                                    std_devs([0.28, 0.24, 0.21, 0.205, 0.23], 1.5), 0.029, dc, flat=flat), 1.0))
    out += [
        (E1, section("CubicInterpolatedSmileSection", "time", 2.0, STRIKES, std_devs(VOLS, 2.0), 0.03, A365,
                     cubic=FRITSCH_BUTLAND), 1.0),
        (E1, section("CubicInterpolatedSmileSection", "date", ql.Date(15, ql.January, 2027), STRIKES,
                     std_devs(VOLS, 2.0), 0.03, A365, flat=True, reference=E1, cubic=CLAMPED_SPLINE), 0.98),
        (E2, section("CubicInterpolatedSmileSection", "floating", ql.Date(28, ql.February, 2025),
                     [-0.01, 0.0, 0.01, 0.02, 0.03, 0.04], [0.006, 0.0062, 0.0065, 0.007, 0.0072, 0.0075], 0.012,
                     A360, N, 0.0, cubic=FRITSCH_BUTLAND), 1.0),
        (E2, section("LinearInterpolatedSmileSection", "date", ql.Date(28, ql.February, 2027),
                     [-0.01, 0.0, 0.01, 0.03], [0.012, 0.011, 0.0105, 0.0115], 0.005, A365, N, 0.0,
                     reference=E2), 0.95),
        (E1, section("SplineCubicInterpolatedSmileSection", "time", 3.0, [-0.005, 0.0, 0.01, 0.02, 0.04],
                     std_devs([0.45, 0.35, 0.25, 0.2, 0.18], 3.0), 0.01, A365, SL, 0.01), 1.0),
        (E1, section("MonotonicCubicInterpolatedSmileSection", "date", ql.Date(15, ql.January, 2030), STRIKES,
                     std_devs(VOLS, 5.0), 0.03, ACT_ISDA, SL, 0.005, True, reference=E1), 1.0),
        (E1, section("LinearInterpolatedSmileSection", "time", 1.0, [0.02, 0.03, 0.04],
                     [0.25, 0.2, 0.22, 0.5, 0.6], 0.03, A365), 1.0),
        (E1, section("LinearInterpolatedSmileSection", "time", 1.0, [0.02, 0.03], [0.3, 0.1], 0.025, A365), 1.0),
        (E1, section("LinearInterpolatedSmileSection", "time", 1.0, [0.02, 0.03], [0.3, 0.1], 0.025, A365,
                     flat=True), 1.0),
        (E1, section("LinearInterpolatedSmileSection", "time", 0.0, STRIKES, VOLS, 0.03, A365), 1.0),
        (E3, section("SplineCubicInterpolatedSmileSection", "floating", E3, STRIKES, VOLS, 0.03, A365), 1.0),
    ]
    return out


REFUSALS = [
    (E1, section("LinearInterpolatedSmileSection", "time", 1.0, [0.02, 0.04, 0.03], [0.2, 0.21, 0.22], 0.03, A365)),
    (E1, section("SplineCubicInterpolatedSmileSection", "date", ql.Date(15, ql.January, 2026), [0.03, 0.02],
                 [0.2, 0.21], 0.03, A365, reference=E1)),
    (E1, section("LinearInterpolatedSmileSection", "time", 1.0, [0.03], [0.2], 0.03, A365)),
    (E1, section("CubicInterpolatedSmileSection", "time", 1.0, [0.03], [0.2], 0.03, A365)),
    (E1, section("MonotonicCubicInterpolatedSmileSection", "time", -0.5, STRIKES, VOLS, 0.03, A365)),
    (E1, section("CubicInterpolatedSmileSection", "date", ql.Date(14, ql.January, 2025), STRIKES, VOLS, 0.03, A365,
                 reference=E1)),
    (E1, section("LinearInterpolatedSmileSection", "floating", ql.Date(10, ql.January, 2025), STRIKES, VOLS, 0.03,
                 A360)),
]


def short_name(spec):
    return spec["type"].removesuffix("InterpolatedSmileSection")


def section_case(k, evaluation_date, spec, discount):
    st = v.settings(evaluation_date)
    s = make_section(spec)
    ks = strikes(spec)

    def at_strikes(f):
        return [v.attempt(lambda x=x: f(x)) for x in ks]

    return {"name": f"{short_name(spec)} {spec['form']} flat {spec['flatStrikeExtrapolation']} / {k}",
            "inputs": {"settings": st, "kind": "section", "section": v.enc(spec), "strikes": ks, "discount": discount},
            "expected": {
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
                "putPrice": at_strikes(lambda x: s.optionPrice(x, ql.Option.Put, discount))}}


def refusal_case(k, evaluation_date, spec):
    st = v.settings(evaluation_date)
    return {"name": f"refusal {short_name(spec)} {spec['form']} / {k}",
            "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
            "expected": {"section": v.attempt(lambda: make_section(spec).volatility(0.03))}}


def cases():
    rows = sections()
    out = [section_case(k, *row) for k, row in enumerate(rows)]
    out += [refusal_case(k, *row) for k, row in enumerate(REFUSALS)]
    for c in out[len(rows):]:
        assert c["expected"]["section"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/interpolatedsmilesection.hpp", cases)
