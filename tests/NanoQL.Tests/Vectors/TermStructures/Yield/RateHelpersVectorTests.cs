// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The rate-helper vectors, <c>tests/vectors/termstructures/yield/ratehelpers.json</c>:
/// <see cref="DepositRateHelper"/>'s three forms, <see cref="SwapRateHelper"/>'s two, <see cref="FraRateHelper"/>'s
/// six but the period-and-length one (the harness's), <see cref="FuturesRateHelper"/>'s three and
/// <see cref="FxSwapRateHelper"/>'s two, each built in its case's context: whether it builds and its five dates; for a
/// swap helper its swap's start and maturity dates and fixed rate, and its spread; for a futures helper its convexity
/// adjustment; for an FX swap helper its inspectors, the three a helper built from dates refuses only for one built
/// from a tenor.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RateHelpersVectorTests
{
    [Fact]
    public void RateHelpersVector()
    {
        var file = VectorFile.Load("termstructures/yield/ratehelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("helper");
            BootstrapHelper<YieldTermStructure>? helper = null;
            check.Output(c, "built", () =>
            {
                helper = RateHelperInputs.HelperOf(spec, context);
                return true;
            });
            if (helper is null)
            {
                continue;
            }

            RateHelperInputs.CheckDates(check, c, helper);
            if (helper is SwapRateHelper swap)
            {
                check.Output(c, "swapStartDate", swap.Swap.StartDate());
                check.Output(c, "swapMaturityDate", swap.Swap.MaturityDate());
                check.Output(c, "swapFixedRate", swap.Swap.FixedRate);
                check.Output(c, "spread", swap.Spread);
            }
            else if (helper is FuturesRateHelper futures)
            {
                check.Output(c, "convexityAdjustment", futures.ConvexityAdjustment);
            }
            else if (helper is FxSwapRateHelper fx)
            {
                check.Output(c, "spot", fx.Spot);
                check.Output(c, "tenor", new object[] { fx.Tenor.Length, (int)fx.Tenor.Units });
                check.Output(c, "isFxBaseCurrencyCollateralCurrency", fx.IsFxBaseCurrencyCollateralCurrency);
                check.Output(c, "calendar", fx.Calendar?.Name);
                check.Output(c, "tradingCalendar", fx.TradingCalendar?.Name);
                check.Output(c, "adjustmentCalendar", fx.AdjustmentCalendar?.Name);
                if (spec.GetProperty("form").GetString() == "tenor")
                {
                    check.Output(c, "fixingDays", fx.FixingDays());
                    check.Output(c, "businessDayConvention", fx.BusinessDayConvention());
                    check.Output(c, "endOfMonth", fx.EndOfMonth());
                }
            }
        }

        check.Complete();
    }
}
