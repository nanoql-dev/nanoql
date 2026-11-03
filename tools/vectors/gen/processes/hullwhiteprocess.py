# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HullWhiteProcess and HullWhiteForwardProcess (ql/processes/hullwhiteprocess.hpp, .cpp),
through the members the wheel binds.

Eighteen processes: six reversion and volatility pairs (a from 1e-3 to 2, sigma from 0.005 to 0.02) on three curves of
tools/vectors/gen/models/shortrate/onefactormodels/hullwhite.py (a flat 4% on Actual/365 (Fixed), a flat 3% compounded
annually on Actual/360, and a bootstrapped log-linear discount curve on its fixture rate helpers). For the process:
x0(), drift and diffusion at eight (t, x) points, expectation, stdDeviation and variance at eight (t0, x0, dt) steps (a
zero step among them), evolve at six (t0, x0, dt, dw) points and apply at three. For the forward process, at forward
measure times 2, 10 and 30, each set on a fresh process with setForwardMeasureTime: the same outputs, its expectation
less M_T(t0, t0 + dt, T) and its drift less B(t, T) sigma^2; and, on the process at T = 10, alpha(t) at six times, B(t,
T) at six points and M_T(s, t, T) at six. The reversions stay at a >= 1e-3, where QuantLib 1.44's forms and nanoql's
cancellation-free ones agree within the tolerance; below it they part
(tests/NanoQL.Tests/Divergences/HullWhiteProcessSmallReversionTests.cs, HullWhiteForwardProcessSmallReversionTests.cs).

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: ql.HullWhiteProcess(YieldTermStructureHandle, a, sigma)
and ql.HullWhiteForwardProcess(YieldTermStructureHandle, a, sigma), x0, drift, diffusion, expectation, stdDeviation,
variance, evolve and apply (StochasticProcess1D's), setForwardMeasureTime, alpha, B and M_T, each forwarding to the C++
member of that name. Each case's inputs hold its settings, the "curve" (hullwhite.py's spec), "a", "sigma", the
forward measure time ("forwardMeasureTime", null for the risk-neutral process) and the points. Read back by
tests/NanoQL.Tests/Vectors/Processes/HullWhiteProcessVectorTests.cs, with tests/NanoQL.Tests/Vectors/ModelInputs.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_hullwhite", os.path.join(v.GEN_ROOT, "models", "shortrate", "onefactormodels", "hullwhite.py"))
hw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hw)

TODAY = hw.TODAY
CURVES = [hw.flat(0.04), hw.flat(0.03, hw.A360, hw.COMPOUNDED, hw.ANNUAL),
          hw.bootstrapped("Discount", hw.pyc.LOG_LINEAR, hw.A360)]
PARAMS = [(0.1, 0.01), (0.03, 0.012), (1e-3, 0.01), (0.5, 0.008), (2.0, 0.02), (0.05, 0.005)]
FORWARD_TIMES = [2.0, 10.0, 30.0]

PROCESS_POINTS = [(0.0, 0.04), (0.25, 0.03), (1.0, 0.05), (2.5, -0.01), (5.0, 0.0), (7.5, 0.1), (10.0, 0.02),
                  (20.0, 0.035)]
STEP_POINTS = [(0.0, 0.04, 0.25), (0.5, 0.03, 0.5), (1.0, 0.05, 1.0), (2.0, -0.01, 0.1), (5.0, 0.02, 0.0),
               (3.0, 0.06, 2.0), (10.0, 0.03, 5.0), (0.1, 0.0, 0.01)]
EVOLVE_POINTS = [(0.0, 0.04, 0.25, 0.3), (1.0, 0.05, 0.5, -1.2), (2.0, 0.0, 1.0, 2.5), (5.0, -0.02, 0.1, 0.0),
                 (0.5, 0.03, 0.01, -0.7), (8.0, 0.08, 2.0, 1.0)]
APPLY_POINTS = [(0.03, 0.001), (0.0, -0.02), (-0.01, 0.05)]
ALPHA_TIMES = [0.0, 0.5, 1.0, 3.0, 7.0, 15.0]
B_POINTS = [(0.0, 10.0), (1.0, 10.0), (5.0, 10.0), (9.5, 10.0), (10.0, 10.0), (2.0, 30.0)]
M_T_POINTS = [(0.0, 0.25, 10.0), (0.0, 1.0, 10.0), (1.0, 2.0, 10.0), (5.0, 10.0, 10.0), (2.0, 2.0, 10.0),
              (0.5, 3.0, 30.0)]


def make_process(curve, a, sigma, forward_time):
    if forward_time is None:
        return ql.HullWhiteProcess(ql.YieldTermStructureHandle(curve), a, sigma)
    p = ql.HullWhiteForwardProcess(ql.YieldTermStructureHandle(curve), a, sigma)
    p.setForwardMeasureTime(forward_time)
    return p


def outputs(p):
    return {"x0": p.x0(),
            "drift": [p.drift(t, x) for t, x in PROCESS_POINTS],
            "diffusion": [p.diffusion(t, x) for t, x in PROCESS_POINTS],
            "expectation": [p.expectation(*s) for s in STEP_POINTS],
            "stdDeviation": [p.stdDeviation(*s) for s in STEP_POINTS],
            "variance": [p.variance(*s) for s in STEP_POINTS],
            "evolve": [p.evolve(*e) for e in EVOLVE_POINTS],
            "apply": [p.apply(*a) for a in APPLY_POINTS]}


def cases():
    out = []
    for i, curve_spec in enumerate(CURVES):
        for a, sigma in PARAMS:
            for forward_time in [None] + FORWARD_TIMES:
                with v.case_settings(TODAY) as st:
                    built = hw.make_curve(curve_spec)
                    curve = built[0] if isinstance(built, tuple) else built
                    p = make_process(curve, a, sigma, forward_time)
                    expected = outputs(p)
                    inputs = {"settings": st, "curve": curve_spec, "a": a, "sigma": sigma,
                              "forwardMeasureTime": forward_time, "processPoints": PROCESS_POINTS,
                              "stepPoints": STEP_POINTS, "evolvePoints": EVOLVE_POINTS, "applyPoints": APPLY_POINTS}
                    if forward_time == 10.0:
                        expected.update({"alpha": [p.alpha(t) for t in ALPHA_TIMES],
                                         "B": [p.B(t, T) for t, T in B_POINTS],
                                         "M_T": [p.M_T(s, t, T) for s, t, T in M_T_POINTS]})
                        inputs.update({"alphaTimes": ALPHA_TIMES, "bPoints": B_POINTS, "mTPoints": M_T_POINTS})
                    kind = "forward" if forward_time is not None else "process"
                    out.append({"name": f"curve {i} / a {a} / sigma {sigma} / {kind}"
                                        + ("" if forward_time is None else f" at T = {forward_time}"),
                                "inputs": inputs, "expected": v.enc(expected)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/hullwhiteprocess.hpp", cases)
