// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using static NanoQL.Tests.Conventions.CdoBuilders;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A random model bound to two baskets in turn (the S-xcr plan's XR8 (viii), fixed by XR1's binding): each binding is a
/// fresh model's simulation. QuantLib's model keeps its first basket's paths: set on three names at 2% then on three at
/// 5%, it reads 3.447 on both, where a fresh model reads 8.007 on the second (the S-xcr run's <c>c2_facts.cpp</c>).
/// </summary>
public class RandomModelBindingTests
{
    [Fact]
    public void EachBindingSimulatesItsBasket()
    {
        GaussianRandomDefaultLM model = Random(3, 0.3, 2000);
        Basket first = Three(0.02).SetLossModel(model);
        Basket second = Three(0.05).SetLossModel(model);

        Assert.Equal(3.447, first.ExpectedTrancheLoss(D5), 1e-12);
        Assert.Equal(8.007, second.ExpectedTrancheLoss(D5), 1e-12);
        Assert.Equal(Three(0.05).SetLossModel(Random(3, 0.3, 2000)).ExpectedTrancheLoss(D5), second.ExpectedTrancheLoss(D5));
    }

    private static Basket Three(double h)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, h), Flat(Today, h), Flat(Today, h)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }
}
