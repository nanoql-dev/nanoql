// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The Rendistato calculator computes in its constructor (the playbook's <c>LazyObject</c> row), each yield solve from
/// QuantLib's first-call guess, 5%. QuantLib's calculator, recalculated after a change, starts each solve from the last
/// calculation's yields (<c>btp.cpp</c>, <c>yields_[i]</c> and <c>swapBondYields_[i]</c> as the guesses), so its
/// recomputed yields can differ from a first calculation's within the solver's accuracy, 1e-10: a documented divergence
/// on a recomputation, nanoql's every calculator being QuantLib's first. On the harness, on three BTPs (3% 2029, 3.35%
/// 2030, 3.1% 2031; outstandings 12, 18 and 6; clean prices 99.1, 101.4 and 99.8) on Euribor 6M on a flat 3.1% curve
/// discounted on a flat 2.9% one on 15 January 2025, QuantLib's first calculation gives the yields 0.03240038273671944,
/// 0.03108728722912678 and 0.03157660374568899.
/// </summary>
public class RendistatoTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly Euribor Index = new(new Period(6, TimeUnit.Months), new FlatForward(Today, 0.031, new Actual365Fixed()));
    private static readonly YieldTermStructure Discount = new FlatForward(Today, 0.029, new Actual365Fixed());

    [Fact]
    public void ACalculatorIsQuantLibsFirstCalculation()
    {
        RendistatoCalculator r = Calculator([99.1, 101.4, 99.8]);
        Assert.Equal([0.03240038273671944, 0.03108728722912678, 0.03157660374568899], r.Yields.ToArray());
    }

    [Fact]
    public void ACalculatorAfterAnotherStartsFromTheFirstGuesses()
    {
        _ = Calculator([97.0, 99.0, 98.0]);
        RendistatoCalculator again = Calculator([99.1, 101.4, 99.8]);
        Assert.Equal(Calculator([99.1, 101.4, 99.8]).Yields.ToArray(), again.Yields.ToArray());
        Assert.Equal([0.03240038273671944, 0.03108728722912678, 0.03157660374568899], again.Yields.ToArray());
    }

    private static RendistatoCalculator Calculator(double[] prices)
    {
        BTP[] btps =
        [
            new(new Date(1, Month.August, 2029), 0.03, Context, new Date(1, Month.February, 2024)),
            new(new Date(15, Month.November, 2030), 0.0335, Context, new Date(15, Month.May, 2023)),
            new(new Date(1, Month.June, 2031), 0.031, Context, new Date(1, Month.December, 2023)),
        ];
        return new RendistatoCalculator(new RendistatoBasket(btps, [12.0, 18.0, 6.0], prices), Index, Discount, Context);
    }
}
