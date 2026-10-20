# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Seasonality base (ql/termstructures/inflation/seasonality.hpp), each output computed by the
C++ harness: its default isConsistent, which every QuantLib seasonality overrides, on the harness's own seasonality,
which overrides only the two corrections (a zero rate plus 0.001, a year-on-year rate less 0.001).

One case per curve of tools/vectors/gen/termstructures/inflationtermstructure.py's CURVES (whose functions build the
specs): isConsistent on the curve, and the curve's rate at 37 dates when built with the harness's seasonality, which
the curve's constructor checks with isConsistent and its rates apply, a date before the base date or past the maximum
date without extrapolation refused.

Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/SeasonalityCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_inflationtermstructure", os.path.join(v.GEN_ROOT, "termstructures", "inflationtermstructure.py"))
its = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(its)


def cases():
    out = []
    with cxx.connect() as H:
        for k, (label, evaluation_date, spec) in enumerate(its.CURVES):
            with v.case_settings(evaluation_date):
                dates = its.rate_dates(its.make_curve(spec))
            with H.case_settings(v.enc(evaluation_date)) as st:
                out.append({"name": f"seasonality on {label} / {k}",
                            "inputs": {"settings": st, "curve": spec, "dates": v.enc(dates)},
                            "expected": {
                                "isConsistent": H.call("Seasonality::isConsistent", spec),
                                "rate": [v.attempt(lambda d=d: H.call("Seasonality::rate", spec, v.enc(d)))
                                         for d in dates]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/inflation/seasonality.hpp", cases)
