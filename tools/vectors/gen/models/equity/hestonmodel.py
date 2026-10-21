# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonModel (ql/models/equity/hestonmodel.hpp, .cpp), on the evaluation date 15 October 2026:
the model's parameters, read from its process.

Four processes on flat and linear zero curves: params (theta, kappa, sigma, rho and v0, in that order), each accessor
and the process's spot; then the parameters set to other values, read back with the accessors; and the constructor's
refusals of a theta, a kappa, a sigma or a v0 at zero and a rho past 1 (QuantLib's ConstantParameter, ": invalid
value"). The calibrated parameters aren't vectored (the S-eq plan's SEQR5); the model's prices are the analytic engine's
file's.

A case holds one process {"riskFree", "dividend", "s0", "v0", "kappa", "theta", "sigma", "rho"}, the curves
tools/vectors/gen/processes/blackscholesprocess.py's specs. The wheel's calls, by QuantLib-SWIG 1.44's
calibratedmodel.i: HestonModel(HestonProcess(...)), params, theta, kappa, sigma, rho, v0, process().s0() and setParams;
the wheel's Constraint binds no member, so the constraint's test is S-bond's CalibratedModel vectors'. Read back by
tests/NanoQL.Tests/Vectors/Models/HestonModelVectorTests.cs."""
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


bsp = _load("blackscholesprocess", "processes/blackscholesprocess.py")
R = bsp.R
PARAMETERS = [("equity", 100.0, 0.04, 1.5, 0.04, 0.3, -0.7), ("high correlation", 80.0, 0.09, 0.5, 0.06, 0.8, -0.9),
              ("positive correlation", 120.0, 0.0225, 3.0, 0.03, 0.15, 0.2), ("rho at -1", 100.0, 0.05, 2.0, 0.05, 0.5, -1.0)]
NEW = [0.05, 2.5, 0.45, -0.3, 0.07]


def process(spec):
    return ql.HestonProcess(ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                            ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])),
                            ql.QuoteHandle(ql.SimpleQuote(spec["s0"])), spec["v0"], spec["kappa"], spec["theta"],
                            spec["sigma"], spec["rho"])


def read(m):
    return {"params": list(m.params()), "theta": m.theta(), "kappa": m.kappa(), "sigma": m.sigma(), "rho": m.rho(),
            "v0": m.v0()}


def cases():
    st = v.settings(R)
    out = []
    for name, s0, v0, kappa, theta, sigma, rho in PARAMETERS:
        for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
            spec = {"riskFree": rf, "dividend": q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta, "sigma": sigma,
                    "rho": rho}
            m = ql.HestonModel(process(spec))
            expected = read(m)
            expected["s0"] = m.process().s0().value()
            m.setParams(ql.Array(NEW))
            expected.update({"after" + k[0].upper() + k[1:]: x for k, x in read(m).items()})
            out.append({"name": f"{name}, {cname} curves",
                        "inputs": {"settings": st, "process": spec, "newParams": NEW},
                        "expected": expected})
    base = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": 100.0, "v0": 0.04, "kappa": 1.5, "theta": 0.04,
            "sigma": 0.3, "rho": -0.7}
    for key, value in (("theta", 0.0), ("kappa", 0.0), ("sigma", 0.0), ("v0", 0.0), ("rho", 1.5)):
        spec = {**base, key: value}
        out.append({"name": f"{key} at {value}", "inputs": {"settings": st, "process": spec},
                    "expected": {"params": v.attempt(lambda: list(ql.HestonModel(process(spec)).params()))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/hestonmodel.hpp", cases)
