// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The implied quantities' conventions: a model outside its enum is an <see cref="ArgumentOutOfRangeException"/>
/// naming it, with QuantLib's "unknown CDS pricing model: 2", before anything is solved; and each model's implied
/// hazard rate reprices the target on its own flat curve, the mid-point model's settled on <see cref="WeekendsOnly"/>,
/// the ISDA model's dated on the evaluation date. On <see cref="CreditFixture"/>'s live swap at 1%, priced by the
/// mid-point engine on its 2% curve, QuantLib's mid-point model implies 0.019999999999999882.
/// </summary>
public class CreditDefaultSwapImpliedTests
{
    [Fact]
    public void AnUnknownModelIsRefused()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        var unknown = (CreditDefaultSwapPricingModel)2;

        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(
            () => cds.ImpliedHazardRate(0.0, CreditFixture.Discount(), new Actual365Fixed(), context, 0.4, 1e-8, unknown));
        Assert.Equal("model", e.ParamName);
        Assert.StartsWith("unknown CDS pricing model: 2", e.Message, StringComparison.Ordinal);
        e = Assert.Throws<ArgumentOutOfRangeException>(
            () => cds.ConventionalSpread(0.4, CreditFixture.Discount(), new Actual365Fixed(), context, unknown));
        Assert.Equal("model", e.ParamName);
        Assert.StartsWith("unknown CDS pricing model: 2", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheImpliedRateRepricesTheTargetOnItsModelsCurve()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(false, 0.01);
        FlatForward discount = CreditFixture.Discount();
        double target = MidPointCdsEngine.Calculate(cds, context, CreditFixture.Probability(), 0.4, discount).NPV!.Value;

        double midpoint = cds.ImpliedHazardRate(target, discount, new Actual365Fixed(), context);
        Assert.Equal(0.019999999999999882, midpoint, 1e-12);

        double isda = cds.ImpliedHazardRate(
            target, discount, new Actual365Fixed(), context, 0.4, 1e-8, CreditDefaultSwapPricingModel.ISDA);
        double repriced = IsdaCdsEngine.Calculate(
            cds, context, new FlatHazardRate(context.EvaluationDate, isda, new Actual365Fixed()), 0.4, discount).NPV!.Value;
        Assert.Equal(target, repriced, 1e-2);
    }
}
