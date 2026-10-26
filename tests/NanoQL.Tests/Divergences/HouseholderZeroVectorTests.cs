// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using static NanoQL.Tests.Divergences.Fd2dMismatchedTimesTests;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Householder transformation's matrix for the zero vector. QuantLib's divides the vector by its norm
/// (<c>householder.cpp:32-42</c>), so the matrix is NaN, where the transformation the zero vector defines,
/// x ↦ x − 2(v·x)v, is the identity. Choi's basket engine reaches it at a zero volatility, where the reflection onto e₁
/// of a vector already along e₁ is the zero vector, and its integration orders <c>lround(NaN)</c> then fail with
/// <c>bad array new length</c> (<c>choibasketengine.cpp:136-167</c>), while Choi's price is continuous down to a
/// volatility of 1e-16. nanoql's matrix of the zero vector is the identity (the S-exo plan's ER5 (iii)): QuantLib's own
/// transformation of the zero vector, and Choi's limit, are the checks.
/// </summary>
public class HouseholderZeroVectorTests
{
    [Fact]
    public void TheZeroVectorsMatrixIsTheIdentity()
    {
        Matrix m = new HouseholderTransformation(new Vector(3, 0.0)).GetMatrix();
        for (int i = 0; i < 3; ++i)
        {
            for (int j = 0; j < 3; ++j)
            {
                Assert.Equal(i == j ? 1.0 : 0.0, m[i, j]);
            }
        }

        // QuantLib's own transformation of the zero vector: the identity
        var x = new Vector([0.3, -0.2, 0.9]);
        Assert.Equal(new HouseholderTransformation(new Vector(3, 0.0)).Evaluate(x).ToArray(), (m * x).ToArray());
    }

    [Fact]
    public void ChoiAtAZeroVolatilityPricesItsLimit()
    {
        var option = new BasketOption(
            new AverageBasketPayoff(new PlainVanillaPayoff(OptionType.Call, 100.0), [0.5, 0.5]), European());
        var rho = new Matrix(2, 2, 1.0);
        rho[0, 1] = rho[1, 0] = 0.5;

        // QuantLib's Choi at a second volatility of 1e-16, 7.381270572170824 (1e-12: 7.381270572180778); at 0 it throws
        double price = ChoiBasketEngine.Calculate(
            option, new EvaluationContext { EvaluationDate = new Time.Date(15, Time.Month.October, 2026) },
            [Process(100.0, Flat(0.0), Flat(0.03), Vol(0.3)), Process(100.0, Flat(0.0), Flat(0.03), Vol(0.0))],
            rho).NPV!.Value;
        Assert.Equal(7.381270572170824, price, 1e-11);
    }
}
