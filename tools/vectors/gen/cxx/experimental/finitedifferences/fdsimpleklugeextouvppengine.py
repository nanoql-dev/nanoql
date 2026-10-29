# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's energy engines and VPP step condition the wheel doesn't expose (ql/experimental/
finitedifferences/fdextoujumpvanillaengine, fdsimpleextoustorageengine, fdklugeextouspreadengine,
fdsimpleklugeextouvppengine, dynprogvppintrinsicvalueengine, fdmvppstepconditionfactory and
fdmvppstartlimitstepcondition, .hpp and .cpp each), each output computed by the C++ harness, on the evaluation date 18
December 2011:

- the jump vanilla engine: European and American calls and puts on Kluge's spot, with and without a seasonal shape;
- the storage engine: QuantLib's storage case on small grids, on the elevator and a uniform storage mesher, and a
  partly filled storage with a shape;
- the spread engine: spread options on power and gas at heat rates 1.5 and 2 on the spread case's process;
- the VPP engine: a plant over a day of hours on the VPP case's process, on shapes from the VPP case's prices, with and
  without a start limit;
- the intrinsic engine, the factory's state mesher and the start-limit condition's rollback: plants with minimum up
  and down times of 2 and 6, start limits of none, 1 and 3, at efficiencies from 0.35 to 0.9, on the VPP case's first
  hours of prices.

