// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.TermStructures;
using static NanoQL.Tests.Conventions.CreditPortfolioBuilders;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A loss model bound to a basket by copy, its precomputation run there, once: QuantLib's model holds a relinkable
/// handle to its basket, so a model set on two baskets in turn can keep the first's precomputation (the recursive
/// model's weights and the random models' simulations: 26.31 and 28.72 against a fresh model's 49.47 and 94.475). Each
/// binding here is a fresh model's; the check is QuantLib's fresh model, bit for bit.
/// </summary>
public class LossModelBindingTests
{
    [Fact]
    public void EachBindingIsAFreshModels()
    {
        IDefaultLossModel model = Constant(3, 0.3);
        Basket first = Three(0.01);
        Basket second = Three(0.05);

        Basket boundFirst = first.SetLossModel(model);
        Basket boundSecond = second.SetLossModel(model);

        Assert.Null(model.Basket);
        Assert.Same(boundFirst, boundFirst.LossModel!.Basket);
        Assert.Same(boundSecond, boundSecond.LossModel!.Basket);
        Assert.Equal(second.SetLossModel(Constant(3, 0.3)).ProbAtLeastNEvents(2, D5), boundSecond.ProbAtLeastNEvents(2, D5));
        Assert.Equal(first.SetLossModel(Constant(3, 0.3)).ProbAtLeastNEvents(2, D5), boundFirst.ProbAtLeastNEvents(2, D5));
        Assert.NotEqual(boundFirst.ProbAtLeastNEvents(2, D5), boundSecond.ProbAtLeastNEvents(2, D5));
    }

    private static Basket Three(double h)
    {
        DefaultProbabilityTermStructure[] curves = [Flat(Today, h), Flat(Today, h), Flat(Today, h)];
        return new Basket(Today, ["A", "B", "C"], [10.0, 20.0, 30.0], PoolOf(["A", "B", "C"], curves), Context);
    }
}
