# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BatesDetJumpModel, BatesDoubleExpModel and BatesDoubleExpDetJumpModel
(ql/models/equity/batesmodel.hpp, .cpp), which the wheel doesn't bind, each output computed by the C++ harness
(docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

The det-jump model on three Bates processes and the double-exponential models on two Heston processes, each at
QuantLib's default arguments and at other ones: params and every accessor, then the parameters set to other values
(setParams), read back, with the process generateArguments builds: its type (a Bates process for the det-jump model,
a Heston process for the double-exponential ones) and its v0, kappa, theta, sigma and rho, and a Bates process's
lambda, nu and delta. Then the constructors' refusals of a mean-reversion speed of the intensity
at 0, a probability of an up-jump above 1 and an intensity at 0 (QuantLib's ConstantParameter, ": invalid value").

Left out (the S-hes plan's §2.10): an up-jump mean nuUp at or above 1, where nanoql's constraint refuses the value
QuantLib takes (HR7 (v), tests/NanoQL.Tests/Divergences/BatesDoubleExpUpJumpTests.cs); every nuUp here is below 1,
before and after setParams.

A case holds the "type", the "process" (the harness's spec, tools/cxx/src/models/equity/hestonspec.hpp: flat
forwards at "riskFree" and "dividend" from the "referenceDate" on the "dayCounter"), the constructor's "arguments" (a
prefix, QuantLib's defaults for the rest) and the "newParams" or null. The harness's call: BatesModel::read
(tools/cxx/src/models/equity/batesmodel.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/Equity/BatesModelCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402

R = 46310
A365 = {"type": "Actual365Fixed"}
HESTON = [  # s0, v0, kappa, theta, sigma, rho
    ("equity", 100.0, 0.04, 1.5, 0.04, 0.3, -0.7),
    ("high vol of variance", 50.0, 0.09, 0.8, 0.06, 1.2, 0.4),
]
JUMPS = [("batesmodel's jumps", 2.0, -0.2, 0.1), ("rare large jumps", 0.3, 0.05, 0.3)]


def process_spec(params, jumps=None, rf=0.04, q=0.02):
    _, s0, v0, kappa, theta, sigma, rho = params
    spec = {"referenceDate": R, "dayCounter": A365, "riskFree": rf, "dividend": q, "s0": s0, "v0": v0,
            "kappa": kappa, "theta": theta, "sigma": sigma, "rho": rho}
    if jumps is not None:
        spec.update({"lambda": jumps[1], "nu": jumps[2], "delta": jumps[3]})
    return spec


BATES = [(f"{h[0]}, {j[0]}", process_spec(h, j)) for h in HESTON for j in JUMPS][:3]
MODELS = [
    ("BatesDetJumpModel", [[], [2.0, 0.3]], [0.05, 2.5, 0.45, -0.3, 0.07, -0.05, 0.2, 0.8, 1.7, 0.25]),
    ("BatesDoubleExpModel", [[], [0.3, 0.2, 0.15, 0.4]], [0.05, 2.5, 0.45, -0.3, 0.07, 0.35, 0.12, 0.25, 0.6]),
    ("BatesDoubleExpDetJumpModel", [[], [0.3, 0.2, 0.15, 0.4, 1.5, 0.2]],
     [0.05, 2.5, 0.45, -0.3, 0.07, 0.35, 0.12, 0.25, 0.6, 2.2, 0.15]),
]
PROCESS_KEYS = ["v0", "kappa", "theta", "sigma", "rho", "lambda", "nu", "delta"]
REFUSED = [("BatesDetJumpModel", "kappaLambda at 0", [0.0, 0.1]),
           ("BatesDoubleExpModel", "p above 1", [0.1, 0.1, 0.1, 1.5]),
           ("BatesDoubleExpDetJumpModel", "lambda at 0", [0.0])]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)

        def case(name, mtype, process, arguments, new_params):
            result = v.attempt(lambda: H.call("BatesModel::read", mtype, process, arguments, new_params))
            if "throws" in result:
                expected = {"params": result}
            else:
                expected = dict(result)
                built = expected.pop("afterProcess", None)
                if built is not None:
                    expected["afterProcessType"] = built["type"]
                    expected["afterProcess"] = [built[k] for k in PROCESS_KEYS if k in built]
            out.append({"name": name, "inputs": {"settings": st, "type": mtype, "process": process,
                                                 "arguments": arguments, "newParams": new_params},
                        "expected": expected})

        for mtype, argument_sets, new_params in MODELS:
            processes = BATES if mtype == "BatesDetJumpModel" else [(h[0], process_spec(h)) for h in HESTON]
            for pname, process in processes:
                for arguments in argument_sets:
                    label = "defaults" if not arguments else "arguments " + ", ".join(f"{x:g}" for x in arguments)
                    case(f"{mtype}, {pname}, {label}", mtype, process, arguments, new_params)
        for mtype, name, arguments in REFUSED:
            process = BATES[0][1] if mtype == "BatesDetJumpModel" else process_spec(HESTON[0])
            case(f"{mtype}, {name}", mtype, process, arguments, None)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/batesmodel.hpp", cases)
