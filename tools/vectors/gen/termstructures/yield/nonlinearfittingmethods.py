# SPDX-FileCopyrightText: 2026 The nanoql Authors
# SPDX-License-Identifier: Apache-2.0
"""Vectors for QuantLib's nonlinear fitting methods (ql/termstructures/yield/nonlinearfittingmethods.hpp, .cpp): each
method's discount function, through a FittedBondDiscountCurve that evaluates it on given parameters without fitting.

Under the evaluation date Monday 16 June 2025, curves dated then on Actual/365 (Fixed) to 30 years, each a method on
parameters near a fitted curve's: exponential splines, constrained at zero and not, with κ free, nine coefficients and
five; Nelson-Siegel and Svensson; cubic B-splines on two knot vectors, constrained at zero and not; simple polynomials
of degree 2 and 4, constrained and not; the spread method over Nelson-Siegel on a zero curve dated the same day on
Actual/365 (Fixed); and Nelson-Siegel and exponential splines with flat-forward cut-offs, 0.5 and 12 years.

A case is one method on one parameter set. Its outputs are the method's size, whether it is constrained at zero, and
the curve's discount at 26 times from 0 to 29 years and its Actual/365 (Fixed) continuous zero rate at 12 dates. A
refused value is {"throws": true}.

Left out: exponential splines with a fixed κ, whose last coefficient QuantLib's discount function leaves out
(tests/NanoQL.Tests/Divergences/ExponentialSplinesFixedKappaTests.cs); the spread method over a discounting curve on
another clock, which QuantLib reads at the fitted curve's time
(tests/NanoQL.Tests/Divergences/SpreadFittingClockTests.cs); and cubic B-splines on a repeated knot, which QuantLib
refuses (tests/NanoQL.Tests/Divergences/BSplineRepeatedKnotTests.cs). NaturalCubicFitting has no wheel class: its
vectors are the C++ harness's (tools/vectors/gen/cxx/termstructures/yield/nonlinearfittingmethods.py). The polynomial's
powers are std::pow's, which agree with .NET's within the tolerance, not bit for bit.

Each case's inputs hold its settings, the "method" ({"type", its arguments, and "discountCurve" for the spread
method, tools/vectors/gen/termstructures/yield/zerospreadedtermstructure.py's spec}), the "parameters", the
"maxDate", the "times" and the "dates". Read back by
tests/NanoQL.Tests/Vectors/TermStructures/Yield/NonlinearFittingMethodsVectorTests.cs."""
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


zs = _load("zerospreadedtermstructure", "termstructures/yield/zerospreadedtermstructure.py")

TODAY = zs.TODAY
MAX_DATE = TODAY + ql.Period(30, ql.Years)
TIMES = [0.0, 0.01, 0.1, 0.25, 0.4, 0.5, 0.6, 0.75, 1.0, 1.5, 2.0, 2.5, 3.0, 4.0, 5.0, 6.0, 7.0, 8.0, 10.0, 12.0, 13.0,
         15.0, 18.0, 20.0, 25.0, 29.0]
DATE_OFFSETS = [ql.Period(n, u) for n, u in [(1, ql.Months), (3, ql.Months), (6, ql.Months), (1, ql.Years),
                                              (2, ql.Years), (3, ql.Years), (5, ql.Years), (7, ql.Years),
                                              (10, ql.Years), (15, ql.Years), (20, ql.Years), (29, ql.Years)]]
KNOTS_A = [-30.0, -20.0, 0.0, 5.0, 10.0, 15.0, 20.0, 25.0, 30.0, 40.0]
KNOTS_B = [-20.0, -10.0, -5.0, 0.0, 2.0, 5.0, 8.0, 12.0, 18.0, 25.0, 35.0, 45.0]
DISC = zs.interpolated("ZeroCurve", [0, 365, 1826, 3652, 10957], [0.02, 0.022, 0.026, 0.03, 0.033], zs.A365, True)

