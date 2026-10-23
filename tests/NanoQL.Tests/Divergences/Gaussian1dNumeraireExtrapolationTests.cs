// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Models;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The numeraire's read of a given curve (the S-g1d plan's G1R6 (i)): GSR's numeraire at t = 0 reads a curve it is
/// given at the numeraire time without extrapolation (<c>gsr.cpp:216-220</c>), where it reads its own with it, so on a
/// 30-year curve QuantLib's <c>numeraire(0, 0, yts)</c> throws "time (60) is past max curve time (30.0192)", and so
/// does every Gaussian1d engine given that discount curve. nanoql reads it with extrapolation. QuantLib's check: its
/// own route, the model built on that curve, whose numeraire at 0 is 0.1319979111949979, and at (1.5, 0.3)
/// 0.1358278427215987, the value QuantLib's given-curve read gives there too. The Markov-functional model's numeraire
/// reads a given curve so too, at t = 0 and in the ratio past it (<c>markovfunctional.cpp:775-785</c>): on a 10-year
/// curve its numeraire time of 15 years throws "time (15.0192) is past max curve time (10.0055)"; QuantLib's check, the
/// same curve with its extrapolation enabled. In the ratio past t = 0 the model's own curve is read too, without
/// extrapolation, where the calibration reads it with (<c>:783-785</c>, <c>:797-799</c>, against <c>:467-469</c>): a
/// model on a 10-year curve calibrated to 1y to 5y into 10y swaptions, its numeraire time 15.02, refuses every read with
/// a given curve, "time (15.01917808219178) is past max curve time (10.005479452054795)"; nanoql reads its own curve
/// with extrapolation there. QuantLib's check: its own curve given, where the ratio is 1, the prices without one.
/// </summary>
public class Gaussian1dNumeraireExtrapolationTests
{
    [Fact]
    public void AGivenCurveIsReadWithExtrapolation()
    {
        var today = new Date(15, Month.January, 2025);
        YieldTermStructure flat = new FlatForward(today, 0.03, new Actual365Fixed());
        Date[] dates = [today, today + new Period(1, TimeUnit.Years), today + new Period(5, TimeUnit.Years), today + new Period(10, TimeUnit.Years), today + new Period(30, TimeUnit.Years)];
        YieldTermStructure curve = new InterpolatedZeroCurve<Numerics.Linear>(dates, [0.03, 0.03, 0.031, 0.032, 0.033], new Actual365Fixed());
        Gsr model = Gaussian1dInputs.Constant(flat, 0.01, 0.01);

        Assert.True(Math.Abs((model.Numeraire(0.0, 0.0, curve) / 0.1319979111949979) - 1.0) <= 1e-15, $"{model.Numeraire(0.0, 0.0, curve)}");
        Assert.True(Math.Abs((model.Numeraire(1.5, 0.3, curve) / 0.1358278427215987) - 1.0) <= 1e-14, $"{model.Numeraire(1.5, 0.3, curve)}");

        // QuantLib's own route: the model on the curve.
        Gsr onCurve = Gaussian1dInputs.Constant(curve, 0.01, 0.01);
        Assert.Equal(onCurve.Numeraire(0.0, 0.0), model.Numeraire(0.0, 0.0, curve));
    }

    [Fact]
    public void TheMarkovFunctionalModelReadsAGivenCurveWithExtrapolation()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var target = new TARGET();
        Date[] steps = [.. new[] { 1, 2, 3 }.Select(i => target.Advance(today, i, TimeUnit.Years))];
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => target.Advance(today, i, TimeUnit.Years))];
        var model = new MarkovFunctional(
            new FlatForward(today, 0.03, new Actual365Fixed()), 0.01, steps, [1.0, 1.0, 1.0, 1.0],
            new ConstantSwaptionVolatility(today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed()), expiries,
            [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)], new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context,
            new MarkovFunctionalModelSettings(32, 7.0, 16, 1e-5, 1e-7, 0.0, 2.0, MarkovFunctionalModelSettingsAdjustments.KahaleSmile));
        Date[] dates = [today, today + new Period(1, TimeUnit.Years), today + new Period(5, TimeUnit.Years), today + new Period(10, TimeUnit.Years)];
        YieldTermStructure curve = new InterpolatedZeroCurve<Numerics.Linear>(dates, [0.03, 0.03, 0.031, 0.032], new Actual365Fixed());

        // QuantLib's on the curve with its extrapolation enabled, on the wheel (G1R3's erf apart past t = 0).
        Assert.True(Math.Abs((model.Numeraire(0.0, 0.0, curve) / 0.6122337677897788) - 1.0) <= 1e-15, $"{model.Numeraire(0.0, 0.0, curve)}");
        Assert.True(Math.Abs((model.Numeraire(1.5, 0.3, curve) / 0.6324605737550852) - 1.0) <= Tolerance.Relative, $"{model.Numeraire(1.5, 0.3, curve)}");
    }

    [Fact]
    public void TheMarkovFunctionalModelReadsItsOwnCurveWithExtrapolationInTheRatio()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var target = new TARGET();
        Date[] dates = [today, today + new Period(1, TimeUnit.Years), today + new Period(5, TimeUnit.Years), today + new Period(10, TimeUnit.Years)];
        YieldTermStructure shortCurve = new InterpolatedZeroCurve<Numerics.Linear>(dates, [0.03, 0.03, 0.031, 0.032], new Actual365Fixed());
        Date[] expiries = [.. Enumerable.Range(1, 5).Select(i => target.Advance(today, i, TimeUnit.Years))];
        var model = new MarkovFunctional(
            shortCurve, 0.01, [], [1.0],
            new ConstantSwaptionVolatility(today, target, BusinessDayConvention.ModifiedFollowing, 0.2, new Actual365Fixed()), expiries,
            [.. Enumerable.Repeat(new Period(10, TimeUnit.Years), 5)], new EuriborSwapIsdaFixA(new Period(1, TimeUnit.Years)), context);
        Assert.True(model.NumeraireTime > 15.0);

        // QuantLib's without a given curve, on the wheel (G1R3's erf apart); with its own curve given, the same, the given
        // curve's own reads within its range.
        Assert.True(Math.Abs((model.Numeraire(1.5, 0.3) / 0.6320153637585068) - 1.0) <= Tolerance.Relative, $"{model.Numeraire(1.5, 0.3)}");
        Assert.True(Math.Abs((model.Zerobond(12.0, 1.5, 0.3) / 0.7032709143144397) - 1.0) <= Tolerance.Relative, $"{model.Zerobond(12.0, 1.5, 0.3)}");
        Assert.True(Math.Abs((model.Numeraire(1.5, 0.3, shortCurve) / model.Numeraire(1.5, 0.3)) - 1.0) <= 1e-15);
        Assert.True(Math.Abs((model.Zerobond(8.0, 1.5, 0.3, shortCurve) / model.Zerobond(8.0, 1.5, 0.3)) - 1.0) <= 1e-15);

        // A long curve given: priced, where QuantLib refuses.
        YieldTermStructure longCurve = new FlatForward(today, 0.03, new Actual365Fixed());
        Assert.True(double.IsFinite(model.Numeraire(1.5, 0.3, longCurve)) && double.IsFinite(model.Zerobond(12.0, 1.5, 0.3, longCurve)));
    }
}
