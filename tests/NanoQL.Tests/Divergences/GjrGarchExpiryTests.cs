// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The analytic GJR-GARCH engine at expiry (the S-hes plan's HR7 (xvii)). QuantLib's engine counts the days to the
/// maturity as T = lround(days·t) and divides by T (<c>analyticgjrgarchengine.cpp:62</c>): an option expiring on the
/// evaluation date, priced with <c>includeReferenceDateEvents</c>, is 0/0, NaN. nanoql prices the forward's discounted
/// intrinsic value there, as S-eq's analytic Heston engine does at model time 0. QuantLib's check: its analytic European
/// engine at the same date, the intrinsic value whatever the volatility.
/// </summary>
public class GjrGarchExpiryTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AnOptionExpiringTodayPricesTheDiscountedIntrinsicValue()
    {
        var context = new EvaluationContext { EvaluationDate = Today, IncludeReferenceDateEvents = true };
        YieldTermStructure riskFree = new FlatForward(Today, 0.05, A365), dividends = new FlatForward(Today, 0.0, A365);
        var model = new GJRGARCHModel(new GJRGARCHProcess(riskFree, dividends, 50.0, 2.0e-4, 2.0e-6, 0.024, 0.93, 0.059, 0.1, 365.0));
        var bsm = new BlackScholesMertonProcess(50.0, dividends, riskFree, new BlackConstantVol(Today, new NullCalendar(), 0.2, A365));
        foreach ((OptionType type, double strike) in new[] { (OptionType.Call, 45.0), (OptionType.Put, 55.0), (OptionType.Call, 55.0) })
        {
            var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today));
            double expected = AnalyticEuropeanEngine.Calculate(option, context, bsm).NPV!.Value;
            Assert.Equal(expected, AnalyticGJRGARCHEngine.Calculate(option, context, model).NPV); // QuantLib: NaN
        }
    }
}
