// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's refusals kept in the mark-to-market swap and its engine, with QuantLib's texts: a fair spread derived
/// from a leg whose BPS is zero (<c>mtmcrosscurrencybasisswap.cpp:238-246</c>, reached where an engine's results
/// carry no fair spreads), and a zero discount factor at the FX settlement date or the NPV date
/// (<c>discountingmtmcrosscurrencybasisswapengine.cpp:107-120</c>).
/// </summary>
public class MtMCrossCurrencyBasisSwapRefusalTests
{
    private static readonly Date Today = new(15, January, 2025);

    [Fact]
    public void AFairSpreadOnAZeroBpsLegIsRefused()
    {
        MtMCrossCurrencyBasisSwap swap = Swap(Context());
        CrossCurrencySwapResults Results(double baseBps, double quoteBps) => new()
        {
            NPV = 12.5,
            LegNPV = [-100.0, 112.5],
            LegBPS = [baseBps, quoteBps],
            InCcyLegNPV = [-90.0, 112.5],
            InCcyLegBPS = [-0.36, 0.45],
            NpvDateDiscounts = [1.0, 1.0],
        };

        NanoQLException fxBase = Assert.Throws<NanoQLException>(() => swap.FetchResults(Results(0.0, 0.45)));
        Assert.Equal("cannot calculate fair FX-base spread: null leg BPS", fxBase.Message);
        NanoQLException fxQuote = Assert.Throws<NanoQLException>(() => swap.FetchResults(Results(-0.4, 0.0)));
        Assert.Equal("cannot calculate fair FX-quote spread: null leg BPS", fxQuote.Message);

        // QuantLib's formula where both are nonzero
        MtMCrossCurrencyBasisSwapResults fetched = swap.FetchResults(Results(-0.4, 0.45));
        Assert.Equal(swap.FxBaseSpread - (12.5 / (-0.4 / 1.0e-4)), fetched.FairFxBaseSpread);
    }

    [Fact]
    public void AZeroDiscountFactorIsRefused()
    {
        EvaluationContext context = Context();
        MtMCrossCurrencyBasisSwap swap = Swap(context);

        // a rate so high the curve's discount is 0 two days on, and 1 on its reference date
        YieldTermStructure zeroing = new FlatForward(Today, 1.0e10, new Actual365Fixed());
        YieldTermStructure usd = new FlatForward(Today, 0.04, new Actual365Fixed());
        Date settle = new Date(17, January, 2025);

        NanoQLException atSettle = Assert.Throws<NanoQLException>(
            () => DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap, context, new USDCurrency(), usd, new EURCurrency(), zeroing, 1.1, spotFxSettleDate: settle));
        Assert.Equal($"Discount factor for currency EUR at {settle} cannot be zero", atSettle.Message);

        NanoQLException atNpvDate = Assert.Throws<NanoQLException>(
            () => DiscountingMtMCrossCurrencyBasisSwapEngine.Calculate(
                swap,
                context,
                new USDCurrency(),
                zeroing,
                new EURCurrency(),
                usd,
                1.1,
                npvDate: settle,
                spotFxSettleDate: Today));
        Assert.Equal($"Discount factor for currency USD at {settle} cannot be zero", atNpvDate.Message);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    // EUR Euribor 3M against USD LIBOR 3M over a year, the USD leg resettable.
    private static MtMCrossCurrencyBasisSwap Swap(EvaluationContext context)
    {
        var schedule = new Schedule(
            new Date(17, January, 2025),
            new Date(17, January, 2026),
            new Period(3, TimeUnit.Months),
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);
        return new MtMCrossCurrencyBasisSwap(
            MtMCrossCurrencyBasisSwapType.PayFxBaseCurrency,
            1e6,
            new EURCurrency(),
            schedule,
            new Euribor3M(new FlatForward(Today, 0.027, new Actual365Fixed())),
            0.0,
            1.0,
            1.1e6,
            new USDCurrency(),
            schedule,
            new USDLibor(new Period(3, TimeUnit.Months), new FlatForward(Today, 0.043, new Actual365Fixed())),
            0.0,
            1.0,
            false,
            context);
    }
}
