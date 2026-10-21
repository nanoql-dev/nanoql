# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's HestonModelHelper (ql/models/equity/hestonmodelhelper.hpp, .cpp), on the evaluation date 15
October 2026: Heston calibration helpers quoted by a Black volatility.

Helpers at three maturities (one month, six months, two years, on TARGET), three strikes and two volatilities, under
each calibration error type, on flat and linear zero curves sharing one day counter and reference date: the market
value, the volatility and its type, the Black price at three volatilities, and, against a Heston model on the same
curves priced by the analytic engine with its default order, the model value, the calibration error and the implied
volatility of the model value. On curves of different day counters QuantLib reads the dividend curve at the risk-free
curve's time, where nanoql reads it by date (tests/NanoQL.Tests/Divergences/HestonForwardByDateTests.cs).

A case holds one helper {"maturity" ([n, QuantLib's TimeUnit]), "calendar", "s0", "strike", "volatility",
"riskFree", "dividend", "errorType" (QuantLib's integer)} and one model {"v0", "kappa", "theta", "sigma", "rho"}. The
wheel binds only the helper's constructor taking the spot as a number, the C++ constructor nanoql's matches. The wheel's
calls, by QuantLib-SWIG 1.44's calibrationhelpers.i: HestonModelHelper(maturity, calendar, s0, strike, volatility,
riskFreeRate, dividendYield, errorType), setPricingEngine(AnalyticHestonEngine(model)), marketValue, volatility,
volatilityType, blackPrice, modelValue, calibrationError and impliedVolatility. Read back by
tests/NanoQL.Tests/Vectors/Models/HestonModelHelperVectorTests.cs."""
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
MATURITIES = [(1, int(ql.Months)), (6, int(ql.Months)), (2, int(ql.Years))]
STRIKES = [80.0, 100.0, 125.0]
VOLS = [0.2, 0.35]
ERRORS = [int(ql.BlackCalibrationHelper.RelativePriceError), int(ql.BlackCalibrationHelper.PriceError),
          int(ql.BlackCalibrationHelper.ImpliedVolError)]
MODEL = {"v0": 0.04, "kappa": 1.5, "theta": 0.05, "sigma": 0.4, "rho": -0.6}


def helper(spec):
    return ql.HestonModelHelper(ql.Period(*spec["maturity"]), ql.TARGET(), spec["s0"], spec["strike"],
                                ql.QuoteHandle(ql.SimpleQuote(spec["volatility"])),
                                ql.YieldTermStructureHandle(bsp.curve(spec["riskFree"])),
                                ql.YieldTermStructureHandle(bsp.curve(spec["dividend"])), spec["errorType"])


def cases():
    st = v.settings(R)
    out = []
    for cname, (rf, q) in (("flat", (bsp.FLAT_R, bsp.FLAT_Q)), ("zero", (bsp.ZERO_R, bsp.ZERO_Q))):
        process = ql.HestonProcess(ql.YieldTermStructureHandle(bsp.curve(rf)), ql.YieldTermStructureHandle(bsp.curve(q)),
                                   ql.QuoteHandle(ql.SimpleQuote(100.0)), MODEL["v0"], MODEL["kappa"], MODEL["theta"],
                                   MODEL["sigma"], MODEL["rho"])
        engine = ql.AnalyticHestonEngine(ql.HestonModel(process))
        for maturity in MATURITIES:
            for strike in STRIKES:
                for vol in VOLS:
                    for error in ERRORS:
                        spec = {"maturity": list(maturity), "s0": 100.0, "strike": strike, "volatility": vol,
                                "riskFree": rf, "dividend": q, "errorType": error}
                        h = helper(spec)
                        h.setPricingEngine(engine)
                        model_value = h.modelValue()
                        out.append({
                            "name": f"{cname}, {maturity[0]}{'M' if maturity[1] == int(ql.Months) else 'Y'}, {strike}, "
                                    f"{vol}, error {error}",
                            "inputs": {"settings": st, "helper": spec, "model": MODEL, "blackVols": [0.1, 0.25, 0.6],
                                       "impliedTarget": model_value},
                            "expected": {"marketValue": h.marketValue(), "volatility": h.volatility().value(),
                                         "volatilityType": int(h.volatilityType()),
                                         "blackPrice": [h.blackPrice(x) for x in (0.1, 0.25, 0.6)],
                                         "modelValue": model_value,
                                         "calibrationError": v.attempt(h.calibrationError),
                                         "impliedVolatility": v.attempt(
                                             lambda: h.impliedVolatility(model_value, 1e-12, 500, 0.001, 4.0))}})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/models/equity/hestonmodelhelper.hpp", cases)
