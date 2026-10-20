# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's year-on-year optionlet volatility structures
(ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp), each output computed by the C++
harness: the members the wheel doesn't bind, on ConstantYoYOptionletVolatility built with a volatility type and a
displacement, which the wheel's constructor doesn't take.

One case per surface of tools/vectors/gen/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.py
(whose functions build the specs), each with a volatility type (shifted lognormal or normal) and a displacement:
volatilityType and displacement, both refused where the constructor refuses the displacement (anything but 0 or 1,
within QuantLib's close_enough: 1e-30 and one ulp above 1 are accepted, 1e-20, 0.5 and -1 refused); and volatility on
a time at 6 times and 3 strikes, as [time][strike], which applies no lag, range or strike check.

Read back by tests/NanoQL.Tests/Vectors/Cxx/TermStructures/YoYOptionletVolatilitySurfaceCxxVectorTests.cs."""
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
    "wheel_yoyinflationoptionletvolatilitystructure",
    os.path.join(v.GEN_ROOT, "termstructures", "volatility", "inflation",
                 "yoyinflationoptionletvolatilitystructure.py"))
yoy = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(yoy)

N = int(ql.Normal)
# (surface index of yoy.SURFACES, volatility type, displacement)
VARIANTS = [(0, yoy.SL, 0.0), (1, N, 0.0), (2, yoy.SL, 1.0), (3, N, 1.0), (4, yoy.SL, 1e-30),
            (0, yoy.SL, 1.0000000000000002), (1, N, 1e-20), (2, yoy.SL, 0.5), (3, N, -1.0)]
TIMES = [0.0, -0.5, 0.25, 1.0, 10.0, 100.0]
STRIKES = [0.025, -10.0, 1000.0]
NAME = "YoYOptionletVolatilitySurface::"


def cases():
    out = []
    with cxx.connect() as H:
        for k, (n, vol_type, displacement) in enumerate(VARIANTS):
            evaluation_date, spec = yoy.SURFACES[n]
            spec = dict(spec, volatilityType=vol_type, displacement=displacement)
            with H.case_settings(v.enc(evaluation_date)) as st:
                def call(method, *args, spec=spec):
                    return v.attempt(lambda: H.call(NAME + method, spec, *args))

                out.append({"name": f"surface {n}, type {vol_type}, displacement {displacement!r} / {k}",
                            "inputs": {"settings": st, "surface": spec, "times": TIMES, "strikes": STRIKES},
                            "expected": {
                                "volatilityType": call("volatilityType"),
                                "displacement": call("displacement"),
                                "volatilityAtTimes": [[call("volatility(Time)", t, x) for x in STRIKES]
                                                      for t in TIMES]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/inflation/yoyinflationoptionletvolatilitystructure.hpp", cases)
