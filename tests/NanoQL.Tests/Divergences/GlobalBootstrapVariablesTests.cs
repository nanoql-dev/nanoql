// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Where a global bootstrap's additional variables end up (the S-core plan's CR3 (b)): QuantLib's bootstrap writes the
/// optimizer's values into the quotes its variables manage (<c>globalbootstrap.hpp:450-451</c>,
/// <c>globalbootstrapvars.cpp:34-38</c>), so they hold the optimum after the curve is built. nanoql's variables are
/// immutable and its curve keeps the optimum, <see cref="PiecewiseYieldCurve{TTraits, TInterpolator, TBootstrap}.AdditionalVariables"/>:
/// on QuantLib's testGlobalBootstrapVariables, the volatility QuantLib's quote holds after the bootstrap, 0.09861506355792292
/// (the C++ harness's vector, <c>cxx/termstructures/globalbootstrapvars.json</c>, "QuantLib's case"). A curve on a
/// bootstrap with no variables keeps none.
/// </summary>
public class GlobalBootstrapVariablesTests
{
    [Fact]
    public void TheCurveKeepsTheOptimum()
    {
        PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap> curve = Conventions.GlobalBootstrapVariablesFixture.Curve();
        Assert.Single(curve.AdditionalVariables);
        Assert.True(
            Math.Abs(curve.AdditionalVariables[0] - 0.09861506355792292) < 1e-10 * 0.09861506355792292,
            $"volatility {curve.AdditionalVariables[0]:R}");

        var plain = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            Conventions.GlobalBootstrapVariablesFixture.Settlement,
            Conventions.GlobalBootstrapVariablesFixture.CurveHelpers(),
            new Actual365Fixed(),
            Conventions.GlobalBootstrapVariablesFixture.Context);
        Assert.Empty(plain.AdditionalVariables);
    }
}
