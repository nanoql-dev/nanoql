# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdG2SwaptionEngine (ql/pricingengines/swaption/fdg2swaptionengine.hpp, .cpp), on the
evaluation date 15 January 2025.

tools/vectors/gen/pricingengines/swaption/fdhullwhiteswaptionengine.py's swaptions and refusals, on the two G2 models
of tools/vectors/gen/methods/finitedifferences/operators/fdmg2op.py, at a grid of 40 time steps on 30 by 30 points
(QuantLib's default scheme, Hundsdorfer) and at a coarse one (10 steps, 12 by 10 points, two damping steps, an epsilon
of 1e-4), under the Hundsdorfer default and the Douglas, Craig-Sneyd, modified Craig-Sneyd and modified Hundsdorfer
schemes, and the implicit Euler scheme, whose two-dimensional step runs QuantLib's BiCGStab, on the coarse grid.

Generated and left out: TR-BDF2 (the S-eq plan's SEQR9, tools/vectors/gen/methods/finitedifferences/solvers/
fdmg2solver.py's reason); and fdhullwhiteswaptionengine.py's American exercise is not generated, likewise.

The specs are fdhullwhiteswaptionengine.py's, an engine spec with "yGrid". The wheel's FdG2SwaptionEngine is the proxy
of the C++ constructor (QuantLib-SWIG 1.44, SWIG/swaption.i). Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/FdG2SwaptionEngineVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fhw = _load("fdhullwhiteswaptionengine", "pricingengines/swaption/fdhullwhiteswaptionengine.py")


def cases():
    return fhw.cases_for(fhw.g2op.G2S,
                         lambda m: fhw.engine_spec(m, 40, 30, 0, 1e-5, "Hundsdorfer", 30),
                         lambda m, s: fhw.engine_spec(m, 10, 12, 2, 1e-4, s, 10),
                         ["Hundsdorfer", "Douglas", "CraigSneyd", "ModifiedCraigSneyd", "ModifiedHundsdorfer",
                          "ImplicitEuler"],
                         ["TrBDF2"])


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/swaption/fdg2swaptionengine.hpp", cases)
