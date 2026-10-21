// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The FX reset pricer reads its fixings from the evaluation context (the S-rx plan's SRXR1): a past reset's rate is
/// the context's exchange-rate registry's on its fixing date, where QuantLib reads the global
/// <c>ExchangeRateManager</c>, and today's is the registry's when the context enforces today's historic fixings, as
/// QuantLib's <c>Settings</c> flag does.
/// </summary>
public class FxResetPricerContextTests
{
    private static readonly Date Today = new(15, January, 2025);
    private static readonly Date Past = new(10, January, 2025);
    private static readonly Date Spot = new(17, January, 2025);

    [Fact]
    public void APastFixingIsTheContextsRegistrys()
    {
        DiscountingFxResetPricer pricer = Pricer();
        var reset = new FxReset(Past, Past + 2);

        Assert.Equal(1.0832, pricer.FxRate(reset, Context(1.0832, Past)));
        Assert.Equal(1.0915, pricer.FxRate(reset, Context(1.0915, Past)));

        var none = new EvaluationContext { EvaluationDate = Today };
        NanoQLException e = Assert.Throws<NanoQLException>(() => pricer.FxRate(reset, none));
        Assert.Equal($"no conversion available from EUR to USD for {Past}", e.Message);
    }

    [Fact]
    public void TodaysFixingIsTheRegistrysWhereTheContextEnforcesIt()
    {
        DiscountingFxResetPricer pricer = Pricer();
        var reset = new FxReset(Today, Spot);
        const double Forecast = 1.100090414674525;

        // stored: read whether enforced or not
        Assert.Equal(1.0915, pricer.FxRate(reset, Context(1.0915, Today)));
        EvaluationContext enforced = Context(1.0915, Today) with { EnforcesTodaysHistoricFixings = true };
        Assert.Equal(1.0915, pricer.FxRate(reset, enforced));

        // missing: forecast, unless enforced
        var none = new EvaluationContext { EvaluationDate = Today };
        Assert.Equal(Forecast, pricer.FxRate(reset, none));
        Assert.Throws<NanoQLException>(
            () => pricer.FxRate(reset, none with { EnforcesTodaysHistoricFixings = true }));
    }

    // EUR constant against USD resettable, on flat curves, the spot 1.1 USD per EUR.
    private static DiscountingFxResetPricer Pricer() => new(
        new EURCurrency(),
        new USDCurrency(),
        new FlatForward(Today, 0.025, new Actual365Fixed()),
        new FlatForward(Today, 0.04, new Actual365Fixed()),
        1.1,
        true);

    private static EvaluationContext Context(double fixing, Date date) => new()
    {
        EvaluationDate = Today,
        ExchangeRates = ExchangeRates.Default.Add(
            new ExchangeRate(new EURCurrency(), new USDCurrency(), fixing), date, date),
    };
}
