# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AndreasenHugeVolatilityAdapter (ql/termstructures/volatility/equityfx/
andreasenhugevolatilityadapter.hpp, .cpp), on the evaluation date 15 October 2026: the Black volatility structure of
tools/vectors/gen/termstructures/volatility/equityfx/andreasenhugevolatilityinterpl.py's six calibrations, at an
implied-volatility accuracy of 1e-6 (QuantLib's default) and 1e-10: its reference and maximum dates, strike range and
day counter's name, the forward (atmLevel) at a grid of times, and the Black volatility and variance at a grid of times
and strikes, extrapolating.

**The stability filter** is the calibration's (andreasenhugevolatilityinterpl.py's): every output is computed again on
the calibrations with the volatilities and the spot moved one ulp each way, and written only where every move leaves
it within 1e-10 relative; the implied volatility, an iteration of Li's rational approximation, is filtered with it (the
S-van plan's VD17). The calibration's leave-outs apply. The wheel's calls, by QuantLib-SWIG 1.44:
AndreasenHugeVolatilityAdapter(interpolation, eps) and the Black volatility structure's members. Read back by
tests/NanoQL.Tests/Vectors/TermStructures/AndreasenHugeVolatilityAdapterVectorTests.cs."""
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
TIMES = [0.05, 0.25, 0.4986301369863014, 1.0, 1.5]
RELATIVE = [0.6, 0.85, 1.0, 1.15, 1.6]


def cases():
    out = []
    st = v.settings(R)
    for name, spec in ah.CASES:
        for eps in (1e-6, 1e-10):
            base = ql.AndreasenHugeVolatilityAdapter(ah.build(spec), eps)
            others = [ql.AndreasenHugeVolatilityAdapter(o, eps) for o in ah.moved(spec)]
            pts = [[t, spec["spot"] * r] for t in TIMES for r in RELATIVE]
            vols = [ah.filtered(base, others, lambda a, t=t, k=k: a.blackVol(t, k, True)) for t, k in pts]
            variances = [ah.filtered(base, others, lambda a, t=t, k=k: a.blackVariance(t, k, True)) for t, k in pts]
            keep = [i for i in range(len(pts)) if vols[i] is not None and variances[i] is not None]
            out.append({"name": f"{name}, eps {eps}",
                        "inputs": {"settings": st, "calibration": spec, "eps": eps, "times": TIMES,
                                   "points": [pts[i] for i in keep]},
                        "expected": v.enc({
                            "referenceDate": base.referenceDate(),
                            "maxDate": base.maxDate(),
                            "minStrike": base.minStrike(),
                            "maxStrike": base.maxStrike(),
                            "dayCounter": base.dayCounter().name(),
                            "atmLevel": [base.atmLevel(t) for t in TIMES],
                            "blackVol": [vols[i] for i in keep],
                            "blackVariance": [variances[i] for i in keep]})})
    print(f"dropped {ah.Tally.dropped} of {ah.Tally.total} outputs")
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/equityfx/andreasenhugevolatilityadapter.hpp", cases)
