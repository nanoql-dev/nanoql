# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AndreasenHugeLocalVolAdapter (ql/termstructures/volatility/equityfx/
andreasenhugelocalvoladapter.hpp, .cpp), on the evaluation date 15 October 2026: the local volatility structure of
tools/vectors/gen/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.py's six calibrations: its reference
and maximum dates, strike range and day counter's name, and the local volatility at a grid of times and strikes, among
them strikes outside the calibration's grid, which the adapter clamps onto it, extrapolating.

**The stability filter** is the calibration's (andreasenhugevolatilityinterpl.py's): every output is computed again on
the calibrations with the volatilities and the spot moved one ulp each way, and written only where every move leaves
it within 1e-10 relative. The calibration's leave-outs apply. The wheel's calls, by QuantLib-SWIG 1.44:
AndreasenHugeLocalVolAdapter(interpolation) and the local volatility structure's members. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/AndreasenHugeLocalVolAdapterVectorTests.cs."""
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


ah = _load("andreasenhugevolatilityinterpl", "termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.py")
R = ah.R
TIMES = [0.0, 0.05, 0.25, 0.4986301369863014, 1.0, 1.5, 3.0]
RELATIVE = [0.01, 0.3, 0.8, 1.0, 1.2, 3.0, 50.0]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in ah.CASES:
        base = ql.AndreasenHugeLocalVolAdapter(ah.build(spec))
        others = [ql.AndreasenHugeLocalVolAdapter(o) for o in ah.moved(spec)]
        pts = [[t, spec["spot"] * r] for t in TIMES for r in RELATIVE]
        local = [ah.filtered(base, others, lambda a, t=t, k=k: a.localVol(t, k, True)) for t, k in pts]
        keep = [i for i in range(len(pts)) if local[i] is not None]
        out.append({"name": name,
                    "inputs": {"settings": st, "calibration": spec, "points": [pts[i] for i in keep]},
                    "expected": v.enc({
                        "referenceDate": base.referenceDate(),
                        "maxDate": base.maxDate(),
                        "minStrike": base.minStrike(),
                        "maxStrike": base.maxStrike(),
                        "dayCounter": base.dayCounter().name(),
                        "localVol": [local[i] for i in keep]})})
    print(f"dropped {ah.Tally.dropped} of {ah.Tally.total} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/andreasenhugelocalvoladapter.hpp", cases)