# (name, method spec, parameters)
METHODS = [
    ("exponential splines, constrained", {"type": "ExponentialSplines", "constrainAtZero": True, "numCoeffs": 9},
     [0.3, -0.2, 0.15, 0.1, -0.05, 0.08, 0.02, -0.01, 0.4]),
    ("exponential splines, unconstrained", {"type": "ExponentialSplines", "constrainAtZero": False, "numCoeffs": 9},
     [0.62, 0.3, -0.2, 0.15, 0.1, -0.05, 0.08, 0.02, -0.01, 0.35]),
    ("exponential splines, five coefficients", {"type": "ExponentialSplines", "constrainAtZero": True,
                                                 "numCoeffs": 5}, [0.4, -0.3, 0.2, 0.1, 0.25]),
    ("Nelson-Siegel", {"type": "NelsonSiegel"}, [0.035, -0.02, 0.01, 0.6]),
    ("Nelson-Siegel, negative slope", {"type": "NelsonSiegel"}, [0.02, 0.015, -0.03, 1.4]),
    ("Svensson", {"type": "Svensson"}, [0.035, -0.02, 0.01, -0.015, 0.6, 0.15]),
    ("cubic B-splines, constrained", {"type": "CubicBSplines", "knots": KNOTS_A, "constrainAtZero": True},
     [0.95, 0.85, 0.75, 0.6, 0.5]),
    ("cubic B-splines, unconstrained", {"type": "CubicBSplines", "knots": KNOTS_A, "constrainAtZero": False},
     [1.05, 0.98, 0.85, 0.75, 0.6, 0.5]),
    ("cubic B-splines, twelve knots", {"type": "CubicBSplines", "knots": KNOTS_B, "constrainAtZero": True},
     [1.02, 0.97, 0.93, 0.88, 0.8, 0.72, 0.6]),
    ("simple polynomial, degree 2, constrained", {"type": "SimplePolynomial", "degree": 2, "constrainAtZero": True},
     [-0.03, 0.0004]),
    ("simple polynomial, degree 4, unconstrained", {"type": "SimplePolynomial", "degree": 4,
                                                     "constrainAtZero": False},
     [1.0, -0.031, 0.0006, -6e-6, 2e-8]),
    ("spread over Nelson-Siegel", {"type": "Spread", "method": {"type": "NelsonSiegel"}, "discountCurve": DISC},
     [0.004, -0.002, 0.001, 0.8]),
    ("Nelson-Siegel with cut-offs", {"type": "NelsonSiegel", "minCutoffTime": 0.5, "maxCutoffTime": 12.0},
     [0.035, -0.02, 0.01, 0.6]),
    ("exponential splines with cut-offs", {"type": "ExponentialSplines", "constrainAtZero": True, "numCoeffs": 9,
                                           "minCutoffTime": 0.5, "maxCutoffTime": 12.0},
     [0.3, -0.2, 0.15, 0.1, -0.05, 0.08, 0.02, -0.01, 0.4]),
]


def make_method(spec, handles):
    """The wheel's method a spec describes; handles keeps the spread method's curve alive."""
    t = spec["type"]
    lo, hi = spec.get("minCutoffTime", 0.0), spec.get("maxCutoffTime", sys.float_info.max)  # QL_MAX_REAL
    if t == "ExponentialSplines":
        return ql.ExponentialSplinesFitting(spec["constrainAtZero"], ql.Array(), ql.Array(), lo, hi,
                                            spec["numCoeffs"])
    if t == "NelsonSiegel":
        return ql.NelsonSiegelFitting(ql.Array(), None, ql.Array(), lo, hi)
    if t == "Svensson":
        return ql.SvenssonFitting(ql.Array(), None, ql.Array(), lo, hi)
    if t == "CubicBSplines":
        return ql.CubicBSplinesFitting(spec["knots"], spec["constrainAtZero"], ql.Array(), ql.Array(), lo, hi)
    if t == "SimplePolynomial":
        return ql.SimplePolynomialFitting(spec["degree"], spec["constrainAtZero"], ql.Array(), ql.Array(), lo, hi)
    if t == "Spread":
        h = ql.YieldTermStructureHandle(zs.original(spec["discountCurve"]))
        handles.append(h)
        return ql.SpreadFittingMethod(make_method(spec["method"], handles), h, lo, hi)
    raise ValueError(t)


def cases():
    out = []
    for name, spec, params in METHODS:
        with v.case_settings(TODAY) as st:
            handles = []
            method = make_method(spec, handles)
            curve = ql.FittedBondDiscountCurve(TODAY, method, ql.Array(params), MAX_DATE, ql.Actual365Fixed())
            dates = [TODAY + p for p in DATE_OFFSETS]
            a365 = ql.Actual365Fixed()
            expected = {
                "size": method.size(),
                "constrainAtZero": method.constrainAtZero(),
                "discount": [v.attempt(lambda t=t: curve.discount(t)) for t in TIMES],
                "zeroRate": [v.attempt(lambda d=d: curve.zeroRate(d, a365, ql.Continuous).rate()) for d in dates],
            }
            out.append({"name": name,
                        "inputs": {"settings": st, "method": spec, "parameters": params, "maxDate": v.enc(MAX_DATE),
                                   "times": TIMES, "dates": v.enc(dates)},
                        "expected": expected})
    return out


if __name__ == "__main__":
    v.main(__file__, "ql/termstructures/yield/nonlinearfittingmethods.hpp", cases)
