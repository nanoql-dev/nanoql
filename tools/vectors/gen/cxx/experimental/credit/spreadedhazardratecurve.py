# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's spreaded hazard-rate curves (ql/experimental/credit/spreadedhazardratecurve.hpp,
factorspreadedhazardratecurve.hpp), which the wheel doesn't expose, each output computed by the C++ harness
(tools/cxx/src/experimental/credit/spreadedhazardratecurve.cpp): the additive and multiplicative curves on flat and
interpolated originals, their reference date, maximum date and time, and their hazard rate at times inside and past the
original's range. Their survival probabilities and densities, which QuantLib integrates numerically and nanoql computes
exactly, are left out. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/SpreadedHazardRateCurveCxxVectorTests.cs."""
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

_spec = importlib.util.spec_from_file_location("wheel_defaulttermstructure",
                                               os.path.join(v.GEN_ROOT, "termstructures", "defaulttermstructure.py"))
dts = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dts)

TODAY = dts.TODAY
NODES = [TODAY, TODAY + ql.Period(1, ql.Years), TODAY + ql.Period(3, ql.Years), TODAY + ql.Period(5, ql.Years)]
ORIGINALS = [
    ("flat 2%", dts.flat_spec(TODAY, 0.02)),
    ("hazard rates backward flat", dts.curve_spec("HazardRateCurve", NODES, [0.01, 0.01, 0.03, 0.06])),
    ("default densities", dts.curve_spec("DefaultDensityCurve", NODES, [0.01, 0.012, 0.015, 0.02])),
]
SPREADS = [0.0, 0.01, -0.005, 0.5]
TIMES = [0.0, 0.5, 2.0, 4.0, 7.0]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            for label, curve in ORIGINALS:
                for factor in (False, True):
                    for spread in SPREADS:
                        args = [factor, curve, spread, TIMES]
                        out.append({"name": f"{label} {'factor' if factor else 'additive'} {spread}",
                                    "inputs": {"settings": st, "args": args},
                                    "expected": flat(call("SpreadedHazardRateCurve::values", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/spreadedhazardratecurve.hpp", cases)
