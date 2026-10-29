# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated year-on-year optionlet stripper
(ql/experimental/inflation/interpolatedyoyoptionletstripper.hpp), through the wheel's
InterpolatedYoYInflationOptionletStripper (QuantLib's InterpolatedYoYOptionletStripper<Linear>) and the
KInterpolatedYoYInflationOptionletVolatilitySurface (QuantLib's KInterpolatedYoYOptionletVolatilitySurface<Linear>)
that strips with it.

- "strip" cases: tools/vectors/gen/experimental/inflation/yoycapfloortermpricesurface.py's surfaces on QuantLib's euro
  market, stripped with the unit-displaced Black engine at QuantLib's case's slope, -0.5, and at 0: the stripped
  volatilities at each strike at dates from the base date to eight years after it (Dslice, each stripped curve read
  through its own lag), and the surface's strikes and last date.

Left out: the k-interpolated surface's own volatility, which reads the stripped curves a lag early in QuantLib's (the
S-xrt plan's XRR5 (xiii), tests/NanoQL.Tests/Divergences/KInterpolatedYoYVolatilityTests.cs). Read back by
tests/NanoQL.Tests/Vectors/Experimental/Inflation/InterpolatedYoYOptionletStripperVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


ys = _load("wheel_yoycapfloortermpricesurface", "experimental/inflation/yoycapfloortermpricesurface.py")
EVAL = ys.EVAL


def strip(surface_spec, slope):
    nominal, index = ys.make_market(ys.market_spec())
    surf = ys.make_surface(surface_spec, nominal, index)
    hvs = ql.RelinkableYoYOptionletVolatilitySurfaceHandle()
    pricer = ql.YoYInflationUnitDisplacedBlackCapFloorEngine(index, hvs, nominal)
    stripper = ql.InterpolatedYoYInflationOptionletStripper()
    return ql.KInterpolatedYoYInflationOptionletVolatilitySurface(
        0, ys.CAL, ql.ModifiedFollowing, ys.A365, surf.observationLag(), surf, pricer, stripper, slope)


def cases():
    out = []
    market = ys.market_spec()
    for label, s in ys.CASES:
        for slope in (-0.5, 0.0):
            with v.case_settings(EVAL) as st:
                k = strip(s, slope)
                dates = [k.baseDate() + ql.Period(n, ql.Years) for n in (1, 2, 3, 5, 8)]
                out.append({"name": f"{label}, slope {slope}",
                            "inputs": {"settings": st, "market": market, "surface": s, "slope": slope,
                                       "dates": [v.enc(d) for d in dates]},
                            "expected": {"minStrike": k.minStrike(), "maxStrike": k.maxStrike(),
                                         "maxDate": v.enc(k.maxDate()),
                                         "dslice": [v.attempt(lambda d=d: [list(x) for x in k.Dslice(d)])
                                                    for d in dates]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/inflation/interpolatedyoyoptionletstripper.hpp", cases)
