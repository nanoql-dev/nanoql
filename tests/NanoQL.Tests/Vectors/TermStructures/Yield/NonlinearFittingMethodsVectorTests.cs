// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The fitting methods' vectors, <c>tests/vectors/termstructures/yield/nonlinearfittingmethods.json</c>: each nonlinear
/// method's discount function on given parameters, through a fitted curve that doesn't fit: exponential splines,
/// Nelson-Siegel, Svensson, cubic B-splines, simple polynomials, the spread method and flat-forward cut-offs; the
/// method's size and constraint, and the curve's discounts at 26 times and zero rates at 12 dates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class NonlinearFittingMethodsVectorTests
{
    [Fact]
    public void NonlinearFittingMethodsVector()
    {
        var file = VectorFile.Load("termstructures/yield/nonlinearfittingmethods");
        var check = new VectorAssert(file);
        var a365 = new Actual365Fixed();
        foreach (VectorCase c in file.Cases)
        {
            FittedBondDiscountCurveFittingMethod method = FittingInputs.MethodOf(c.Input("method"), c.Context());
            double[] parameters = [.. c.Input("parameters").EnumerateArray().Select(x => x.GetDouble())];
            var curve = new FittedBondDiscountCurve(
                c.Context().EvaluationDate, method, parameters, new Date(c.Int("maxDate")), a365);
            check.Output(c, "size", method.Size);
            check.Output(c, "constrainAtZero", method.ConstrainAtZero);
            double[] times = [.. c.Input("times").EnumerateArray().Select(t => t.GetDouble())];
            check.Output(c, "discount", times.Select(t => (Func<object?>)(() => curve.Discount(t))).ToArray());
            check.Output(
                c,
                "zeroRate",
                YieldCurveInputs.Each(c.DateInputs("dates"), d => curve.ZeroRate(d, a365, Compounding.Continuous).Rate));
        }

        check.Complete();
    }
}
