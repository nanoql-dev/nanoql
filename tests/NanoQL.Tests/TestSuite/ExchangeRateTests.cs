// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/exchangerate.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// QuantLib's global ExchangeRateManager and Money::Settings are the context's ExchangeRates and MoneySettings: each
// case builds its own registry, where QuantLib's cases add to the one singleton.
[Trait("Oracle", "QuantLib")]
public class ExchangeRateTests
{
    [Fact]
    public void TestDirect()
    {
        // Testing direct exchange rates...

        Currency EUR = new EURCurrency(), USD = new USDCurrency();

        var eur_usd = new ExchangeRate(EUR, USD, 1.2042);

        Money m1 = 50000.0 * EUR;
        Money m2 = 100000.0 * USD;

        EvaluationContext context = Context();

        Money calculated = eur_usd.Exchange(m1);
        var expected = new Money(m1.Value * eur_usd.Rate, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        calculated = eur_usd.Exchange(m2);
        expected = new Money(m2.Value / eur_usd.Rate, EUR);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));
    }

    [Fact]
    public void TestDerived()
    {
        // Testing derived exchange rates...

        Currency EUR = new EURCurrency(), USD = new USDCurrency(), GBP = new GBPCurrency();

        var eur_usd = new ExchangeRate(EUR, USD, 1.2042);
        var eur_gbp = new ExchangeRate(EUR, GBP, 0.6612);

        ExchangeRate derived = ExchangeRate.Chain(eur_usd, eur_gbp);

        Money m1 = 50000.0 * GBP;
        Money m2 = 100000.0 * USD;

        EvaluationContext context = Context();

        Money calculated = derived.Exchange(m1);
        var expected = new Money(m1.Value * eur_usd.Rate / eur_gbp.Rate, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        calculated = derived.Exchange(m2);
        expected = new Money(m2.Value * eur_gbp.Rate / eur_usd.Rate, GBP);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));
    }

    [Fact]
    public void TestDirectLookup()
    {
        // Testing lookup of direct exchange rates...

        Currency EUR = new EURCurrency(), USD = new USDCurrency();

        var eur_usd1 = new ExchangeRate(EUR, USD, 1.1983);
        var eur_usd2 = new ExchangeRate(USD, EUR, 1.0 / 1.2042);
        ExchangeRates rateManager = ExchangeRates.Default
            .Add(eur_usd1, new Date(4, Month.August, 2004))
            .Add(eur_usd2, new Date(5, Month.August, 2004));

        Money m1 = 50000.0 * EUR;
        Money m2 = 100000.0 * USD;

        EvaluationContext context = Context() with { ExchangeRates = rateManager };

        ExchangeRate eur_usd = rateManager.Lookup(
            EUR, USD, context, new Date(4, Month.August, 2004), ExchangeRateType.Direct);
        Money calculated = eur_usd.Exchange(m1);
        var expected = new Money(m1.Value * eur_usd1.Rate, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        eur_usd = rateManager.Lookup(EUR, USD, context, new Date(5, Month.August, 2004), ExchangeRateType.Direct);
        calculated = eur_usd.Exchange(m1);
        expected = new Money(m1.Value / eur_usd2.Rate, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        ExchangeRate usd_eur = rateManager.Lookup(
            USD, EUR, context, new Date(4, Month.August, 2004), ExchangeRateType.Direct);

        calculated = usd_eur.Exchange(m2);
        expected = new Money(m2.Value / eur_usd1.Rate, EUR);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        usd_eur = rateManager.Lookup(USD, EUR, context, new Date(5, Month.August, 2004), ExchangeRateType.Direct);

        calculated = usd_eur.Exchange(m2);
        expected = new Money(m2.Value * eur_usd2.Rate, EUR);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));
    }

    [Fact]
    public void TestTriangulatedLookup()
    {
        // Testing lookup of triangulated exchange rates...

        Currency EUR = new EURCurrency(), USD = new USDCurrency(), ITL = new ITLCurrency();

        var eur_usd1 = new ExchangeRate(EUR, USD, 1.1983);
        var eur_usd2 = new ExchangeRate(EUR, USD, 1.2042);
        ExchangeRates rateManager = ExchangeRates.Default
            .Add(eur_usd1, new Date(4, Month.August, 2004))
            .Add(eur_usd2, new Date(5, Month.August, 2004));

        Money m1 = 50000000.0 * ITL;
        Money m2 = 100000.0 * USD;

        EvaluationContext context = Context() with { ExchangeRates = rateManager };

        ExchangeRate itl_usd = rateManager.Lookup(ITL, USD, context, new Date(4, Month.August, 2004));
        Money calculated = itl_usd.Exchange(m1);
        var expected = new Money(m1.Value * eur_usd1.Rate / 1936.27, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        itl_usd = rateManager.Lookup(ITL, USD, context, new Date(5, Month.August, 2004));
        calculated = itl_usd.Exchange(m1);
        expected = new Money(m1.Value * eur_usd2.Rate / 1936.27, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        ExchangeRate usd_itl = rateManager.Lookup(USD, ITL, context, new Date(4, Month.August, 2004));

        calculated = usd_itl.Exchange(m2);
        expected = new Money(m2.Value * 1936.27 / eur_usd1.Rate, ITL);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        usd_itl = rateManager.Lookup(USD, ITL, context, new Date(5, Month.August, 2004));

        calculated = usd_itl.Exchange(m2);
        expected = new Money(m2.Value * 1936.27 / eur_usd2.Rate, ITL);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));
    }

    [Fact]
    public void TestSmartLookup()
    {
        // Testing lookup of derived exchange rates...

        Currency EUR = new EURCurrency(), USD = new USDCurrency(), GBP = new GBPCurrency(),
            CHF = new CHFCurrency(), SEK = new SEKCurrency(), JPY = new JPYCurrency();

        ExchangeRates rateManager = ExchangeRates.Default;

        var eur_usd1 = new ExchangeRate(EUR, USD, 1.1983);
        var eur_usd2 = new ExchangeRate(USD, EUR, 1.0 / 1.2042);
        rateManager = rateManager.Add(eur_usd1, new Date(4, Month.August, 2004));
        rateManager = rateManager.Add(eur_usd2, new Date(5, Month.August, 2004));

        var eur_gbp1 = new ExchangeRate(GBP, EUR, 1.0 / 0.6596);
        var eur_gbp2 = new ExchangeRate(EUR, GBP, 0.6612);
        rateManager = rateManager.Add(eur_gbp1, new Date(4, Month.August, 2004));
        rateManager = rateManager.Add(eur_gbp2, new Date(5, Month.August, 2004));

        var usd_chf1 = new ExchangeRate(USD, CHF, 1.2847);
        var usd_chf2 = new ExchangeRate(CHF, USD, 1.0 / 1.2774);
        rateManager = rateManager.Add(usd_chf1, new Date(4, Month.August, 2004));
        rateManager = rateManager.Add(usd_chf2, new Date(5, Month.August, 2004));

        var chf_sek1 = new ExchangeRate(SEK, CHF, 0.1674);
        var chf_sek2 = new ExchangeRate(CHF, SEK, 1.0 / 0.1677);
        rateManager = rateManager.Add(chf_sek1, new Date(4, Month.August, 2004));
        rateManager = rateManager.Add(chf_sek2, new Date(5, Month.August, 2004));

        var jpy_sek1 = new ExchangeRate(SEK, JPY, 14.5450);
        var jpy_sek2 = new ExchangeRate(JPY, SEK, 1.0 / 14.6110);
        rateManager = rateManager.Add(jpy_sek1, new Date(4, Month.August, 2004));
        rateManager = rateManager.Add(jpy_sek2, new Date(5, Month.August, 2004));

        Money m1 = 100000.0 * USD;
        Money m2 = 100000.0 * EUR;
        Money m3 = 100000.0 * GBP;
        Money m5 = 100000.0 * SEK;
        Money m6 = 100000.0 * JPY;

        EvaluationContext context = Context() with { ExchangeRates = rateManager };

        // two-rate chain

        ExchangeRate usd_sek = rateManager.Lookup(USD, SEK, context, new Date(4, Month.August, 2004));
        Money calculated = usd_sek.Exchange(m1);
        var expected = new Money(m1.Value * usd_chf1.Rate / chf_sek1.Rate, SEK);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        usd_sek = rateManager.Lookup(SEK, USD, context, new Date(5, Month.August, 2004));
        calculated = usd_sek.Exchange(m5);
        expected = new Money(m5.Value * usd_chf2.Rate / chf_sek2.Rate, USD);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        // three-rate chain

        ExchangeRate eur_sek = rateManager.Lookup(EUR, SEK, context, new Date(4, Month.August, 2004));
        calculated = eur_sek.Exchange(m2);
        expected = new Money(m2.Value * eur_usd1.Rate * usd_chf1.Rate / chf_sek1.Rate, SEK);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        eur_sek = rateManager.Lookup(SEK, EUR, context, new Date(5, Month.August, 2004));
        calculated = eur_sek.Exchange(m5);
        expected = new Money(m5.Value * eur_usd2.Rate * usd_chf2.Rate / chf_sek2.Rate, EUR);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        // four-rate chain

        ExchangeRate eur_jpy = rateManager.Lookup(EUR, JPY, context, new Date(4, Month.August, 2004));
        calculated = eur_jpy.Exchange(m2);
        expected = new Money(m2.Value * eur_usd1.Rate * usd_chf1.Rate * jpy_sek1.Rate / chf_sek1.Rate, JPY);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        eur_jpy = rateManager.Lookup(JPY, EUR, context, new Date(5, Month.August, 2004));
        calculated = eur_jpy.Exchange(m6);
        expected = new Money(m6.Value * jpy_sek2.Rate * eur_usd2.Rate * usd_chf2.Rate / chf_sek2.Rate, EUR);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        // five-rate chain

        ExchangeRate gbp_jpy = rateManager.Lookup(GBP, JPY, context, new Date(4, Month.August, 2004));
        calculated = gbp_jpy.Exchange(m3);
        expected = new Money(
            m3.Value * eur_gbp1.Rate * eur_usd1.Rate * usd_chf1.Rate * jpy_sek1.Rate / chf_sek1.Rate, JPY);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));

        gbp_jpy = rateManager.Lookup(JPY, GBP, context, new Date(5, Month.August, 2004));
        calculated = gbp_jpy.Exchange(m6);
        expected = new Money(
            m6.Value * jpy_sek2.Rate * eur_usd2.Rate * usd_chf2.Rate * eur_gbp2.Rate / chf_sek2.Rate, GBP);

        Assert.True(MoneyFunctions.Close(calculated, expected, context), Message(expected, calculated));
    }

    private static EvaluationContext Context() => new() { EvaluationDate = new Date(16, Month.September, 2015) };

    private static string Message(Money expected, Money calculated) =>
        $"Wrong result: \n    expected:   {expected}\n    calculated: {calculated}";
}
