// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Rendistato calculator's unsolved swaps (a run-found R8 defect in the S-cms plan's C4): QuantLib's calculator
/// stops at the first swap whose duration exceeds the basket's (<c>btp.cpp</c>, <c>performCalculations</c>), so the
/// later swaps' rates and durations stay <c>Null&lt;Real&gt;()</c> and their yields the solver's starting guess,
/// <c>swapBondYields_(nSwaps_, 0.05)</c>: <c>swapYields()</c> reports a yield of 5% for a swap it never solved. On the
/// harness, on three BTPs (3% 2029, 3.35% 2030, 3.1% 2031; outstandings 12, 18 and 6; clean prices 99.1, 101.4 and
/// 99.8) on Euribor 6M on a flat 3.1% curve discounted on a flat 2.9% one on 15 January 2025, the basket's duration is
/// 4.878701860912479, the sixth swap's 5.398671198108271, and swaps seven to fifteen have null rates and durations and
/// yields of 0.05. nanoql's rates, yields and durations of the swaps it never solves are null alike. QuantLib's check:
/// its own null rates and durations, which mark the same swaps.
/// </summary>
public class RendistatoUnsolvedSwapsTests
{
    [Fact]
    public void TheSwapsNeverSolvedHaveNoYield()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new Euribor(new Period(6, TimeUnit.Months), new FlatForward(today, 0.031, new Actual365Fixed()));
        BTP[] btps =
        [
            new(new Date(1, Month.August, 2029), 0.03, context, new Date(1, Month.February, 2024)),
            new(new Date(15, Month.November, 2030), 0.0335, context, new Date(15, Month.May, 2023)),
            new(new Date(1, Month.June, 2031), 0.031, context, new Date(1, Month.December, 2023)),
        ];
        var r = new RendistatoCalculator(
            new RendistatoBasket(btps, [12.0, 18.0, 6.0], [99.1, 101.4, 99.8]),
            index,
            new FlatForward(today, 0.029, new Actual365Fixed()),
            context);

        Assert.Equal(5.0, r.EquivalentSwapLength);
        for (int i = 0; i < 15; i++)
        {
            Assert.Equal(i >= 6, r.SwapRates[i] is null);
            Assert.Equal(i >= 6, r.SwapYields[i] is null);
            Assert.Equal(i >= 6, r.SwapDurations[i] is null);
        }

        Assert.True(Math.Abs(r.SwapYields[5]!.Value - 0.03143824237229898) <= 1e-15, $"{r.SwapYields[5]}");
    }
}
