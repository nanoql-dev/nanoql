// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The COS Heston engine's characteristic function at small σ. QuantLib's COS engine computes the classic form, divided
/// by σ² (<c>coshestonengine.cpp:132-150</c>), which cancels as σ falls: its at-the-money price is off by 2.9e-11
/// relative at σ = 1e-3, 3.7e-5 at 1e-6 and 3.4% at 1e-8, where the analytic engine's Andersen and Lake form,
/// QuantLib's own, holds. nanoql's COS engine takes the analytic engine's form of the same function below σ = 0.07, the
/// cut where the classic form first departs from it by more than 1e-12 relative, measured on a grid of six parameter
/// sets, ten u and nine times to 30 years; above it QuantLib's form and bits. The check: QuantLib's analytic engine,
/// and the two forms within 1e-12 at the cut.
/// </summary>
public class CosHestonSmallSigmaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void BelowTheCutCosMatchesTheAnalyticEngine()
    {
        // QuantLib 1.44's COS engine prices 8.827318003627013, 8.82732259535544, 8.827645320471433 and 9.13180644107807
        // at sigma 1e-3, 1e-5, 1e-6 and 1e-8 (QuantLib-Python 1.44)
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        foreach ((double sigma, double analytic) in new[]
                 {
                     (1e-3, 8.82731800337272), (1e-5, 8.827321225029902), (1e-6, 8.827321225348905), (1e-8, 8.827321225352128),
                 })
        {
            var model = new HestonModel(new HestonProcess(
                new FlatForward(Today, 0.03, new Actual365Fixed()), new FlatForward(Today, 0.01, new Actual365Fixed()), 100.0, 0.04, 1.5, 0.04, sigma, -0.6));
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, Context, model, 192).NPV!.Value, 1e-13);
            double cos = COSHestonEngine.Calculate(call, Context, model).NPV!.Value;
            Assert.True(Math.Abs((cos / analytic) - 1.0) < 1e-10, $"sigma {sigma}: {cos} against {analytic}");
        }
    }

    [Fact]
    public void AtTheCutTheTwoFormsAgree()
    {
        // the classic form at sigma = 0.07, the cut, against the analytic engine's form on the measurement's worst set
        var rTS = new FlatForward(Today, 0.03, new Actual365Fixed());
        var model = new HestonModel(new HestonProcess(rTS, rTS, 100.0, 1.0, 1.0, 1.0, 0.07, -0.3));
        var cos = new COSHestonEngine(model);
        var analytic = new AnalyticHestonEngine(model);
        foreach (double u in new[] { 0.05, 0.25, 1.0, 3.0, 10.0 })
        {
            foreach (double t in new[] { 0.05, 1.0, 10.0, 30.0 })
            {
                Complex a = analytic.ChF(new Complex(u, 0.0), t);
                if (Complex.Abs(a) < 1e-200)
                {
                    continue;
                }

                double relative = Complex.Abs(cos.ChF(u, t) - a) / Complex.Abs(a);
                Assert.True(relative <= 1e-12, $"u {u}, t {t}: {relative}");
            }
        }
    }
}
