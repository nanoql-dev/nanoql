# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's Fdm1DimSolver and FdmSolverDesc (ql/methods/finitedifferences/solvers/fdm1dimsolver.hpp,
.cpp, fdmsolverdesc.hpp), on the evaluation date 15 October 2026: the one-dimensional solver's rollback from the
calculator's cell averages at maturity, with its theta snapshot joined to the conditions, read through its cubic
spline: interpolateAt, thetaAt (null where the first stopping time is 0, as QuantLib's Null<Real>), derivativeX and
derivativeXX at five log spots.

A European and an American put on a flat Merton process; a call on zero curves and a variance surface, damped, under
the Douglas, Crank-Nicolson and implicit Euler descriptions; with a Bermudan condition, cash dividends and the local
volatility; and with a snapshot at time 0, where thetaAt is null.

The specs are tools/vectors/gen/methods/finitedifferences/solvers/fdmbackwardsolver.py's, a solver {"op",
"calculator", "conditions", "stoppingTimes", "referenceDate", "dayCounter", "scheme", "maturity", "timeSteps",
"dampingSteps"}. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmSolverDesc(mesher, bcSet, condition,
calculator, maturity, timeSteps, dampingSteps) and Fdm1DimSolver(solverDesc, schemeDesc, op), the C++ constructors;
interpolateAt, thetaAt, derivativeX and derivativeXX the C++ members. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/Fdm1DimSolverVectorTests.cs."""
import importlib.util
import math
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


fbs = _load("fdmbackwardsolver", "methods/finitedifferences/solvers/fdmbackwardsolver.py")
fbo, mc, bsm, bsp, bc = fbs.fbo, fbs.mc, fbs.bsm, fbs.bsp, fbs.bc
R = fbs.R
XS = [math.log(s) for s in (80.0, 90.0, 100.0, 110.0, 125.0)]


def solver_spec(op, calc, conditions, desc, maturity, steps, damping):
    spec = fbs.rollback_spec(op, calc, conditions, desc, maturity, 0.0, steps, damping)
    spec["maturity"] = spec.pop("from")
    del spec["to"]
    spec["timeSteps"] = spec.pop("steps")
    return spec


def outputs(spec):
    op, mesher, keep = fbo.bs_op(spec["op"])
    calc = fbs.calculator(spec["calculator"], mesher)
    reference = ql.Date(spec["referenceDate"])
    comp, times, conds = fbs.composite(spec["conditions"], mesher, calc, reference,
                                       bsm.bvts.day_counter(spec["dayCounter"]))
    spec["stoppingTimes"] = v.enc(times)
    desc = ql.FdmSolverDesc(mesher, ql.FdmBoundaryConditionSet(), comp, calc, spec["maturity"], spec["timeSteps"],
                            spec["dampingSteps"])
    solver = ql.Fdm1DimSolver(desc, fbs.scheme(spec["scheme"]), op)
    return {"interpolateAt": v.enc([solver.interpolateAt(x) for x in XS]),
            "thetaAt": v.enc([solver.thetaAt(x) for x in XS]),
            "derivativeX": v.enc([solver.derivativeX(x) for x in XS]),
            "derivativeXX": v.enc([solver.derivativeXX(x) for x in XS])}


def cases():
    st = v.settings(R)
    flat, surface = bsp.PROCESSES[0][1], bsp.PROCESSES[3][1]
    put = bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 100.0)
    call = bc.payoff_spec("PlainVanilla", int(ql.Option.Call), 110.0)
    m_flat = mc.composite_spec(bsm.bs_spec(101, flat, 1.0, 100.0))
    m_surface = mc.composite_spec(bsm.bs_spec(81, surface, 1.5, 110.0, c_point=(110.0, 0.1)))
    bermudan = [{"type": "Bermudan", "dates": [(R + d).serialNumber() for d in (91, 182, 273, 365)]}]
    dividends = [{"type": "Dividend", "dividends": [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)], "direction": 0}]
    douglas = ql.FdmSchemeDesc.Douglas()
    setups = [
        ("European put", fbo.op_spec(m_flat, flat), put, [], douglas, 1.0, 100, 0),
        ("American put", fbo.op_spec(m_flat, flat), put, [{"type": "American"}], douglas, 1.0, 100, 0),
        ("Bermudan put", fbo.op_spec(m_flat, flat), put, bermudan, douglas, 1.0, 100, 0),
        ("put with dividends", fbo.op_spec(m_flat, flat), put, dividends, douglas, 1.0, 100, 0),
        ("American put with dividends", fbo.op_spec(m_flat, flat), put, [{"type": "American"}] + dividends, douglas,
         1.0, 100, 0),
        ("snapshot at time 0", fbo.op_spec(m_flat, flat), put, [{"type": "Snapshot", "t": 0.0}], douglas, 1.0, 50, 0),
        ("local volatility, put", fbo.op_spec(m_surface, surface, local_vol=True), put, [], douglas, 1.5, 60, 0),
    ]
    for dname in ("Douglas", "CrankNicolson", "ImplicitEuler"):
        setups.append((f"surface call, damped, {dname}", fbo.op_spec(m_surface, surface, strike=110.0), call, [],
                       getattr(ql.FdmSchemeDesc, dname)(), 1.5, 80, 2))
    out = []
    for name, op, payoff, conds, desc, maturity, steps, damping in setups:
        spec = solver_spec(op, fbs.calculator_spec("Log", payoff), conds, desc, maturity, steps, damping)
        expected = outputs(spec)
        out.append({"name": name, "inputs": {"settings": st, "solver": spec, "x": XS}, "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdm1dimsolver.hpp", cases)
