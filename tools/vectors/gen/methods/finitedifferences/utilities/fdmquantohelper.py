# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmQuantoHelper (ql/methods/finitedifferences/utilities/fdmquantohelper.hpp, .cpp), on the
evaluation date 15 October 2026. The wheel binds only its constructor, so its quantoAdjustment (the domestic less the
foreign forward rate, plus the correlation times the equity and exchange-rate volatilities) is read through the
Black-Scholes operator it adjusts: FdmBlackScholesOp's apply and solve_splitting after setTime, as
tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesop.py reads them, with the Black and the local
volatility, over helpers with five correlations, two constant and one curve exchange-rate volatility, flat and zero
foreign curves, two at-the-money levels (the strike the exchange-rate volatility is read at, which a surface reads), and
a domestic curve other than the process's. Every structure is on Act/365 and dated from the evaluation date, the
risk-free curve's day counter and reference date: on others QuantLib reads them at the risk-free curve's times,
where nanoql reads them at their instants (tests/NanoQL.Tests/Divergences/FdmMismatchedTimesTests.cs).

The specs are fdmblackscholesmesher.py's quanto helpers and fdmblackscholesop.py's operators. The wheel's calls, by
QuantLib-SWIG 1.44's fdm.i: FdmQuantoHelper(rTS, fTS, fxVolTS, equityFxCorrelation, exchRateATMlevel), the C++
constructor, given to FdmBlackScholesOp. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Utilities/FdmQuantoHelperVectorTests.cs."""
import importlib.util
import os
import random
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import vectors as v  # noqa: E402


def _load(name, rel):
    spec = importlib.util.spec_from_file_location(name, os.path.join(v.GEN_ROOT, *rel.split("/")))
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


fbo = _load("fdmblackscholesop", "methods/finitedifferences/operators/fdmblackscholesop.py")
mc, bsm, bsp = fbo.mc, fbo.bsm, fbo.bsp
R = bsm.R
FX_CONSTANT = bsm.bvts.vol_spec_constant(R, 0.12)
FX_HIGH = bsm.bvts.vol_spec_constant(R, 0.3)
FX_CURVE = bsm.bvts.vol_spec_curve(R, [182, 365, 730], [0.10, 0.11, 0.12])
FOREIGN_FLAT = bsp.flat_spec(R, 0.06)
FOREIGN_ZERO = bsp.zero_spec(R, [0, 365, 1825], [0.05, 0.055, 0.06])


def helpers():
    out = []
    for corr in (-0.9, -0.3, 0.0, 0.5, 0.9):
        out.append((f"correlation {corr}", bsm.quanto_spec(bsp.FLAT_R, FOREIGN_FLAT, FX_CONSTANT, corr)))
    out += [("high exchange-rate volatility", bsm.quanto_spec(bsp.FLAT_R, FOREIGN_FLAT, FX_HIGH, -0.5)),
            ("exchange-rate volatility curve", bsm.quanto_spec(bsp.FLAT_R, FOREIGN_FLAT, FX_CURVE, -0.5)),
            ("zero foreign curve", bsm.quanto_spec(bsp.FLAT_R, FOREIGN_ZERO, FX_CONSTANT, -0.5)),
            ("at-the-money level 0.8", bsm.quanto_spec(bsp.FLAT_R, FOREIGN_FLAT, FX_CURVE, -0.5, 0.8)),
            ("another domestic curve", bsm.quanto_spec(bsp.ZERO_R, FOREIGN_ZERO, FX_CONSTANT, 0.4, 1.2))]
    return out


def cases():
    st = v.settings(R)
    rng = random.Random(20261029)
    out = []
    for pname, p in (("flat", bsp.PROCESSES[0][1]), ("surface", bsp.PROCESSES[3][1])):
        mesher = mc.composite_spec(bsm.bs_spec(21, p, 1.0, 100.0))
        for hname, helper in helpers():
            for local in (False, True):
                spec = fbo.op_spec(mesher, p, local_vol=local, quanto=helper)
                r = [rng.uniform(-1.0, 1.0) for _ in range(21)]
                expected = fbo.outputs(spec, r)
                out.append({"name": f"{pname}, {hname}{', local volatility' if local else ''}",
                            "inputs": {"settings": st, "op": spec, "r": r, "times": [list(t) for t in fbo.TIMES],
                                       "s": fbo.S},
                            "expected": {k: expected[k] for k in ("apply", "solveSplitting")}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/utilities/fdmquantohelper.hpp", cases)
