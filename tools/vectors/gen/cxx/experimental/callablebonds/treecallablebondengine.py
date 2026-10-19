# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's TreeCallableFixedRateBondEngine and TreeCallableZeroCouponBondEngine
(ql/experimental/callablebonds/treecallablebondengine.hpp, .cpp) where the wheel can't reach, each output computed by
the C++ harness: the engines with a spread, which only CallableBond's NPVSpreadHelper sets, writing it into the
bond's arguments and running the engine directly, as NPVSpreadHelper does.

Six bonds of tools/vectors/gen/experimental/callablebonds/callablebond.py's fixtures (testCached's calls, puts and
dirty calls, a call two days before a coupon date, which the spread's discount factors carry to its coupon, the wheel
survey's bond, whose curve starts on the evaluation date, and testInterplay's zero-coupon bond), each on four engines
(the fixed-rate engine with 40 and 120 time steps and on a grid through the bond's mandatory times, and the zero-coupon
engine with 60 time steps), at spreads of -0.01, 0, 0.0025 and 0.02. A case's outputs are the engine's value (the NPV
at the curve's reference date) and settlement value at each spread. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Experimental/CallableBonds/TreeCallableBondEngineCxxVectorTests.cs, with
tests/NanoQL.Tests/Vectors/CallableBondInputs.cs."""
import importlib.util
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)

import QuantLib as ql  # noqa: E402

import cxx  # noqa: E402
import vectors as v  # noqa: E402

_spec = importlib.util.spec_from_file_location(
    "wheel_callablebond", os.path.join(v.GEN_ROOT, "experimental", "callablebonds", "callablebond.py"))
cb = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(cb)

SPREADS = [-0.01, 0.0, 0.0025, 0.02]


def bonds():
    """(name, settings, bond spec, curve spec, face amount)."""
    tables = {}
    for name, *entry in (cb.cached_cases() + cb.runner_cases() + cb.survey_cases()):
        tables[name] = entry
    out = []
    for name in ("cached / calls at 110, 240 steps", "cached / puts at 100, 240 steps",
                 "cached / calls at a dirty 110, 100 steps", "cached / a call two days before a coupon date",
                 "survey / 14 par calls, 200 steps, compounded semiannually",
                 "interplay / an out-of-the-money call ahead of an in-the-money put"):
        st, spec, engine, scale = tables[name][:4]
        out.append((name, st, spec, engine["model"]["curve"], scale))
    return out


def cases():
    out = []
    with cxx.connect() as H:
        for name, st, spec, curve, scale in bonds():
            with v.case_settings(st[0]):
                times = cb.mandatory_times(spec, curve, st[0])
            hw = cb.hull_white(curve)
            engines = [("40 steps", cb.tree_steps(hw, 40, curve)), ("120 steps", cb.tree_steps(hw, 120)),
                       ("a grid", cb.tree_grid(hw, times, 70, curve)),
                       ("the zero-coupon engine, 60 steps", dict(cb.tree_steps(hw, 60, curve), type="TreeZeroSteps"))]
            for label, engine in engines:
                with H.case_settings(st[0].serialNumber(), include_reference_date_events=st[1],
                                     include_todays_cash_flows=st[2]) as s:
                    results = [H.call("TreeCallableFixedRateBondEngine::calculateWithSpread", spec, engine, x)
                               for x in SPREADS]
                    out.append({"name": f"{name} / {label}",
                                "inputs": {"settings": s, "bond": spec, "engine": engine, "spreads": SPREADS},
                                "expected": {"value": [v.amount(r["value"], scale) for r in results],
                                             "settlementValue": [v.amount(r["settlementValue"], scale)
                                                                 for r in results]}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/callablebonds/treecallablebondengine.hpp", cases)
