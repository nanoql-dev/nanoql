# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CTSMMCapletOriginalCalibration
(ql/models/marketmodels/models/capletcoterminalswaptioncalibration.hpp, .cpp) and its CTSMMCapletCalibration base
(ctsmmcapletcalibration.hpp, .cpp), which the wheel doesn't expose, each output computed by the C++ harness.

The calibration on QuantLib's case (marketmodel_smmcapletcalibration.cpp: ten semi-annual rates, forwards from 3% by
0.25%, abcd (0, 0.17, 1, 0.1), an exponential forward correlation (0.5, 0.2) as the swap rates', three factors, α −0.05,
the lowest root, two passes) and on varied ones: the highest root, the full approximation, α 0, one and ten factors,
displaced 1%, the caplets at 70% (no fit), another abcd, six annual rates; each the calibration's results
(calibrations.cpp). The last caplet volatility is the last swaption's (the variances' own, from the harness). The pass
function alone (calibrationFunction) on QuantLib's case and three varied ones. Throws: α short of the rates.

The harness unit is tools/cxx/src/models/marketmodels/models/calibrations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CTSMMCapletOriginalCalibrationCxxVectorTests.cs."""
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


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        settings = [3, 2, 1e-5, 50, 1e-9]
        variations = [
            ("QuantLib's case", {}, -0.05, True, False, settings),
            ("the highest root", {}, -0.05, False, False, settings),
            ("the full approximation", {}, -0.05, True, True, settings),
            ("alpha 0", {}, 0.0, True, False, settings),
            ("one factor", {}, -0.05, True, False, [1, 2, 1e-5, 50, 1e-9]),
            ("ten factors", {}, -0.05, True, False, [10, 2, 1e-5, 50, 1e-9]),
            ("displaced 1%", {"displacement": 0.01}, -0.05, True, False, settings),
            ("caplets at 70%", {"scale": 0.7}, -0.05, True, False, settings),
            ("another abcd, five passes", {"abcd": OTHER_ABCD}, -0.05, True, False, [3, 5, 1e-6, 50, 1e-9]),
            ("six annual rates", {"times": ANNUAL}, -0.05, True, False, [3, 3, 1e-5, 50, 1e-9]),
        ]
        for name, kw, alpha, lowest, full, s in variations:
            c = spec(H, **kw)
            n = len(c["rateTimes"]) - 1
            args = [c, [alpha] * n, lowest, full] + s
            out.append({"name": "calibrate, " + name,
                        "inputs": {"settings": st, "call": "calibrate", "spec": c, "alpha": [alpha] * n,
                                   "lowestRoot": lowest, "useFullApprox": full, "factors": s[0],
                                   "maxIterations": s[1], "tolerance": s[2], "innerMaxIterations": s[3],
                                   "innerTolerance": s[4]},
                        "expected": outcome(lambda a=args: H.call("CTSMMCapletOriginalCalibration.calibrate", *a))})
        for name, kw, alpha, lowest, full, factors in [
                ("QuantLib's case", {}, -0.05, True, False, 3),
                ("the highest root, full approximation", {}, -0.05, False, True, 3),
                ("displaced 1%, two factors", {"displacement": 0.01}, 0.1, True, False, 2),
                ("six annual rates", {"times": ANNUAL}, -0.05, True, False, 6)]:
            c = spec(H, **kw)
            n = len(c["rateTimes"]) - 1
            out.append({"name": "calibrationFunction, " + name,
                        "inputs": {"settings": st, "call": "calibrationFunction", "spec": c, "alpha": [alpha] * n,
                                   "lowestRoot": lowest, "useFullApprox": full, "factors": factors},
                        "expected": outcome(lambda c=c, n=n, a=alpha, lo=lowest, f=full, k=factors: H.call(
                            "CTSMMCapletOriginalCalibration::calibrationFunction", c, [a] * n, lo, f, k))})
        c = spec(H)
        out.append({"name": "calibrate, alpha short of the rates (refused)",
                    "inputs": {"settings": st, "call": "calibrate", "spec": c, "alpha": [0.0] * 9, "lowestRoot": True,
                               "useFullApprox": False, "factors": 3, "maxIterations": 2, "tolerance": 1e-5,
                               "innerMaxIterations": 50, "innerTolerance": 1e-9},
                    "expected": outcome(lambda: H.call("CTSMMCapletOriginalCalibration.calibrate", c, [0.0] * 9, True,
                                                       False, 3, 2, 1e-5, 50, 1e-9))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/capletcoterminalswaptioncalibration.hpp", cases)
