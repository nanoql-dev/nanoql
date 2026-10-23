# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AbcdCalibration (ql/termstructures/volatility/abcdcalibration.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness on calibrations built inside the call: fits of term structures of
Black volatilities at 0.5 to 20 years, the S-g1d plan's sets 0, 1, 4 and 5 (falling, rising, and two dips whose total
variance grows), from QuantLib's default guess (−0.06, 0.17, 0.54, 0.17) and from a second (0.05, −0.05, 0.30, 0.15),
with d fixed, vega weighted, and capped at 20 iterations; each gives the fitted a, b, c and d, the errors and the end
criteria, the adjustment factors k on the fit's times and the fitted volatility at six times. Then the members at the
guesses, before any fit, on sets 0 and 6; and the fits QuantLib refuses: sets 2 and 3, whose total variance shrinks, and
a guess with c negative.

**The stability filter** (the S7 plan's S7R6) runs on the fits' outputs: each fit is rerun with every volatility moved
one ulp up, then down, by math.nextafter, together; then each alone, up and down; an output (a parameter, an error, the
end criteria, an array) is written only where every refit leaves it within 1e-12 x max(|base|, |moved|, 1), and the
generator prints "dropped n of m outputs" per case.

Left out, each a documented divergence: set 6, a dip QuantLib refuses that the guard fits (the S-g1d plan's G1R6 (xxv);
tests/NanoQL.Tests/Divergences/AbcdCalibrationGuardTests.cs); and a fixed with d free, which QuantLib's transformation
fixes as a + d (tests/NanoQL.Tests/Divergences/AbcdCalibrationFixedATests.cs). A refused value is {"throws": true}. Each
case's input "fit" is the spec tools/cxx/src/termstructures/volatility/abcdspec.hpp reads;
tests/NanoQL.Tests/Vectors/Cxx/TermStructures/AbcdCalibrationCxxVectorTests.cs reads it back."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

RELATIVE = 1e-12
TODAY = ql.Date(15, ql.January, 2025)
T = [0.5, 1.0, 2.0, 3.0, 5.0, 7.0, 10.0, 15.0, 20.0]
SETS = [[0.30, 0.28, 0.25, 0.22, 0.20, 0.19, 0.18, 0.175, 0.17],
        [0.10, 0.12, 0.15, 0.17, 0.19, 0.20, 0.21, 0.215, 0.22],
        [0.40, 0.25, 0.20, 0.21, 0.23, 0.24, 0.25, 0.255, 0.26],
        [0.25, 0.10, 0.08, 0.09, 0.12, 0.14, 0.16, 0.17, 0.18],
        [0.30, 0.25, 0.22, 0.225, 0.23, 0.235, 0.24, 0.245, 0.25],
        [0.40, 0.29, 0.22, 0.22, 0.23, 0.24, 0.25, 0.255, 0.26],
        [0.25, 0.18, 0.13, 0.12, 0.13, 0.14, 0.16, 0.17, 0.18]]
GUESS = [-0.06, 0.17, 0.54, 0.17]
SECOND = [0.05, -0.05, 0.30, 0.15]
POINTS = [0.0, 0.25, 1.0, 4.0, 12.0, 30.0]
FITTED = ["a", "b", "c", "d", "error", "maxError", "errors", "endCriteria", "k", "value"]


def fit(vols, guess=GUESS, fixed=(False, False, False, False), weighted=False, end_criteria=None):
    return {"t": T, "blackVols": list(vols), "guess": list(guess), "fixed": list(fixed), "vegaWeighted": weighted,
            "endCriteria": end_criteria, "method": None}


def specs():
    return [
        ("set 0, the default guess", fit(SETS[0])),
        ("set 1, the default guess", fit(SETS[1])),
        ("set 4, the default guess", fit(SETS[4])),
        ("set 5, the default guess", fit(SETS[5])),
        ("set 0, the second guess", fit(SETS[0], SECOND)),
        ("set 1, the second guess", fit(SETS[1], SECOND)),
        ("set 1, d fixed", fit(SETS[1], fixed=(False, False, False, True))),
        ("set 5, vega weighted", fit(SETS[5], weighted=True)),
        ("set 1, capped at 20 iterations", fit(SETS[1], end_criteria={
            "maxIterations": 20, "maxStationaryStateIterations": 10, "rootEpsilon": 1e-8, "functionEpsilon": 0.3e-4,
            "gradientNormEpsilon": 0.3e-4})),
    ]


def guesses():
    return [("set 0 at the guess", fit(SETS[0])), ("set 6 at the second guess", fit(SETS[6], SECOND))]


def refusals():
    return [("refused: set 2", fit(SETS[2])), ("refused: set 3", fit(SETS[3])),
            ("refused: c negative", fit(SETS[0], [-0.06, 0.17, -0.1, 0.17]))]


def within(a, b):
    if isinstance(a, list) or isinstance(b, list):
        return isinstance(a, list) and isinstance(b, list) and len(a) == len(b) and all(map(within, a, b))
    if isinstance(a, int) or isinstance(b, int) or isinstance(a, dict) or isinstance(b, dict):
        return a == b
    return a == b or abs(a - b) <= RELATIVE * max(abs(a), abs(b), 1.0)


def moves(vols):
    out = [[math.nextafter(x, d) for x in vols] for d in (math.inf, -math.inf)]
    for i, x in enumerate(vols):
        for d in (math.inf, -math.inf):
            moved = list(vols)
            moved[i] = math.nextafter(x, d)
            out.append(moved)
    return out


def cases():
    with cxx.connect() as H:
        out = []
        dropped_all = total_all = 0
        st = H.settings(v.enc(TODAY))
        for name, spec in specs():
            base = H.call("AbcdCalibration::compute", spec, POINTS)
            keep = set(FITTED)
            for moved in moves(spec["blackVols"]):
                refit = H.call("AbcdCalibration::compute", {**spec, "blackVols": moved}, POINTS)
                keep = {k for k in keep if within(base[k], refit[k])}
            dropped = len(FITTED) - len(keep)
            dropped_all += dropped
            total_all += len(FITTED)
            print(f"{name}: dropped {dropped} of {len(FITTED)} outputs")
            out.append({"name": name, "inputs": {"settings": st, "kind": "compute", "fit": spec, "points": POINTS},
                        "expected": {k: base[k] for k in FITTED if k in keep}})
        for name, spec in guesses():
            out.append({"name": name, "inputs": {"settings": st, "kind": "guess", "fit": spec, "points": POINTS},
                        "expected": H.call("AbcdCalibration::guess", spec, POINTS)})
        for name, spec in refusals():
            out.append({"name": name, "inputs": {"settings": st, "kind": "refusal", "fit": spec, "points": POINTS},
                        "expected": {"compute": v.attempt(lambda s=spec: H.call("AbcdCalibration::compute", s,
                                                                                POINTS))}})
        print(f"file: dropped {dropped_all} of {total_all} outputs")
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/volatility/abcdcalibration.hpp", cases)
