# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CapletVarianceCurve (ql/termstructures/volatility/optionlet/capletvariancecurve.hpp), which
the wheel doesn't bind, each output computed by the C++ harness on curves built inside the call, referenced on 15
January 2025: four curves of caplet volatilities at 6 months to 10 years, lognormal on Actual/365 (Fixed), normal on
Actual/360, shifted lognormal displaced by 2%, and one whose total variance falls between two dates (QuantLib's
BlackVarianceCurve doesn't force it monotone); and the curves QuantLib refuses: a date count that doesn't match the
volatilities, and a first date on the reference date. Each gives the reference date, the maximum date and time, the
strike range, the volatility type and the displacement; the volatility and the variance by date and by time, before the
first date, at and between the dates and past the last, with and without extrapolation, at two strikes; and the smile
section by date and by time, its exercise time and its volatility and variance at a strike.

Left out, a documented divergence: the smile section's volatility type and shift, which QuantLib's FlatSmileSection
drops (tests/NanoQL.Tests/Divergences/CapletVarianceCurveSmileTests.cs). A refused value is {"throws": true}. Each
case's input "curve" is the spec tools/cxx/src/termstructures/volatility/optionlet/capletvariancecurve.cpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/CapletVarianceCurveCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)
CAL = ql.TARGET()
A365, A360 = {"type": "Actual365Fixed"}, {"type": "Actual360"}
SL, N = int(ql.ShiftedLognormal), int(ql.Normal)
NODES = [CAL.advance(TODAY, p) for p in (ql.Period(6, ql.Months), ql.Period(1, ql.Years), ql.Period(2, ql.Years),
                                         ql.Period(5, ql.Years), ql.Period(10, ql.Years))]
DATES = [CAL.advance(TODAY, 1, ql.Months), NODES[0], CAL.advance(TODAY, 9, ql.Months), NODES[2],
         CAL.advance(TODAY, 3, ql.Years), NODES[-1], CAL.advance(TODAY, 12, ql.Years)]
TIMES = [0.0, 0.1, 0.5, 1.75, 4.0, 10.0, 12.5]
STRIKES = [0.01, 0.03]


def curve(vols, dc=A365, type_=SL, displacement=0.0, dates=NODES):
    return {"referenceDate": v.enc(TODAY), "dates": v.enc(list(dates)), "vols": list(vols), "dayCounter": dc,
            "volatilityType": type_, "displacement": displacement}


def specs():
    return [
        ("lognormal, Actual/365 (Fixed)", curve([0.25, 0.22, 0.20, 0.18, 0.17])),
        ("normal, Actual/360", curve([0.0085, 0.0090, 0.0095, 0.0100, 0.0098], dc=A360, type_=N)),
        ("shifted lognormal, displaced by 2%", curve([0.12, 0.115, 0.11, 0.105, 0.10], displacement=0.02)),
        ("a falling total variance", curve([0.40, 0.25, 0.20, 0.18, 0.17])),
    ]


def refusals():
    return [
        ("refused: four dates for five volatilities", curve([0.25, 0.22, 0.20, 0.18, 0.17], dates=NODES[:4])),
        ("refused: a first date on the reference date", curve([0.25, 0.22, 0.20, 0.18, 0.17],
                                                               dates=[TODAY] + NODES[1:])),
    ]


def cases():
    with cxx.connect() as H:
        def call(method, *params):
            return v.attempt(lambda: H.call(method, *params))

        out = []
        with H.case_settings(v.enc(TODAY)) as st:
            for name, spec in specs():
                expected = H.call("CapletVarianceCurve::inspectors", spec)
                for member in ("volatility", "blackVariance"):
                    for extrapolate in (False, True):
                        suffix = ", extrapolated" if extrapolate else ""
                        expected[f"{member}(Date){suffix}"] = [
                            [call(f"CapletVarianceCurve::{member}(Date)", spec, v.enc(d), k, extrapolate)
                             for k in STRIKES] for d in DATES]
                        expected[f"{member}(Time){suffix}"] = [
                            [call(f"CapletVarianceCurve::{member}(Time)", spec, t, k, extrapolate) for k in STRIKES]
                            for t in TIMES]
                for kind, points in (("Date", v.enc(DATES)), ("Time", TIMES)):
                    sections = [call(f"CapletVarianceCurve::smileSection({kind})", spec, p, STRIKES[1])
                                for p in points]
                    for field in ("exerciseTime", "volatility", "variance"):
                        expected[f"smileSection({kind}).{field}"] = [
                            x if x == v.THROWS else x[field] for x in sections]
                out.append({"name": name,
                            "inputs": {"settings": st, "kind": "curve", "curve": spec, "dates": v.enc(DATES),
                                       "times": TIMES, "strikes": STRIKES},
                            "expected": expected})
            for name, spec in refusals():
                out.append({"name": name, "inputs": {"settings": st, "kind": "refusal", "curve": spec},
                            "expected": {"curve": call("CapletVarianceCurve::inspectors", spec)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/optionlet/capletvariancecurve.hpp", cases)
