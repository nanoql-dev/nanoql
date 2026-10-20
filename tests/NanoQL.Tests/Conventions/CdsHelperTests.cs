// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The credit-default-swap helpers' contracts: a model outside its enum is refused at construction, an
/// <see cref="ArgumentOutOfRangeException"/> naming it with QuantLib's "unknown CDS pricing model: 2", where QuantLib
/// refuses it when the helper is bound; an unbound helper has no swap, as QuantLib's has none before
/// <c>setTermStructure</c>, and refuses its implied quote with "term structure not set", while a bound copy prices its
/// swap on the curve and the helper stays unbound; the upfront helper prices an upfront settling today whatever the
/// context's flag for today's cash flows, as QuantLib's sets it for its implied quote; and the latest date is the
/// schedule's last adjusted, a day later under the ISDA model.
/// </summary>
public class CdsHelperTests
{
    [Fact]
    public void AnUnknownModelIsRefusedAtConstruction()
    {
        var unknown = (CreditDefaultSwapPricingModel)2;
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => Spread(unknown));
        Assert.Equal("model", e.ParamName);
        Assert.StartsWith("unknown CDS pricing model: 2", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => new UpfrontCdsHelper(
                0.02,
                0.01,
                new Period(5, TimeUnit.Years),
                0,
                new WeekendsOnly(),
                Frequency.Quarterly,
                BusinessDayConvention.Following,
                DateGenerationRule.CDS,
                new Actual360(),
                0.4,
                CreditFixture.Discount(),
                CreditFixture.Context(),
                model: unknown));
        Assert.Equal("model", e.ParamName);
    }

    [Fact]
    public void ABoundCopyPricesItsSwapAndTheHelperStaysUnbound()
    {
        EvaluationContext context = CreditFixture.Context();
        SpreadCdsHelper helper = Spread(CreditDefaultSwapPricingModel.Midpoint);
        FlatHazardRate curve = CreditFixture.Probability();

        Assert.Null(helper.Swap);
        Assert.Equal("term structure not set", Assert.Throws<NanoQLException>(() => helper.ImpliedQuote(context)).Message);

        SpreadCdsHelper bound = helper.SetTermStructure(curve);
        Assert.NotNull(bound.Swap);
        Assert.Null(helper.Swap);
        double fairSpread = MidPointCdsEngine.Calculate(bound.Swap!, context, curve, 0.4, CreditFixture.Discount()).FairSpread!.Value;
        Assert.Equal(fairSpread, bound.ImpliedQuote(context));
    }

    [Fact]
    public void TheUpfrontHelperPricesAnUpfrontSettlingToday()
    {
        // no upfront settlement days: the upfront falls on the evaluation date, where today's cash flows are excluded
        EvaluationContext context = CreditFixture.Context() with { IncludeTodaysCashFlows = false };
        var helper = new UpfrontCdsHelper(
            0.02,
            0.01,
            new Period(5, TimeUnit.Years),
            0,
            new WeekendsOnly(),
            Frequency.Quarterly,
            BusinessDayConvention.Following,
            DateGenerationRule.CDS,
            new Actual360(),
            0.4,
            CreditFixture.Discount(),
            context,
            upfrontSettlementDays: 0);

        var curve = new PiecewiseFlatHazardRate(CreditFixture.Today, [helper], new Actual365Fixed(), context);
        double hazardRate = curve.HazardRate(new Date(15, Month.January, 2025));

        // QuantLib's, on the wheel under includeTodaysCashFlows false: 0.024202978066284856
        Assert.True(Tolerance.Within(0.024202978066284856, hazardRate), $"{hazardRate}");
        Assert.True(Tolerance.Within(0.02, helper.SetTermStructure(curve).ImpliedQuote(context)));
    }

    [Fact]
    public void TheIsdaModelsLatestDateIsADayLater()
    {
        SpreadCdsHelper midpoint = Spread(CreditDefaultSwapPricingModel.Midpoint);
        SpreadCdsHelper isda = Spread(CreditDefaultSwapPricingModel.ISDA);

        // the 2009 rule: the schedule starts on the previous twentieth, and matures five years and a quarter after it
        Assert.Equal(new Date(20, Month.December, 2023), midpoint.EarliestDate);
        Assert.Equal(new Date(20, Month.March, 2029), midpoint.LatestDate);
        Assert.Equal(midpoint.EarliestDate, isda.EarliestDate);
        Assert.Equal(midpoint.LatestDate!.Value + 1, isda.LatestDate);
    }

    private static SpreadCdsHelper Spread(CreditDefaultSwapPricingModel model) =>
        new(
            0.01,
            new Period(5, TimeUnit.Years),
            0,
            new WeekendsOnly(),
            Frequency.Quarterly,
            BusinessDayConvention.Following,
            DateGenerationRule.CDS,
            new Actual360(),
            0.4,
            CreditFixture.Discount(),
            CreditFixture.Context(),
            model: model);
}
