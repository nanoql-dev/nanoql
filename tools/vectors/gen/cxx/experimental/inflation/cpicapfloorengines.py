# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolating CPI cap and floor engine (ql/experimental/inflation/cpicapfloorengines.hpp,
.cpp), which the wheel doesn't bind, each output computed by the C++ harness
(tools/cxx/src/experimental/inflation/cpicapfloortermpricesurface.cpp, "InterpolatingCPICapFloorEngine::npv").

- "npv" cases: tools/vectors/gen/cxx/experimental/inflation/cpicapfloortermpricesurface.py's surfaces, each pricing caps
  and floors from the evaluation date to two to twelve years, observed flat and linearly, at the surface's lag and a
  month longer, at strikes on and between the quotes: each NPV. An instrument observed a month shorter than the surface
  is refused (QuantLib's "lag difference must be non-negative").

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/InterpolatingCPICapFloorEngineCxxVectorTests.cs."""
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


cs = _load("cxx_cpicapfloortermpricesurface", "cxx/experimental/inflation/cpicapfloortermpricesurface.py")
TODAY = cs.TODAY
M = cs.M
CALL, PUT = int(ql.Option.Call), int(ql.Option.Put)


def cap_floor(t, years, strike, lag, interpolation):
    return {"type": t, "nominal": 1.0, "startDate": v.enc(TODAY), "baseCPI": 300.0,
            "maturity": v.enc(TODAY + ql.Period(years, ql.Years)), "fixCalendar": cs.UK,
            "fixConvention": int(ql.Unadjusted), "payCalendar": cs.UK, "payConvention": int(ql.ModifiedFollowing),
            "strike": strike, "observationLag": [lag, M], "observationInterpolation": interpolation}


INSTRUMENTS = ([cap_floor(t, years, k, lag, interp)
                for t, k in ((CALL, 0.03), (CALL, 0.045), (PUT, 0.01), (PUT, 0.015))
                for years in (2, 3, 5, 7, 12)
                for lag in (2, 3)
                for interp in (cs.FLAT, cs.LINEAR)]
               + [cap_floor(CALL, 3, 0.03, 1, cs.FLAT)])


def cases():
    out = []
    with cxx.connect() as H:
        for label, s in cs.SURFACES:
            with H.case_settings(v.enc(TODAY), fixings=cs.ic.harness_fixings()) as st:
                out.append({"name": label, "inputs": {"settings": st, "surface": s, "capFloors": INSTRUMENTS},
                            "expected": {"npv": H.call("InterpolatingCPICapFloorEngine::npv", s, INSTRUMENTS)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/cpicapfloorengines.hpp", cases)
