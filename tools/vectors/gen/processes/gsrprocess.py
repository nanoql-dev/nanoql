# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's GsrProcess (ql/processes/gsrprocess.hpp, .cpp), through the members the wheel binds.

Seven processes: a constant reversion with no step times, one with three step times and piecewise volatilities, and
piecewise reversions on the same times, among them a zero reversion (QuantLib's zero-reversion formulas, which nanoql
keeps at a = 0 exactly), a negative one and a fast one, at forward measure times from 10 to 60. For each: x0(), the
drift and diffusion at eight (t, x) points, the expectation, standard deviation and variance at eight (t0, x0, dt)
steps (a zero step and steps across the step times among them), evolve at five points and apply at three, sigma(t),
reversion(t) and y(t) at seven times, and G(t, T) at eight pairs. Every time stays at or below the forward measure
time, which the process checks. The reversions stay at 0 or at |a| >= 1e-2, where nanoql keeps QuantLib's formulas;
between them nanoql's forms are cancellation-free (the S-g1d plan's G1R4 (i);
tests/NanoQL.Tests/Divergences/GsrSmallReversionTests.cs).

QuantLib 1.44's setForwardMeasureTime moves the process's time and leaves its core's, so its later outputs mix two
times; nanoql's WithForwardMeasureTime moves both, as QuantLib's master does after the pin (#2842). So each process is
also checked at another forward measure time ("atOtherTime"): the expectation, variance, drift, y and G of the wheel's
process built at that time, the value #2842 gives (the outputs "atOtherTimeExpectation" and the like); QuantLib 1.44's mixed outputs are recorded in
tests/NanoQL.Tests/Divergences/GsrForwardMeasureTimeTests.cs. The refusals: a time past the forward measure time in
the expectation, the variance, the diffusion and y; G at w < t and past T; the constructor's three count and order
checks.

The wheel's calls, by QuantLib-SWIG's gsr.i: ql.GsrProcess(Array times, Array vols, Array reversions, T), and x0,
drift, diffusion, expectation, stdDeviation, variance, evolve, apply, sigma, reversion, y and G, each forwarding to the
C++ member of that name. Each case's inputs hold its settings, "times", "vols", "reversions", "T", the other time
"otherT" and the points. Read back by tests/NanoQL.Tests/Vectors/Processes/GsrProcessVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402

TODAY = ql.Date(15, ql.January, 2025)

PROCESSES = [
    ("constant", [], [0.01], [0.01], 60.0, 30.0),
    ("constant, fast", [], [0.012], [0.3], 20.0, 40.0),
    ("constant, zero reversion", [], [0.009], [0.0], 30.0, 15.0),
    ("piecewise volatilities", [1.0, 2.0, 5.0], [0.008, 0.009, 0.0105, 0.012], [0.03], 60.0, 10.0),
    ("piecewise reversions", [1.0, 2.0, 5.0], [0.008, 0.009, 0.0105, 0.012], [0.02, 0.05, -0.02, 0.04], 40.0, 12.0),
    ("piecewise, a zero reversion", [0.5, 3.0], [0.006, 0.01, 0.008], [0.0, 0.025, 0.0], 25.0, 50.0),
    ("testGsrProcess's piecewise", [1.0, 2.0], [0.2, 0.3, 0.4], [0.50, 0.80, 1.30], 10.0, 60.0),
]

PROCESS_POINTS = [(0.0, 0.0), (0.25, 0.03), (1.0, 0.05), (2.5, -0.01), (5.0, 0.0), (7.5, 0.1), (3.0, 0.02),
                  (9.5, -0.035)]
STEP_POINTS = [(0.0, 0.0, 0.25), (0.5, 0.03, 0.5), (1.0, 0.05, 1.0), (2.0, -0.01, 0.1), (5.0, 0.02, 0.0),
               (0.0, 0.0, 6.0), (1.5, 0.03, 4.0), (0.1, -0.02, 9.8)]
EVOLVE_POINTS = [(0.0, 0.0, 0.25, 0.3), (1.0, 0.05, 0.5, -1.2), (2.0, 0.0, 1.0, 2.5), (5.0, -0.02, 0.1, 0.0),
                 (0.5, 0.03, 0.01, -0.7)]
APPLY_POINTS = [(0.03, 0.001), (0.0, -0.02), (-0.01, 0.05)]
TIMES = [0.0, 0.5, 1.0, 1.5, 2.0, 4.0, 9.0]
G_POINTS = [(0.0, 1.0), (0.0, 10.0), (0.5, 3.0), (1.0, 2.0), (2.0, 2.0), (1.5, 7.0), (4.0, 9.5), (0.25, 0.75)]


def make_process(times, vols, reversions, T):
    return ql.GsrProcess(ql.Array(times), ql.Array(vols), ql.Array(reversions), T)


def outputs(p):
    return {"x0": p.x0(),
            "drift": [p.drift(t, x) for t, x in PROCESS_POINTS],
            "diffusion": [p.diffusion(t, x) for t, x in PROCESS_POINTS],
            "expectation": [p.expectation(*s) for s in STEP_POINTS],
            "stdDeviation": [p.stdDeviation(*s) for s in STEP_POINTS],
            "variance": [p.variance(*s) for s in STEP_POINTS],
            "evolve": [p.evolve(*e) for e in EVOLVE_POINTS],
            "apply": [p.apply(*a) for a in APPLY_POINTS],
            "sigma": [p.sigma(t) for t in TIMES],
            "reversion": [p.reversion(t) for t in TIMES],
            "y": [p.y(t) for t in TIMES],
            "G": [p.G(t, w, 0.0) for t, w in G_POINTS]}


def at_other_time(p):
    return {"expectation": [p.expectation(*s) for s in STEP_POINTS],
            "variance": [p.variance(*s) for s in STEP_POINTS],
            "drift": [p.drift(t, x) for t, x in PROCESS_POINTS],
            "y": [p.y(t) for t in TIMES],
            "G": [p.G(t, w, 0.0) for t, w in G_POINTS]}


def process_cases():
    out = []
    for name, times, vols, reversions, T, other in PROCESSES:
        with v.case_settings(TODAY) as st:
            expected = outputs(make_process(times, vols, reversions, T))
            for key, value in at_other_time(make_process(times, vols, reversions, other)).items():
                expected["atOtherTime" + key[0].upper() + key[1:]] = value
            out.append({"name": name,
                        "inputs": {"settings": st, "times": times, "vols": vols, "reversions": reversions, "T": T,
                                   "otherT": other, "processPoints": PROCESS_POINTS, "stepPoints": STEP_POINTS,
                                   "evolvePoints": EVOLVE_POINTS, "applyPoints": APPLY_POINTS, "pointTimes": TIMES,
                                   "gPoints": G_POINTS},
                        "expected": v.enc(expected)})
    return out


def refusal_cases():
    """(name, process args, member, its arguments)."""
    base = ([1.0, 2.0], [0.01, 0.012, 0.009], [0.03], 10.0)
    rows = [
        ("expectation past T", base, "expectation", [9.0, 0.0, 1.5]),
        ("expectation at a negative time", base, "expectation", [0.0, 0.0, -0.5]),
        ("variance past T", base, "variance", [9.5, 0.0, 1.0]),
        ("diffusion past T", base, "diffusion", [10.5, 0.0]),
        ("y past T", base, "y", [11.0]),
        ("G at w below t", base, "G", [3.0, 2.0, 0.0]),
        ("G past T", base, "G", [1.0, 12.0, 0.0]),
        ("G at a negative t", base, "G", [-1.0, 2.0, 0.0]),
        ("too few volatilities", ([1.0, 2.0], [0.01, 0.012], [0.03], 10.0), None, None),
        ("reversions of the wrong count", ([1.0, 2.0], [0.01, 0.012, 0.009], [0.03, 0.04], 10.0), None, None),
        ("times not increasing", ([2.0, 1.0], [0.01, 0.012, 0.009], [0.03], 10.0), None, None),
    ]
    out = []
    for name, args, member, call in rows:
        with v.case_settings(TODAY) as st:
            if member is None:
                result = v.attempt(lambda: make_process(*args).x0())
            else:
                p = make_process(*args)
                result = v.attempt(lambda: getattr(p, member)(*call))
            out.append({"name": "refused / " + name,
                        "inputs": {"settings": st, "times": args[0], "vols": args[1], "reversions": args[2],
                                   "T": args[3], "member": member, "arguments": call},
                        "expected": {"result": result}})
    return out


def cases():
    out = process_cases() + refusal_cases()
    for c in out:
        if c["name"].startswith("refused / "):
            assert c["expected"]["result"] == v.THROWS, c["name"]
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/processes/gsrprocess.hpp", cases)
