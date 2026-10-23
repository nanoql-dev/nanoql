# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's G2Process and G2ForwardProcess (ql/processes/g2process.hpp, .cpp), through the members the
wheel binds.

Eight parameter sets (reversions from 1e-3 to 0.5, correlations from -1 to 1, a = b among them), each with no curve
and on a flat 4% Actual/365 (Fixed) curve, as the process and as the forward process at forward measure times 5 and
20 (each set on a fresh process with setForwardMeasureTime). For each: size() and factors(), initialValues(),
phi(t) at five times (refused with no curve: "no term structure given to G2Process"), shortRate(t, z1, z2) at three
points, drift and diffusion at five (t, [x, y]) points, expectation at six (t0, [x, y], dt) steps (a zero step among
them), stdDeviation and covariance at the five nonzero ones, and evolve at four (t0, [x, y], dt, [dw1, dw2]) points.

The two sets with rho = 1 and rho = -1 at a = b are generated and left out: there QuantLib's conditional correlation,
rho times the ratio of the factors' kernels, rounds just outside [-1, 1], and its standard deviation's
sqrt(1 - rho^2) is NaN, where nanoql's is rho itself (the S-sr plan's SSR6 (xix);
tests/NanoQL.Tests/Divergences/G2ProcessCorrelationBoundTests.cs). So are the zero step's standard deviation and
covariance, whose correlation is QuantLib's 0/0, NaN, and nanoql's rho, the step's limit (the same test). Elsewhere QuantLib 1.44's forms and nanoql's
cancellation-free ones (SSR3) agree within the tolerance.

The wheel's calls, by QuantLib-SWIG's stochasticprocess.i: ql.G2Process(a, sigma, b, eta, rho,
YieldTermStructureHandle) and ql.G2ForwardProcess with the same arguments, size, factors, initialValues, phi,
shortRate, drift, diffusion, expectation, stdDeviation, covariance and evolve (on Arrays and Matrices), and
setForwardMeasureTime, each forwarding to the C++ member of that name. Each case's inputs hold its settings, the
parameters, the "curve" (a FlatForward spec of tools/vectors/gen/models/shortrate/onefactormodels/hullwhite.py, or
null), the forward measure time (null for the process) and the points. Read back by
tests/NanoQL.Tests/Vectors/Processes/G2ProcessVectorTests.cs."""
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
CURVE = hw.flat(0.04)
PARAMS = [(0.1, 0.01, 0.2, 0.013, -0.5), (0.05, 0.012, 0.3, 0.008, 0.3), (0.12, 0.011, 0.12, 0.009, -0.3),
          (0.5, 0.02, 0.05, 0.01, 0.9), (1e-3, 0.01, 0.2, 0.01, -0.7), (0.1, 0.01, 0.3, 0.01, 1.0),
          (0.1, 0.01, 0.1, 0.01, 1.0), (0.2, 0.01, 0.2, 0.012, -1.0)]
# rho = +-1 at a = b, generated and left out (SSR6 (xix))
LEFT_OUT = {6, 7}
FORWARD_TIMES = [5.0, 20.0]

PHI_TIMES = [0.0, 0.25, 1.0, 5.0, 10.0]
SHORT_RATE_POINTS = [(1.0, 0.01, 0.02), (0.0, -0.01, 0.005), (5.0, 0.03, -0.02)]
PROCESS_POINTS = [(0.0, (0.04, 0.0)), (0.5, (0.03, 0.01)), (1.0, (0.05, -0.02)), (5.0, (0.0, 0.0)),
                  (10.0, (-0.01, 0.03))]
STEP_POINTS = [(0.0, (0.04, 0.0), 0.25), (0.5, (0.03, 0.01), 0.5), (1.0, (0.05, -0.02), 1.0),
               (2.0, (0.0, 0.0), 0.0), (3.0, (0.02, 0.005), 2.0), (0.1, (0.01, -0.01), 0.01)]
# the zero step's standard deviation and covariance generated and left out (SSR6 (xix))
NONZERO_STEPS = [s for s in STEP_POINTS if s[2] != 0.0]
EVOLVE_POINTS = [(0.0, (0.04, 0.0), 0.25, (0.3, -1.1)), (1.0, (0.05, -0.02), 0.5, (-1.2, 0.4)),
                 (2.0, (0.0, 0.0), 1.0, (2.5, 0.0)), (0.5, (0.03, 0.01), 0.01, (-0.7, 1.9))]


def matrix(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def make_process(params, curve, forward_time):
    handle = ql.YieldTermStructureHandle(curve) if curve is not None else ql.YieldTermStructureHandle()
    if forward_time is None:
        return ql.G2Process(*params, handle)
    p = ql.G2ForwardProcess(*params, handle)
    p.setForwardMeasureTime(forward_time)
    return p


def outputs(p):
    return {"size": p.size(), "factors": p.factors(),
            "initialValues": list(p.initialValues()),
            "phi": [v.attempt(lambda t=t: p.phi(t)) for t in PHI_TIMES],
            "shortRate": [p.shortRate(*s) for s in SHORT_RATE_POINTS],
            "drift": [list(p.drift(t, ql.Array(x))) for t, x in PROCESS_POINTS],
            "diffusion": [matrix(p.diffusion(t, ql.Array(x))) for t, x in PROCESS_POINTS],
            "expectation": [list(p.expectation(t0, ql.Array(x), dt)) for t0, x, dt in STEP_POINTS],
            "stdDeviation": [matrix(p.stdDeviation(t0, ql.Array(x), dt)) for t0, x, dt in NONZERO_STEPS],
            "covariance": [matrix(p.covariance(t0, ql.Array(x), dt)) for t0, x, dt in NONZERO_STEPS],
            "zeroStep": [matrix(p.stdDeviation(t0, ql.Array(x), dt)) for t0, x, dt in STEP_POINTS if dt == 0.0],
            "evolve": [list(p.evolve(t0, ql.Array(x), dt, ql.Array(dw))) for t0, x, dt, dw in EVOLVE_POINTS]}


def cases():
    out = []
    for k, params in enumerate(PARAMS):
        for curve_spec in (None, CURVE):
            for forward_time in [None] + FORWARD_TIMES:
                with v.case_settings(TODAY) as st:
                    curve = hw.make_curve(curve_spec) if curve_spec is not None else None
                    expected = outputs(make_process(params, curve, forward_time))
                    del expected["zeroStep"]
                    if k in LEFT_OUT:
                        continue
                    kind = "process" if forward_time is None else f"forward at T = {forward_time}"
                    out.append({"name": f"params {k} / {'curve' if curve_spec else 'no curve'} / {kind}",
                                "inputs": {"settings": st, "params": list(params), "curve": curve_spec,
                                           "forwardMeasureTime": forward_time, "phiTimes": PHI_TIMES,
                                           "shortRatePoints": SHORT_RATE_POINTS,
                                           "processPoints": [[t, list(x)] for t, x in PROCESS_POINTS],
                                           "stepPoints": [[t0, list(x), dt] for t0, x, dt in STEP_POINTS],
                                           "nonzeroSteps": [[t0, list(x), dt] for t0, x, dt in NONZERO_STEPS],
                                           "evolvePoints": [[t0, list(x), dt, list(dw)]
                                                            for t0, x, dt, dw in EVOLVE_POINTS]},
                                "expected": v.enc(expected)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/g2process.hpp", cases)
