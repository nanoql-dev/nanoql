# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's CTSMMCapletMaxHomogeneityCalibration
(ql/models/marketmodels/models/capletcoterminalmaxhomogeneity.hpp, .cpp), which the wheel doesn't expose, each output
computed by the C++ harness.

The calibration on QuantLib's case (marketmodel_smmcaplethomocalibration.cpp: capletcoterminalswaptioncalibration.py's
setup, priority 1, three factors, ten passes) and on varied ones: priorities 0 and 0.5, the projection (no inner
iterations), one and ten factors, displaced 1%, the caplets at 70% (failures), another abcd, six annual rates; each the
calibration's results (calibrations.cpp). The pass function alone (capletMaxHomogeneityCalibration) on QuantLib's case
and three varied ones: its failures, swaption error and pseudo-roots. Throws: a priority outside [0, 1].

The deformation size is left out: QuantLib's sum adds the step index's change once per volatility
(tests/NanoQL.Tests/Divergences/CTSMMCapletMaxHomogeneityDeformationTests.cs).

The harness unit is tools/cxx/src/models/marketmodels/models/calibrations.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/CTSMMCapletMaxHomogeneityCalibrationCxxVectorTests.cs."""
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
        settings = [3, 10, 1e-4, 100, 1e-8]
        variations = [
            ("QuantLib's case", {}, 1.0, settings),
            ("priority 0", {}, 0.0, settings),
            ("priority 0.5", {}, 0.5, settings),
            ("the projection", {}, 1.0, [3, 10, 1e-4, 0, 1e-8]),
            ("one factor", {}, 1.0, [1, 10, 1e-4, 100, 1e-8]),
            ("ten factors", {}, 1.0, [10, 10, 1e-4, 100, 1e-8]),
            ("displaced 1%", {"displacement": 0.01}, 1.0, settings),
            ("caplets at 70%", {"scale": 0.7}, 1.0, settings),
            ("caplets at 70%, priority 0", {"scale": 0.7}, 0.0, settings),
            ("another abcd", {"abcd": OTHER_ABCD}, 1.0, settings),
            ("six annual rates", {"times": ANNUAL}, 1.0, settings),
        ]
        for name, kw, priority, s in variations:
            c = spec(H, **kw)
            args = [c, priority] + s
            out.append({"name": "calibrate, " + name,
                        "inputs": {"settings": st, "call": "calibrate", "spec": c, "priority": priority,
                                   "factors": s[0], "maxIterations": s[1], "tolerance": s[2],
                                   "innerMaxIterations": s[3], "innerTolerance": s[4]},
                        "expected": outcome(lambda a=args: H.call("CTSMMCapletMaxHomogeneityCalibration.calibrate",
                                                                  *a))})
        for name, kw, priority, factors, iterations in [
                ("QuantLib's case", {}, 1.0, 3, 100),
                ("the projection", {}, 1.0, 3, 0),
                ("caplets at 70%, priority 0", {"scale": 0.7}, 0.0, 3, 100),
                ("six annual rates, two factors", {"times": ANNUAL}, 0.5, 2, 100)]:
            c = spec(H, **kw)
            args = [c, priority, factors, iterations, 1e-8]
            out.append({"name": "capletMaxHomogeneityCalibration, " + name,
                        "inputs": {"settings": st, "call": "capletMaxHomogeneityCalibration", "spec": c,
                                   "priority": priority, "factors": factors, "maxIterations": iterations,
                                   "tolerance": 1e-8},
                        "expected": outcome(lambda a=args: H.call(
                            "CTSMMCapletMaxHomogeneityCalibration::capletMaxHomogeneityCalibration", *a))})
        c = spec(H)
        out.append({"name": "calibrate, priority 1.5 (refused)",
                    "inputs": {"settings": st, "call": "calibrate", "spec": c, "priority": 1.5, "factors": 3,
                               "maxIterations": 10, "tolerance": 1e-4, "innerMaxIterations": 100,
                               "innerTolerance": 1e-8},
                    "expected": outcome(lambda: H.call("CTSMMCapletMaxHomogeneityCalibration.calibrate", c, 1.5, 3, 10,
                                                       1e-4, 100, 1e-8))})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/models/capletcoterminalmaxhomogeneity.hpp", cases)