No output is left out (the S-xeq plan's §2.8): every shape's last time is past each engine's last lookup (XER5 (xxxi));
the intrinsic engine's fuel and power lists are as long. Nothing is fitted, so no output is filtered.

The processes are tools/cxx/src/experimental/processes/extouspec.hpp's specs and the methods
tools/cxx/src/experimental/finitedifferences/fdsimpleklugeextouvppengine.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/FiniteDifferences/FdSimpleKlugeExtOUVPPEngineCxxVectorTests.cs."""
import math
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 40895  # 18 December 2011
CALL, PUT = 1, -1
FUEL = [20.74, 21.65, 20.78, 21.58, 21.43, 20.82, 22.02, 21.52, 21.02, 21.46, 21.75, 20.69,
        22.16, 20.38, 20.82, 20.68, 20.57, 21.92, 22.04, 20.45, 20.75, 21.92, 20.53, 20.67, 20.88]
POWER = [40.40, 36.71, 31.87, 25.81, 31.61, 35.00, 46.22, 60.68, 42.45, 38.01, 33.84, 29.79,
         31.84, 38.53, 49.23, 59.92, 43.85, 37.47, 34.89, 29.99, 30.85, 29.19, 29.25, 38.67, 36.90]


def ou(speed, sigma, x0, level):
    return {"speed": speed, "sigma": sigma, "x0": x0, "level": level, "discretization": 0, "intEps": 1e-4}


def vpp(heat_rate, t_min_up, t_min_down, n_starts=None):
    return {"heatRate": heat_rate, "pMin": 8.0, "pMax": 40.0, "tMinUp": t_min_up, "tMinDown": t_min_down,
            "startUpFuel": 20.0, "startUpFixCost": 100.0, "nStarts": n_starts}


def case(name, st, method, args, expected):
    return {"name": name, "inputs": {"settings": st, "method": method, "args": args}, "expected": expected}


def cases():
    out = []
    constant = {"type": "constant", "b": 3.0}
    zero = {"type": "constant", "b": 0.0}
    swing = {"ou": ou(1.0, 2.0, 3.0, constant), "y0": 0.0, "beta": 5.0, "jumpIntensity": 1.0, "eta": 2.0}
    spread = {"kluge": swing, "gas": ou(1.0, math.sqrt(1.4), 3.0, {"type": "linear", "c0": 3.0, "c1": 0.0}),
              "rho": 0.5}
    vppProcess = {"kluge": {"ou": ou(7.0, 1.4, 0.0, zero), "y0": 0.0, "beta": 200.0, "jumpIntensity": 4.0,
                            "eta": 5.0}, "gas": ou(4.45, math.sqrt(1.3), 0.0, zero), "rho": 0.7}
    with cxx.connect() as H:
        st = H.settings(R)
        # the jump vanilla engine
        for name, type_, strike, american, shape in [
                ("jump vanilla: a European call", CALL, 30.0, False, None),
                ("jump vanilla: an American put", PUT, 25.0, True, None),
                ("jump vanilla: a European put with a shape", PUT, 30.0, False, [[0.25, 0.1], [0.6, -0.1]])]:
            args = [swing, type_, strike, american, 6, 0.1, [20, 40, 20], shape]
            out.append(case(name, st, "FdExtOUJumpVanillaEngine::npv", args,
                            {"npv": H.call("FdExtOUJumpVanillaEngine::npv", *args)}))
        # the storage engine
        storage = ou(1.0, 0.5, 3.0, constant)
        for name, args in [
                ("storage: QuantLib's case, the elevator mesher", [storage, 0.1, 50.0, 0.0, 1.0, 30, [1, 25], None,
                                                                   None]),
                ("storage: a uniform mesher", [storage, 0.1, 50.0, 0.0, 1.0, 30, [1, 25], 21, None]),
                ("storage: partly filled, a shape", [storage, 0.05, 20.0, 8.0, 2.0, 20, [2, 20], None,
                                                     [[0.03, 0.05], [0.06, -0.05]]])]:
            out.append(case(name, st, "FdSimpleExtOUStorageEngine::npv", args,
                            {"npv": H.call("FdSimpleExtOUStorageEngine::npv", *args)}))
        # the spread engine
        for name, heat_rate in [("spread: heat rate 2", 2.0), ("spread: heat rate 1.5", 1.5)]:
            args = [spread, heat_rate, 0.0, 12, [5, 30, 10, 10], None, None]
            out.append(case(name, st, "FdKlugeExtOUSpreadEngine::npv", args,
                            {"npv": H.call("FdKlugeExtOUSpreadEngine::npv", *args)}))
        # the VPP engine on a day of hours, its shapes over three days, past the engine's last lookup
        hours = 72
        fuel_shape, power_shape = [], []
        for i in range(hours):
            t = (i + 1) / (365 * 24.0)
            fuel, power = FUEL[i % len(FUEL)], POWER[i % len(POWER)]
            fuel_shape.append([t, math.log(fuel) - 1.3 / (4 * 4.45) * (1 - math.exp(-2 * 4.45 * t))])
            power_shape.append([t, math.log(power) - 1.4 ** 2 / (4 * 7.0) * (1 - math.exp(-2 * 7.0 * t))
                                - 4.0 / 200.0 * math.log((5.0 - math.exp(-200.0 * t)) / (5.0 - 1.0))])
        for name, plant in [("VPP: heat rate 2.5, up 6 and down 2", vpp(2.5, 6, 2)),
                            ("VPP: heat rate 1.8, up 2 and down 2, 1 start", vpp(1.8, 2, 2, 1))]:
            args = [vppProcess, plant, 1, 0.0, fuel_shape, power_shape, 3.0, [1, 10, 5, 6]]
            out.append(case(name, st, "FdSimpleKlugeExtOUVPPEngine::npv", args,
                            {"npv": H.call("FdSimpleKlugeExtOUVPPEngine::npv", *args)}))
        # the intrinsic engine, the state mesher and the condition's rollback
        for efficiency in (0.35, 0.5, 0.6, 0.9):
            for up, down, starts in ((2, 2, None), (6, 2, None), (2, 2, 1), (2, 3, 3)):
                plant = vpp(1.0 / efficiency, up, down, starts)
                label = f"efficiency {efficiency}, up {up}, down {down}, starts {starts}"
                args = [plant, 1, FUEL, POWER, 3.0]
                out.append(case(f"intrinsic: {label}", st, "DynProgVPPIntrinsicValueEngine::npv", args,
                                {"npv": H.call("DynProgVPPIntrinsicValueEngine::npv", *args)}))
                if efficiency == 0.5:
                    out.append(case(f"state mesher: {label}", st, "FdmVPPStepConditionFactory::stateMesher",
                                    [plant, 1],
                                    {"locations": H.call("FdmVPPStepConditionFactory::stateMesher", plant, 1)}))
                    out.append(case(f"rollback: {label}", st, "FdmVPPStartLimitStepCondition::rollback", args,
                                    H.call("FdmVPPStartLimitStepCondition::rollback", *args)))
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/finitedifferences/fdsimpleklugeextouvppengine.hpp", cases)
