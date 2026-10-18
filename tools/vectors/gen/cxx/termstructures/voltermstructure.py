# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for VolatilityTermStructure (ql/termstructures/voltermstructure.hpp) on QuantLib's
ConstantOptionletVolatility, each output computed by the C++ harness: optionDateFromTenor, which the wheel doesn't
expose, at 20 tenors (0D to 30Y; -1D, -1M and -1Y, which give dates before the reference date; and 200Y, whose date
is past Date::maxDate, which QuantLib refuses), and businessDayConvention, minStrike and maxStrike, on each of the 12
surfaces of tools/vectors/gen/termstructures/volatility/optionlet/constantoptionletvol.py, whose functions build them
(fixed and moving reference dates; month ends, a leap day, a Saturday and Christmas Eve among them; seven
business-day conventions). A refused value is {"throws": true}.

Each case's inputs hold the surface spec ("surface", as in that generator) and the tenors ("tenors", each [length,
units]). tests/NanoQL.Tests/Vectors/Cxx/TermStructures/VolTermStructureCxxVectorTests.cs reads it back."""
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

D, W, M, Y = int(ql.Days), int(ql.Weeks), int(ql.Months), int(ql.Years)
TENORS = [[0, D], [1, D], [2, D], [1, W], [2, W], [1, M], [2, M], [3, M], [6, M], [9, M], [1, Y], [18, M], [2, Y],
          [5, Y], [10, Y], [30, Y], [-1, D], [-1, M], [-1, Y], [200, Y]]


def cases():
    with cxx.connect() as H:
        out = []
        for k, (evaluation_date, spec, _) in enumerate(cov.SURFACES):
            with H.case_settings(v.enc(evaluation_date)) as st:
                kind = "fixed" if "referenceDate" in spec else f"{spec['settlementDays']} days"
                out.append({
                    "name": f"{kind} {evaluation_date.ISO()} / {k}",
                    "inputs": {"settings": st, "surface": spec, "tenors": TENORS},
                    "expected": {
                        "optionDateFromTenor": [
                            v.attempt(lambda p=p: H.call("VolatilityTermStructure::optionDateFromTenor", spec, p))
                            for p in TENORS],
                        "businessDayConvention": H.call("VolatilityTermStructure::businessDayConvention", spec),
                        "minStrike": H.call("VolatilityTermStructure::minStrike", spec),
                        "maxStrike": H.call("VolatilityTermStructure::maxStrike", spec)}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/voltermstructure.hpp", cases)
