# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmAffineModelTermStructure
(ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.hpp, .cpp), which the wheel doesn't bind, each
output computed by the C++ harness (docs/porting-playbook.md §9.4): the curve an affine model implies at a state of its
factors, its discount at six times and at four dates, on the evaluation date 15 January 2025.

Two Hull-White models and two G2 models on flat 2.7% and 3.1% Actual/365 (Fixed) curves, each at three states (Hull-White
a short rate below, at and above the curve's level; G2 its two factors off zero in turn), the curve dated at the
evaluation date and a year and five years after it.

The harness's call is tools/cxx/src/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.cpp's; the curve
spec is tools/vectors/gen/indexes/interestrateindex.py's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Utilities/FdmAffineModelTermStructureCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


irx = _load("interestrateindex", "indexes/interestrateindex.py")
TODAY = ql.Date(15, ql.January, 2025)
CURVES = [irx.curve_spec(TODAY, 0.027), irx.curve_spec(TODAY, 0.031)]
MODELS = [("HullWhite", [0.05, 0.01], [[0.01], [0.027], [0.06]]),
          ("HullWhite", [0.2, 0.015], [[-0.005], [0.031], [0.045]]),
          ("G2", [0.07, 0.011, 0.3, 0.008, -0.6], [[0.0, 0.0], [0.01, -0.004], [-0.02, 0.006]]),
          ("G2", [0.1, 0.01, 0.1, 0.01, -0.75], [[0.005, 0.0], [0.0, -0.01], [0.015, 0.015]])]
TIMES = [0.0, 0.25, 1.0, 2.5, 7.0, 15.0]
OFFSETS = [0, 1, 5]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(v.enc(TODAY))
        for k, (model, params, states) in enumerate(MODELS):
            curve = CURVES[k % 2]
            for state in states:
                for years in OFFSETS:
                    reference = TODAY + ql.Period(years, ql.Years)
                    dates = [v.enc(reference + ql.Period(n, ql.Months)) for n in (0, 6, 30, 120)]
                    r = H.call("FdmAffineModelTermStructure::discount", curve, model, params, state, v.enc(reference),
                               TIMES, dates)
                    out.append({"name": f"{model} {params}, state {state}, dated {years}Y ahead",
                                "inputs": {"settings": st, "curve": curve, "model": model, "parameters": params,
                                           "state": state, "referenceDate": v.enc(reference), "times": TIMES,
                                           "dates": dates},
                                "expected": {"discount": r["discount"], "discountAt": r["discountAt"]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmaffinemodeltermstructure.hpp", cases)
