// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Experimental;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Student t copula policy's quantile near ½ at 4 and 6 degrees of freedom, one Newton step on Boost's cumulative
/// function there (S-core's C3-O4, applied where the policy reads Boost's quantile): Boost's quantile takes Shaw's
/// forms, which cancel near ½, returning ±0 for ±2.67e-9 within 1e-9 of ½ and 58% off at ½ − 1e-8 at 4 degrees of
/// freedom (<see cref="Numerics.InverseCumulativeStudent"/>). The convolution refuses even orders, so only a policy
/// built without variables reads them. QuantLib's value is Boost's quantile times the variance factor; the check is
/// Boost's cumulative function at the result, which returns the probability within the quantile's conditioning.
/// </summary>
public class TCopulaQuantileCentreTests
{
    private static readonly Func<double, double, double> Quantile =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Quantile");

    private static readonly Func<double, double, double> Cdf =
        BoostInternals.Function<Func<double, double, double>>("StudentsTFunctions", "Cdf");

    [Theory]
    [InlineData(4)]
    [InlineData(6)]
    public void TheQuantileInvertsTheCumulativeNearTheCentre(int df)
    {
        TCopulaPolicy policy = TCopulaPolicy.Create(ImmutableArray<ImmutableArray<double>>.Empty, new TCopulaPolicyInitTraits([df, df]));
        double vf = policy.VarianceFactors[1];
        double p = 0.5 - 1e-8;

        double q = policy.InverseCumulativeZ(p);
        double boost = Quantile(df, p) * vf;

        Assert.Equal(p, Cdf(df, q / vf), 1e-15);
        Assert.True(Math.Abs(Cdf(df, boost / vf) - p) > 100 * Math.Abs(Cdf(df, q / vf) - p) || Math.Abs(Cdf(df, boost / vf) - p) < 1e-16);
    }
}
