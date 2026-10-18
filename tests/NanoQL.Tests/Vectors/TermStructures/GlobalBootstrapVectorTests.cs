// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The global bootstrap vectors, <c>tests/vectors/termstructures/globalbootstrap.json</c>: piecewise curves on
/// <see cref="GlobalBootstrap"/> (<see cref="GlobalBootstrapInputs"/>) with QuantLib's defaults, additional helpers
/// and dates with QuantLib-Python's penalty, an explicit optimizer and end criteria, and a constant initial guess, and
/// the global named classes in both their forms: whether each curve builds, its nodes, maximum date and discounts, and
/// each helper's quote error against it, one element per curve of the case.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class GlobalBootstrapVectorTests
{
    [Fact]
    public void GlobalBootstrapVector()
    {
        var file = VectorFile.Load("termstructures/globalbootstrap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            BootstrapHelper<YieldTermStructure>[] helpers = RateHelperInputs.HelpersOf(c.Input("helpers"), context);
            BootstrapInputs.CheckCurves(check, c, helpers, context, curveOf: GlobalBootstrapInputs.CurveOf);
        }

        check.Complete();
    }
}
