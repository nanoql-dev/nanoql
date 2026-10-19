# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Vasicek (ql/models/shortrate/onefactormodels/vasicek.hpp, .cpp) that the
wheel doesn't bind, and its unfitted tree (ql/models/shortrate/onefactormodel.hpp's OneFactorModel::tree), each
output computed by the C++ harness (docs/porting-playbook.md §9.4).

Five models: QuantLib's defaults, a mean reversion of 1e-9 (below sqrt(QL_EPSILON)), a large one with a risk premium,
negative rates, level and premium, and the defaults after setParams. For each: a(), b(), sigma(), lambda() and r0();
dynamics()'s variable(t, r) and shortRate(t, x) at four points each, and its process's x0(), drift and diffusion at
three points and expectation, stdDeviation and variance at three steps; AffineModel's five-argument
discountBondOption at five points, whose default ignores the bond's start; the model's constraint() (test,
upperBound and lowerBound) at four parameter arrays; and the unfitted tree(grid) (OneFactorModel's), on a grid per
model: every column's size, every step's discounts and every step's state prices, which the unfitted tree computes
when asked, the last step's among them. Then a tree at a zero sigma, which setParams sets (NaN probabilities,
nanoql's MD5).

Read back by tests/NanoQL.Tests/Vectors/Cxx/Models/ShortRate/OneFactorModels/VasicekCxxVectorTests.cs, with
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

_spec = importlib.util.spec_from_file_location("cxx_hullwhite", os.path.join(
    v.GEN_ROOT, "cxx", "models", "shortrate", "onefactormodels", "hullwhite.py"))
hw = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(hw)
da = hw.da


def vasicek(r0=0.05, a=0.1, b=0.05, sigma=0.01, lam=0.0, params=None):
    return {"type": "Vasicek", "r0": r0, "a": a, "b": b, "sigma": sigma, "lambda": lam, "params": params}


MODELS = [
    ("QuantLib's defaults", vasicek(), {"end": 5.0, "steps": 10}),
    ("reversion 1e-9", vasicek(0.03, 1e-9, 0.04, 0.012), {"end": 3.0, "steps": 6}),
    ("large reversion, a risk premium", vasicek(0.02, 3.0, 0.06, 0.02, 0.1), {"times": [0.5, 1.0, 2.0], "steps": 8}),
    ("negative rate, level and premium", vasicek(-0.01, 0.2, -0.005, 0.008, -0.2),
     {"times": [0.25, 0.252, 1.0, 2.0]}),
    ("defaults after setParams", vasicek(params=[0.2, 0.03, 0.015, 0.05]), {"end": 2.0, "steps": 8}),
]

OPTIONS = [(hw.CALL, 0.95, 1.0, 1.0, 3.0), (hw.PUT, 0.95, 1.0, 1.5, 3.0), (hw.CALL, 0.9, 0.5, 2.0, 2.5),
           (hw.PUT, 0.7, 2.0, 2.5, 5.0), (hw.CALL, 1.0, 0.0, 0.5, 1.0)]


def cases():
    with cxx.connect() as H:
        st = H.settings(da.REFERENCE)
        out = []
        for name, model, grid in MODELS:
            p = [H.call("Vasicek::a", model), H.call("Vasicek::b", model), H.call("Vasicek::sigma", model),
                 H.call("Vasicek::lambda", model)]
            arrays = [p, [p[0], p[1], -p[2], p[3]], [0.0, -1.0, 0.01, 2.0], [p[0], math.nan, p[2], p[3]]]
            lat = da.lattice(model, grid)
            columns = H.call("TimeGrid::size", grid)
            sizes = [H.call("OneFactorModel::ShortRateTree::size", lat, i) for i in range(columns)]
            out.append({"name": name,
                        "inputs": {"settings": st, "model": model, "timeGrid": grid,
                                   "variablePoints": hw.VARIABLE_POINTS, "shortRatePoints": hw.SHORT_RATE_POINTS,
                                   "processPoints": hw.PROCESS_POINTS, "stepPoints": hw.STEP_POINTS,
                                   "options": OPTIONS, "constraintParams": v.enc(arrays)},
                        "expected": {
                            "a": p[0], "b": p[1], "sigma": p[2], "lambda": p[3], "r0": H.call("Vasicek::r0", model),
                            "variable": [H.call("OneFactorModel::dynamics()->variable", model, t, r)
                                         for t, r in hw.VARIABLE_POINTS],
                            "shortRate": [H.call("OneFactorModel::dynamics()->shortRate", model, t, x)
                                          for t, x in hw.SHORT_RATE_POINTS],
                            "processX0": H.call("OneFactorModel::dynamics()->process()->x0", model),
                            "drift": [H.call("OneFactorModel::dynamics()->process()->drift", model, t, x)
                                      for t, x in hw.PROCESS_POINTS],
                            "diffusion": [H.call("OneFactorModel::dynamics()->process()->diffusion", model, t, x)
                                          for t, x in hw.PROCESS_POINTS],
                            "expectation": [H.call("OneFactorModel::dynamics()->process()->expectation", model, *s)
                                            for s in hw.STEP_POINTS],
                            "stdDeviation": [H.call("OneFactorModel::dynamics()->process()->stdDeviation", model, *s)
                                             for s in hw.STEP_POINTS],
                            "variance": [H.call("OneFactorModel::dynamics()->process()->variance", model, *s)
                                         for s in hw.STEP_POINTS],
                            "discountBondOption": [H.call(
                                "AffineModel::discountBondOption(Option::Type,Real,Time,Time,Time)", model, *o)
                                for o in OPTIONS],
                            "constraintTest": [H.call("CalibratedModel::constraint()->test", model, a) for a in arrays],
                            "upperBound": [H.call("CalibratedModel::constraint()->upperBound", model, a)
                                           for a in arrays],
                            "lowerBound": [H.call("CalibratedModel::constraint()->lowerBound", model, a)
                                           for a in arrays],
                            "treeSize": sizes,
                            "treeDiscount": [[H.call("OneFactorModel::ShortRateTree::discount", lat, i, j)
                                              for j in range(sizes[i])] for i in range(columns - 1)],
                            "statePrices": [H.call("TreeLattice::statePrices", lat, i) for i in range(columns)]}})

        model = vasicek(params=[0.1, 0.05, 0.0, 0.0])
        grid = {"end": 1.0, "steps": 4}
        lat = da.lattice(model, grid)
        out.append({"name": "refused: a tree at sigma 0",
                    "inputs": {"settings": st, "model": model, "timeGrid": grid},
                    "expected": {"treeSize": [v.attempt(
                        lambda: H.call("OneFactorModel::ShortRateTree::size", lat, 0))]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/vasicek.hpp", cases)
