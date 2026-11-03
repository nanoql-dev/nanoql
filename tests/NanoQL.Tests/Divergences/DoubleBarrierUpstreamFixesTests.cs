// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Upstream's fix after v1.44 the discretized double-barrier assets take (master first): #2878 (QuantLib's master
/// <c>eaeac5651</c>), the American window's bounds compared with <c>isOnTime</c> as well
/// (<c>discretizeddoublebarrieroption.cpp:61-66</c>), where QuantLib 1.44's plain comparison skips the exercise at
/// expiry when the grid's last time rounds below maturity. QuantLib's own case,
/// <c>DoubleBarrierOptionTests.TestBinomialExerciseAtMaturity</c>, checks the plain asset; here the Derman-Kani asset,
/// which reads the plain one's check, on that case's market, until the pin moves to a release that carries the fix.
/// </summary>
public class DoubleBarrierUpstreamFixesTests
{
    [Fact]
    public void TheDermanKaniAssetExercisesAnAmericanAtMaturity()
    {
        var today = new Date(21, September, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        Date maturity = today + 91;
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.0, dc), new FlatForward(today, 0.05, dc),
            new BlackConstantVol(today, new NullCalendar(), 0.15, dc));
        var payoff = new PlainVanillaPayoff(OptionType.Call, 85.0);
        double expected = BinomialVanillaEngine.Calculate<CoxRossRubinstein>(
            new VanillaOption(payoff, new EuropeanExercise(maturity)), context, process, 51).NPV!.Value;

        // All stock nodes lie between 58 and 171, so neither barrier can knock out.
        var american = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 1.0, 1000.0, 0.0, payoff, new AmericanExercise(today, maturity));
        double value = BinomialDoubleBarrierEngine.Calculate<CoxRossRubinstein, DiscretizedDermanKaniDoubleBarrierOption>(
            american, context, process, 51).NPV!.Value;
        Assert.Equal(expected, value, 1e-10);
    }
}
