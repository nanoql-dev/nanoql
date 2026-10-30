// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Extensions;

/// <summary>
/// The SOFR curve example, which <c>docs/conventions.md</c> walks through (CVD36): an illustrative <c>USD-SOFR</c> OIS
/// convention in a file of its own, <c>SofrCurveExample.xml</c>, a curve bootstrapped from a handful of illustrative
/// SOFR swap quotes through its rate helpers, and a 5-year SOFR swap priced on the curve on the example's conventions
/// and on <see cref="MakeOIS"/>'s defaults. The quotes are illustrative, not market data, and the curve's list of
/// instruments is code.
/// </summary>
public class SofrCurveExample
{
    // The evaluation date and the illustrative SOFR swap rates by tenor.
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly (Period Tenor, double Rate)[] Quotes =
    [
        (new(1, TimeUnit.Months), 0.0430), (new(3, TimeUnit.Months), 0.0428), (new(6, TimeUnit.Months), 0.0421),
        (new(1, TimeUnit.Years), 0.0405), (new(2, TimeUnit.Years), 0.0380), (new(3, TimeUnit.Years), 0.0368),
        (new(5, TimeUnit.Years), 0.0360), (new(7, TimeUnit.Years), 0.0362), (new(10, TimeUnit.Years), 0.0370),
    ];

    [Fact]
    public void TheCurveRepricesItsQuotesAndTheSwapPricesOnBothConventions()
    {
        // the example's conventions over QuantLib's presets
        ConventionSet set = ConventionSet.QuantLib.With(ConventionsXml.Read(Path.Combine(AppContext.BaseDirectory, "Extensions", "SofrCurveExample.xml")));
        var context = new EvaluationContext { EvaluationDate = Today };
        OisConvention sofr = set.Ois("USD-SOFR");

        // a helper per quote, through the convention, and the curve they bootstrap
        OISRateHelper[] helpers = [.. Quotes.Select(q => sofr.RateHelper(q.Rate, q.Tenor, context))];
        var curve = new PiecewiseLogLinearDiscount(Today, helpers, new Actual365Fixed(), context);

        // every helper reprices to its quote
        Assert.All(helpers.Zip(Quotes), h => Assert.Equal(h.Second.Rate, h.First.SetTermStructure(curve).ImpliedQuote(context), 1e-12));

        // a 5-year SOFR swap paying 3.60% fixed on 10 million, on the example's conventions and on MakeOIS's defaults
        OvernightIndexedSwap onExample = sofr.Builder(new Period(5, TimeUnit.Years), curve).WithFixedRate(0.036).WithNominal(10e6).ToOvernightIndexedSwap(context);
        OvernightIndexedSwap onDefaults = new MakeOIS(new Period(5, TimeUnit.Years), set.OvernightIndex("Sofr", curve)).WithFixedRate(0.036).WithNominal(10e6)
            .ToOvernightIndexedSwap(context);
        double example = DiscountingSwapEngine.Calculate(onExample, context, curve).NPV!.Value;
        double defaults = DiscountingSwapEngine.Calculate(onDefaults, context, curve).NPV!.Value;

        // the example's swap is the 5-year quote's, at par; on MakeOIS's defaults it pays on the periods' end dates,
        // with no lag, and is worth -3.1103
        Assert.Equal(0.0, example, 1e-6);
        Assert.Equal(-3.1103, defaults, 1e-4);
        Assert.Equal(3.1103, example - defaults, 1e-4);
        Assert.Equal((2, 0), (onExample.PaymentLag, onDefaults.PaymentLag));
    }
}
