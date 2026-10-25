# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for FdHestonHullWhiteVanillaEngine's multiple-strikes caching
(ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp, .cpp), which the wheel doesn't bind, each output computed
by the C++ harness (docs/porting-playbook.md §9.4), on the evaluation date 15 October 2026.

An engine with enableMultipleStrikesCaching(strikes), a reference option priced on it, then an option at each strike
served from its cache: each strike's NPV, delta, gamma and theta, which nanoql's CalculateStrikes returns from one solve
(the S-hes plan's HR4 (ii)). On tools/vectors/gen/cxx/models/equity/batesmodel.py's equity Heston process with a
Hull-White process on its risk-free curve (reversion 0.1, volatility 0.01) at the equity-rate correlation -0.5, on a
20 x 40 x 10 x 7 grid: European calls and puts, the reference strike at the spot and away from it, with and without the
control variate; American puts; the second Hull-White process (0.03, 0.02) at 0.3; and the Douglas scheme.

Left out (the S-hes plan's §2.10): a reference whose payoff isn't a plain vanilla, whose strikes nanoql solves one by
one (HR7 (xxiv), tests/NanoQL.Tests/Divergences/FdHestonHullWhiteStrikesReferenceTests.cs); markets whose
multi-strike grid doesn't hold the true forwards' band, whose strikes nanoql solves one by one too (D3-1
of the S-hes run record): none here, the carry 0.02 at a volatility of 0.2; and every structure off the process's
reference date and day counter (HR7 (xix)).

A case holds the "process" (batesmodel.py's harness spec), "a", "sigma", "correlation", "optionType",
"referenceStrike", "exercise", "strikes", "tGrid", "xGrid", "vGrid", "rGrid", "dampingSteps", "controlVariate" and
"scheme" ([type, theta, mu]). The harness's call: FdHestonHullWhiteVanillaEngine::multipleStrikes
(tools/cxx/src/pricingengines/vanilla/fdhestonhullwhitevanillaengine.cpp). Read back by
tests/NanoQL.Tests/Vectors/Cxx/PricingEngines/FdHestonHullWhiteVanillaEngineCxxVectorTests.cs."""
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
KEYS = ["NPV", "delta", "gamma", "theta"]
HUNDSDORFER = [0, 0.5 + 3.0 ** 0.5 / 6.0, 0.5]
DOUGLAS = [1, 0.5, 0.0]
GRID = (20, 40, 10, 7, 0)


def cases():
    out = []
    process = bm.process_spec(bm.HESTON[0])
    european, american = ["European", R + 365], ["American", R + 365]
    listed, away = [80.0, 90.0, 100.0, 110.0, 120.0], [80.0, 90.0, 100.0, 110.0]
    with cxx.connect() as H:
        st = H.settings(R)

        def strikes_case(name, option_type, reference, exercise, strikes, a=0.1, sigma=0.01, correlation=-0.5,
                         control_variate=True, scheme=HUNDSDORFER):
            r = H.call("FdHestonHullWhiteVanillaEngine::multipleStrikes", process, a, sigma, correlation, option_type,
                       reference, exercise, strikes, *GRID, control_variate, scheme)
            t_grid, x_grid, v_grid, r_grid, damping = GRID
            out.append({"name": f"multiple strikes, {name}",
                        "inputs": {"settings": st, "process": process, "a": a, "sigma": sigma,
                                   "correlation": correlation, "optionType": option_type,
                                   "referenceStrike": reference, "exercise": exercise, "strikes": strikes,
                                   "tGrid": t_grid, "xGrid": x_grid, "vGrid": v_grid, "rGrid": r_grid,
                                   "dampingSteps": damping, "controlVariate": control_variate, "scheme": scheme},
                        "expected": {k: [v.enc(s[k]) for s in r["strikes"]] for k in KEYS}})

        for option_type, tname in ((CALL, "European calls"), (PUT, "European puts")):
            for cv in (True, False):
                cname = "control variate " + ("on" if cv else "off")
                strikes_case(f"{tname}, reference at the spot, {cname}", option_type, 100.0, european, listed,
                             control_variate=cv)
                strikes_case(f"{tname}, reference away from the spot, {cname}", option_type, 120.0, european, away,
                             control_variate=cv)
        strikes_case("American puts, reference at the spot", PUT, 100.0, american, listed)
        strikes_case("European calls, the second Hull-White process", CALL, 100.0, european, listed, a=0.03,
                     sigma=0.02, correlation=0.3)
        strikes_case("European puts, Douglas", PUT, 100.0, european, listed, scheme=DOUGLAS)
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp", cases)
