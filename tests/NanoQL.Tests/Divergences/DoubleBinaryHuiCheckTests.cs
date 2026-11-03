// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The double-barrier binary's convergence check on Hui's series (fixed). QuantLib checks only its 99th term, |term|
/// &lt; 1e-8 (<c>analyticdoublebarrierbinaryengine.cpp:98-110</c>), and that term carries sin(99πx/Z), so at x/Z = j/99
/// the check passes unconverged: on [80, 125] at 5%, r 3%, q 2.7%, two days, the spot at x/Z = 1/9 gets a unit-cash
/// knock-out of 1.000413699, above its discounted cash 0.9998356, which no knock-out can exceed. nanoql decides Hui's
/// convergence by a bound on the dropped tail free of its sines, the terms' envelope, and takes QuantLib's image series
/// where it fails. Check: the image series at x/Z = j/99 (0.9998356299486).
/// </summary>
public class DoubleBinaryHuiCheckTests
{
    private static readonly Date Today = new(16, September, 2015);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void ASpotWhereTheLastSineVanishesIsPricedConverged()
    {
        foreach (double fraction in new[] { 1.0 / 9.0, 1.0 / 3.0 })
        {
            double spot = 80.0 * Math.Pow(125.0 / 80.0, fraction);
            var process = new BlackScholesMertonProcess(
                spot,
                new FlatForward(Today, 0.027, A365),
                new FlatForward(Today, 0.03, A365),
                new BlackConstantVol(Today, new NullCalendar(), 0.05, A365));
            var option = new DoubleBarrierOption(
                DoubleBarrierType.KnockOut, 80.0, 125.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0),
                new EuropeanExercise(Today + 2));
            double value = AnalyticDoubleBarrierBinaryEngine.Calculate(option, Context, process).NPV!.Value;

            // QuantLib 1.000413699255 at 1/9, 0.999932922821 at 1/3
            Assert.Equal(0.9998356299486, value, 1e-12);
        }
    }
}
