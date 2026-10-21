# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's FdmBackwardSolver and FdmSchemeDesc
(ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp, .cpp), on the evaluation date 15 October 2026: rollbacks of
a payoff's cell-averaged values through the Black-Scholes operator, the solver dispatching on the scheme description and
running the implicit Euler damping steps first, its finite-difference model stopping at the conditions' times.

The ten named descriptions' types, thetas and mus; then rollbacks under the Douglas, Crank-Nicolson and implicit Euler
descriptions with no, two and three damping steps, from one year to zero and to a quarter; with an American condition, a
Bermudan condition, a snapshot, cash dividends, and an American condition with dividends; and the refusal of a
description of an unknown type ("Unknown scheme type"). Each scheme's own file
(tools/vectors/gen/methods/finitedifferences/schemes/) rolls back through this solver too.

The specs, which the scheme and step-condition generators share, are a rollback {"op", "calculator", "conditions",
"stoppingTimes", "referenceDate", "dayCounter", "scheme", "from", "to", "steps", "dampingSteps"} with the initial values
an input, the operator tools/vectors/gen/methods/finitedifferences/operators/fdmblackscholesop.py's, a calculator
{"type": "Log" | "CellAveraging" | "Zero", "payoff", "direction"}, a condition {"type": "American"}, {"type":
"Bermudan", "dates"}, {"type": "Snapshot", "t"} or {"type": "Dividend", "dividends", "direction"}, read with the
reference date and day counter, and the stopping times written out as QuantLib's conditions give them; a scheme {"type"
(QuantLib's FdmSchemeType), "theta", "mu"}. The wheel's calls, by QuantLib-SWIG 1.44's fdm.i: FdmBackwardSolver(op,
bcSet, conditions, schemeDesc) and rollback, FdmSchemeDesc's constructor and named factories,
FdmStepConditionComposite(stoppingTimes, conditions), FdmAmericanStepCondition, FdmBermudanStepCondition,
FdmSnapshotCondition and FdmDividendHandler, FdmLogInnerValue, FdmCellAveragingInnerValue and FdmZeroInnerValue, each
the C++ constructor or member. Read back by
tests/NanoQL.Tests/Vectors/Methods/FiniteDifferences/Solvers/FdmBackwardSolverVectorTests.cs."""
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


fbo = _load("fdmblackscholesop", "methods/finitedifferences/operators/fdmblackscholesop.py")
bc = _load("blackcalculator", "pricingengines/blackcalculator.py")
mc, bsm, bsp = fbo.mc, fbo.bsm, fbo.bsp
R = bsm.R
NAMED = ["Douglas", "CrankNicolson", "CraigSneyd", "ModifiedCraigSneyd", "Hundsdorfer", "ModifiedHundsdorfer",
         "ExplicitEuler", "ImplicitEuler", "MethodOfLines", "TrBDF2"]


def scheme_spec(desc):
    return {"type": int(desc.type), "theta": desc.theta, "mu": desc.mu}


def scheme(spec):
    return ql.FdmSchemeDesc(spec["type"], spec["theta"], spec["mu"])


def calculator_spec(kind, payoff_spec, direction=0):
    return {"type": kind, "payoff": payoff_spec, "direction": direction}


def calculator(spec, mesher):
    if spec["type"] == "Zero":
        return ql.FdmZeroInnerValue()
    cls = ql.FdmLogInnerValue if spec["type"] == "Log" else ql.FdmCellAveragingInnerValue
    return cls(bc.payoff(spec["payoff"]), mesher, spec["direction"])


def condition(spec, mesher, calc, reference, dc):
    t = spec["type"]
    if t == "American":
        return ql.FdmAmericanStepCondition(mesher, calc), []
    if t == "Bermudan":
        c = ql.FdmBermudanStepCondition([ql.Date(d) for d in spec["dates"]], reference, dc, mesher, calc)
        return c, list(c.exerciseTimes())
    if t == "Snapshot":
        return ql.FdmSnapshotCondition(spec["t"]), [spec["t"]]
    if t == "Dividend":
        c = ql.FdmDividendHandler(bsm.dividends(spec["dividends"]), mesher, reference, dc, spec["direction"])
        return c, list(c.dividendTimes())
    raise ValueError(f"unknown condition {t}")


def composite(specs, mesher, calc, reference, dc):
    """The conditions' composite, its stopping times and the conditions (to keep them alive)."""
    conds, times = [], []
    for s in specs:
        c, ts = condition(s, mesher, calc, reference, dc)
        conds.append(c)
        times += ts
    vec = ql.FdmStepConditionVector()
    for c in conds:
        vec.push_back(c)
    return ql.FdmStepConditionComposite(times, vec), times, conds


