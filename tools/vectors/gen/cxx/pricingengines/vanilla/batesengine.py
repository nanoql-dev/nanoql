# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's BatesDetJumpEngine, BatesDoubleExpEngine and BatesDoubleExpDetJumpEngine
(ql/pricingengines/vanilla/batesengine.hpp, .cpp), and the Bates engines' numbers of evaluations, which the wheel
doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October
2026.

Each engine on two processes (Bates processes for the Bates and det-jump engines, Heston processes for the
double-exponential ones, tools/vectors/gen/cxx/models/equity/batesmodel.py's), its model at QuantLib's default
arguments and at other ones (the Bates engine's model has none), by the Gauss-Laguerre constructor at QuantLib's default
order 144 and at 64 and by the Gauss-Lobatto constructor at a relative tolerance of 1e-8 and 10,000 evaluations: the
NPV and the number of evaluations of calls and puts at 80%, 100% and 120% of the spot and three maturities.

Left out (the S-hes plan's §2.10): the det-jump engines where the intensity's mean-reversion speed times the maturity
is below 1e-2, which nanoql evaluates by a series (HR7 (iv), tests/NanoQL.Tests/Divergences/
BatesDetJumpSmallReversionTests.cs: here it is at least 0.25); and the double-exponential engines at an up-jump mean of
1 or more, which nanoql refuses (HR7 (v), tests/NanoQL.Tests/Divergences/BatesDoubleExpUpJumpTests.cs: here it is at
most 0.2).

A case holds the "type", the "process" (batesmodel.py's harness spec), the model's "arguments", the "integration"
({"order"} or {"relTolerance", "maxEvaluations"}), and the "maturities" (days from the evaluation date) and "strikes";
its outputs are in maturity, then call and put, then strike order. The harness's call: BatesEngine::results
(tools/cxx/src/pricingengines/vanilla/batesengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/BatesEngineCxxVectorTests.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import cxx  # noqa: E402
import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
MATURITIES = [91, 365, 1825]
STRIKES = [0.8, 1.0, 1.2]
INTEGRATIONS = [("Laguerre 144", {"order": 144}), ("Laguerre 64", {"order": 64}),
                ("Lobatto at 1e-8", {"relTolerance": 1e-8, "maxEvaluations": 10000})]
ENGINES = [
    ("BatesEngine", [[]]),
    ("BatesDetJumpEngine", [[], [2.0, 0.3]]),
    ("BatesDoubleExpEngine", [[], [0.3, 0.2, 0.15, 0.4]]),
    ("BatesDoubleExpDetJumpEngine", [[], [0.3, 0.2, 0.15, 0.4, 1.5, 0.2]]),
]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for etype, argument_sets in ENGINES:
            bates = etype in ("BatesEngine", "BatesDetJumpEngine")
            processes = bm.BATES[:2] if bates else [(h[0], bm.process_spec(h)) for h in bm.HESTON]
            for pname, process in processes:
                strikes = [round(k * process["s0"], 8) for k in STRIKES]
                for arguments in argument_sets:
                    for iname, integration in INTEGRATIONS:
                        npv, count = [], []
                        for days in MATURITIES:
                            for option_type in (1, -1):
                                for k in strikes:
                                    r = v.attempt(lambda: H.call("BatesEngine::results", etype, process, arguments,
                                                                 integration, option_type, k, R + days))
                                    npv.append(r if "throws" in r else v.enc(r["NPV"]))
                                    count.append(r if "throws" in r else r["numberOfEvaluations"])
                        label = "defaults" if not arguments else "arguments " + ", ".join(f"{x:g}" for x in arguments)
                        out.append({"name": f"{etype}, {pname}, {label}, {iname}",
                                    "inputs": {"settings": st, "type": etype, "process": process,
                                               "arguments": arguments, "integration": integration,
                                               "maturities": MATURITIES, "strikes": strikes},
                                    "expected": {"NPV": npv, "numberOfEvaluations": count}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/batesengine.hpp", cases)
