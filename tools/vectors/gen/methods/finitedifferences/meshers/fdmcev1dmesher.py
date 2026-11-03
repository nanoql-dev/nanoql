# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmCEV1dMesher (ql/methods/finitedifferences/meshers/fdmcev1dmesher.hpp, .cpp): the CEV
process's mesher, from the density's eps-quantile to its (1 - eps)-quantile, on the evaluation date 15 October 2026.

Elasticities on both sides of 1 and below 0 (delta below and above 2, and a mass at zero above eps), over volatility
parameters at 10% to 75%, maturities of a quarter to five years, sizes 2 to 101, eps 1e-4 to 1e-8, scale factors 1 and
1.5, with no critical point, one inside the bounds and one outside: the locations, dplus and dminus. The band |beta -
1| < 1e-3, where nanoql takes the lognormal limit's bounds (tests/NanoQL.Tests/Divergences/FdCevNearOneTests.cs), is
none of the grid's; the cases where QuantLib's density inverse steps below zero and Boost refuses it, which nanoql
fixes (tests/NanoQL.Tests/Divergences/CevDensityBelowZeroTests.cs), are left out by Boost's message.

A spec is {"type": "CEV", "size", "f0", "alpha", "beta", "maturity", "eps", "scaleFactor", "cPoint": [point, density]
or [null, null]}, which the CEV operator's and engines' generators share. The wheel's calls, by QuantLib-SWIG 1.44's
fdm.i: FdmCEV1dMesher(size, f0, alpha, beta, maturity, eps, scaleFactor, cPoint), and the mesher's locations, dplus
and dminus. Read back by tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/FdmCEV1dMesherVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


spm = _load("fdmsimpleprocess1dmesher", "methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.py")
R = ql.Date(15, ql.October, 2026)

# Boost's refusal where QuantLib's density inverse steps below zero, which nanoql fixes
# (tests/NanoQL.Tests/Divergences/CevDensityBelowZeroTests.cs): the cases left out
BELOW_ZERO = "Non centrality parameter is"


def cev_spec(size, f0, alpha, beta, maturity, eps=1e-4, scale_factor=1.5, c_point=(None, None)):
    return {"type": "CEV", "size": size, "f0": f0, "alpha": alpha, "beta": beta, "maturity": maturity, "eps": eps,
            "scaleFactor": scale_factor, "cPoint": list(c_point)}


def mesher_1d(spec):
    """The wheel's 1-D mesher for a spec: the CEV mesher, or the others' builders."""
    if spec["type"] != "CEV":
        return spm.mesher_1d(spec)
    point, density = spec["cPoint"]
    args = [spec["size"], spec["f0"], spec["alpha"], spec["beta"], spec["maturity"], spec["eps"], spec["scaleFactor"]]
    if point is not None:
        args.append((point, density))
    return ql.FdmCEV1dMesher(*args)


def composite(spec):
    """The wheel's composite mesher for a spec, and its 1-D meshers (to keep them alive)."""
    meshers = [mesher_1d(m) for m in spec["meshers"]]
    if len(meshers) == 1:
        return ql.FdmMesherComposite(meshers[0]), meshers
    return ql.FdmMesherComposite(meshers), meshers


def cases():
    st = v.settings(R)
    out = []

    def add(name, spec):
        try:
            m = mesher_1d(spec)
            expected = spm.read(m)
        except RuntimeError as e:
            if BELOW_ZERO in str(e):
                return
            expected = {"mesher": dict(v.THROWS)}
        out.append({"name": name, "inputs": {"settings": st, "mesher": spec}, "expected": expected})

    for beta in (-0.5, 0.25, 0.45, 0.9, 0.99, 1.01, 1.2, 1.45):
        for sigma in (0.1, 0.4):
            for maturity in (0.25, 1.0, 5.0):
                alpha = sigma * 100.0 ** (1.0 - beta)
                add(f"beta {beta} sigma {sigma}, {maturity} years",
                    cev_spec(41, 100.0, alpha, beta, maturity))
    add("testFdmCevOp's grid", cev_spec(101, 2.1, 0.75, 0.45, 1.0, 1e-6, 1.0, (2.3, 0.1)))
    add("a mass at zero, beta -2", cev_spec(51, 2.1, 0.75, -2.0, 1.0, 1e-4, 1.0, (2.3, 0.1)))
    add("a mass at zero, beta 0.45, five years", cev_spec(51, 2.1, 0.75, 0.45, 5.0, 1e-4, 1.0))
    add("critical point outside", cev_spec(31, 100.0, 3.0, 0.5, 1.0, 1e-4, 1.5, (1000.0, 0.1)))
    add("critical point at the forward", cev_spec(31, 100.0, 3.0, 0.5, 1.0, 1e-8, 1.0, (100.0, 0.05)))
    add("two points", cev_spec(2, 100.0, 3.0, 0.5, 1.0))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmcev1dmesher.hpp", cases)