def rollback_spec(op, calc, conditions, desc, start, end, steps, damping):
    return {"op": op, "calculator": calc, "conditions": conditions, "stoppingTimes": None,
            "referenceDate": R.serialNumber(), "dayCounter": bsm.bvts.A365, "scheme": scheme_spec(desc),
            "from": start, "to": end, "steps": steps, "dampingSteps": damping}


def rollback(spec):
    """Completes the spec's stopping times and initial values, and returns the rolled-back values, or the throw."""
    op, mesher, keep = fbo.bs_op(spec["op"])
    calc = calculator(spec["calculator"], mesher)
    reference = ql.Date(spec["referenceDate"])
    comp, times, conds = composite(spec["conditions"], mesher, calc, reference, bsm.bvts.day_counter(spec["dayCounter"]))
    spec["stoppingTimes"] = v.enc(times)
    layout = mesher.layout()
    it = layout.begin()
    initial = []
    for _ in range(layout.size()):
        initial.append(calc.avgInnerValue(it, spec["from"]))
        it.increment()
    a = ql.Array(initial)

    def run():
        ql.FdmBackwardSolver(op, ql.FdmBoundaryConditionSet(), comp, scheme(spec["scheme"])).rollback(
            a, spec["from"], spec["to"], spec["steps"], spec["dampingSteps"])
        return [a[i] for i in range(len(a))]

    return v.enc(initial), v.attempt(run)


def setups(explicit=False, flat_only=False):
    """(name, op spec, calculator spec, conditions, from, to, steps, damping) for the scheme files."""
    flat = bsp.PROCESSES[0][1]
    surface = bsp.PROCESSES[3][1]
    put = bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 100.0)
    call = bc.payoff_spec("PlainVanilla", int(ql.Option.Call), 110.0)
    m_flat = mc.composite_spec(bsm.bs_spec(51, flat, 1.0, 100.0))
    m_surface = mc.composite_spec(bsm.bs_spec(41, surface, 1.5, 110.0, c_point=(110.0, 0.1)))
    m_two = mc.composite_spec(mc.U4, bsm.bs_spec(21, flat, 1.0, 100.0))
    k = 8 if explicit else 1
    out = [("flat, put", fbo.op_spec(m_flat, flat), calculator_spec("Log", put), [], 1.0, 0.0, 50 * k, 0),
           ("flat, American put", fbo.op_spec(m_flat, flat), calculator_spec("Log", put), [{"type": "American"}], 1.0,
            0.0, 50 * k, 0)]
    if flat_only:
        return out
    return out + [
        ("zero curves and surface, call, damped", fbo.op_spec(m_surface, surface, strike=110.0),
         calculator_spec("Log", call), [], 1.5, 0.0, 40 * k, 2),
        ("zero curves and surface, American call", fbo.op_spec(m_surface, surface, strike=110.0),
         calculator_spec("Log", call), [{"type": "American"}], 1.5, 0.0, 40 * k, 0),
        ("local volatility, put", fbo.op_spec(m_surface, surface, local_vol=True), calculator_spec("Log", put), [],
         1.5, 0.0, 30 * k, 0),
        ("quanto, put", fbo.op_spec(m_flat, flat, quanto=bsm.FLAT_QUANTO), calculator_spec("Log", put), [], 1.0, 0.0,
         30 * k, 0),
        ("two dimensions, put", fbo.op_spec(m_two, flat, direction=1), calculator_spec("Log", put, 1), [], 1.0, 0.0,
         30 * k, 0)]


