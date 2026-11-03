# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonSLVFDMModel's log (ql/models/equity/hestonslvfdmmodel.hpp, .cpp), which the wheel
doesn't bind, each output computed by the C++ harness (docs/porting-playbook.md §9.4): the density and mesher the
finite-difference calibration logs at each of its times.

tools/vectors/gen/cxx/models/equity/batesmodel.py's first Heston parameter set on flat curves, a constant local
volatility of 25%, to 73 days, on 51 x 51 points and at most 200 steps a year, under the log transformation with the
zero-correlation Green's function and modified Craig-Sneyd: the log's number of entries, and its first, second, eleventh
and last entries' times, densities and meshers.

Filtered by the stability filter: the case is refit with every input moved one ulp up, then down (the Heston parameters
and spot, the rates, the local volatility), and each Heston parameter and the spot alone; an entry's time, density or
mesher is written only where every refit leaves each of its values within 1e-12 x max(|a|, |b|, 1). The dropped count is
printed.

The harness's call is tools/cxx/src/models/equity/hestonslvfdmmodel.cpp's. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/Equity/HestonSLVFDMModelCxxVectorTests.cs."""
import copy
import importlib.util
import math
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
SCALARS = ["s0", "v0", "kappa", "theta", "sigma", "rho"]
# QuantLib's HestonSLVFokkerPlanckFdmParams in order: 17 numbers, the zero-correlation Green's function, the log
# transformation
PARAMS = [51, 51, 200, 50, 100.0, 5, 2, 0.1, 1e-4, 10000, 1e-5, 1e-5, 0.0000025, 1.0, 0.1, 0.9, 1e-5, 0, 2]
# QuantLib's FdmSchemeDesc::ModifiedCraigSneyd(): type 4, theta 1/3, mu 1/3
MCS = [4, 1.0 / 3.0, 1.0 / 3.0]
ENTRIES = [0, 1, 10]
KEYS = ["t", "prob", "x", "v"]


def ulp(x, direction):
    return math.nextafter(x, direction) if x != 0.0 else x


def refits(spec):
    out = []
    for d in (math.inf, -math.inf):
        s = copy.deepcopy(spec)
        for k in SCALARS + ["riskFree", "dividend"]:
            s["process"][k] = ulp(s["process"][k], d)
        s["volatility"] = ulp(s["volatility"], d)
        out.append(s)
    for k in SCALARS:
        for d in (math.inf, -math.inf):
            s = copy.deepcopy(spec)
            s["process"][k] = ulp(s["process"][k], d)
            out.append(s)
    return out


def stable(a, b):
    return abs(a - b) <= 1e-12 * max(abs(a), abs(b), 1.0)


def cases():
    with cxx.connect() as H:
        st = H.settings(R)
        spec = {"process": bm.process_spec(bm.HESTON[0]), "volatility": 0.25, "endDate": R + 73, "params": PARAMS,
                "scheme": MCS, "mixingFactor": 1.0}

        def log(s, entries):
            return H.call("HestonSLVFDMModel::logEntries", s["process"], s["volatility"], s["endDate"], s["params"],
                          s["scheme"], s["mixingFactor"], entries)

        size = int(log(spec, [])["size"])
        entries = ENTRIES + [size - 1]
        base = log(spec, entries)
        moved = [log(s, entries) for s in refits(spec)]
        expected = {"size": size}
        kept, dropped, total = [], 0, 0
        for i, e in enumerate(base["entries"]):
            for k in KEYS:
                values = e[k] if isinstance(e[k], list) else [e[k]]
                total += len(values)
                if all(stable(a, (m["entries"][i][k] if isinstance(e[k], list) else [m["entries"][i][k]])[j])
                       for m in moved for j, a in enumerate(values)):
                    expected[f"{k}{entries[i]}"] = e[k]
                    kept.append(f"{k}{entries[i]}")
                else:
                    dropped += len(values)
        print(f"the log: dropped {dropped} of {total} outputs", file=sys.stderr)
        return [{"name": "a constant local volatility, the log transformation",
                 "inputs": {"settings": st, "model": spec, "entries": entries, "kept": kept},
                 "expected": expected}]


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/hestonslvfdmmodel.hpp", cases)
