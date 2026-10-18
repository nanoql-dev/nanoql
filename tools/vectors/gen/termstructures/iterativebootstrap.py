# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's IterativeBootstrap (ql/termstructures/iterativebootstrap.hpp), its nine parameters
(accuracy, minValue, maxValue, maxAttempts, maxFactor, minFactor, dontThrow, dontThrowSteps, maxEvaluations), on the
piecewise curves of termstructures/yield/piecewiseyieldcurve.py: 32 curves in 3 cases, one per helper set, on the
evaluation date 15 January 2024, each curve with a fixed reference date two TARGET days later.

- On 3 deposits and 9 swaps (20 curves): QuantLib's default bootstrap, and all nine parameters at their defaults; the
  cubic zero curve at accuracies 1e-8 (where the nodes keep the last value the solver evaluated, up to 7.8e-9 from the
  root it returns) and 1e-17 (which QuantLib refuses, its passes converging no closer than 1e-15, and with
  dontThrow keeps as it is after its last pass), the log-linear discount curve at 1e-3, the linear zero curve at 1e-6,
  the monotonic log-cubic discount curve at 1e-14; bounds: a minimum of -0.1 on the log-linear discount curve, which
  QuantLib refuses through its log interpolation's refusal of a negative discount ("invalid value (-0.1) at index
  1"), the zero curve's bracket [-0.1, 0.01], which can't bracket its first node, and [-0.05, 0.2], which can, a
  maximum of 0.3 on the backward-flat forward curve, the discount curve's bracket [0.5, 1.2], which can't bracket its
  10th node, and [-0.02, 0.3] on the simple-zero curve; maxEvaluations of 1 on the log-linear discount curve and of 8
  on the cubic zero curve, which QuantLib refuses; maxFactor 0.5 and minFactor 0.99, which QuantLib refuses when the
  curve is built; dontThrow with maxEvaluations 3 on the cubic zero curve, whose fallback's own refusal ("Expected xMin
  to be less than xMax") escapes; and two attempts at accuracy 1e-10 on Kruger's log-cubic discount curve.
- On the same helpers with the first deposit quoted at -500%, which no default bracket holds (11 curves): the
  log-linear discount curve refused; with three attempts, which widen the bracket until it holds, by the default
  factors and by 4 and 3; with three attempts and factors of 1, which don't widen it, refused; with dontThrow, on 10,
  50 and 0 steps, its first node the fallback of least error; the linear forward curve with three attempts and
  dontThrow; the cubic zero curve refused, refused after three attempts, and kept with dontThrow.
- On 3 deposits and 9 swaps with pillars at maturity and at custom dates (one curve): the log-linear discount curve at
  accuracy 1e-8, a local interpolation whose custom pillars require the passes that run the finite-difference
  Newton-safe solver.

