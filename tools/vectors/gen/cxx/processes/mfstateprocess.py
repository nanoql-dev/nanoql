# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's MfStateProcess (ql/processes/mfstateprocess.hpp, .cpp), the Markov-functional model's state
process, which the wheel doesn't bind, each output computed by the C++ harness on a fresh process: on six processes
(no step times at a volatility of 1, and four step times with volatilities varying; at reversions 0, a reversion below
QuantLib's epsilon, 0.01, -0.02 and 0.3) x0, the drift, the expectation, the variance, the standard deviation and the
evolution at nine (t0, x0, dt) steps (an interval below QuantLib's epsilon, and steps within a piece, across pieces and
past the last), and the diffusion at nine times on the processes at a zero reversion. Then the constructor's
refusals: one volatility too many or too few, times not increasing, a negative volatility.

Left out, where nanoql's process is its equation's (the S-g1d plan's G1R6 (vi), G1R4 (ii)): the diffusion at a
reversion other than 0, which QuantLib's computes without the factor e^{at}; a process without step times at a
volatility other than 1, whose variance QuantLib's computes without the volatility's square; and reversions between
QuantLib's epsilon and 1e-2, where nanoql's variance takes the expm1 form. tests/NanoQL.Tests/Vectors/Cxx/Processes/
MfStateProcessCxxVectorTests.cs reads it back."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

TIMES = [1.0, 2.0, 5.0, 10.0]
VOLS = [0.8, 1.0, 1.2, 0.9, 1.1]
PROCESSES = [
    ("no step times, a zero reversion", 0.0, [], [1.0]),
    ("no step times, a reversion of 0.05", 0.05, [], [1.0]),
    ("four steps, a zero reversion", 0.0, TIMES, VOLS),
    ("four steps, a reversion below QuantLib's epsilon", 1e-17, TIMES, VOLS),
    ("four steps, a reversion of 0.01", 0.01, TIMES, VOLS),
    ("four steps, a reversion of -0.02", -0.02, TIMES, VOLS),
    ("four steps, a reversion of 0.3", 0.3, TIMES, VOLS),
]
# (t0, x0, dt)
STEPS = [(0.0, 0.0, 1e-17), (0.0, 0.0, 0.5), (0.0, 0.3, 1.0), (0.5, -0.2, 0.25), (0.7, 0.0, 3.0), (1.5, 0.1, 7.0),
         (5.0, 0.0, 5.0), (9.0, 0.4, 6.0), (12.0, 0.0, 3.0)]
DIFFUSION_TIMES = [0.0, 0.5, 1.0, 1.7, 2.0, 4.9, 5.0, 10.0, 20.0]
DW = [0.0, 0.5, -1.2, 2.0, -0.3, 1.1, -2.5, 0.8, 0.0]
REFUSALS = [
    ("one volatility too many", 0.01, [1.0, 2.0], [1.0, 1.0, 1.0, 1.0]),
    ("one volatility too few", 0.01, [1.0, 2.0], [1.0, 1.0]),
    ("times not increasing", 0.01, [1.0, 1.0], [1.0, 1.0, 1.0]),
    ("a negative volatility", 0.01, [1.0, 2.0], [1.0, -0.1, 1.0]),
]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 1, 2025))
        out = []
        for name, reversion, times, vols in PROCESSES:
            spec = {"reversion": reversion, "times": times, "vols": vols}

            def call(member, *args):
                return v.attempt(lambda: H.call("MfStateProcess::" + member, spec, *args))

            expected = {"x0": call("x0"),
                        "drift": [call("drift", t0, x0) for t0, x0, _ in STEPS],
                        "expectation": [call("expectation", t0, x0, dt) for t0, x0, dt in STEPS],
                        "variance": [call("variance", t0, x0, dt) for t0, x0, dt in STEPS],
                        "stdDeviation": [call("stdDeviation", t0, x0, dt) for t0, x0, dt in STEPS],
                        "evolve": [call("evolve", t0, x0, dt, dw) for (t0, x0, dt), dw in zip(STEPS, DW)]}
            if reversion == 0.0:
                expected["diffusion"] = [call("diffusion", t, 0.0) for t in DIFFUSION_TIMES]
            out.append({"name": name,
                        "inputs": {"settings": st, "process": spec, "steps": [list(s) for s in STEPS], "dw": DW,
                                   "diffusionTimes": DIFFUSION_TIMES},
                        "expected": expected})
        for name, reversion, times, vols in REFUSALS:
            spec = {"reversion": reversion, "times": times, "vols": vols}
            out.append({"name": "refused: " + name, "inputs": {"settings": st, "process": spec},
                        "expected": {"x0": v.attempt(lambda: H.call("MfStateProcess::x0", spec))}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/mfstateprocess.hpp", cases)