def scheme_cases(descs, explicit=False, flat_only=False):
    """The cases of a scheme's file: each description over the set-ups, and a rollback towards negative time."""
    st = v.settings(R)
    out = []
    for dname, desc in descs:
        for name, op, calc, conds, start, end, steps, damping in setups(explicit, flat_only):
            spec = rollback_spec(op, calc, conds, desc, start, end, steps, damping)
            initial, values = rollback(spec)
            out.append({"name": f"{dname}, {name}", "inputs": {"settings": st, "rollback": spec, "initial": initial},
                        "expected": {"values": values}})
        name, op, calc, conds, start, end, steps, damping = setups(explicit)[0]
        spec = rollback_spec(op, calc, conds, desc, 1.0, -0.5, 30, 0)
        initial, values = rollback(spec)
        out.append({"name": f"{dname}, towards negative time", "inputs": {"settings": st, "rollback": spec,
                                                                          "initial": initial},
                    "expected": {"values": values}})
    return out


def cases():
    st = v.settings(R)
    out = [{"name": "named descriptions", "inputs": {"settings": st, "names": NAMED},
            "expected": {"type": [int(getattr(ql.FdmSchemeDesc, n)().type) for n in NAMED],
                         "theta": [getattr(ql.FdmSchemeDesc, n)().theta for n in NAMED],
                         "mu": [getattr(ql.FdmSchemeDesc, n)().mu for n in NAMED]}}]
    flat = bsp.PROCESSES[0][1]
    surface = bsp.PROCESSES[3][1]
    put = bc.payoff_spec("PlainVanilla", int(ql.Option.Put), 100.0)
    m_flat = mc.composite_spec(bsm.bs_spec(51, flat, 1.0, 100.0))
    m_surface = mc.composite_spec(bsm.bs_spec(41, surface, 1.0, 100.0))
    divs = [bsm.fixed(2.0, 91), bsm.fixed(3.0, 273)]
    conditions = [
        ("none", []),
        ("American", [{"type": "American"}]),
        ("Bermudan", [{"type": "Bermudan", "dates": [(R + d).serialNumber() for d in (91, 182, 273, 365)]}]),
        ("snapshot", [{"type": "Snapshot", "t": 0.5}]),
        ("dividends", [{"type": "Dividend", "dividends": divs, "direction": 0}]),
        ("American with dividends", [{"type": "American"}, {"type": "Dividend", "dividends": divs, "direction": 0}]),
    ]
    for dname in ("Douglas", "CrankNicolson", "ImplicitEuler"):
        desc = getattr(ql.FdmSchemeDesc, dname)()
        for damping in (0, 2, 3):
            for mname, m, p in (("flat", m_flat, flat), ("surface", m_surface, surface)):
                for cname, conds in conditions:
                    for end in (0.0, 0.25):
                        if end and (damping != 2 or cname not in ("none", "American")):
                            continue
                        spec = rollback_spec(fbo.op_spec(m, p), calculator_spec("Log", put), conds, desc, 1.0, end,
                                             40, damping)
                        initial, values = rollback(spec)
                        out.append({"name": f"{dname}, {damping} damping steps, {mname}, {cname}, to {end}",
                                    "inputs": {"settings": st, "rollback": spec, "initial": initial},
                                    "expected": {"values": values}})
    spec = rollback_spec(fbo.op_spec(m_flat, flat), calculator_spec("Log", put), [],
                         ql.FdmSchemeDesc(ql.FdmSchemeDesc.DouglasType, 0.5, 0.0), 1.0, 0.0, 10, 0)
    spec["scheme"]["type"] = 99
    spec["stoppingTimes"] = []
    initial, _ = rollback(dict(spec, scheme=scheme_spec(ql.FdmSchemeDesc.Douglas())))
    out.append({"name": "unknown scheme type", "inputs": {"settings": st, "rollback": spec, "initial": initial},
                "expected": {"values": v.attempt(lambda: ql.FdmBackwardSolver(
                    fbo.bs_op(spec["op"])[0], ql.FdmBoundaryConditionSet(), None,
                    ql.FdmSchemeDesc(99, 0.5, 0.0)).rollback(ql.Array(initial), 1.0, 0.0, 10, 0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp", cases)
