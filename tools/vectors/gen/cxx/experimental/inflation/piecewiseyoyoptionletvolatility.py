# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's piecewise year-on-year optionlet volatility curve
(ql/experimental/inflation/piecewiseyoyoptionletvolatility.hpp), which the wheel doesn't bind, each output computed by
the C++ harness (tools/cxx/src/experimental/inflation/yoyoptionletvolatility.cpp,
"PiecewiseYoYOptionletVolatilityCurve::strip"): the curves QuantLib's InterpolatedYoYOptionletStripper<Linear>
bootstraps at each strike.

- "strip" cases: tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py's surfaces on the euro
  market, stripped at slopes -0.5 and 0: each strike's curve's reference, base and last dates, base level, times, dates
  and data, and its volatility at its strike at dates from a year to twelve years, through its own date route.

Left out: the curves' nodes, empty on QuantLib 1.44 for a bootstrapped curve and its dates and data on QuantLib's master
(#2941, taken: tests/NanoQL.Tests/Divergences/PiecewiseYoYVolatilityNodesTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Inflation/PiecewiseYoYOptionletVolatilityCurveCxxVectorTests.cs."""
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


ys = _load("wheel_yoycapfloortermpricesurface", "experimental/inflation/yoycapfloortermpricesurface.py")
EVAL = ys.EVAL


def cases():
    out = []
    market = ys.market_spec()
    dates = [v.enc(EVAL + ql.Period(n, ql.Years)) for n in (1, 2, 4, 7, 12)]
    with cxx.connect() as H:
        st = H.settings(v.enc(EVAL))
        for label, s in ys.CASES:
            for slope in (-0.5, 0.0):
                out.append({"name": f"{label}, slope {slope}",
                            "inputs": {"settings": st, "market": market, "surface": s, "slope": slope,
                                       "dates": dates},
                            "expected": H.call("PiecewiseYoYOptionletVolatilityCurve::strip", market, s, slope,
                                               dates)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/piecewiseyoyoptionletvolatility.hpp", cases)
