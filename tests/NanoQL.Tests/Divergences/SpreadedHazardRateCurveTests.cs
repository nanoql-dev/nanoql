// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Conventions;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The spreaded curves' survival: exact, where QuantLib's integrates the hazard rate with its base's 48-point
/// Gauss-Chebyshev rule. On a flat hazard rate of 0.02 (Actual/365 Fixed) with an additive spread of 0.01, or a factor
/// of 0.5, QuantLib reads 0.97044033656246698, 0.8606849301062508 and 0.74077854891200179 at 1, 5 and 10 years, for the
/// exact e^(−0.03t); and on a stepped original (0.01, 0.01, 0.03, 0.06 backward flat on today and one, three and five
/// years out) spread by 0, 0.96078257879155837 and 0.87807505391690122 at two and four years, for the original's
/// 0.96078943915232318 and 0.8781676061138235. The survival is the exact integral of the hazard rate the curve reports,
/// so on originals whose rate isn't their jump-free survival's it is QuantLib's within the 48-point rule's error: a
/// linear survival curve (1, 0.98, 0.94, 0.90, 0.80 today and 1, 3, 5 and 10 years out) and a linear density curve
/// (0.02, 0.02, 0.025, 0.03, 0.03) with a jump of 0.95 at two years, whose rate is the density over the jumped
/// survival; and backward-flat affine curves (0.01, 0.01, 0.015, 0.02, 0.02) on Vasicek models, whose rate is the
/// deterministic part alone. A user's subclass that reports its own rate is integrated with QuantLib's rule, as
/// QuantLib integrates every original: on a structure deriving the one-factor affine survival structure with a
/// deterministic rate of 0.02 (Vasicek r0 = b = 0.02), the additive spread of 0.01 reads QuantLib's 0.8606849301062508
/// at five years. A piecewise curve of each kind spreads as the interpolated curve on its nodes.
/// </summary>
public class SpreadedHazardRateCurveTests
{
    private static readonly Date Today = new(31, August, 2006);

    [Theory]
    [InlineData(1.0, 0.97044033656246698)]
    [InlineData(5.0, 0.8606849301062508)]
    [InlineData(10.0, 0.74077854891200179)]
    public void TheSurvivalIsExact(double t, double quantLibs)
    {
        var flat = new FlatHazardRate(Today, 0.02, new Actual365Fixed());
        double exact = Math.Exp(-0.03 * t);
        Assert.Equal(exact, new SpreadedHazardRateCurve(flat, 0.01).SurvivalProbability(t), 1e-15);
        Assert.Equal(exact, new FactorSpreadedHazardRateCurve(flat, 0.5).SurvivalProbability(t), 1e-15);
        Assert.NotEqual(quantLibs, exact, 1e-6);
    }

    [Theory]
    [InlineData("survival", 1.0, 0.97024360579485036, 0.97014525536725615)]
    [InlineData("survival", 3.0, 0.91122963597533391, 0.90988916646391349)]
    [InlineData("survival", 5.0, 0.85324418915935407, 0.84954797589610609)]
    [InlineData("survival", 8.0, 0.77006478588170468, 0.76193081288324038)]
    [InlineData("density", 3.0, 0.90613944970011862, 0.90227577600811049)]
    [InlineData("density", 5.0, 0.83331795451589585, 0.81996253471684544)]
    [InlineData("density", 8.0, 0.72184923349571961, 0.69150351235479546)]
    [InlineData("affine", 1.0, 0.98019517382588572, 0.98510930183531764)]
    [InlineData("affine", 5.0, 0.87792498673097652, 0.88667408434878325)]
    [InlineData("affine", 8.0, 0.80231619789525388, 0.81029466820446649)]
    [InlineData("ordinary affine", 5.0, 0.87792498673097652, 0.88667408434878325)]
    [InlineData("ordinary affine", 8.0, 0.80231619789525388, 0.81029466820446649)]
    public void OnEveryKindOfOriginalTheSurvivalIsTheReportedRates(string kind, double t, double quantLibsAdditive, double quantLibsFactor)
    {
        DefaultProbabilityTermStructure original = Original(kind);
        var additive = new SpreadedHazardRateCurve(original, 0.01);
        var factor = new FactorSpreadedHazardRateCurve(original, 0.5);
        // the rule's error: 1e-5 on the smooth rates, 3.8e-4 on the affine curves' steps, which it integrates worst
        double band = kind.Contains("affine", StringComparison.Ordinal) ? 4e-4 : 2e-4;
        Assert.Equal(quantLibsAdditive, additive.SurvivalProbability(t, true), band * quantLibsAdditive);
        Assert.Equal(quantLibsFactor, factor.SurvivalProbability(t, true), band * quantLibsFactor);

        // on the affine curves, the deterministic steps' integral in closed form
        if (original is InterpolatedAffineHazardRateCurve<BackwardFlat> affine)
        {
            double integral = 0.0, from = 0.0;
            double[] times = [.. affine.Times()], rates = [.. affine.Data()];
            for (int i = 1; i < times.Length && from < t; i++)
            {
                double to = Math.Min(times[i], t);
                integral += rates[i] * (to - from);
                from = to;
            }

            integral += rates[^1] * Math.Max(t - from, 0.0);
            Assert.Equal(Math.Exp(-integral - (0.01 * t)), additive.SurvivalProbability(t, true), 1e-15);
            Assert.Equal(Math.Pow(Math.Exp(-integral), 1.5), factor.SurvivalProbability(t, true), 1e-15);
        }

        // each curve's hazard rate is its survival's -d log S/dt, a quarter-year off the nodes and the jump
        foreach (DefaultProbabilityTermStructure curve in new DefaultProbabilityTermStructure[] { additive, factor })
        {
            const double h = 1e-5;
            double u = t - 0.25;
            double slope = -(Math.Log(curve.SurvivalProbability(u + h, true)) - Math.Log(curve.SurvivalProbability(u - h, true))) / (2 * h);
            Assert.Equal(curve.HazardRate(u, true), slope, 1e-6);
        }
    }

