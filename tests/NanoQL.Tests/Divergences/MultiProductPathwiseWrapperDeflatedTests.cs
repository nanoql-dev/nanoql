// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The pathwise wrapper on an already-deflated product (fixed). QuantLib's <c>MultiProductPathwiseWrapper</c> hands the
/// accounting engine a deflated product's amount as it is (<c>multisteppathwisewrapper.cpp:69</c>), and the engine
/// discounts it again: deflated caplets struck at the forwards, wrapped, come out 1.8% to 11.7% below
/// <c>MultiStepOptionlets</c> on the same paths. nanoql divides a deflated product's amount by its payment time's
/// money-market deflator on the current state (<see cref="MarketModelDiscounter.NumeraireBonds"/> at numeraire 0), the
/// factor the pathwise engine skips for it. Checks: QuantLib's <c>MultiStepOptionlets</c> on the same paths; QuantLib's
/// bits on an undeflated product (the vectors, QuantLib's <c>testInverseFloater</c>).
/// </summary>
public class MultiProductPathwiseWrapperDeflatedTests
{
    private static readonly MarketModelProductMarket Market = new(6);

    [Fact]
    public void AWrappedDeflatedCapletIsItsOptionlet()
    {
        var caplets = new MarketModelPathwiseMultiDeflatedCaplet(Market.RateTimes, Market.Accruals, Market.PaymentTimes, Market.Forwards);
        Payoff[] payoffs = [.. Market.Forwards.Select(f => (Payoff)new PlainVanillaPayoff(OptionType.Call, f))];
        var optionlets = new MultiStepOptionlets(Market.RateTimes, Market.Accruals, Market.PaymentTimes, payoffs);

        (ImmutableArray<double> wrapped, _) = Market.TerminalPc(new MultiProductPathwiseWrapper(caplets), 3, 1023);
        (ImmutableArray<double> options, _) = Market.TerminalPc(optionlets, 3, 1023);

        // the optionlets: QuantLib's values, bit for bit
        double[] quantLibOptionlets =
        [
            0.00080846767878108678, 0.00119988512785996, 0.0014122815622738217, 0.0015592132013022772,
            0.0016865403676953742, 0.00192517721980297,
        ];
        Assert.Equal(quantLibOptionlets, options);

        // QuantLib's wrapped caplets, discounted twice
        double[] quantLibWrapped =
        [
            0.00079416546316884767, 0.0011559531003987817, 0.0013333516576135586, 0.0014418793179346969,
            0.001525363286814264, 0.0016995103397520548,
        ];
        for (int i = 0; i < Market.N; ++i)
        {
            Assert.Equal(options[i], wrapped[i], 1e-15 * options[i]);
            Assert.True(quantLibWrapped[i] < 0.99 * options[i], $"caplet {i}: {quantLibWrapped[i]} against {options[i]}");
        }
    }
}
