// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The finite-difference CIR engine with a quanto helper. QuantLib's engine passes the helper to its equity mesher
/// alone, and its operator takes none (<c>fdcirvanillaengine.cpp:60-63</c>, <c>fdmcirop.cpp</c>), so a quanto prices
/// without the quanto drift, on a mesher centred for it. nanoql's operator subtracts the helper's adjustment with the
/// grid's short rate as the domestic one, r − q − ½v − (r − r_f + ρσσ_fx): the quanto drift r_f − q − ½v − ρσσ_fx, the
/// domestic rate only discounting. QuantLib's check: as the rate's volatility goes to 0 with r₀ = θ the helper's
/// domestic rate, the engine is QuantLib's finite-difference Black-Scholes engine on the same quanto helper.
/// </summary>
public class FdCirQuantoTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly DayCounter Dc = new Actual365Fixed();
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Theory]
    [InlineData(OptionType.Call, 100.0)]
    [InlineData(OptionType.Put, 95.0)]
    public void AsTheRatesVolatilityVanishesItIsTheBlackScholesQuanto(OptionType type, double strike)
    {
        // domestic 5%, foreign 2%, q 1%, σ 25%, the exchange rate's volatility 15% at a correlation of −0.4; a year
        var domestic = new FlatForward(Today, 0.05, Dc);
        var helper = new FdmQuantoHelper(
            domestic, new FlatForward(Today, 0.02, Dc), new BlackConstantVol(Today, new NullCalendar(), 0.15, Dc), -0.4, 1.0);
        var process = new BlackScholesMertonProcess(
            100.0, new FlatForward(Today, 0.01, Dc), domestic, new BlackConstantVol(Today, new NullCalendar(), 0.25, Dc));
        var option = new VanillaOption(new PlainVanillaPayoff(type, strike), new EuropeanExercise(Today + 365));

        var cir = new CoxIngersollRossProcess(1.0, 1e-6, 0.05, 0.05);
        double quanto = FdCIRVanillaEngine.Calculate(option, Context, cir, process, 100, 400, 50, 0, 0.0, quantoHelper: helper).NPV!.Value;
        double bs = FdBlackScholesVanillaEngine.Calculate(option, Context, process, quantoHelper: helper, tGrid: 100, xGrid: 400).NPV!.Value;
        Assert.Equal(bs, quanto, 2e-3);

        // the quanto drift moves the value by more than a hundred times that
        double plain = FdCIRVanillaEngine.Calculate(option, Context, cir, process, 100, 400, 50, 0, 0.0).NPV!.Value;
        Assert.True(Math.Abs(plain - quanto) > 0.2, $"{plain} against {quanto}");
    }
}
