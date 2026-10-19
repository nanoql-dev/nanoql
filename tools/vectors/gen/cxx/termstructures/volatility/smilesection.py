# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for SmileSection (ql/termstructures/volatility/smilesection.hpp), each output computed by the C++ harness.

- "section" cases, on 14 of the harness's own sections (tools/cxx/src/termstructures/volatility/smilesectionspec.hpp),
  which override only what SmileSection leaves abstract, with a volatility a + b k + c k^2 at a strike k, so that the
  base's defaults are what they price with: by date and by time, both volatility types, shifts, with and without an
  ATM level, finite and infinite strike ranges, an exercise time of 0 and QuantLib's empty day counter; four of them
  override volatilityType() or shift() to report another type or shift than the one they were built with, where
  volatility(k, type, shift) compares the type it was built with and the shift it reports. Each gives its
  minStrike, maxStrike, atmLevel (null for none), exerciseDate, referenceDate (refused for a section by time),
  exerciseTime, volatilityType, shift and dayCounter (its name, or null for the empty one); and at 10 strikes the
  variance and volatility, the volatility converted to a shifted lognormal one with the case's target shift, to a
  normal one, and to the type and shift the section was built with, the call and put prices with the case's
  discount, the digital call and put prices and the density with the case's gaps, and the vega.
- "flat" cases, on FlatSmileSection by time, which the wheel can't build with QuantLib's empty day counter: its
  dayCounter (null for the empty one), exerciseDate (null), referenceDate (refused), exerciseTime and its volatility
  at the case's strike.
- "refusals" cases: sections QuantLib refuses to build, the harness's by date before its reference date and by a NaN
  time, and FlatSmileSection by date and floating with the empty day counter, whose exercise time QuantLib computes
  with it and can't.

A refused value is {"throws": true}. Each case's input "section" is the spec smilesectionspec.hpp reads; a section's
smile is "smile" ([a, b, c]), its strike range "minStrike" and "maxStrike", and "overriddenType" and
"overriddenShift" null where the section reports what it was built with. The case's evaluation date sets the
floating section's reference date. tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SmileSectionCxxVectorTests.cs reads
it back."""
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
    "wheel_flatsmilesection", os.path.join(v.GEN_ROOT, "termstructures", "volatility", "flatsmilesection.py"))
fss = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(fss)

A360, A365, ACT_ISDA, THIRTY360_BB = fss.A360, fss.A365, fss.ACT_ISDA, fss.THIRTY360_BB
SL, N, CALL, PUT = fss.SL, fss.N, fss.CALL, fss.PUT
MAX = 1.7976931348623157e308
NAN = float("nan")
E1 = ql.Date(15, ql.January, 2025)


def harness_spec(form, when, dc, smile, atm, vol_type=SL, shift=0.0, reference=None, strikes=(-MAX, MAX),
                 overridden_type=None, overridden_shift=None):
    spec = {"type": "HarnessSmileSection", "form": form}
    if form == "date":
        spec["exerciseDate"] = v.enc(when)
        spec["referenceDate"] = v.enc(reference)
    else:
        spec["exerciseTime"] = when
    spec.update({"dayCounter": dc, "atmLevel": atm, "volatilityType": vol_type, "shift": shift, "smile": smile,
                 "minStrike": strikes[0], "maxStrike": strikes[1], "overriddenType": overridden_type,
                 "overriddenShift": overridden_shift})
    return spec


# (evaluation date, spec, discount, gap of the digital prices, gap of the density, target shift)
SECTIONS = [
    (E1, harness_spec("date", ql.Date(15, ql.January, 2026), A365, [0.2, -1.0, 10.0], 0.03, reference=E1), 1.0,
     1.0e-5, 1.0e-4, 0.0),
    (E1, harness_spec("date", ql.Date(15, ql.July, 2027), A360, [0.3, -2.0, 30.0], 0.01, SL, 0.02, reference=E1),
     0.96, 1.0e-5, 1.0e-4, 0.01),
    (E1, harness_spec("time", 2.0, ACT_ISDA, [0.25, 0.5, 5.0], 0.025), 1.0, 1.0e-4, 1.0e-3, 0.0),
    (E1, harness_spec("time", 1.5, A365, [0.008, 0.01, 0.1], 0.03, N), 0.9, 1.0e-5, 1.0e-4, 0.02),
    (E1, harness_spec("date", ql.Date(16, ql.January, 2030), THIRTY360_BB, [0.006, -0.02, 0.5], 0.02, N, 0.01,
                      reference=ql.Date(16, ql.January, 2025)), 1.0, 2.0e-5, 5.0e-4, 0.03),
    (E1, harness_spec("time", 1.0, A365, [0.2, 0.0, 0.0], None), 1.0, 1.0e-5, 1.0e-4, 0.0),
    (E1, harness_spec("time", 1.0, A365, [0.01, 0.0, 0.0], None, N), 1.0, 1.0e-5, 1.0e-4, 0.0),
    (E1, harness_spec("time", 1.0, A365, [0.2, -1.0, 10.0], 0.03, SL, 0.0, overridden_type=N), 1.0, 1.0e-5,
     1.0e-4, 0.0),
    (E1, harness_spec("time", 3.0, A360, [0.009, 0.02, 0.0], 0.03, N, 0.0, overridden_type=SL), 1.0, 1.0e-5,
     1.0e-4, 0.0),
    (E1, harness_spec("time", 0.5, A365, [0.3, -1.5, 12.0], 0.03, SL, 0.0, overridden_shift=0.01), 1.0, 1.0e-5,
     1.0e-4, 0.0),
    (E1, harness_spec("date", ql.Date(15, ql.January, 2028), ACT_ISDA, [0.25, 0.0, 2.0], 0.04, SL, 0.02,
                      reference=E1, overridden_shift=0.02), 1.0, 1.0e-5, 1.0e-4, 0.02),
    (E1, harness_spec("time", 4.0, None, [0.18, -0.5, 4.0], 0.035, SL, 0.005), 1.0, 1.0e-5, 1.0e-4, 0.005),
    (E1, harness_spec("time", 0.0, A365, [0.2, -1.0, 10.0], 0.03), 1.0, 1.0e-5, 1.0e-4, 0.01),
    (E1, harness_spec("time", 1.0, A365, [0.22, 0.3, -1.0], 0.05, SL, 0.01, strikes=(0.0, 0.1)), 0.99, 1.0e-6,
     1.0e-5, 0.0),
]
FLAT = [
    (E1, fss.time_spec(1.0, 0.2, None, 0.03), 0.03),
    (E1, fss.time_spec(0.25, 0.01, None, None, N, 0.01), 0.0),
    (E1, fss.time_spec(2.0, 0.3, A360, 0.02, SL, 0.02), 0.04),
    (E1, fss.time_spec(0.0, 0.15, None), 0.05),
]
REFUSALS = [
    (E1, harness_spec("date", ql.Date(14, ql.January, 2025), A365, [0.2, 0.0, 0.0], 0.03, reference=E1)),
    (E1, harness_spec("time", NAN, A365, [0.2, 0.0, 0.0], 0.03)),
    (E1, harness_spec("date", ql.Date(15, ql.January, 2026), None, [0.2, 0.0, 0.0], 0.03, reference=E1)),
    (E1, fss.date_spec(ql.Date(15, ql.January, 2026), E1, 0.2, None, 0.03)),
    (E1, fss.floating_spec(ql.Date(15, ql.January, 2026), 0.2, None, 0.03)),
]


def strikes(spec):
    """10 strikes around the ATM level (0.03 where the section has none), as the wheel's file chooses them."""
    return fss.strikes(spec)


