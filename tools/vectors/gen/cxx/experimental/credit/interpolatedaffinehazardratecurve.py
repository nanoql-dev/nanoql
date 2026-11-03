# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's interpolated affine hazard-rate curve and the piecewise curve on its traits
(ql/experimental/credit/interpolatedaffinehazardratecurve.hpp), which the wheel doesn't expose, each output computed
by the C++ harness (tools/cxx/src/experimental/credit/affinehazardratecurve.cpp).

QuantLib's curve squares the process's x0 where nanoql takes the model's own initial intensity, its dynamics' short rate
at x0; the two agree on a Vasicek model whose x0 = r0 - b squares exactly to r0, so every case is on such a model, their
floating-point values exact: x0 0.125, r0 0.015625, b -0.109375, and x0 0.25, r0 0.0625, b -0.1875
(tests/NanoQL.Tests/Divergences/AffineHazardRateTests.cs holds the cases where they differ).

- "curve" cases: hazard rates on nodes today and one, three and five years out, linear and backward flat: the
  curve's maximum date, and at times to seven years (past its range, extrapolated) its survival probability, default
  density and hazard rate, and its conditional survival probability from a prior time at a realized intensity, each
  {"throws": true} where QuantLib refuses it (a prior time after the target).
- "piecewise" cases: the piecewise curve on the affine traits bootstrapped on CDS spread helpers of
  tools/vectors/gen/termstructures/credit/defaultprobabilityhelpers.py's form: its dates and data and its survival
  probability at sample times.

Read back by tests/NanoQL.Tests/Vectors/Cxx/Experimental/Credit/InterpolatedAffineHazardRateCurveCxxVectorTests.cs."""
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
    "wheel_defaultprobabilityhelpers", os.path.join(v.GEN_ROOT, "termstructures", "credit",
                                                    "defaultprobabilityhelpers.py"))
dph = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(dph)
dts = dph.dts

TODAY = dts.TODAY
MODELS = [
    ("x0 0.125", ["Vasicek", 0.015625, 0.1, -0.109375, 0.01]),
    ("x0 0.25, fast", ["Vasicek", 0.0625, 0.8, -0.1875, 0.02]),
]
NODES = [TODAY, TODAY + ql.Period(1, ql.Years), TODAY + ql.Period(3, ql.Years), TODAY + ql.Period(5, ql.Years)]
RATES = [
    ("rising", [0.01, 0.02, 0.03, 0.04]),
    ("negative deterministic part", [-0.005, -0.004, 0.0, 0.002]),
]
TIMES = [0.0, 0.5, 2.0, 4.0, 5.0, 7.0]
CONDITIONALS = [[0.0, 1.0, 0.02], [1.0, 3.0, 0.025], [2.0, 2.0, 0.03], [4.0, 6.5, 0.01], [3.0, 1.0, 0.02]]
QUOTES = [0.005, 0.0075, 0.01]


def helpers():
    return [dph.helper_spec("spread", q, (n, int(ql.Years)), TODAY, ql.DateGeneration.CDS)
            for q, n in zip(QUOTES, (1, 3, 5))]


def flat(result):
    """The harness's object result, one output per key; a refused call's one output, "values"."""
    return dict(result) if isinstance(result, dict) and "throws" not in result else {"values": result}


def cases():
    out = []
    with cxx.connect() as H:
        def call(m, *a):
            return v.attempt(lambda: H.call(m, *a))

        with H.case_settings(TODAY.serialNumber()) as st:
            for label, model in MODELS:
                for rates_label, rates in RATES:
                    for interpolator in ("Linear", "BackwardFlat"):
                        args = [model, v.enc(NODES), rates, interpolator, TIMES, CONDITIONALS]
                        out.append({"name": f"curve {label} {rates_label} {interpolator}",
                                    "inputs": {"settings": st, "kind": "curve", "args": args},
                                    "expected": flat(call("InterpolatedAffineHazardRateCurve::values", *args))})
                args = [model, "BackwardFlat", helpers(), QUOTES, v.enc(TODAY), TIMES]
                out.append({"name": f"piecewise {label}", "inputs": {"settings": st, "kind": "piecewise", "args": args},
                            "expected": flat(call("PiecewiseAffineHazardRateCurve::build", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/credit/interpolatedaffinehazardratecurve.hpp", cases)