    [Fact]
    public void AUsersSubclassIsIntegratedAsQuantLibDoes()
    {
        var derived = new DeterministicAffine(Today, new Vasicek(0.02, 0.1, 0.02, 0.01));
        var spreaded = new SpreadedHazardRateCurve(derived, 0.01);
        Assert.Equal(0.8606849301062508, spreaded.SurvivalProbability(5.0, true), 1e-15);

        // its hazard rate is its survival's -d log S/dt within the rule's error, as QuantLib's: 1.8e-4 relative on the
        // affine structure's constant, 3e-4 on the survival curve's rate, which has a kink at its node
        Date[] nodes = [Today, Today + new Period(1, TimeUnit.Years), Today + new Period(5, TimeUnit.Years)];
        var shifted = new ShiftedSurvival(nodes, [1.0, 0.98, 0.90]);
        foreach (DefaultProbabilityTermStructure curve in new DefaultProbabilityTermStructure[] { spreaded, new SpreadedHazardRateCurve(shifted, 0.01) })
        {
            const double h = 1e-5;
            double slope = -(Math.Log(curve.SurvivalProbability(2.6 + h, true)) - Math.Log(curve.SurvivalProbability(2.6 - h, true))) / (2 * h);
            Assert.Equal(curve.HazardRate(2.6, true), slope, 5e-4 * curve.HazardRate(2.6, true));
        }
    }

    [Fact]
    public void APiecewiseCurveSpreadsAsTheInterpolatedCurveOnItsNodes()
    {
        EvaluationContext context = CreditFixture.Context();
        Date reference = context.EvaluationDate;
        SpreadCdsHelper[] helpers = [Helper(1, 0.005, context), Helper(3, 0.0075, context), Helper(5, 0.01, context)];
        double[] jumps = [0.99];
        Date[] jumpDates = [reference + new Period(2, TimeUnit.Years)];

        var hazard = new PiecewiseDefaultCurve<HazardRate, BackwardFlat, IterativeBootstrap>(reference, helpers, new Actual365Fixed(), context);
        var density = new PiecewiseDefaultCurve<DefaultDensity, Linear, IterativeBootstrap>(
            reference, helpers, new Actual365Fixed(), context, jumps, jumpDates);
        var affine = new PiecewiseDefaultCurve<AffineHazardRate, BackwardFlat, IterativeBootstrap>(
            reference, helpers, new Actual365Fixed(), new Vasicek(0.015625, 0.1, -0.109375, 0.01), context);
        (DefaultProbabilityTermStructure Piecewise, DefaultProbabilityTermStructure Nodes)[] pairs =
        [
            (hazard, new InterpolatedHazardRateCurve<BackwardFlat>(hazard.Dates(), hazard.Data(), new Actual365Fixed())),
            (density, new InterpolatedDefaultDensityCurve<Linear>(density.Dates(), density.Data(), new Actual365Fixed(), null, jumps, jumpDates)),
            (affine, InterpolatedAffineHazardRateCurve<BackwardFlat>.FromNodes(affine)),
        ];
        foreach ((DefaultProbabilityTermStructure piecewise, DefaultProbabilityTermStructure onNodes) in pairs)
        {
            foreach (double t in new[] { 0.5, 2.5, 4.0, 7.0 })
            {
                Assert.Equal(
                    new SpreadedHazardRateCurve(onNodes, 0.01).SurvivalProbability(t, true),
                    new SpreadedHazardRateCurve(piecewise, 0.01).SurvivalProbability(t, true),
                    1e-15);
                Assert.Equal(
                    new FactorSpreadedHazardRateCurve(onNodes, 0.5).SurvivalProbability(t, true),
                    new FactorSpreadedHazardRateCurve(piecewise, 0.5).SurvivalProbability(t, true),
                    1e-15);
            }
        }
    }

