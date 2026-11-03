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
/// The analytic double-barrier engine's rebate (fixed). QuantLib's engine never reads <c>DoubleBarrierOption</c>'s
/// rebate, where its finite-difference Heston double-barrier engine pays a knock-out's at the hit
/// (<c>fdhestondoublebarrierengine.cpp:96-99</c>): a knock-out call on [80, 120] prices 1.420391 at rebates 0 and 5,
/// where the FD engine at a vanishing vol of vol gives 1.426200 and 3.920732. nanoql takes QuantLib's single-barrier
/// convention: a knock-out's rebate paid at the first touch of either barrier, R × (KIKO + KOKI) at unit cash through
/// the double-barrier binary engine, and a knock-in's paid at expiry where neither is touched, R × the binary knock-out
/// at unit cash and strike 0. Checks: the survival route on QuantLib's image series (0.60784983008 per unit rebate on
/// [80, 120] at 20% over a year), QuantLib's FD Heston engine on a corridor its grid resolves, and QuantLib's binary
/// engine.
/// </summary>
public class DoubleBarrierRebateTests
{
    private static readonly Date Today = new(1, January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AKnockOutsRebateIsPaidAtTheFirstTouch()
    {
        GeneralizedBlackScholesProcess process = Process(0.20);
        var exercise = new EuropeanExercise(Today + 365);
        double rebated = KnockOut(process, exercise, 5.0, OptionType.Call);
        double bare = KnockOut(process, exercise, 0.0, OptionType.Call);

        // the survival route: 0.60784983008 per unit rebate
        Assert.Equal(0.60784983008, (rebated - bare) / 5.0, 1e-10);

        // QuantLib's FD Heston double-barrier engine at a vanishing vol of vol, 182 days at 25%: KO(5) 3.920732 against
        // 1.420391 + 5 × 0.499468 = 3.917733, within its grid's 0.006 at rebate 0
        GeneralizedBlackScholesProcess p25 = Process(0.25);
        var halfYear = new EuropeanExercise(Today + 182);
        var model = new HestonModel(new HestonProcess(p25.RiskFreeRate, p25.DividendYield, 100.0, 0.0625, 5.0, 0.0625, 1e-4, 0.0));
        var option = new DoubleBarrierOption(
            DoubleBarrierType.KnockOut, 80.0, 120.0, 5.0, new PlainVanillaPayoff(OptionType.Call, 100.0), halfYear);
        double fd = FdHestonDoubleBarrierEngine.Calculate(option, Context, model, 400, 400, 50).NPV!.Value;
        Assert.Equal(fd, KnockOut(p25, halfYear, 5.0, OptionType.Call), 1e-2);
        Assert.Equal(3.917733, KnockOut(p25, halfYear, 5.0, OptionType.Call), 1e-6);
    }

    [Fact]
    public void AKnockInsRebateIsPaidAtExpiryWhereNeitherBarrierIsTouched()
    {
        GeneralizedBlackScholesProcess process = Process(0.20);
        var exercise = new EuropeanExercise(Today + 365);
        var payoff = new PlainVanillaPayoff(OptionType.Put, 100.0);
        double rebated = AnalyticDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockIn, 80.0, 120.0, 3.0, payoff, exercise), Context, process).NPV!.Value;
        double bare = AnalyticDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockIn, 80.0, 120.0, 0.0, payoff, exercise), Context, process).NPV!.Value;
        double noTouch = AnalyticDoubleBarrierBinaryEngine.Calculate(
            new DoubleBarrierOption(
                DoubleBarrierType.KnockOut, 80.0, 120.0, 0.0, new CashOrNothingPayoff(OptionType.Call, 0.0, 1.0), exercise),
            Context,
            process).NPV!.Value;
        Assert.Equal(3.0 * noTouch, rebated - bare, 1e-12);
    }

    private static double KnockOut(GeneralizedBlackScholesProcess process, Exercise exercise, double rebate, OptionType type) =>
        AnalyticDoubleBarrierEngine.Calculate(
            new DoubleBarrierOption(DoubleBarrierType.KnockOut, 80.0, 120.0, rebate, new PlainVanillaPayoff(type, 100.0), exercise),
            Context,
            process).NPV!.Value;

    private static GeneralizedBlackScholesProcess Process(double sigma) => new BlackScholesMertonProcess(
        100.0,
        new FlatForward(Today, 0.02, A365),
        new FlatForward(Today, 0.05, A365),
        new BlackConstantVol(Today, new NullCalendar(), sigma, A365));
}
