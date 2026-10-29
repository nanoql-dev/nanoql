# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ExtendedOrnsteinUhlenbeckProcess, ExtOUWithJumpsProcess and KlugeExtOUProcess
(ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp, .cpp, extouwithjumpsprocess.hpp, .cpp,
klugeextouprocess.hpp, .cpp), on the evaluation date 18 December 2011.

- "extended" cases: the extended Ornstein-Uhlenbeck process on its mid-point rule (the wheel binds no other) with a
  constant, a linear and a sine level, at speeds from 0.5 to 7 and volatilities from 0.3 to 2: x0, speed and
  volatility; drift and diffusion at times and values; expectation, standard deviation and variance over steps; and
  evolve on draws.
- "jumps" cases: Kluge's process on it: size, factors, initial values, drift, the 2 x 2 diffusion, and evolve on draws
  with and without a jump in the step (QuantLib's one jump at most, XER5 (xxxii)).
- "kluge" cases: the Kluge-OU process: size, factors, initial values, drift and evolve.

The outputs nanoql fixes are left out (the S-xeq plan's §2.8): the Kluge-OU process's diffusion, whose gas row QuantLib
writes past its matrix (XER5 (xxviii)). The trapezoidal and Gauss-Lobatto rules, which the wheel doesn't bind, are
tools/vectors/gen/cxx/experimental/processes/extendedornsteinuhlenbeckprocess.py's. Nothing is fitted, so no output is
filtered.

A level is {"type": "constant", "b"}, {"type": "linear", "c0", "c1"} (c0 + c1 t) or {"type": "sine", "amplitude",
"shift"} (shift + amplitude sin t). The wheel's calls, by QuantLib-SWIG 1.44's stochasticprocess.i:
ExtendedOrnsteinUhlenbeckProcess(speed, sigma, x0, b), ExtOUWithJumpsProcess(process, y0, beta, jumpIntensity, eta),
KlugeExtOUProcess(rho, kluge, extOU), and their StochasticProcess members. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Processes/ExtendedOrnsteinUhlenbeckProcessVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

R = ql.Date(18, ql.December, 2011)
POINTS = [(0.0, 0.5), (0.25, 3.0), (1.0, -1.0), (2.5, 2.2)]
STEPS = [(0.0, 3.0, 0.01), (0.5, 2.0, 0.25), (1.0, -0.5, 1.0), (3.0, 4.0, 0.002)]
DRAWS = [-1.3, 0.0, 0.7, 2.1]


def level(spec):
    if spec["type"] == "constant":
        b = spec["b"]
        return lambda t: b
    if spec["type"] == "linear":
        c0, c1 = spec["c0"], spec["c1"]
        return lambda t: c0 + c1 * t
    a, s = spec["amplitude"], spec["shift"]
    return lambda t: s + a * math.sin(t)


def extended(spec):
    return ql.ExtendedOrnsteinUhlenbeckProcess(spec["speed"], spec["sigma"], spec["x0"], level(spec["level"]))


def extended_outputs(p):
    return {"x0": p.x0(),
            "drift": [p.drift(t, x) for t, x in POINTS],
            "diffusion": [p.diffusion(t, x) for t, x in POINTS],
            "expectation": [p.expectation(t, x, dt) for t, x, dt in STEPS],
            "stdDeviation": [p.stdDeviation(t, x, dt) for t, x, dt in STEPS],
            "variance": [p.variance(t, x, dt) for t, x, dt in STEPS],
            "evolve": [p.evolve(t, x, dt, w) for (t, x, dt) in STEPS for w in DRAWS]}


JUMP_STATES = [[3.0, 0.0], [2.5, 0.4], [-0.5, 1.2]]
# (t0, dt, dw): the second draw times the jump, the third sizes it; 2.5 makes a jump certain in the step
JUMP_DRAWS = [(0.0, 0.01, [0.3, -1.0, 0.2]), (0.5, 0.1, [-0.7, 2.5, -0.4]), (1.0, 0.25, [1.1, 2.5, 1.5]),
              (2.0, 0.02, [0.0, 0.1, 0.0])]


def arr(a):
    return [a[i] for i in range(len(a))]


def matrix(m):
    return [[m[i][j] for j in range(m.columns())] for i in range(m.rows())]


def cases():
    st = v.settings(R)
    out = []
    specs = [
        ("a constant level", {"speed": 1.0, "sigma": 2.0, "x0": 3.0, "level": {"type": "constant", "b": 3.0}}),
        ("a linear level", {"speed": 2.5, "sigma": 0.7, "x0": 0.0, "level": {"type": "linear", "c0": 1.0, "c1": 0.5}}),
        ("a sine level", {"speed": 7.0, "sigma": 1.4, "x0": 0.2, "level": {"type": "sine", "amplitude": 0.8,
                                                                           "shift": 1.0}}),
        ("a slow speed", {"speed": 0.5, "sigma": 0.3, "x0": 1.5, "level": {"type": "linear", "c0": 2.0,
                                                                           "c1": -0.2}}),
    ]
    for name, spec in specs:
        out.append({"name": f"extended: {name}", "inputs": {"settings": st, "kind": "extended", "process": spec},
                    "expected": extended_outputs(extended(spec))})
    for name, spec, jumps in [
            ("Kluge's swing case", specs[0][1], {"y0": 0.0, "beta": 5.0, "jumpIntensity": 1.0, "eta": 2.0}),
            ("frequent small jumps", specs[2][1], {"y0": 0.3, "beta": 200.0, "jumpIntensity": 4.0, "eta": 5.0})]:
        p = ql.ExtOUWithJumpsProcess(extended(spec), jumps["y0"], jumps["beta"], jumps["jumpIntensity"],
                                     jumps["eta"])
        expected = {"size": p.size(), "factors": p.factors(), "initialValues": arr(p.initialValues()),
                    "drift": [arr(p.drift(t, ql.Array(x))) for t, x in zip((0.0, 0.5, 1.0), JUMP_STATES)],
                    "diffusion": [matrix(p.diffusion(t, ql.Array(x))) for t, x in zip((0.0, 0.5, 1.0), JUMP_STATES)],
                    "evolve": [arr(p.evolve(t, ql.Array(x), dt, ql.Array(w)))
                               for x in JUMP_STATES for t, dt, w in JUMP_DRAWS]}
        out.append({"name": f"jumps: {name}", "inputs": {"settings": st, "kind": "jumps", "process": spec,
                                                         "jumps": jumps}, "expected": expected})
    gas = {"speed": 4.45, "sigma": math.sqrt(1.3), "x0": 0.0, "level": {"type": "constant", "b": 0.0}}
    for name, spec, jumps, rho in [
            ("the VPP case", {"speed": 7.0, "sigma": 1.4, "x0": 0.0, "level": {"type": "constant", "b": 0.0}},
             {"y0": 0.0, "beta": 200.0, "jumpIntensity": 4.0, "eta": 5.0}, 0.7),
            ("the spread case", specs[0][1], {"y0": 0.0, "beta": 5.0, "jumpIntensity": 1.0, "eta": 2.0}, -0.5)]:
        kluge = ql.ExtOUWithJumpsProcess(extended(spec), jumps["y0"], jumps["beta"], jumps["jumpIntensity"],
                                         jumps["eta"])
        p = ql.KlugeExtOUProcess(rho, kluge, extended(gas))
        states = [x + [u] for x, u in zip(JUMP_STATES, (0.0, 0.3, -0.4))]
        draws = [(t, dt, w + [w4]) for (t, dt, w), w4 in zip(JUMP_DRAWS, (0.5, -1.2, 0.0, 2.0))]
        expected = {"size": p.size(), "factors": p.factors(), "initialValues": arr(p.initialValues()),
                    "drift": [arr(p.drift(t, ql.Array(x))) for t, x in zip((0.0, 0.5, 1.0), states)],
                    "evolve": [arr(p.evolve(t, ql.Array(x), dt, ql.Array(w))) for x in states for t, dt, w in draws]}
        out.append({"name": f"kluge: {name}", "inputs": {"settings": st, "kind": "kluge", "process": spec,
                                                         "jumps": jumps, "gas": gas, "rho": rho},
                    "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/processes/extendedornsteinuhlenbeckprocess.hpp", cases)
