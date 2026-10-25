# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Harness vectors for QuantLib's Heston expansions and HestonExpansionEngine
(ql/pricingengines/vanilla/hestonexpansionengine.hpp, .cpp), on the evaluation date 15 October 2026, which the wheel
doesn't expose.

The implied volatilities of LPP2HestonExpansion, LPP3HestonExpansion and FordeHestonExpansion at seven strikes on
QuantLib's testExpansionOnFordeReference parameters (κ 1.15) at terms from 2 to 10 and at κ 4 at terms from 0.5 to 10;
and the engine's NPVs of calls and puts at five strikes and two maturities, three and five years, on batesmodel.py's two
Heston sets. Every κt is between 2 and 40, at or above every coefficient's cut and below HR11's, so the coefficients
are QuantLib's double arithmetic (the S-hes plan's HR6, HR11); §2.10 leaves out the κt below the cuts, where
QuantLib's rounding passes 1e-12 (HR7 (x)'s facts), and a term of 0 (HR7 (xxxii)'s).

An expansion case holds "formula" (HestonExpansionEngine::HestonExpansionFormula's integer: LPP2 0, LPP3 1, Forde 2),
"kappa", "theta", "sigma", "v0", "rho", "term", "strikes" and "forward"; an engine case the "process"
(tools/vectors/gen/cxx/models/equity/batesmodel.py's harness spec), "formula", "maturities" and "strikes". The harness's
calls: HestonExpansion::impliedVolatility and HestonExpansionEngine::results
(tools/cxx/src/pricingengines/vanilla/hestonexpansionengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/HestonExpansionEngineCxxVectorTests.cs."""
import importlib.util
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
CALL, PUT = 1, -1
FORMULAS = [("LPP2", 0), ("LPP3", 1), ("Forde", 2)]
SETS = [("Forde reference", 1.15, 0.04, 0.2, 0.04, -0.4, [2.0, 5.0, 10.0]),
        ("fast reversion", 4.0, 0.25, 1.0, 0.04, -0.5, [0.5, 1.0, 5.0, 10.0])]
STRIKES = [60.0, 80.0, 90.0, 100.0, 110.0, 120.0, 140.0]
MATURITIES = [1095, 1825]
ENGINE_STRIKES = [0.7, 0.9, 1.0, 1.1, 1.4]


def cases():
    out = []
    with cxx.connect() as H:
        st = H.settings(R)
        for sname, kappa, theta, sigma, v0, rho, terms in SETS:
            for fname, f in FORMULAS:
                for term in terms:
                    r = H.call("HestonExpansion::impliedVolatility", f, kappa, theta, sigma, v0, rho, term, STRIKES,
                               100.0)
                    out.append({"name": f"{fname}, {sname}, term {term}",
                                "inputs": {"settings": st, "kind": "expansion", "formula": f, "kappa": kappa,
                                           "theta": theta, "sigma": sigma, "v0": v0, "rho": rho, "term": term,
                                           "strikes": STRIKES, "forward": 100.0},
                                "expected": {"impliedVolatility": [v.enc(x) for x in r]}})
        for params in bm.HESTON:
            process = bm.process_spec(params)
            strikes = [k * params[1] for k in ENGINE_STRIKES]
            for fname, f in FORMULAS:
                npv = [v.enc(v.attempt(lambda: H.call("HestonExpansionEngine::results", process, f, t, k, R + d)))
                       for d in MATURITIES for t in (CALL, PUT) for k in strikes]
                out.append({"name": f"engine, {params[0]}, {fname}",
                            "inputs": {"settings": st, "kind": "engine", "process": process, "formula": f,
                                       "maturities": MATURITIES, "strikes": strikes},
                            "expected": {"npv": npv}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/hestonexpansionengine.hpp", cases)