    private static SpreadCdsHelper Helper(int years, double spread, EvaluationContext context) =>
        new(
            spread, new Period(years, TimeUnit.Years), 0, new WeekendsOnly(), Frequency.Quarterly, BusinessDayConvention.Following,
            DateGenerationRule.CDS, new Actual360(), 0.4, CreditFixture.Discount(), context);

    private static DefaultProbabilityTermStructure Original(string kind)
    {
        Date[] nodes =
        [
            Today, Today + new Period(1, TimeUnit.Years), Today + new Period(3, TimeUnit.Years), Today + new Period(5, TimeUnit.Years),
            Today + new Period(10, TimeUnit.Years),
        ];
        double[] jumps = [0.95];
        Date[] jumpDates = [Today + new Period(2, TimeUnit.Years)];
        double[] rates = [0.01, 0.01, 0.015, 0.02, 0.02];
        return kind switch
        {
            "survival" => new InterpolatedSurvivalProbabilityCurve<Linear>(nodes, [1, 0.98, 0.94, 0.90, 0.80], new Actual365Fixed(), null, jumps, jumpDates),
            "density" => new InterpolatedDefaultDensityCurve<Linear>(nodes, [0.02, 0.02, 0.025, 0.03, 0.03], new Actual365Fixed(), null, jumps, jumpDates),
            "affine" => new InterpolatedAffineHazardRateCurve<BackwardFlat>(nodes, rates, new Actual365Fixed(), new Vasicek(0.015625, 0.1, -0.109375, 0.01)),
            _ => new InterpolatedAffineHazardRateCurve<BackwardFlat>(nodes, rates, new Actual365Fixed(), new Vasicek(0.02, 0.1, 0.02, 0.01)),
        };
    }

    // A user's affine structure with a deterministic part of 0.02, as QuantLib's header invites.
    private sealed class DeterministicAffine(Date referenceDate, Vasicek model)
        : OneFactorAffineSurvivalStructure(model, referenceDate, new Actual365Fixed())
    {
        protected override double HazardRateImpl(double t) => 0.02;
    }

    // A user's survival curve reporting its base's rate plus 0.01.
    private sealed class ShiftedSurvival(Date[] dates, double[] survival)
        : InterpolatedSurvivalProbabilityCurve<Linear>(dates, survival, new Actual365Fixed())
    {
        protected override double HazardRateImpl(double t) => base.HazardRateImpl(t) + 0.01;
    }

    [Theory]
    [InlineData(2.0, 0.96078257879155837)]
    [InlineData(4.0, 0.87807505391690122)]
    public void ASpreadOfZeroIsTheOriginal(double t, double quantLibs)
    {
        Date[] nodes = [Today, Today + new Period(1, TimeUnit.Years), Today + new Period(3, TimeUnit.Years), Today + new Period(5, TimeUnit.Years)];
        var original = new InterpolatedHazardRateCurve<BackwardFlat>(nodes, [0.01, 0.01, 0.03, 0.06], new Actual365Fixed());

        // QuantLib's original: 0.96078943915232318 and 0.8781676061138235
        Assert.Equal(original.SurvivalProbability(t), new SpreadedHazardRateCurve(original, 0.0).SurvivalProbability(t));
        Assert.Equal(original.SurvivalProbability(t), new FactorSpreadedHazardRateCurve(original, 0.0).SurvivalProbability(t));
        Assert.NotEqual(quantLibs, original.SurvivalProbability(t), 1e-6);
    }
}
