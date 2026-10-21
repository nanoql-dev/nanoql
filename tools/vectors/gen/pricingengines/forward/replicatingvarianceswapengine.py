# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ReplicatingVarianceSwapEngine (ql/pricingengines/forward/replicatingvarianceswapengine.hpp),
on the evaluation date 15 October 2026: variance swaps priced by the cost of their replicating portfolio.

Long and short swaps at two strikes and notionals, maturing in 90 days and two years, on flat and linear zero risk-free
curves, a constant volatility and Derman, Kamal and Zou's skewed surface (test-suite/varianceswaps.cpp's), with that
case's strikes and coarser ones, at strike steps of 5 and 2.5: the fair variance and the NPV. Every dividend yield is
zero: elsewhere QuantLib's replication leaves the yield out of its drift and forward, which nanoql fixes
(tests/NanoQL.Tests/Divergences/ReplicatingVarianceSwapDividendTests.cs); and every put strike is above the step,
where QuantLib returns NaN and nanoql refuses (ReplicatingVarianceSwapRefusalTests.cs). Then an expired swap, whose
NPV is zero and whose variance QuantLib doesn't provide, and the refusals: no call or put strikes ("no strike(s)
given"), a put strike at zero ("min put strike must be positive"), the call and put strikes not meeting ("min call and
max put strikes differ"), and a strike and a notional at zero ("negative or null strike given", "negative or null
notional given").

A case holds one swap, process and engine setting, the swap {"position" (QuantLib's Long 0, Short 1), "strike",
"notional", "startDate", "maturityDate"}, the engine {"dk", "callStrikes", "putStrikes"}; the process specs are
tools/vectors/gen/processes/blackscholesprocess.py's. The wheel's calls, by QuantLib-SWIG 1.44's varianceswaps.i:
VarianceSwap(position, strike, notional, startDate, maturityDate), ReplicatingVarianceSwapEngine(process, dk,
callStrikes, putStrikes); variance and NPV the swap's accessors. Read back by
tests/NanoQL.Tests/Vectors/PricingEngines/ReplicatingVarianceSwapEngineVectorTests.cs."""
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
bvts = bsp.bvts
R = bsp.R
ZERO_Q = bsp.flat_spec(R, 0.0)
DKZ_PUTS = [50.0, 55.0, 60.0, 65.0, 70.0, 75.0, 80.0, 85.0, 90.0, 95.0, 100.0]
DKZ_CALLS = [100.0, 105.0, 110.0, 115.0, 120.0, 125.0, 130.0, 135.0]
DKZ_VOLS = [0.30, 0.29, 0.28, 0.27, 0.26, 0.25, 0.24, 0.23, 0.22, 0.21, 0.20, 0.19, 0.18, 0.17, 0.16, 0.15, 0.14, 0.13]
STRIKES = DKZ_PUTS + DKZ_CALLS[1:]


def dkz_surface(days):
    return bvts.vol_spec_surface(R, [days], STRIKES, [[x] for x in DKZ_VOLS], cal=bvts.NULL_CALENDAR)


def swap_spec(position, strike, notional, days, start=0):
    return {"position": position, "strike": strike, "notional": notional, "startDate": (R + start).serialNumber(),
            "maturityDate": (R + days).serialNumber()}


def engine_spec(dk, calls, puts):
    return {"dk": dk, "callStrikes": list(calls), "putStrikes": list(puts)}


def priced(swap, pspec, eng):
    s = ql.VarianceSwap(swap["position"], swap["strike"], swap["notional"], ql.Date(swap["startDate"]),
                        ql.Date(swap["maturityDate"]))
    try:
        s.setPricingEngine(ql.ReplicatingVarianceSwapEngine(bsp.process(pspec), eng["dk"], eng["callStrikes"],
                                                            eng["putStrikes"]))
        s.NPV()
    except RuntimeError:
        return {"variance": dict(v.THROWS), "NPV": dict(v.THROWS)}
    return {"variance": v.result(s.variance), "NPV": v.result(s.NPV)}


def cases():
    st = v.settings(R)
    out = []
    long_, short = int(ql.Position.Long), int(ql.Position.Short)
    processes = [
        ("flat, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, bsp.CONSTANT, ZERO_Q)),
        ("zero curve, constant", bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.ZERO_R, bsp.CONSTANT, ZERO_Q)),
    ]
    for days in (90, 730):
        processes.append((f"flat, the DKZ surface at {days} days",
                          bsp.process_spec("BlackScholesMertonProcess", 100.0, bsp.FLAT_R, dkz_surface(days), ZERO_Q)))
    engines = [("DKZ strikes, dk 5", engine_spec(5.0, DKZ_CALLS, DKZ_PUTS)),
               ("coarse strikes, dk 2.5", engine_spec(2.5, [100.0, 120.0, 140.0], [60.0, 80.0, 100.0])),
               ("unsorted strikes with a repeat, dk 5", engine_spec(5.0, [120.0, 100.0, 110.0, 110.0], [90.0, 70.0, 100.0]))]
    for pname, pspec in processes:
        for days in (90, 730):
            if "surface" in pname and str(days) not in pname:
                continue
            for ename, eng in engines:
                for position, strike, notional in ((long_, 0.04, 50000.0), (short, 0.09, 1.0)):
                    swap = swap_spec(position, strike, notional, days)
                    out.append({"name": f"{pname}, {days} days, {ename}, {'long' if position == long_ else 'short'}",
                                "inputs": {"settings": st, "process": pspec, "swap": swap, "engine": eng},
                                "expected": priced(swap, pspec, eng)})
    pspec = processes[0][1]
    eng = engines[0][1]
    for name, swap, e in (
            ("expired", swap_spec(long_, 0.04, 1.0, -1, -91), eng),
            ("no call strikes", swap_spec(long_, 0.04, 1.0, 90), engine_spec(5.0, [], DKZ_PUTS)),
            ("no put strikes", swap_spec(long_, 0.04, 1.0, 90), engine_spec(5.0, DKZ_CALLS, [])),
            ("a put strike at zero", swap_spec(long_, 0.04, 1.0, 90), engine_spec(5.0, DKZ_CALLS, [0.0, 50.0, 100.0])),
            ("strikes not meeting", swap_spec(long_, 0.04, 1.0, 90), engine_spec(5.0, DKZ_CALLS, [50.0, 95.0])),
            ("strike at zero", swap_spec(long_, 0.0, 1.0, 90), eng),
            ("notional at zero", swap_spec(short, 0.04, 0.0, 90), eng)):
        out.append({"name": name, "inputs": {"settings": st, "process": pspec, "swap": swap, "engine": e},
                    "expected": priced(swap, pspec, e)})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/forward/replicatingvarianceswapengine.hpp", cases)
