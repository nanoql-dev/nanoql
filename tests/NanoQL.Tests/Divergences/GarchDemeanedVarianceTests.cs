// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// GARCH's calibrations on squared returns less their mean (refused). QuantLib's two <c>calibrate_r2</c> overloads that
/// subtract the mean from the squared returns (<c>garch.cpp:512-523</c>, <c>:544-556</c>) feed the cost negative
/// "squared returns", so the conditional variance can fall below zero, whose log the likelihood takes, NaN; QuantLib
/// calls neither. nanoql's cost refuses a conditional variance that isn't positive, which only those overloads can
/// produce under the constraint (ω &gt; 0, α, β ≥ 0 on squared returns). Check: on QuantLib's calibration series from
/// (ω, α, β) = (0.01, 0.5, 0), each overload refused with its text, the constraint GARCH's own or a given one.
/// </summary>
public class GarchDemeanedVarianceTests
{
    [Fact]
    public void ANonPositiveConditionalVarianceIsRefused()
    {
        (ImmutableArray<double> r2, double mean) = Garch11.ToR2(GarchSeries.Values);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Garch11.CalibrateR2(r2.AsSpan(), mean, new Simplex(0.001), new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8), [0.01, 0.5, 0.0]));
        Assert.Equal("the GARCH(1,1) conditional variance isn't positive: squared returns less their mean", e.Message);
    }

    [Fact]
    public void ANonPositiveConditionalVarianceIsRefusedUnderAGivenConstraint()
    {
        (ImmutableArray<double> r2, double mean) = Garch11.ToR2(GarchSeries.Values);
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => Garch11.CalibrateR2(r2.AsSpan(), mean, new Simplex(0.001), new NoConstraint(), new EndCriteria(10000, 500, 1e-8, 1e-8, 1e-8), [0.01, 0.5, 0.0]));
        Assert.Equal("the GARCH(1,1) conditional variance isn't positive: squared returns less their mean", e.Message);
    }
}
