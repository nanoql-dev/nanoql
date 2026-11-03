# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's ProxyGreekEngine (ql/models/marketmodels/proxygreekengine.hpp, .cpp), which the wheel doesn't
expose, each output computed by the C++ harness: the original values' means and errors, and the Greeks' means, over a
few paths of a log-normal Euler evolver and constrained Euler evolvers on bumped models, each step's forward
constrained, testGreeks' weights.

Call and put optionlets at the forwards on six semi-annual rates and five irregular ones, FlatVol 20%, 3
factors, the money-market measure; forwards bumped 1e-6 and volatilities 1e-4; the Mersenne twister and Sobol.

The constrained evolvers' reference is each one's own model, QuantLib's weight
(tests/NanoQL.Tests/Divergences/ProxyGreekVegaTests.cs holds the reference model's). The harness calls each constrained
evolver's setConstraintType again before each step, so each step's covariances are its own pseudo-root's, nanoql's fix,
as evolvers/lognormalfwdrateeulerconstrained.py does.

The harness unit is tools/cxx/src/models/marketmodels/pathwisegreeks/pathwisegreeks.cpp. Read back by
tests/NanoQL.Tests/Vectors/Cxx/Models/MarketModels/ProxyGreekEngineCxxVectorTests.cs."""
import os
import sys

_dir = os.path.dirname(os.path.abspath(__file__))
while not os.path.isfile(os.path.join(_dir, "vectors.py")):
    _dir = os.path.dirname(_dir)
sys.path.insert(0, _dir)
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
sys.path.insert(0, os.path.join(os.path.dirname(os.path.abspath(__file__)), "products", "multistep"))

import cxx  # noqa: E402
import multistepforwards as mf  # noqa: E402
import pathwiseaccountingengine as pe  # noqa: E402
import vectors as v  # noqa: E402

R = 46322
PATHS = 64
H_FWD = 1e-6
H_VOL = 1e-4


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for label, times in mf.GRIDS:
            n = len(times) - 1
            f0 = mf.forwards_of(times)
            m = pe.model(times)
            groups = [[pe.model(times, forwards=[f - H_FWD for f in f0]),
                       pe.model(times, forwards=[f + H_FWD for f in f0])],
                      [pe.model(times, vol=0.2 - H_VOL), pe.model(times, vol=0.2 + H_VOL)]]
            weights = [[[0.0, -1.0 / (2 * H_FWD), 1.0 / (2 * H_FWD)],
                        [-2.0 / (H_FWD * H_FWD), 1.0 / (H_FWD * H_FWD), 1.0 / (H_FWD * H_FWD)]],
                       [[0.0, -1.0 / (2 * H_VOL), 1.0 / (2 * H_VOL)]]]
            start, end = list(range(n)), list(range(1, n + 1))
            nums = pe.money_market(times)
            for plabel, payoff in (("call optionlets", 1), ("put optionlets", -1)):
                p = mf.optionlets_spec(times, [[payoff, f] for f in f0])
                for gl, g in pe.GENERATORS:
                    r = H.call("ProxyGreekEngine.values", m, g, nums, groups, weights, start, end, p, 0.95, PATHS)
                    out.append({"name": f"{label}, {gl}: {plabel}",
                                "inputs": {"settings": st, "model": m, "generator": g, "numeraires": nums,
                                           "groups": groups, "diffWeights": weights, "startIndexOfConstraint": start,
                                           "endIndexOfConstraint": end, "product": p, "initialNumeraireValue": 0.95,
                                           "paths": PATHS},
                                "expected": {"mean": v.enc(r["mean"]), "errorEstimate": v.enc(r["errorEstimate"]),
                                             "greeks": v.enc(r["greeks"])}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/marketmodels/proxygreekengine.hpp", cases)
