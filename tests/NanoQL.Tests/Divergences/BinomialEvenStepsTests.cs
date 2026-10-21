// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Leisen-Reimer and Joshi trees on an even step count. Both build their tree on the next odd count
/// (<c>binomialtree.cpp:106-109</c>, <c>:148-151</c>), while QuantLib's engine builds its time grid and its lattice on
/// the count given (<c>binomialengine.hpp:111-116</c>, <c>bsmlattice.hpp:77-79</c>), so it rolls back a tree built for
/// n + 1 steps over n: at 250 steps a one-year at-the-money call reads 9.1899281 on the Leisen-Reimer tree, 3.7e-2
/// below the analytic engine's 9.2270055, where at 251 it reads 6.0e-6 below. nanoql's engine takes its count from the
/// tree it built, so at 250 steps it computes QuantLib's engine at 251, step for step; every odd count and every other
/// tree is QuantLib's.
/// </summary>
public class BinomialEvenStepsTests
{
    [Fact]
    public void AnEvenCountRollsBackTheTreeItBuilt()
    {
        var today = new Date(15, Month.October, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        DayCounter dc = new Actual365Fixed();
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(today, 0.02, dc), new FlatForward(today, 0.05, dc), new BlackConstantVol(today, null, 0.2, dc));
        Date maturity = today + new Period(1, TimeUnit.Years);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(maturity));
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, 100.0), new EuropeanExercise(maturity));

        // QuantLib's analytic engine: 9.227005508154061 and 6.3300806275499175
        Assert.Equal(9.227005508154061, AnalyticEuropeanEngine.Calculate(call, context, process).NPV!.Value, 1e-12);

        // QuantLib at 250 steps: the Leisen-Reimer call 9.189928090515856, put 6.304718031799062; the Joshi call
        // 9.189933904908628, put 6.3047238461922355. At 251: 9.226999556714963 and 6.330074676109362;
        // 9.227005506567153 and 6.330080625961957, the values nanoql reads at 250.
        Assert.Equal(9.226999556714963, BinomialLRVanillaEngine.Calculate(call, context, process, 250).NPV!.Value, 1e-10);
        Assert.Equal(6.330074676109362, BinomialLRVanillaEngine.Calculate(put, context, process, 250).NPV!.Value, 1e-10);
        Assert.Equal(9.227005506567153, BinomialJ4VanillaEngine.Calculate(call, context, process, 250).NPV!.Value, 1e-10);
        Assert.Equal(6.330080625961957, BinomialJ4VanillaEngine.Calculate(put, context, process, 250).NPV!.Value, 1e-10);
        Assert.Equal(
            BinomialLRVanillaEngine.Calculate(call, context, process, 251).NPV!.Value,
            BinomialLRVanillaEngine.Calculate(call, context, process, 250).NPV!.Value);
    }
}
