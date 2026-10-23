// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Flat payoff extrapolation's right tail (the S-g1d plan's G1R6 (xxi)): with <c>flatPayoffExtrapolation</c>,
/// QuantLib's Gaussian1d engines integrate past the grid's last node the constant <c>p[n − 2]</c>, the second-last
/// node's value (the swaption engine <c>gaussian1dswaptionengine.cpp:166-171</c>, the nonstandard, float-float and
/// cap/floor engines, <c>Gaussian1dModel::zerobondOption</c> and the Markov-functional model alike), where the left
/// tail takes
/// <c>p[0]</c>, its boundary node. On the wheel, a 5Y into 10Y payer at 2% on a GSR model (a = 0.02, σ = 0.01) on a
/// flat 3% curve, on a grid of 3 standard deviations and 16 points a side, is worth 0.102661800916881; on a grid of 12
/// standard deviations and 256 points, where the tail's weight vanishes, 0.10415806152703168, and so on the nonstandard
/// engine; on the float-float engine, the same payer with its first leg fixed in all but name (a zero gearing on
/// Euribor 1Y), 0.10173470853181348 against 0.10416117493270043; a 5-year cap at 2% on Euribor 6M from the same start,
/// 0.060558824087763516 against 0.06245438129328943; a put struck at 0.8 on the 10-year bond from the same exercise, on
/// the model's <c>zerobondOption</c>, 0.07911771510819356 against 0.08048473087856346; on the harness, the
/// Markov-functional model's own premium of its first calibration point's call at 4.57%, calibrated to five CMS 10y
/// swaptions at 20% on 32 points a side, on 3 standard deviations 0.0004285947663144643 and on 7 0.0004751894691285253.
/// nanoql takes <c>p[n − 1]</c>. For a payoff that increases past the grid, the fixed tail lies between QuantLib's and
/// the true one, the one assertion the two values can't both pass; the nonstandard and cap/floor facts ask a tenth of
/// the way from QuantLib's at least, since QuantLib's own tail ties its value only within <c>erf</c>'s last bits
/// (G1R3). No QuantLib case sets the flag. QuantLib's check: the same engine on the widened grid. The Markov-functional
/// model's calibration reads the tail too (<c>markovfunctional.cpp:516-519</c>), seen alone in its puts, whose payoff
/// vanishes past the grid: on 2 standard deviations, QuantLib's puts on 32 points move as the grid refines, its
/// second-last node meeting the last, and nanoql's on 32 points lie closer to QuantLib's on 512 at each strike from
/// 2.3% to 15%, by up to fifteen times. QuantLib's check: its own section on the refined grid.
/// </summary>
public class FlatPayoffExtrapolationTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void TheSwaptionEnginesRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        Date exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);
        var swaption = new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 10, 0.02, SwapType.Payer, Context), new EuropeanExercise(exercise));
        double flat = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model, 16, 3.0, true, true).NPV!.Value;
        double widened = Gaussian1dSwaptionEngine.Calculate(swaption, Context, model, 256, 12.0, true, true).NPV!.Value;
        Assert.True(Math.Abs((widened / 0.10415806152703168) - 1.0) <= 1e-12, $"{widened}");
        Assert.True(0.102661800916881 < flat && flat < widened, $"{flat} against QuantLib's 0.102661800916881 and the widened grid's {widened}");
    }

    [Fact]
    public void TheNonstandardEnginesRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        Date exercise = new TARGET().Advance(Today, 5, TimeUnit.Years);
        var swaption = new NonstandardSwaption(
            new Swaption(Gaussian1dInputs.SwapOf(new Euribor6M(Curve), exercise, 10, 0.02, SwapType.Payer, Context), new EuropeanExercise(exercise)), Context);
        double flat = Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, Context, model, 16, 3.0, true, true).NPV!.Value;
        double widened = Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, Context, model, 256, 12.0, true, true).NPV!.Value;
        Assert.True(Math.Abs((widened / 0.10415806152703168) - 1.0) <= 1e-12, $"{widened}");

        // a tenth of the way from QuantLib's at least, which QuantLib's own tail ties within erf's last bits
        Assert.True(
            flat - 0.102661800916881 > 0.1 * (widened - 0.102661800916881) && flat < widened,
            $"{flat} against QuantLib's 0.102661800916881 and the widened grid's {widened}");
    }

    [Fact]
    public void TheFloatFloatEnginesRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        var calendar = new TARGET();
        Date exercise = calendar.Advance(Today, 5, TimeUnit.Years);
        Date start = calendar.Advance(exercise, 2, TimeUnit.Days);
        Schedule Schedule(int months) => new(
            start,
            calendar.Advance(start, 10, TimeUnit.Years),
            new Period(months, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            Context);
        Schedule yearly = Schedule(12), semiannual = Schedule(6);

        // the payer at 2%, its first leg fixed in all but name: a zero gearing on Euribor 1Y, 30/360
        var swap = new FloatFloatSwap(
            SwapType.Payer,
            Enumerable.Repeat(1.0, yearly.Size - 1),
            Enumerable.Repeat(1.0, semiannual.Size - 1),
            yearly,
            new Euribor1Y(Curve),
            new Thirty360(Thirty360Convention.BondBasis),
            semiannual,
            new Euribor6M(Curve),
            new Actual360(),
            Context,
            gearing1: new double[yearly.Size - 1],
            spread1: Enumerable.Repeat(0.02, yearly.Size - 1),
            paymentConvention1: BusinessDayConvention.Following,
            paymentConvention2: BusinessDayConvention.Following);
        var swaption = new FloatFloatSwaption(swap, new EuropeanExercise(exercise));
        double flat = Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, Context, model, 16, 3.0, true, true).NPV!.Value;
        double widened = Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, Context, model, 256, 12.0, true, true).NPV!.Value;

        // the wheel's 0.10416117493270043, to erf's last bits (G1R3)
        Assert.True(Math.Abs((widened / 0.10416117493270043) - 1.0) <= 1e-11, $"{widened}");

        // a tenth of the way from QuantLib's at least, which QuantLib's own tail ties within erf's last bits
        Assert.True(
            flat - 0.10173470853181348 > 0.1 * (widened - 0.10173470853181348) && flat < widened,
            $"{flat} against QuantLib's 0.10173470853181348 and the widened grid's {widened}");
    }

    [Fact]
    public void TheCapFloorEnginesRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        var calendar = new TARGET();
        Date start = calendar.Advance(calendar.Advance(Today, 5, TimeUnit.Years), 2, TimeUnit.Days);
        var schedule = new Schedule(
            start,
            start + new Period(5, TimeUnit.Years),
            new Period(6, TimeUnit.Months),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            Context);
        var cap = new Cap(new IborLeg(schedule, new Euribor6M(Curve)).WithNotionals(1.0).WithPaymentDayCounter(new Actual360()).ToLeg(Context), [0.02]);

        // calls, whose payoff increases with the state
        double flat = Gaussian1dCapFloorEngine.Calculate(cap, Context, model, 16, 3.0, true, true).NPV!.Value;
        double widened = Gaussian1dCapFloorEngine.Calculate(cap, Context, model, 256, 12.0, true, true).NPV!.Value;

        // QuantLib's to 6.0e-12, its bits on the C runtime's erf (G1R3)
        Assert.True(Math.Abs((widened / 0.06245438129328943) - 1.0) <= 1e-11, $"{widened}");

        // a tenth of the way from QuantLib's at least, which QuantLib's own tail ties within erf's last bits
        Assert.True(
            flat - 0.060558824087763516 > 0.1 * (widened - 0.060558824087763516) && flat < widened,
            $"{flat} against QuantLib's 0.060558824087763516 and the widened grid's {widened}");
    }

    [Fact]
    public void TheZerobondOptionsRightTailTakesTheLastNode()
    {
        Gsr model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);
        var calendar = new TARGET();
        Date expiry = calendar.Advance(Today, 5, TimeUnit.Years);
        Date valueDate = calendar.Advance(expiry, 2, TimeUnit.Days);
        Date maturity = valueDate + new Period(10, TimeUnit.Years);

        // a put on the bond, whose payoff increases with the state
        double flat = model.ZerobondOption(OptionType.Put, expiry, valueDate, maturity, 0.8, null, 0.0, null, 3.0, 16, true, true);
        double widened = model.ZerobondOption(OptionType.Put, expiry, valueDate, maturity, 0.8, null, 0.0, null, 12.0, 256, true, true);
        Assert.True(Math.Abs((widened / 0.08048473087856346) - 1.0) <= 1e-12, $"{widened}");
        Assert.True(0.07911771510819356 < flat && flat < widened, $"{flat} against QuantLib's 0.07911771510819356 and the widened grid's {widened}");
    }

    [Fact]
    public void TheMarkovFunctionalModelsRightTailTakesTheLastNode()
    {
        var today = new Date(14, Month.November, 2012);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(
            today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => new TARGET().Advance(today, i, TimeUnit.Years))];
        Period[] tenors = [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)];
        double Premium(double stdDevs)
        {
            var settings = new MarkovFunctionalModelSettings(
                32, stdDevs, 16, 1e-5, 1e-7, 0.0, 2.0,
                MarkovFunctionalModelSettingsAdjustments.KahaleSmile | MarkovFunctionalModelSettingsAdjustments.SmileExponentialExtrapolation
                    | MarkovFunctionalModelSettingsAdjustments.ExtrapolatePayoffFlat,
                [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0]);
            var model = new MarkovFunctional(
                curve, 0.01, [], [1.0], vol, expiries, tenors, new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context, settings);
            return model.ModelOutputs(context).ModelCallPremium[0][^3];
        }

        double narrow = Premium(3.0), wide = Premium(7.0);

        // QuantLib's on 3 standard deviations, below; the widened grid's, above.
        Assert.True(narrow > 0.0004285947663144643 && narrow < wide, $"{narrow}, {wide}");
    }

    [Fact]
    public void TheMarkovFunctionalCalibrationsRightTailTakesTheLastNode()
    {
        var today = new Date(14, Month.November, 2012);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.03, new Actual365Fixed());
        SwaptionVolatilityStructure vol = new ConstantSwaptionVolatility(
            today, new TARGET(), BusinessDayConvention.ModifiedFollowing, 0.20, new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => new TARGET().Advance(today, i, TimeUnit.Years))];
        Period[] tenors = [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)];
        var settings = new MarkovFunctionalModelSettings(
            32, 2.0, 16, 1e-5, 1e-7, 0.0, 2.0,
            MarkovFunctionalModelSettingsAdjustments.KahaleSmile | MarkovFunctionalModelSettingsAdjustments.SmileExponentialExtrapolation
                | MarkovFunctionalModelSettingsAdjustments.ExtrapolatePayoffFlat,
            [0.1, 0.25, 0.50, 0.75, 1.0, 1.25, 1.50, 2.0, 5.0]);
        var model = new MarkovFunctional(
            curve, 0.01, [], [1.0], vol, expiries, tenors, new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context, settings);
        ImmutableArray<double> puts = model.ModelOutputs(context).ModelPutPremium[0];

        // QuantLib's on 32 and 512 points a side, on the harness, at the strikes from 2.3% to 15%.
        double[] quantLib32 = [0.0010474974820009033, 0.019337673737027376, 0.0656798427107685, 0.1254774134150286, 0.25083895179032434, 1.0030081820420986];
        double[] quantLib512 = [0.0010478321465105629, 0.019339380918539158, 0.06568407872628357, 0.12537621907131305, 0.2507400138220497, 1.002922782326594];
        for (int i = 0; i < quantLib32.Length; i++)
        {
            double put = puts[i + 4];
            Assert.True(Math.Abs(put - quantLib512[i]) < Math.Abs(quantLib32[i] - quantLib512[i]), $"{i + 4}: {put}");
        }
    }
}
