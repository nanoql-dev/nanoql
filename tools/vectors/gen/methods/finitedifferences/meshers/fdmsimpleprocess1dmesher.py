# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmSimpleProcess1dMesher (ql/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.hpp,
.cpp): the mesher spanning a 1-D process's quantiles averaged over its time steps, on the evaluation date 15 October 2026.

Ornstein-Uhlenbeck processes (the short-rate engines' factors: speeds of 0.03 to 0.3, volatilities of 0.008 to 0.02,
with and without a level and a start) and two Black-Scholes processes of tools/vectors/gen/processes/blackscholesprocess.py,
at sizes of 1, 2, 3, 10 and 51, maturities of 0.5 to 10 years, 1 to 10 averaging steps, epsilons of 1e-5 to 1e-2, and
mandatory points inside the range, at its ends and outside it. Each case's outputs are the locations, dplus and dminus
(QuantLib's Null<Real> written as null). A mesher of size 0, which QuantLib builds by reading past an empty array, is
nanoql's refusal (tests/NanoQL.Tests/Conventions/SimpleProcessMesherSizeTests.cs), not generated.

The 1-D mesher spec, which the short-rate finite-difference generators share, is {"type": "SimpleProcess", "size",
"process", "maturity", "tAvgSteps", "epsilon", "mandatoryPoint" (or null)}, the process {"type":
"OrnsteinUhlenbeckProcess", "speed", "volatility", "x0", "level"} or a Black-Scholes process spec; mesher_1d below
builds it and delegates every other 1-D spec to tools/vectors/gen/methods/finitedifferences/meshers/fdmblackscholesmesher.py,
and composite builds a composite of tools/vectors/gen/methods/finitedifferences/meshers/fdmmeshercomposite.py's form on
them. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i and stochasticprocess.i: FdmSimpleProcess1dMesher(size, process,
maturity, tAvgSteps, epsilon, mandatoryPoint) and OrnsteinUhlenbeckProcess(speed, volatility, x0, level), the C++
constructors; locations, dplus and dminus the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Meshers/FdmSimpleProcess1dMesherVectorTests.cs."""
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


mc = _load("fdmmeshercomposite", "methods/finitedifferences/meshers/fdmmeshercomposite.py")
bsm, bsp = mc.bsm, mc.bsm.bsp
R = bsp.R


def ou_spec(speed, volatility, x0=0.0, level=0.0):
    return {"type": "OrnsteinUhlenbeckProcess", "speed": speed, "volatility": volatility, "x0": x0, "level": level}


def mesher_spec(size, process, maturity, t_avg_steps=10, epsilon=0.0001, mandatory_point=None):
    return {"type": "SimpleProcess", "size": size, "process": process, "maturity": maturity, "tAvgSteps": t_avg_steps,
            "epsilon": epsilon, "mandatoryPoint": mandatory_point}


def process_1d(spec):
    if spec["type"] == "OrnsteinUhlenbeckProcess":
        return ql.OrnsteinUhlenbeckProcess(spec["speed"], spec["volatility"], spec["x0"], spec["level"])
    return bsp.process(spec)


def mesher_1d(spec):
    if spec["type"] != "SimpleProcess":
        return bsm.mesher_1d(spec)
    point = spec["mandatoryPoint"]
    return ql.FdmSimpleProcess1dMesher(spec["size"], process_1d(spec["process"]), spec["maturity"], spec["tAvgSteps"],
                                       spec["epsilon"], *([] if point is None else [point]))


def composite(spec):
    """The wheel's composite mesher for a spec, and its 1-D meshers (to keep them alive)."""
    meshers = [mesher_1d(m) for m in spec["meshers"]]
    if len(meshers) == 1:
        return ql.FdmMesherComposite(meshers[0]), meshers
    return ql.FdmMesherComposite(meshers), meshers


def read(m):
    return {"locations": v.enc(list(m.locations())),
            "dplus": [v.enc(m.dplus(i)) for i in range(m.size())],
            "dminus": [v.enc(m.dminus(i)) for i in range(m.size())]}


def specs():
    out = []
    ous = [("OU 0.1, 0.01", ou_spec(0.1, 0.01)), ("OU 0.03, 0.008", ou_spec(0.03, 0.008)),
           ("OU 0.3, 0.02, x0 0.01, level 0.02", ou_spec(0.3, 0.02, 0.01, 0.02))]
    for pname, p in ous:
        for size, maturity, steps, eps in ((51, 5.0, 1, 1e-5), (10, 0.5, 10, 1e-4), (3, 10.0, 4, 1e-2), (2, 1.0, 1, 1e-4),
                                           (1, 1.0, 1, 1e-4)):
            out.append((f"{pname}, {size} points to {maturity}, {steps} steps, eps {eps}",
                        mesher_spec(size, p, maturity, steps, eps)))
    p = ous[0][1]
    for point in (0.0, 0.004, -0.05, 0.05):
        out.append((f"OU 0.1, 0.01, mandatory point {point}", mesher_spec(21, p, 5.0, 1, 1e-5, point)))
    for i in (0, 5):
        pname, pspec = bsp.PROCESSES[i]
        out.append((f"{pname}, 11 points to 1", mesher_spec(11, pspec, 1.0, 5, 1e-4)))
    return out


def cases():
    st = v.settings(R)
    return [{"name": name, "inputs": {"settings": st, "mesher": spec}, "expected": read(mesher_1d(spec))}
            for name, spec in specs()]


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.hpp", cases)
