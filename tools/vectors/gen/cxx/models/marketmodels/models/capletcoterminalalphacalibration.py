# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CTSMMCapletAlphaFormCalibration
(ql/models/marketmodels/models/capletcoterminalalphacalibration.hpp, .cpp) on its alpha forms (alphaformconcrete.hpp,
.cpp) and AlphaFinder (alphafinder.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness.

The calibration on QuantLib's case (marketmodel_smmcapletalphacalibration.cpp: capletcoterminalswaptioncalibration.py's
setup, α 0 in [−1, 1], the linear-hyperbolic form, three factors, ten passes) and on varied ones: the inverse-linear
form at bounds ±0.5, bounds ±0.2, α from 0.1, one factor, six annual rates; five and ten factors, another abcd,
displaced 1% and the caplets at 70%, where no α fits a caplet (QuantLib's "alpha form failure"); each the calibration's
results and α (calibrations.cpp). The pass function alone (capletAlphaFormCalibration) on QuantLib's case and two varied
ones, the inverse-linear form at bounds ±0.5 among them: its α, a, b and pseudo-roots. Throws: α bounds short of the
rates.

Maximum homogeneity is left out: QuantLib's Minimize tests the objective's value where it means the trial α, so it stops
at its first trial where that value isn't an acceptable α, its α and pseudo-roots then other than the fix's (the S-mm
plan's MR3 (x), tests/NanoQL.Tests/Divergences/AlphaFinderTests.cs); with QuantLib's test restored nanoql reproduces
both cases bit for bit. One maximum-homogeneity case stays, caplets × 0.95 on eight factors, where the fixed
minimization leaves a caplet no α and the pass runs again on QuantLib's test (the user's ruling of 2026-10-27), so
nanoql's results are QuantLib's; and the pass function on it.

Where a step's final part fails, QuantLib 1.44's solve returns it as solved, and QuantLib's master's, which nanoql
takes, returns false, so the calibration throws "alpha form failure" (#2910, the upstream sweep's SWR2 (a) and SWD14;
tests/NanoQL.Tests/Divergences/CTSMMCapletAlphaFormCalibrationTests.cs). So no case has such a step: a calibration
whose swaptions miss by more than 1e-10, the mark of one, or a pass whose b is QuantLib's caller's 0 stops the
generator. The inverse-linear form's cases are at bounds ±0.5, where every step solves; at ±1 six of its steps are
half-solved, and the refusal on those inputs is checked in that class.

The harness unit is tools/cxx/src/models/marketmodels/models/calibrations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CTSMMCapletAlphaFormCalibrationCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
SEMIANNUAL = [0.5 * (i + 1) for i in range(11)]
ANNUAL = [1.0 * (i + 1) for i in range(7)]
CAPLETS = [0.1640, 0.1740, 0.1840, 0.1940, 0.1840, 0.1740, 0.1640, 0.1540, 0.1440]
ABCD = [0.0, 0.17, 1.0, 0.10]
OTHER_ABCD = [-0.0597, 0.1677, 0.5403, 0.1710]


def spec(H, times=SEMIANNUAL, abcd=ABCD, scale=1.0, displacement=0.0):
    """QuantLib's caplet calibration setup on the rate times given, its last caplet volatility the last swaption's."""
    n = len(times) - 1
    last = H.call("PiecewiseConstantAbcdVariance",
                  {"a": abcd[0], "b": abcd[1], "c": abcd[2], "d": abcd[3], "resetIndex": n - 1,
                   "rateTimes": times})["totalVolatility"][n - 1]
    return {"rateTimes": times, "forwards": [0.03 + 0.0025 * i for i in range(n)], "longTermCorr": 0.5, "beta": 0.2,
            "abcd": abcd, "capletVols": [scale * x for x in CAPLETS[:n - 1]] + [last], "displacement": displacement}


def outcome(f):
    """The call's outputs, or {"throws": {"throws": true}} where QuantLib throws."""
    r = v.attempt(f)
    return {"throws": r} if r == v.THROWS else r


def final_parts(e):
    """The pass function's outputs, refused where a step's final part failed (QuantLib 1.44's b there the caller's 0, a
    solved step's positive), which nanoql refuses with QuantLib's master."""
    assert not ("b" in e and any(x == 0.0 for x in e["b"][1:])), "a half-solved pass"
    return e


def half_solved(e):
    """A calibration's outputs, refused where its swaptions miss by more than 1e-10, the mark of a step whose final
    part failed, which nanoql refuses with QuantLib's master."""
    assert not e.get("swaptionMaxError", 0.0) > 1e-10, "a half-solved calibration"
    return e


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        settings = [3, 10, 1e-4, 100, 1e-8]
        variations = [
            ("QuantLib's case", {}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", settings),
            ("the inverse-linear form, bounds 0.5", {}, 0.0, 0.5, -0.5, False, "InverseLinear", settings),
            ("bounds 0.2", {}, 0.0, 0.2, -0.2, False, "LinearHyperbolic", settings),
            ("alpha from 0.1", {}, 0.1, 1.0, -1.0, False, "LinearHyperbolic", settings),
            ("one factor", {}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", [1, 10, 1e-4, 100, 1e-8]),
            ("five factors (no fit)", {}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", [5, 10, 1e-4, 100, 1e-8]),
            ("another abcd (no fit)", {"abcd": OTHER_ABCD}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", settings),
            ("six annual rates", {"times": ANNUAL}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", settings),
            ("ten factors (no fit)", {}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", [10, 10, 1e-4, 100, 1e-8]),
            ("maximum homogeneity, caplets at 95%, eight factors (QuantLib's test)", {"scale": 0.95}, 0.0, 1.0, -1.0,
             True, "LinearHyperbolic", [8, 10, 1e-4, 100, 1e-8]),
            ("displaced 1% (no fit)", {"displacement": 0.01}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", settings),
            ("caplets at 70% (no fit)", {"scale": 0.7}, 0.0, 1.0, -1.0, False, "LinearHyperbolic", settings),
        ]
        for name, kw, a0, hi, lo, homogeneity, form, s in variations:
            c = spec(H, **kw)
            n = len(c["rateTimes"]) - 1
            args = [c, [a0] * n, [hi] * n, [lo] * n, homogeneity, form] + s
            out.append({"name": "calibrate, " + name,
                        "inputs": {"settings": st, "call": "calibrate", "spec": c, "alphaInitial": [a0] * n,
                                   "alphaMax": [hi] * n, "alphaMin": [lo] * n, "maximizeHomogeneity": homogeneity,
                                   "form": form, "factors": s[0], "maxIterations": s[1], "tolerance": s[2],
                                   "innerMaxIterations": s[3], "innerTolerance": s[4]},
                        "expected": half_solved(outcome(lambda a=args: H.call(
                            "CTSMMCapletAlphaFormCalibration.calibrate", *a)))})
        for name, kw, homogeneity, form, factors, bound in [
                ("QuantLib's case", {}, False, "LinearHyperbolic", 3, 1.0),
                ("the inverse-linear form, bounds 0.5", {}, False, "InverseLinear", 3, 0.5),
                ("six annual rates, two factors", {"times": ANNUAL}, False, "LinearHyperbolic", 2, 1.0),
                ("maximum homogeneity, caplets at 95%, eight factors (QuantLib's test)", {"scale": 0.95}, True,
                 "LinearHyperbolic", 8, 1.0)]:
            c = spec(H, **kw)
            n = len(c["rateTimes"]) - 1
            args = [c, [0.0] * n, [bound] * n, [-bound] * n, homogeneity, form, factors, 100, 1e-8]
            out.append({"name": "capletAlphaFormCalibration, " + name,
                        "inputs": {"settings": st, "call": "capletAlphaFormCalibration", "spec": c,
                                   "alphaInitial": [0.0] * n, "alphaMax": [bound] * n, "alphaMin": [-bound] * n,
                                   "maximizeHomogeneity": homogeneity, "form": form, "factors": factors, "steps": 100,
                                   "tolerance": 1e-8},
                        "expected": final_parts(outcome(lambda a=args: H.call(
                            "CTSMMCapletAlphaFormCalibration::capletAlphaFormCalibration", *a)))})
        c = spec(H)
        args = [c, [0.0] * 10, [1.0] * 9, [-1.0] * 10, False, "LinearHyperbolic", 3, 10, 1e-4, 100, 1e-8]
        out.append({"name": "calibrate, alphaMax short of the rates (refused)",
                    "inputs": {"settings": st, "call": "calibrate", "spec": c, "alphaInitial": [0.0] * 10,
                               "alphaMax": [1.0] * 9, "alphaMin": [-1.0] * 10, "maximizeHomogeneity": False,
                               "form": "LinearHyperbolic", "factors": 3, "maxIterations": 10, "tolerance": 1e-4,
                               "innerMaxIterations": 100, "innerTolerance": 1e-8},
                    "expected": outcome(lambda: H.call("CTSMMCapletAlphaFormCalibration.calibrate", *args))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/capletcoterminalalphacalibration.hpp", cases)
