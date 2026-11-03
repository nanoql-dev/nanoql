# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's JointStochasticProcess (ql/processes/jointstochasticprocess.hpp, .cpp), through the C++
harness's own derived process (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

The joint process of a geometric Brownian motion (100, drift 0.05, volatility 0.2), tools/vectors/gen/cxx/models/
equity/batesmodel.py's equity Heston process and an Ornstein-Uhlenbeck process (speed 0.5, volatility 0.1, from 0.02 to
0.03) with a cross-model correlation among the three: with the default factors and the correlation state-independent,
then state-dependent, then with three and two factors (QuantLib's rank-reduced square root): size, factors and initial
values; the drift, diffusion and numeraire at three states and two times; the expectation, covariance and standard
deviation at the states and times over two steps; apply at the states; and evolve from the states at the times over
the steps with two draws, each on a fresh process. Then the constructor's refusal of more factors than the size.

Left out: an evolve on a process that has evolved before at the same (t0, dt), which QuantLib serves from its
correlation cache where nanoql builds the step's correlation again
(tests/NanoQL.Tests/Conventions/JointStochasticProcessTests.cs).

A case holds the "process" (tools/cxx/src/processes/jointstochasticprocess.cpp's spec), the "times", "states", "steps",
"draws" and "increments" (apply's dx). The harness's calls: JointStochasticProcess::summary, ::drift, ::expectation,
::diffusion, ::covariance, ::stdDeviation, ::apply, ::evolve and ::numeraire. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Processes/JointStochasticProcessCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
GBM = [100.0, 0.05, 0.2]
OU = [0.5, 0.1, 0.02, 0.03]
# the order: the GBM's value, the Heston spot and variance, the OU's state; no correlation within a model
CORRELATION = [[0.0, 0.3, -0.2, 0.1],
               [0.3, 0.0, 0.0, -0.15],
               [-0.2, 0.0, 0.0, 0.05],
               [0.1, -0.15, 0.05, 0.0]]
STATES = [[100.0, 100.0, 0.04, 0.02], [90.0, 110.0, 0.09, 0.03], [110.0, 95.0, 0.0004, -0.01]]
TIMES = [0.0, 0.5]
STEPS = [0.05, 0.25]
DRAWS = [[0.3, -1.2, 0.7, -0.4], [-2.0, 0.5, 0.1, 1.3]]
INCREMENTS = [[1.5, -2.0, 0.01, 0.005], [-0.5, 3.0, -0.02, -0.01]]


def process_spec(state_dependent=False, factors=None):
    return {"gbm": GBM, "heston": bm.process_spec(bm.HESTON[0]), "ou": OU, "correlation": CORRELATION,
            "stateDependent": state_dependent, "factors": factors}


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def call(name, *args):
            return v.attempt(lambda: v.enc(H.call("JointStochasticProcess::" + name, *args)))

        for name, spec in (("default factors", process_spec()),
                           ("state-dependent correlation", process_spec(state_dependent=True)),
                           ("three factors", process_spec(factors=3)),
                           ("two factors", process_spec(factors=2))):
            summary = H.call("JointStochasticProcess::summary", spec)
            n = summary["factors"]
            points = [(t, x) for t in TIMES for x in STATES]
            steps = [(t, x, dt) for t, x in points for dt in STEPS]
            evolves = [(t, x, dt, dw[:n]) for t, x, dt in steps for dw in DRAWS]
            applies = [(x, dx) for x in STATES for dx in INCREMENTS]
            out.append({"name": name,
                        "inputs": {"settings": st, "process": spec, "times": TIMES, "states": STATES, "steps": STEPS,
                                   "draws": [dw[:n] for dw in DRAWS], "increments": INCREMENTS},
                        "expected": {"size": summary["size"], "factors": n,
                                     "initialValues": v.enc(summary["initialValues"]),
                                     "drift": [call("drift", spec, t, x) for t, x in points],
                                     "diffusion": [call("diffusion", spec, t, x) for t, x in points],
                                     "numeraire": [call("numeraire", spec, t, x) for t, x in points],
                                     "expectation": [call("expectation", spec, t, x, dt) for t, x, dt in steps],
                                     "covariance": [call("covariance", spec, t, x, dt) for t, x, dt in steps],
                                     "stdDeviation": [call("stdDeviation", spec, t, x, dt) for t, x, dt in steps],
                                     "apply": [call("apply", spec, x, dx) for x, dx in applies],
                                     "evolve": [call("evolve", spec, *e) for e in evolves]}})
        spec = process_spec(factors=5)
        out.append({"name": "more factors than the size", "inputs": {"settings": st, "process": spec},
                    "expected": {"size": v.attempt(lambda: H.call("JointStochasticProcess::summary", spec)["size"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/jointstochasticprocess.hpp", cases)