Each curve's outputs are whether it builds, QuantLib's refusals recorded as its throw, its nodes (each [date, value])
and its nodes' times, as arrays of one element per curve, a refused curve's empty. The wheel's IterativeBootstrap is
write-only, so each curve's spec records the parameters passed. The specs are termstructures/yield/
piecewiseyieldcurve.py's: each case's inputs hold its settings, "helpers" (helper specs) and "curves" (curve specs,
each with its "bootstrap"). Read back by tests/NanoQL.Tests/Vectors/TermStructures/IterativeBootstrapVectorTests.cs."""
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
    "wheel_piecewiseyieldcurve", os.path.join(v.GEN_ROOT, "termstructures", "yield", "piecewiseyieldcurve.py"))
pw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(pw)
rh = pw.rh

TODAY = pw.EVALUATION_DATES[0]
B = pw.bootstrap_spec
A360, A365 = pw.A360, pw.A365
LL = ("Discount", pw.LOG_LINEAR)
CZ = ("ZeroYield", pw.KRUGER_CUBIC)
LZ = ("ZeroYield", pw.LINEAR)


def helpers(failing=False):
    """3 deposits and 9 swaps; with the first deposit quoted at -500% when failing."""
    out = [rh.deposit_index(-5.0 if failing else 0.0458, rh.E1M)] + pw.SHORT_DEPOSITS[1:]
    return out + [rh.swap_tenor(rate, (n, ql.Years)) for n, rate in pw.SWAP_YEARS]


ROWS = [
    ("helpers", helpers(), [
        (LL, A360, None),
        (CZ, A360, B()),
        (CZ, A360, B(accuracy=1e-8)),
        (CZ, A360, B(accuracy=1e-17)),
        (CZ, A360, B(accuracy=1e-17, dont_throw=True)),
        (LL, A365, B(accuracy=1e-3)),
        (LZ, A360, B(accuracy=1e-6)),
        (("Discount", pw.MONOTONIC_LOG_CUBIC), A360, B(accuracy=1e-14)),
        (LL, A360, B(min_value=-0.1)),
        (LZ, A360, B(min_value=-0.1, max_value=0.01)),
        (LZ, A365, B(min_value=-0.05, max_value=0.2)),
        (("ForwardRate", pw.BACKWARD_FLAT), A360, B(max_value=0.3)),
        (LL, A360, B(min_value=0.5, max_value=1.2)),
        (("SimpleZeroYield", pw.LINEAR), A360, B(min_value=-0.02, max_value=0.3)),
        (LL, A360, B(max_evaluations=1)),
        (CZ, A360, B(max_evaluations=8)),
        (LL, A360, B(max_factor=0.5)),
        (LL, A360, B(min_factor=0.99)),
        (CZ, A360, B(dont_throw=True, max_evaluations=3)),
        (("Discount", pw.KRUGER_LOG), A365, B(accuracy=1e-10, max_attempts=2)),
    ]),
    ("a failing quote", helpers(failing=True), [
        (LL, A360, None),
        (LL, A360, B(max_attempts=3)),
        (LL, A360, B(max_attempts=3, max_factor=4.0, min_factor=3.0)),
        (LL, A360, B(max_attempts=3, max_factor=1.0, min_factor=1.0)),
        (LL, A360, B(dont_throw=True)),
        (LL, A360, B(dont_throw=True, dont_throw_steps=50)),
        (LL, A360, B(dont_throw=True, dont_throw_steps=0)),
        (("ForwardRate", pw.LINEAR), A360, B(max_attempts=3, dont_throw=True)),
        (CZ, A360, None),
        (CZ, A360, B(max_attempts=3)),
        (CZ, A360, B(dont_throw=True)),
    ]),
    ("pillared swaps", pw.pillared_swaps(TODAY), [
        (LL, A360, B(accuracy=1e-8)),
    ]),
]


def cases():
    out = []
    for name, helper_specs, rows in ROWS:
        with v.case_settings(TODAY) as st:
            specs = [pw.curve_spec(traits, interpolator, dc, TODAY, bootstrap=b)
                     for (traits, interpolator), dc, b in rows]
            expected = {"built": [], "nodes": [], "times": []}
            for spec in specs:
                hs = [rh.make_helper(h) for h in helper_specs]
                try:
                    # the wheel builds the curve's bootstrap with it, and bootstraps on its first read
                    curve = pw.make_curve(spec, hs)
                    nodes = curve.nodes()
                except RuntimeError:
                    expected["built"].append(dict(v.THROWS))
                    expected["nodes"].append([])
                    expected["times"].append([])
                else:
                    expected["built"].append(True)
                    expected["nodes"].append([[d.serialNumber(), x] for d, x in nodes])
                    expected["times"].append(list(curve.times()))
            out.append({"name": f"{TODAY.ISO()} {name}",
                        "inputs": {"settings": st, "helpers": helper_specs, "curves": specs},
                        "expected": expected})
    check_rows(out)
    return out


def check_rows(out):
    refused = {"helpers": [3, 8, 9, 12, 14, 15, 16, 17, 18], "a failing quote": [0, 3, 8, 9],
               "pillared swaps": []}
    assert sum(len(c["expected"]["built"]) for c in out) == 32
    for c in out:
        got = [k for k, b in enumerate(c["expected"]["built"]) if b is not True]
        assert got == refused[c["name"][11:]], (c["name"], got)


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/iterativebootstrap.hpp", cases)
