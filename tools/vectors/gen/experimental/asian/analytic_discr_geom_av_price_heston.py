# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's AnalyticDiscreteGeometricAveragePriceAsianHestonEngine
(ql/experimental/asian/analytic_discr_geom_av_price_heston.hpp, .cpp), on the evaluation date 15 October 2026: Kim, Kim,
Kim and Wee's discrete geometric average-price Asian options under Heston.

Every price the engine makes is left out (the S-bar plan's §2.10: (xlix) and (xxxiv) move each by at least 8.5e-10,
the Fourier integral's left-out stretch and its cut), so the outputs are the ones nothing moves: the refusals of an
exercise that isn't European, a payoff that isn't plain vanilla and a running product of 0, and an expired option, on
the three parameter sets of tools/vectors/gen/processes/hestonprocess.py on flat curves. The prices are checked in
tests/NanoQL.Tests/Divergences/, against QuantLib's AnalyticHestonEngine at one fixing and against QUADPACK elsewhere.

The wheel's calls, by QuantLib-SWIG 1.44's options.i: AnalyticDiscreteGeometricAveragePriceAsianHestonEngine(process)
and the option's NPV. Read back by
tests/NanoQL.Tests/Vectors/Experimental/Asian/AnalyticDiscreteGeometricAveragePriceAsianHestonEngineVectorTests.cs."""
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


ao = _load("asianoption", "instruments/asianoption.py")
hp = _load("hestonprocess", "processes/hestonprocess.py")
abe, bsp, bc = ao.abe, ao.bsp, ao.bc
R, CALL, PUT, GEOMETRIC = ao.R, ao.CALL, ao.PUT, ao.GEOMETRIC
QE = int(ql.HestonProcess.QuadraticExponentialMartingale)


def cases():
    st = v.settings(R)
    out = []
    for pname, s0, v0, kappa, theta, sigma, rho in hp.PARAMETERS:
        pspec = {"riskFree": bsp.FLAT_R, "dividend": bsp.FLAT_Q, "s0": s0, "v0": v0, "kappa": kappa, "theta": theta,
                 "sigma": sigma, "rho": rho, "discretization": QE}
        engine = ql.AnalyticDiscreteGeometricAveragePriceAsianHestonEngine(hp.process(pspec))
        pay = bc.payoff_spec("PlainVanilla", CALL, s0)
        ex = abe.exercise_spec("European", 365)
        for name, spec in (
                ("an American exercise", ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, abe.exercise_spec("American", 365))),
                ("a cash-or-nothing payoff",
                 ao.option_spec(GEOMETRIC, ao.dates(365, 12), bc.payoff_spec("CashOrNothing", CALL, s0), ex)),
                ("a running product of 0", ao.option_spec(GEOMETRIC, ao.dates(365, 12), pay, ex, running=0.0, past=1)),
                ("expired", ao.option_spec(GEOMETRIC, ao.dates(-1, 1), pay, abe.exercise_spec("European", -1)))):
            out.append({"name": f"{pname}, {name}", "inputs": {"settings": st, "process": pspec, "option": spec},
                        "expected": {"NPV": ao.npv(ao.option(spec), engine)}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/experimental/asian/analytic_discr_geom_av_price_heston.hpp", cases)
