// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Experimental;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The discounting FX reset pricer's rate for a reset fixed today (fixed). QuantLib forecasts today's rate when the
/// registry's lookup fails, as a fixing not yet published, but catches every <c>Error</c> to do so
/// (<c>fxresetcashflows.cpp:92-98</c>): a published fixing of zero or below, which its own check refuses ("FX fixing …
/// must be positive", <c>:126-129</c>), is forecast instead, 1.100090414674525 on the facts' curves, while the same
/// fixing yesterday is refused. nanoql forecasts only a missing rate, which the registry refuses with either of its
/// two texts, the smart lookup's "no conversion available" and the direct lookup's "no direct conversion available",
/// which a pair triangulated through the other currency reaches (<c>exchangeratemanager.cpp:137-146</c>,
/// <c>:65-79</c>); every other refusal passes. QuantLib's check: the same fixing on a past date (QuantLib-Python
/// 1.44).
/// </summary>
public class FxResetTodaysFixingTests
{
    private const double Forecast = 1.100090414674525;
    private static readonly Date Today = new(15, January, 2025);
    private static readonly Date Spot = new(17, January, 2025);

    [Fact]
    public void TodaysNonPositiveFixingIsRefused()
    {
        Currency eur = new EURCurrency(), usd = new USDCurrency();
        DiscountingFxResetPricer pricer = Pricer(eur);
        foreach (double fixing in new[] { 0.0, -1.08 })
        {
            // QuantLib forecasts over it: 1.100090414674525
            EvaluationContext today = Context(new ExchangeRate(eur, usd, fixing), Today);
            NanoQLException e = Assert.Throws<NanoQLException>(() => pricer.FxRate(new FxReset(Today, Spot), today));
            Assert.Equal($"FX fixing from EUR to USD on {Today} must be positive", e.Message);

            // QuantLib's check: the same fixing yesterday, refused by both
            Date yesterday = Today - 1;
            EvaluationContext past = Context(new ExchangeRate(eur, usd, fixing), yesterday);
            Assert.Throws<NanoQLException>(() => pricer.FxRate(new FxReset(yesterday, Spot), past));
        }

        // A missing fixing is still forecast, as QuantLib's
        var none = new EvaluationContext { EvaluationDate = Today };
        Assert.Equal(Forecast, pricer.FxRate(new FxReset(Today, Spot), none));
    }

    [Fact]
    public void ATriangulatedPairsMissingFixingIsForecast()
    {
        // A currency triangulated through USD: the registry looks the pair up directly, and refuses with the direct
        // lookup's text; QuantLib forecasts, 1.100090414674525, and refuses the same pair yesterday
        var tst = new Currency("Test currency", "TST", 999, "T", string.Empty, 100, new Rounding(), new USDCurrency());
        DiscountingFxResetPricer pricer = Pricer(tst);
        var context = new EvaluationContext { EvaluationDate = Today };
        Assert.Equal(Forecast, pricer.FxRate(new FxReset(Today, Spot), context));

        NanoQLException e = Assert.Throws<NanoQLException>(() => pricer.FxRate(new FxReset(Today - 1, Spot), context));
        Assert.Equal($"no direct conversion available from TST to USD for {Today - 1}", e.Message);
    }

    // The constant leg's currency against USD, resettable, on flat curves, the spot 1.1 USD per unit.
    private static DiscountingFxResetPricer Pricer(Currency constant) => new(
        constant,
        new USDCurrency(),
        new FlatForward(Today, 0.025, new Actual365Fixed()),
        new FlatForward(Today, 0.04, new Actual365Fixed()),
        1.1,
        true);

    private static EvaluationContext Context(ExchangeRate fixing, Date date) => new()
    {
        EvaluationDate = Today,
        ExchangeRates = Currencies.ExchangeRates.Default.Add(fixing, date, date),
    };
}
