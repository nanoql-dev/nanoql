// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A nonstandard swaption converted from a swaption (QuantLib's <c>NonstandardSwaption(const Swaption&amp;)</c>, which
/// the wheel doesn't bind): the underlying converted, the exercise, settlement type and method carried, so the
/// nonstandard engine prices it as the swaption engine prices the source.
/// </summary>
public class NonstandardSwaptionConversionTests
{
    [Fact]
    public void AConvertedSwaptionPricesAsItsSource()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        YieldTermStructure discount = new FlatForward(today, 0.028, new Actual365Fixed());
        var calendar = new TARGET();
        Date first = calendar.Advance(today, 1, TimeUnit.Years);
        var exercise = new BermudanExercise([first, calendar.Advance(today, 2, TimeUnit.Years), calendar.Advance(today, 3, TimeUnit.Years)]);
        var source = new Swaption(
            Gaussian1dInputs.SwapOf(new Euribor6M(curve), first, 5, 0.03, SwapType.Payer, context),
            exercise,
            SettlementType.Cash,
            SettlementMethod.CollateralizedCashPrice);

        var converted = new NonstandardSwaption(source, context);
        Assert.Same(exercise, converted.Exercise);
        Assert.Equal(SettlementType.Cash, converted.SettlementType);
        Assert.Equal(SettlementMethod.CollateralizedCashPrice, converted.SettlementMethod);
        Assert.Equal(SwapType.Payer, converted.Type);
        Assert.Equal(source.Underlying.FixedLeg.Length, converted.Underlying.FixedLeg.Length);
        Assert.Equal(source.Underlying.FloatingLeg.Length, converted.Underlying.FloatingLeg.Length);

        Gsr model = Gaussian1dInputs.Constant(curve, 0.03, 0.008);
        double price = Gaussian1dNonstandardSwaptionEngine.Calculate(converted, context, model, discountCurve: discount).NPV!.Value;
        double expected = Gaussian1dSwaptionEngine.Calculate(source, context, model, discountCurve: discount).NPV!.Value;
        Assert.True(Math.Abs((price / expected) - 1.0) <= 1e-12, $"{price} against the swaption engine's {expected}");
    }
}
