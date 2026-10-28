// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's synthetic CDO vectors, <c>tests/vectors/cxx/experimental/credit/syntheticcdo.json</c>: CDOs on
/// tranches of baskets bound to each model, priced by both engines.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SyntheticCDOCxxVectorTests
{
    [Fact]
    public void SyntheticCDOCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/credit/syntheticcdo");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Basket basket = PortfolioInputs.BoundBasketOf(c.Input("basket"), c.Input("model"), context);
            YieldTermStructure discount = CreditInputs.DiscountOf(c.Input("discount"))!;
            if (c.Name == "an expired CDO")
            {
                SyntheticCDO expired = Cdo(basket, c.Input("cdo"), context);
                SyntheticCDOResults r = MidPointCDOEngine.Calculate(expired, context, discount);
                check.Output(c, "expired", new object?[]
                {
                    expired.IsExpired(context), r.NPV, r.PremiumValue, r.ProtectionValue, r.RemainingNotional, r.ExpectedTrancheLoss.ToArray(),
                });
                continue;
            }

            JsonElement[] cdos = [.. c.Input("cdos").EnumerateArray()];
            Period[] steps = [.. c.Input("steps").EnumerateArray().Select(PortfolioInputs.PeriodOf)];
            check.Output(c, "inspect", Each(cdos, s =>
            {
                SyntheticCDO x = Cdo(basket, s, context);
                return new object[] { x.LeverageFactor, x.Maturity, x.IsExpired(context) };
            }));
            check.Output(c, "midPoint", Each(cdos, s => Results(MidPointCDOEngine.Calculate(Cdo(basket, s, context), context, discount))));
            check.Output(c, "integral", Each(
                cdos.SelectMany(s => steps.Select(p => (s, p))),
                x => Results(IntegralCDOEngine.Calculate(Cdo(basket, x.s, context), context, discount, x.p))));
        }

        check.Complete();
    }

    private static SyntheticCDO Cdo(Basket basket, JsonElement s, EvaluationContext context) => new(
        basket,
        (ProtectionSide)s.GetProperty("side").GetInt32(),
        CreditInputs.ScheduleOf(s.GetProperty("schedule"), context),
        s.GetProperty("upfrontRate").GetDouble(),
        s.GetProperty("runningRate").GetDouble(),
        CreditInputs.DayCounterOf(s.GetProperty("dayCounter")),
        (BusinessDayConvention)s.GetProperty("paymentConvention").GetInt32(),
        s.GetProperty("notional").ValueKind == JsonValueKind.Null ? null : s.GetProperty("notional").GetDouble());

    private static object?[] Results(SyntheticCDOResults r) =>
    [
        r.NPV, r.PremiumValue, r.ProtectionValue, r.FairPremium, r.PremiumLegNPV, r.ProtectionLegNPV, r.RemainingNotional,
        r.Error, r.ExpectedTrancheLoss.ToArray(), r.AdditionalResults["premiumLegNPV"], r.AdditionalResults["protectionLegNPV"],
    ];

    // Each value lazily, so that a throw is checked element by element.
    private static Func<object?>[] Each<T>(IEnumerable<T> xs, Func<T, object?> f) =>
        [.. xs.Select(x => (Func<object?>)(() => f(x)))];
}
