// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's fix after v1.44 to the holder-extensible engine (#2892, <c>453e6605c</c>): the put's correlation sign, the
/// Newton seeds at the strike, the put's derivative, the empty extension region and the call's with dividends
/// (<c>analyticholderextensibleoptionengine.cpp:87-88</c>, <c>:96-197</c>); 1.44 prices a put −1.063 where its value
/// is 7.428. nanoql takes the fix (the S-exo plan's ER6 (iii)). The check is master's case,
/// <c>testHolderExtensibleOptionCriticalPrices</c>: options whose extension region is empty or unbounded against values
/// from numerical integration at the first expiry, within 1e-4, here until the pin moves.
/// </summary>
public class HolderExtensibleCriticalPricesTests
{
    // QuantLib's test runner's evaluation date (quantlibglobalfixture.cpp:62-63)
    private static readonly Date RunnerDate = new(16, Month.September, 2015);

    [Fact]
    public void TestHolderExtensibleOptionCriticalPrices()
    {
        // Testing holder-extensible options whose extension region is empty or unbounded...
        DayCounter dc = new Actual360();
        Date today = RunnerDate;
        var context = new EvaluationContext { EvaluationDate = today };
        Date exDate1 = today + 180;
        Date exDate2 = today + 270;

        // expected values from numerical integration at the first expiry
        (OptionType Type, Real Spot, Real Strike1, Real Strike2, Real Premium, Real DividendYield, Real Expected)[] cases =
        [
            (OptionType.Put, 100.0, 100.0, 95.0, 1.0, 0.0, 5.20095543317),
            (OptionType.Put, 100.0, 100.0, 105.0, 1.0, 0.0, 7.2042184187),
            (OptionType.Put, 80.0, 100.0, 95.0, 1.0, 0.0, 17.3209767623),
            (OptionType.Call, 100.0, 100.0, 120.0, 1.0, 0.0, 9.0411753344),
            (OptionType.Call, 130.0, 100.0, 105.0, 1.0, 0.0, 34.3706760702),
            (OptionType.Call, 100.0, 100.0, 100.0, 1.0, 0.03, 9.3728040611),
            (OptionType.Call, 120.0, 100.0, 100.0, 1.0, 0.03, 24.0816774969),
        ];

        Real tolerance = 1e-4;
        foreach (var c in cases)
        {
            var process = new BlackScholesMertonProcess(
                c.Spot, new FlatForward(today, c.DividendYield, dc), new FlatForward(today, 0.08, dc),
                new BlackConstantVol(today, new NullCalendar(), 0.25, dc));
            var option = new HolderExtensibleOption(
                c.Type, c.Premium, exDate2, c.Strike2, new PlainVanillaPayoff(c.Type, c.Strike1), new EuropeanExercise(exDate1));

            Real calculated = AnalyticHolderExtensibleOptionEngine.Calculate(option, context, process).NPV!.Value;
            Real error = Math.Abs(calculated - c.Expected);
            Assert.True(
                error <= tolerance,
                $"Failed to reproduce holder-extensible option value\n    type:       {c.Type}\n    spot:       {c.Spot}"
                + $"\n    strikes:    {c.Strike1}, {c.Strike2}\n    premium:    {c.Premium}\n    dividend:   {c.DividendYield}"
                + $"\n    expected:   {c.Expected}\n    calculated: {calculated}\n    error:      {error}");
        }
    }
}
