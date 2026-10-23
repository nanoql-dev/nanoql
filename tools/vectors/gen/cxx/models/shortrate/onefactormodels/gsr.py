# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for the members of QuantLib's Gsr and Gaussian1dModel (ql/models/shortrate/onefactormodels/gsr.hpp,
gaussian1dmodel.hpp) the wheel doesn't bind, each output computed by the C++ harness: on the three GSR models of
gen/pricingengines/swaption/gaussian1dswaptionengine.py and gen/models/shortrate/onefactormodels/gsr.py's stepped
model, numeraireTime(), yGrid at five (stdDevs, gridPoints, T, t, y) points, the calibration flags FixedReversions(),
FixedVolatilities(), MoveVolatility(i) and MoveReversion(i) for every index and one past the end, refused; and
Gaussian1dModel's static gaussianPolynomialIntegral and gaussianShiftedPolynomialIntegral at eight points each.
tests/NanoQL.Tests/Vectors/Cxx/Models/GsrCxxVectorTests.cs reads it back."""
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


g1d = _load("wheel_gaussian1dswaptionengine", "pricingengines/swaption/gaussian1dswaptionengine.py")
gsr = _load("wheel_gsr", "models/shortrate/onefactormodels/gsr.py")

MODELS = g1d.MODELS + [gsr.STEPPED]
Y_GRIDS = [(7.0, 64, 1.0, 0.0, 0.0), (5.0, 8, 2.0, 1.0, 0.7), (7.0, 16, 10.0, 5.0, -1.3), (6.0, 4, 3.0, 3.0, 0.0),
           (8.0, 32, 20.0, 0.5, 2.1)]
INTEGRALS = [(0.0, 0.0, 0.0, 0.0, 1.0, -100.0, 100.0), (0.0, 0.1, -0.2, 0.3, 0.05, -1.0, 0.5),
             (0.01, -0.02, 0.03, 0.4, -0.5, 0.0, 2.0), (0.0, 0.0, 0.0, 0.0, 0.7, 3.0, 100.0),
             (0.0, 0.0, 0.0, 0.0, 0.2, -100.0, -2.5), (0.002, 0.001, -0.01, 0.02, 0.03, -7.0, -6.8),
             (0.0, 0.05, 0.1, -0.2, 0.3, 6.5, 7.0), (0.0, 1e-3, -2e-3, 4e-3, 1e-2, -0.01, 0.01)]
SHIFTED = [(0.0, 0.1, -0.2, 0.3, 0.05, -1.0, -1.0, -0.9), (0.0, 0.02, 0.03, -0.01, 0.5, 2.0, 2.0, 2.1),
           (0.0, 0.0, 0.0, 0.0, 0.8, 6.9, 7.0, 100.0), (0.0, 0.0, 0.0, 0.0, 0.1, -7.0, -100.0, -7.0),
           (0.01, -0.03, 0.02, 0.1, -0.2, 0.3, 0.3, 0.5), (0.0, -0.4, 0.2, 0.6, 1.0, -3.0, -3.0, -2.89),
           (0.0, 1e-4, -2e-4, 1e-3, 0.02, 0.0, 0.0, 0.11), (0.0, 0.5, 0.5, 0.5, 0.5, 5.0, 5.0, 5.11)]


def cases():
    with cxx.connect() as H:
        st = H.settings(H.call("Date(Day,Month,Year)", 15, 1, 2025))
        out = []
        for k, spec in enumerate(MODELS):
            n_vols, n_revs = len(spec["volatilities"]), len(spec["reversions"])
            expected = {"numeraireTime": v.attempt(lambda: H.call("Gsr::numeraireTime", spec)),
                        "yGrid": [v.attempt(lambda: H.call("Gsr::yGrid", spec, *g)) for g in Y_GRIDS],
                        "FixedReversions": H.call("Gsr::FixedReversions", spec),
                        "FixedVolatilities": H.call("Gsr::FixedVolatilities", spec),
                        "MoveVolatility": [v.attempt(lambda: H.call("Gsr::MoveVolatility", spec, i))
                                           for i in range(n_vols + 1)],
                        "MoveReversion": [v.attempt(lambda: H.call("Gsr::MoveReversion", spec, i))
                                          for i in range(n_revs + 1)]}
            assert expected["MoveVolatility"][-1] == v.THROWS and expected["MoveReversion"][-1] == v.THROWS
            out.append({"name": f"model {k}", "inputs": {"settings": st, "model": spec, "yGrids": Y_GRIDS},
                        "expected": expected})
        out.append({"name": "Gaussian integrals",
                    "inputs": {"settings": st, "integrals": INTEGRALS, "shifted": SHIFTED},
                    "expected": {"gaussianPolynomialIntegral": [
                        H.call("Gaussian1dModel::gaussianPolynomialIntegral", *p) for p in INTEGRALS],
                        "gaussianShiftedPolynomialIntegral": [
                            H.call("Gaussian1dModel::gaussianShiftedPolynomialIntegral", *p) for p in SHIFTED]}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/shortrate/onefactormodels/gsr.hpp", cases)
