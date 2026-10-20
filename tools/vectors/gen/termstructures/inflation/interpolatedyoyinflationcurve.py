# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated year-on-year inflation curve
(ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp), through the wheel's YoYInflationCurve, QuantLib's
InterpolatedYoYInflationCurve<Linear>.

The cases of tools/vectors/gen/termstructures/inflation/interpolatedzeroinflationcurve.py, whose curves and refusals
they take, on the year-on-year curve: "curve" cases with yoyRate in zeroRate's place and the curve's baseRate, its first
rate, added; and "construction" cases. No date or no rate at all isn't sampled: QuantLib reads the first date and the
first rate before checking their counts, throwing std::out_of_range or reading past an empty vector, which the port
refuses with QuantLib's own texts instead (tests/NanoQL.Tests/Divergences/InflationCurveFirstDateTests.cs). Read back
by tests/NanoQL.Tests/Vectors/TermStructures/InterpolatedYoYInflationCurveVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_interpolatedzeroinflationcurve",
    os.path.join(v.GEN_ROOT, "termstructures", "inflation", "interpolatedzeroinflationcurve.py"))
zic = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(zic)

KIND = "YoYInflationCurve"
RATE = "yoyRate"


def cases():
    out = zic.curve_cases(KIND, RATE)
    for c in out:
        with v.case_settings(v.ql.Date(c["inputs"]["settings"]["evaluationDate"])):
            curve = zic.its.make_curve(c["inputs"]["curve"])
            c["expected"]["baseRate"] = v.attempt(curve.baseRate)
    return out + zic.construction_cases(KIND)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/interpolatedyoyinflationcurve.hpp", cases)
