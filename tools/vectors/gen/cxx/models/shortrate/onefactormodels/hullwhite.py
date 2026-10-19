# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's HullWhite (ql/models/shortrate/onefactormodels/hullwhite.hpp, .cpp) that the
wheel doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4).

Six models (tools/cxx/src/models/modelspec.hpp's specs, the curves those of tools/vectors/gen/cxx/discretizedasset.py):
flat and log-linear discount curves, positive and negative rates, mean reversion from 1e-9 (below sqrt(QL_EPSILON),
QuantLib's small-reversion branches) to 2, and the first model after setParams. For each: Vasicek's inspectors a(),
b(), sigma(), lambda() and r0() (the curve's zero rate at 0); dynamics()'s variable(t, r) and shortRate(t, x) at four
points each, through the analytical fitting parameter, and its process's x0(), drift and diffusion at three points
and expectation, stdDeviation and variance at three steps; the five-argument discountBondOption, called through
AffineModel, at eight points (a bond starting at the option's maturity, later, and calls and puts in and out of the
money); the model's constraint() (test, upperBound and lowerBound) at four parameter arrays; and the fitted tree(grid),
on a grid per model (uniform, mandatory times with steps, and a step under the dx floor's threshold): every column's
size, every node's underlying, every step's discounts, which carry the fitted values (exp(-(x + phi(t_i)) dt_i)), and
every step's state prices, the last step's among them; the first model's tree also every node's descendants and
probabilities. Then FixedReversion(), and the refusals: trees on a model whose sigma setParams sets to 0, its
reversion to NaN (NaN probabilities, nanoql's MD5) or its sigma negative (the process's), and on a grid past the
discount curve's last date; and the five-argument option at a negative sigma (blackFormula's).

Read back by tests/NanoQL.Tests/Vectors/Cxx/Models/ShortRate/OneFactorModels/HullWhiteCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/ModelInputs.cs."""
import importlib.util
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location("cxx_discretizedasset",
                                               os.path.join(v.GEN_ROOT, "cxx", "discretizedasset.py"))
da = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(da)

CALL, PUT = 1, -1

FIRST = da.hull_white(da.flat(0.04), 0.1, 0.01)
MODELS = [
    ("flat 4%", FIRST, {"end": 5.0, "steps": 10}),
    ("flat 3% Actual/360 annual", da.hull_white(da.flat(0.03, da.A360, da.COMPOUNDED, da.ANNUAL), 0.03, 0.012),
     {"times": [0.5, 1.0, 2.0, 3.0], "steps": 9}),
    ("upward discount curve", da.hull_white(da.UPWARD, 0.2, 0.005), {"times": [1.0, 2.5, 4.0, 6.0], "steps": 12}),
    ("reversion 1e-9", da.hull_white(da.flat(0.02), 1e-9, 0.01), {"end": 3.0, "steps": 6}),
    ("negative rates, reversion 2, a floored step", da.hull_white(da.NEGATIVE, 2.0, 0.02),
     {"times": [0.25, 0.252, 1.0, 2.0]}),
    ("flat 4% after setParams", dict(FIRST, params=[0.15, 0.02]), {"end": 4.0, "steps": 8}),
]

VARIABLE_POINTS = [(0.0, 0.04), (0.5, 0.03), (2.0, -0.01), (4.5, 0.06)]
SHORT_RATE_POINTS = [(0.0, 0.0), (0.5, 0.01), (2.0, -0.02), (4.5, 0.005)]
PROCESS_POINTS = [(0.0, 0.0), (1.0, 0.02), (3.0, -0.015)]
STEP_POINTS = [(0.0, 0.0, 0.25), (1.0, 0.02, 0.5), (2.5, -0.01, 2.0)]
OPTIONS = [(CALL, 0.95, 1.0, 1.0, 3.0), (PUT, 0.95, 1.0, 1.0, 3.0), (CALL, 0.9, 1.0, 1.5, 3.0),
           (PUT, 0.9, 1.0, 1.5, 3.0), (CALL, 1.02, 0.5, 2.0, 2.5), (PUT, 0.7, 2.0, 2.5, 5.0),
           (CALL, 0.5, 0.25, 0.5, 4.0), (PUT, 1.1, 3.0, 3.0, 3.5)]


def cases():
    with cxx.connect() as H:
        st = H.settings(da.REFERENCE)
        out = []

        def lattice_of(model, grid):
            return da.lattice(model, grid)

        for k, (name, model, grid) in enumerate(MODELS):
            m = H.call("Vasicek::a", model), H.call("Vasicek::sigma", model)
            arrays = [[m[0], m[1]], [-m[0], m[1]], [m[0], 0.0], [m[0], math.nan]]
            lat = lattice_of(model, grid)
            columns = H.call("TimeGrid::size", grid)
            sizes = [H.call("OneFactorModel::ShortRateTree::size", lat, i) for i in range(columns)]
            expected = {
                "a": m[0], "b": H.call("Vasicek::b", model), "sigma": m[1], "lambda": H.call("Vasicek::lambda", model),
                "r0": H.call("Vasicek::r0", model),
                "variable": [H.call("OneFactorModel::dynamics()->variable", model, t, r) for t, r in VARIABLE_POINTS],
                "shortRate": [H.call("OneFactorModel::dynamics()->shortRate", model, t, x)
                              for t, x in SHORT_RATE_POINTS],
                "processX0": H.call("OneFactorModel::dynamics()->process()->x0", model),
                "drift": [H.call("OneFactorModel::dynamics()->process()->drift", model, t, x)
                          for t, x in PROCESS_POINTS],
                "diffusion": [H.call("OneFactorModel::dynamics()->process()->diffusion", model, t, x)
                              for t, x in PROCESS_POINTS],
                "expectation": [H.call("OneFactorModel::dynamics()->process()->expectation", model, *p)
                                for p in STEP_POINTS],
                "stdDeviation": [H.call("OneFactorModel::dynamics()->process()->stdDeviation", model, *p)
                                 for p in STEP_POINTS],
                "variance": [H.call("OneFactorModel::dynamics()->process()->variance", model, *p)
                             for p in STEP_POINTS],
                "discountBondOption": [H.call("AffineModel::discountBondOption(Option::Type,Real,Time,Time,Time)",
                                              model, *o) for o in OPTIONS],
                "constraintTest": [H.call("CalibratedModel::constraint()->test", model, p) for p in arrays],
                "upperBound": [H.call("CalibratedModel::constraint()->upperBound", model, p) for p in arrays],
                "lowerBound": [H.call("CalibratedModel::constraint()->lowerBound", model, p) for p in arrays],
                "treeSize": sizes,
                "treeUnderlying": [[H.call("OneFactorModel::ShortRateTree::underlying", lat, i, j)
                                    for j in range(sizes[i])] for i in range(columns)],
                "treeDiscount": [[H.call("OneFactorModel::ShortRateTree::discount", lat, i, j)
                                  for j in range(sizes[i])] for i in range(columns - 1)],
                "statePrices": [H.call("TreeLattice::statePrices", lat, i) for i in range(columns)],
            }
            if k == 0:
                expected["treeDescendant"] = [[[H.call("OneFactorModel::ShortRateTree::descendant", lat, i, j, b)
                                                for b in range(3)] for j in range(sizes[i])]
                                              for i in range(columns - 1)]
                expected["treeProbability"] = [[[H.call("OneFactorModel::ShortRateTree::probability", lat, i, j, b)
                                                 for b in range(3)] for j in range(sizes[i])]
                                               for i in range(columns - 1)]
            out.append({"name": name,
                        "inputs": {"settings": st, "model": v.enc(model), "timeGrid": grid,
                                   "variablePoints": VARIABLE_POINTS, "shortRatePoints": SHORT_RATE_POINTS,
                                   "processPoints": PROCESS_POINTS, "stepPoints": STEP_POINTS, "options": OPTIONS,
                                   "constraintParams": v.enc(arrays)},
                        "expected": expected})

        out.append({"name": "FixedReversion", "inputs": {"settings": st},
                    "expected": {"fixedReversion": H.call("HullWhite::FixedReversion")}})

        grid = {"end": 2.0, "steps": 4}
        refused = [
            ("a tree at sigma 0", dict(FIRST, params=[0.1, 0.0]), grid),
            ("a tree at a NaN reversion", dict(FIRST, params=[math.nan, 0.01]), grid),
            ("a tree at a negative sigma", dict(FIRST, params=[0.1, -0.01]), grid),
            ("a tree past the curve's last date", da.hull_white(da.UPWARD, 0.2, 0.005), {"end": 12.0, "steps": 6}),
        ]
        for name, model, g in refused:
            lat = lattice_of(model, g)
            out.append({"name": "refused: " + name,
                        "inputs": {"settings": st, "model": v.enc(model), "timeGrid": g},
                        "expected": {"treeSize": [v.attempt(
                            lambda: H.call("OneFactorModel::ShortRateTree::size", lat, 0))]}})
        model = dict(FIRST, params=[0.1, -0.01])
        out.append({"name": "refused: the five-argument option at a negative sigma",
                    "inputs": {"settings": st, "model": model, "options": OPTIONS[:1]},
                    "expected": {"discountBondOption": [v.attempt(lambda: H.call(
                        "AffineModel::discountBondOption(Option::Type,Real,Time,Time,Time)", model, *OPTIONS[0]))]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/hullwhite.hpp", cases)
