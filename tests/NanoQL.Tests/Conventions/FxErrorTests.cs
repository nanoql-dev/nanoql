// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The type, text and parameter of the refusals of <see cref="Money"/>, <see cref="ExchangeRate"/>,
/// <see cref="ExchangeRates"/>, <see cref="FxForward"/> and <see cref="DiscountingFxForwardEngine"/> that the vectors
/// record as <c>{"throws": true}</c>, which accepts either exception type: QuantLib's texts, with the playbook's types.
/// </summary>
public class FxErrorTests
{
    private static readonly Currency Eur = new EURCurrency();
    private static readonly Currency Usd = new USDCurrency();
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void MoneyRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        EvaluationContext automated = context with { MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.AutomatedConversion } };

        NanoQLException noData = Assert.Throws<NanoQLException>(() => new Money(1.0, null).Rounded());
        Assert.Equal("no currency data provided", noData.Message);
        noData = Assert.Throws<NanoQLException>(() => new Money(1.0, null).Add(new Money(1.0, Eur), automated));
        Assert.Equal("no currency data provided", noData.Message);

        NanoQLException noRate = Assert.Throws<NanoQLException>(() => new Money(1.0, Eur).Add(new Money(1.0, new JPYCurrency()), automated));
        Assert.Equal("no conversion available from JPY to EUR for October 15th, 2026", noRate.Message);

        NanoQLException noBase = Assert.Throws<NanoQLException>(() => new Money(1.0, Eur).LessThan(new Money(1.0, Usd), context with
        {
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.BaseCurrencyConversion },
        }));
        Assert.Equal("no base currency set", noBase.Message);

        Assert.Throws<ArgumentNullException>(() => new Money(1.0, Eur).Add(new Money(1.0, Eur), null!));
        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(
            () => new MoneySettings { ConversionType = (MoneyConversionType)(-1) });
        Assert.Equal("ConversionType", type.ParamName);
        Assert.StartsWith("unknown conversion type (-1)", type.Message);
    }

    [Fact]
    public void ExchangeRateRefusals()
    {
        var eurUsd = new ExchangeRate(Eur, Usd, 1.2042);
        ArgumentException notApplicable = Assert.Throws<ArgumentException>(() => eurUsd.Exchange(new Money(1.0, new GBPCurrency())));
        Assert.Equal("amount", notApplicable.ParamName);
        Assert.StartsWith("exchange rate not applicable", notApplicable.Message);

        ExchangeRate derived = ExchangeRate.Chain(eurUsd, new ExchangeRate(Eur, new GBPCurrency(), 0.6612));
        notApplicable = Assert.Throws<ArgumentException>(() => derived.Exchange(new Money(1.0, Eur)));
        Assert.Equal("amount", notApplicable.ParamName);
        Assert.Throws<ArgumentException>(() => derived.Exchange(new Money(1.0, new JPYCurrency())));

        ArgumentException notChainable = Assert.Throws<ArgumentException>(
            () => ExchangeRate.Chain(eurUsd, new ExchangeRate(new GBPCurrency(), new JPYCurrency(), 191.3)));
        Assert.Equal("r2", notChainable.ParamName);
        Assert.StartsWith("exchange rates not chainable", notChainable.Message);

        Assert.Throws<ArgumentNullException>(() => ExchangeRate.Chain(eurUsd, null!));
    }

    [Fact]
    public void RegistryRefusals()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        ExchangeRates rates = ExchangeRates.Default;

        NanoQLException direct = Assert.Throws<NanoQLException>(() => rates.Lookup(Eur, Usd, context, type: ExchangeRateType.Direct));
        Assert.Equal("no direct conversion available from EUR to USD for October 15th, 2026", direct.Message);
        NanoQLException smart = Assert.Throws<NanoQLException>(() => rates.Lookup(Usd, new JPYCurrency(), context));
        Assert.Equal("no conversion available from USD to JPY for October 15th, 2026", smart.Message);

        // a triangulation whose two legs both fail refuses with the second leg's text, as MSVC, the wheel's compiler,
        // evaluates QuantLib's chain(first, second) arguments last to first
        var y1998 = new Date(1, Month.January, 1998);
        Assert.Equal(
            "no conversion available from EUR to JPY for January 1st, 1998",
            Assert.Throws<NanoQLException>(() => rates.Lookup(new ITLCurrency(), new JPYCurrency(), context, y1998)).Message);
        Assert.Equal(
            "no direct conversion available from EUR to ITL for January 1st, 1998",
            Assert.Throws<NanoQLException>(() => rates.Lookup(new JPYCurrency(), new ITLCurrency(), context, y1998)).Message);

        Assert.Equal("no currency data provided", Assert.Throws<NanoQLException>(() => rates.Lookup(null, Usd, context)).Message);
        Assert.Equal("no currency data provided", Assert.Throws<NanoQLException>(() => rates.Add(new ExchangeRate(Eur, null, 1.0))).Message);
        Assert.Equal(1.0, rates.Lookup(null, null, context).Rate);

        ArgumentOutOfRangeException type = Assert.Throws<ArgumentOutOfRangeException>(
            () => rates.Lookup(Eur, Usd, context, type: (ExchangeRateType)2));
        Assert.Equal("type", type.ParamName);
        Assert.StartsWith("unknown exchange-rate type", type.Message);
        Assert.Throws<ArgumentNullException>(() => rates.Lookup(Eur, Usd, null!));
        Assert.Equal("date", Assert.Throws<ArgumentException>(() => rates.Lookup(Eur, Usd, context, default(Date))).ParamName);
        Assert.Throws<ArgumentNullException>(() => rates.Add(null!));
    }

    [Fact]
    public void FxForwardRefusals()
    {
        Date maturity = Today + 180;
        Currency sgd = new SGDCurrency();
        Assert.Equal("sourceNominal", Refusal(() => new FxForward(0.0, Usd, 1.0, sgd, maturity, true), "source nominal must be positive"));
        Assert.Equal("targetNominal", Refusal(() => new FxForward(1.0, Usd, -1.0, sgd, maturity, true), "target nominal must be positive"));
        Assert.Equal("forwardRate", Refusal(() => new FxForward(1.0, Usd, sgd, 0.0, maturity, true), "forward rate must be positive"));
        Assert.Equal("targetCurrency", Refusal(() => new FxForward(1.0, Usd, 1.0, new USDCurrency(), maturity, true), "source and target currencies must be different"));
        ArgumentNullException empty = Assert.Throws<ArgumentNullException>(() => new FxForward(1.0, null!, 1.0, sgd, maturity, true));
        Assert.Equal("sourceCurrency", empty.ParamName);
        Assert.StartsWith("source currency must not be empty", empty.Message);
        empty = Assert.Throws<ArgumentNullException>(() => new FxForward(1.0, Usd, null!, 1.36, maturity, true));
        Assert.Equal("targetCurrency", empty.ParamName);
        Assert.StartsWith("target currency must not be empty", empty.Message);
        Assert.Throws<ArgumentOutOfRangeException>(() => new FxForward(1.0, Usd, 1.0, sgd, maturity, true, -1));

        var context = new EvaluationContext { EvaluationDate = Today };
        var forward = new FxForward(1000000.0, Usd, 1350000.0, sgd, maturity, true, 2, new TARGET());
        YieldTermStructure usdCurve = new FlatForward(Today, 0.05, new Actual365Fixed());
        YieldTermStructure late = new FlatForward(Today + 5, 0.035, new Actual365Fixed());
        Assert.Equal(
            "targetCurrencyDiscountCurve",
            Refusal(
                () => DiscountingFxForwardEngine.Calculate(forward, context, usdCurve, late, 1.35),
                "target currency discount curve reference date (October 20th, 2026) must be on or before settlement date (October 19th, 2026)"));
        Assert.Equal(
            "sourceCurrencyDiscountCurve",
            Refusal(
                () => DiscountingFxForwardEngine.Calculate(forward, context, late, usdCurve, 1.35),
                "source currency discount curve reference date (October 20th, 2026) must be on or before settlement date (October 19th, 2026)"));
        Assert.Equal("spotFx", Refusal(() => DiscountingFxForwardEngine.Calculate(forward, context, usdCurve, usdCurve, 0.0), "spot FX rate must be positive"));
        ArgumentNullException noCurve = Assert.Throws<ArgumentNullException>(
            () => DiscountingFxForwardEngine.Calculate(forward, context, usdCurve, null!, 1.35));
        Assert.Equal("targetCurrencyDiscountCurve", noCurve.ParamName);
        Assert.StartsWith("target currency discount curve handle is empty", noCurve.Message);
        noCurve = Assert.Throws<ArgumentNullException>(
            () => DiscountingFxForwardEngine.Calculate(forward, context, null!, usdCurve, 1.35));
        Assert.Equal("sourceCurrencyDiscountCurve", noCurve.ParamName);
        Assert.StartsWith("source currency discount curve handle is empty", noCurve.Message);
    }

    // An ArgumentException with QuantLib's text; its parameter's name.
    private static string? Refusal(Action a, string text)
    {
        ArgumentException e = Assert.Throws<ArgumentException>(a);
        Assert.StartsWith(text, e.Message);
        return e.ParamName;
    }

    private static string? Refusal(Func<object> f, string text) => Refusal(() => { _ = f(); }, text);
}