def section_case(H, k, evaluation_date, spec, discount, gap, density_gap, target_shift):
    with H.case_settings(v.enc(evaluation_date)) as st:
        ks = strikes(spec)

        def call(method, *args):
            return v.attempt(lambda: H.call("SmileSection::" + method, spec, *args))

        def at_strikes(method, *args):
            return [v.attempt(lambda x=x: H.call("SmileSection::" + method, spec, x, *args)) for x in ks]

        expected = {m: call(m) for m in ("minStrike", "maxStrike", "atmLevel", "exerciseDate", "referenceDate",
                                         "exerciseTime", "volatilityType", "shift", "dayCounter")}
        expected.update({
            "variance": at_strikes("variance"),
            "volatility": at_strikes("volatility(Rate)"),
            "lognormalVolatility": at_strikes("volatility(Rate,VolatilityType,Real)", SL, target_shift),
            "normalVolatility": at_strikes("volatility(Rate,VolatilityType,Real)", N, 0.0),
            "ownVolatility": at_strikes("volatility(Rate,VolatilityType,Real)", spec["volatilityType"],
                                        spec["shift"]),
            "callPrice": at_strikes("optionPrice", CALL, discount),
            "putPrice": at_strikes("optionPrice", PUT, discount),
            "digitalCallPrice": at_strikes("digitalOptionPrice", CALL, discount, gap),
            "digitalPutPrice": at_strikes("digitalOptionPrice", PUT, discount, gap),
            "vega": at_strikes("vega", discount),
            "density": at_strikes("density", discount, density_gap)})
        return {"name": f"section {spec['form']} {'Normal' if spec['volatilityType'] == N else 'ShiftedLognormal'} "
                        f"{spec['shift']!r} / {k}",
                "inputs": {"settings": st, "kind": "section", "section": v.enc(spec), "strikes": ks,
                           "discount": discount, "gap": gap, "densityGap": density_gap,
                           "targetShift": target_shift},
                "expected": expected}


def flat_case(H, k, evaluation_date, spec, strike):
    with H.case_settings(v.enc(evaluation_date)) as st:
        expected = {m: v.attempt(lambda m=m: H.call("SmileSection::" + m, spec))
                    for m in ("dayCounter", "exerciseDate", "referenceDate", "exerciseTime")}
        expected["volatility"] = v.attempt(lambda: H.call("SmileSection::volatility(Rate)", spec, strike))
        return {"name": f"flat time {spec['exerciseTime']!r} / {k}",
                "inputs": {"settings": st, "kind": "flat", "section": v.enc(spec), "strike": strike},
                "expected": expected}


def refusal_case(H, k, evaluation_date, spec):
    with H.case_settings(v.enc(evaluation_date)) as st:
        return {"name": f"refusal {spec['type']} {spec['form']} / {k}",
                "inputs": {"settings": st, "kind": "refusal", "section": v.enc(spec)},
                "expected": {"section": v.attempt(lambda: H.call("SmileSection::exerciseTime", spec))}}


def cases():
    with cxx.connect() as H:
        out = [section_case(H, k, *row) for k, row in enumerate(SECTIONS)]
        out += [flat_case(H, k, *row) for k, row in enumerate(FLAT)]
        out += [refusal_case(H, k, *row) for k, row in enumerate(REFUSALS)]
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/smilesection.hpp", cases)
