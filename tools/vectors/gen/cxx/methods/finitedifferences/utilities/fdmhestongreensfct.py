# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmHestonGreensFct (ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp, .cpp),
which the wheel binds for its enumeration only, each output computed by the C++ harness (docs/porting-playbook.md
§9.4): the Heston model's Green's function, the density after a short time from the spot and the initial variance.

tools/vectors/gen/cxx/models/equity/batesmodel.py's two Heston parameter sets on flat curves, each under the plain,
power and log transformations of the variance, by the zero-correlation and Gaussian algorithms, at a day and at 0.02
years, the leverage 1, and at 0.8 for the first set; on a mesher of 13 log-spot points concentrated at the spot and 9
variances (uniform from 0.002 to 0.5, or their logs under the log transformation). For each, the density at every
point.

Left out: the semi-analytical algorithm, which reads nanoql's fixed HestonProcess.Pdf where QuantLib's is NaN
(tests/NanoQL.Tests/Divergences/HestonGreensFctSemiAnalyticalTests.cs); structures dated or day-counted apart
(tests/NanoQL.Tests/Divergences/HestonSlvMismatchedTimesTests.cs).

The harness's call is tools/cxx/src/methods/finitedifferences/utilities/fdmhestongreensfct.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Methods/FiniteDifferences/Utilities/FdmHestonGreensFctCxxVectorTests.cs."""
import importlib.util
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


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


bm = _load("cxx_batesmodel", "cxx/models/equity/batesmodel.py")
R = bm.R
TYPES = [("plain", 0), ("power", 1), ("log", 2)]
ALGORITHMS = [("zero correlation", 0), ("Gaussian", 1)]
TIMES = [1.0 / 365, 0.02]
VARIANCES = [0.002 + i * (0.5 - 0.002) / 8 for i in range(9)]


def locations(process, transform):
    x0 = math.log(process["s0"])
    x = list(ql.Concentrating1dMesher(x0 - 0.5, x0 + 0.5, 13, (x0, 0.1), True).locations())
    return [x, [math.log(u) for u in VARIANCES] if transform == 2 else VARIANCES]


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        out = []
        variants = [(params, l0) for params in bm.HESTON for l0 in (1.0,)] + [(bm.HESTON[0], 0.8)]
        for params, l0 in variants:
            process = bm.process_spec(params)
            for tname, t in TYPES:
                mesher = locations(process, t)
                for aname, a in ALGORITHMS:
                    densities = [H.call("FdmHestonGreensFct::get", process, mesher, t, l0, time, a) for time in TIMES]
                    out.append({"name": f"{params[0]}, {tname}, {aname}, leverage {l0}",
                                "inputs": {"settings": st, "process": process, "locations": mesher, "trafoType": t,
                                           "l0": l0, "times": TIMES, "algorithm": a},
                                "expected": {"density": densities}})
        return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmhestongreensfct.hpp", cases)
