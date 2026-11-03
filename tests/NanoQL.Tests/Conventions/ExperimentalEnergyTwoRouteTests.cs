// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Two routes to one value for the experimental energy models: Kluge's jump model at a vanishing jump intensity against
/// the extended Ornstein-Uhlenbeck closed form (a Black call on the lognormal spot e^X); a swing option with one
/// exercise and one right against the European option at its date, on the same grids; and the VPP's finite-difference
/// value at vanishing volatility against its intrinsic value by dynamic programming.
/// </summary>
public class ExperimentalEnergyTwoRouteTests
{
    private static readonly Date Today = new(18, Month.December, 2011);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtAVanishingIntensityTheJumpModelIsTheExtendedOrnsteinUhlenbeckModel()
    {
        // X: speed 1, σ 0.4, x₀ = b = ln 30; the spot e^(X + Y), Y ~ 0: a call is Black's on X's normal law
        const double speed = 1.0, sigma = 0.4, rate = 0.05;
        double x0 = Math.Log(30.0);
        var process = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(speed, sigma, x0, _ => x0), 0.0, 5.0, 1e-6, 2.0);
        YieldTermStructure rTS = new FlatForward(Today, rate, A365);
        Date maturity = Today + new Period(6, TimeUnit.Months);
        double t = A365.YearFraction(Today, maturity);
        foreach (double strike in new[] { 25.0, 30.0, 36.0 })
        {
            var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(maturity));
            double fd = FdExtOUJumpVanillaEngine.Calculate(option, Context, process, rTS, 100, 400, 25).NPV!.Value;

            double variance = sigma * sigma / (2 * speed) * (1 - Math.Exp(-2 * speed * t));
            double forward = Math.Exp(x0 + (0.5 * variance));
            double expected = BlackFormulaFunctions.BlackFormula(OptionType.Call, strike, forward, Math.Sqrt(variance), rTS.Discount(maturity));
            Assert.True(Math.Abs(fd - expected) <= 2e-3 * expected, $"strike {strike}: {fd} against {expected}");
        }
    }

    [Fact]
    public void AOneRightSwingIsTheEuropeanAtItsDate()
    {
        var process = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(1.0, 2.0, 3.0, _ => 3.0), 0.0, 5.0, 1.0, 2.0);
        YieldTermStructure rTS = new FlatForward(Today, 0.1, A365);
        Date exerciseDate = Today + new Period(3, TimeUnit.Months);
        const double strike = 30.0;

        // QuantLib's swing payoff is the forward payoff, its right not obliged: the put at one exercise
        var swing = new VanillaSwingOption(new VanillaForwardPayoff(OptionType.Put, strike), new SwingExercise([exerciseDate]), 0, 1);
        double swingValue = FdSimpleExtOUJumpSwingEngine.Calculate(swing, Context, process, rTS, 50, 100, 50).NPV!.Value;

        var european = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new EuropeanExercise(exerciseDate));
        double europeanValue = FdExtOUJumpVanillaEngine.Calculate(european, Context, process, rTS, 50, 100, 50).NPV!.Value;
        Assert.True(Math.Abs(swingValue - europeanValue) <= 1e-3 * europeanValue, $"{swingValue} against {europeanValue}");
    }

    [Fact]
    public void AtVanishingVolatilityTheVppIsItsIntrinsicValue()
    {
        // power and gas deterministic at the forwards the shapes give, at the exercise times themselves, the jumps
        // vanishing: the finite-difference value is the plant's optimal schedule, which dynamic programming finds
        double[] fuelCycle = [20.74, 21.65, 20.78, 21.58, 21.43, 20.82, 22.02, 21.52, 21.02, 21.46, 21.75, 20.69];
        double[] powerCycle = [40.40, 36.71, 31.87, 25.81, 31.61, 35.00, 46.22, 60.68, 42.45, 38.01, 33.84, 29.79];
        var exercise = new SwingExercise(Today, Today + 1, 3600);
        DayCounter dc = new ActualActual(ActualActualConvention.ISDA);
        System.Collections.Immutable.ImmutableArray<double> times = exercise.ExerciseTimes(dc, Today);
        double[] fuel = [.. Enumerable.Range(0, times.Length).Select(i => fuelCycle[i % fuelCycle.Length])];
        double[] power = [.. Enumerable.Range(0, times.Length).Select(i => powerCycle[i % powerCycle.Length])];
        var option = new VanillaVPPOption(1.6, 8, 40, 2, 2, 20, 100, exercise);
        double intrinsic = DynProgVPPIntrinsicValueEngine.Calculate(option, Context, fuel, power, 3.0).NPV!.Value;

        var lnPower = new ExtOUWithJumpsProcess(new ExtendedOrnsteinUhlenbeckProcess(7.0, 1e-4, 0.0, _ => 0.0), 0.0, 200.0, 1e-8, 5.0);
        var lnGas = new ExtendedOrnsteinUhlenbeckProcess(4.45, 1e-4, 0.0, _ => 0.0);
        var process = new KlugeExtOUProcess(0.7, lnPower, lnGas);
        (double Time, double Value)[] fuelShape = [.. times.Select((t, i) => (t, Math.Log(fuel[i])))];
        (double Time, double Value)[] powerShape = [.. times.Select((t, i) => (t, Math.Log(power[i])))];

        YieldTermStructure rTS = new FlatForward(Today, 0.0, dc);
        double fd = FdSimpleKlugeExtOUVPPEngine.Calculate(option, Context, process, rTS, fuelShape, powerShape, 3.0, 1, 5, 5, 5).NPV!.Value;
        Assert.True(intrinsic > 0.0, $"intrinsic {intrinsic}");
        Assert.True(Math.Abs(fd - intrinsic) <= 1e-3 * intrinsic, $"{fd} against {intrinsic}");
    }
}
