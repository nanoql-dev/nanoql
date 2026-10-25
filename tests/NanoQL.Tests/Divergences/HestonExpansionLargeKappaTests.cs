// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The LPP2 and LPP3 expansions past κt = 100 (the S-hes plan's HR11, under R8). QuantLib's coefficients
/// (<c>hestonexpansionengine.cpp:128-720</c>) take powers of e^{κt} whose products overflow: LPP3's z₀ is inf at
/// κt = 140, its z₁ NaN and its z₃ 0, LPP2's z₀ inf at κt = 200 and NaN from 300, so the engine prices the forward's
/// discounted intrinsic value under the volatility's 1e-8 floor, or throws. nanoql evaluates each coefficient past its
/// upper cut in an extended-range double, QuantLib's expression unchanged, and QuantLib's double, and so its bits, up
/// to it. The check: QuantLib's expressions in Boost's 50-digit <c>cpp_bin_float_50</c> [executed at the run],
/// QuantLib's analytic engine, and QuantLib's implied volatilities at κt ≤ 100.
/// </summary>
public class HestonExpansionLargeKappaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    [Fact]
    public void PastTheUpperCutTheCoefficientsAreTheFiftyDigitValues()
    {
        // QuantLib 1.44's double values [executed in the same probe, QuantLib's expressions in double]: at kappa t 140
        // LPP3's z0 to z3 are inf, NaN, 3.4940051897423107e-05 and 0; at 200 LPP2's z0 is inf; at 300 and 600 every
        // value below is NaN but LPP2's z1 and z2 at 300 (-0.0012365211458333332, 7.7861979166666756e-06)
        foreach ((double kappa, double t, int order, int i, double digits) in new[]
                 {
                     (2.0, 70.0, 3, 0, 0.1960484488789410277485996),
                     (2.0, 70.0, 3, 1, -0.00260259721859412000485247),
                     (2.0, 70.0, 3, 2, 3.49400518974231078117529e-05),
                     (2.0, 70.0, 3, 3, 1.88442738930891593934965e-06),
                     (2.0, 100.0, 2, 0, 0.1960940260742187521956595),
                     (5.0, 60.0, 2, 0, 0.1984583378125000021253724),
                     (5.0, 60.0, 3, 0, 0.1984598038829265646252031),
                     (5.0, 60.0, 3, 1, -0.001235878908789496469583131),
                     (5.0, 60.0, 3, 2, 7.727617233072915855580993e-06),
                     (5.0, 60.0, 3, 3, 1.935303023726851536273587e-07),
                     (10.0, 60.0, 2, 0, 0.1992395578320312521025712),
                     (10.0, 60.0, 2, 1, -0.0006216224153645833039450711),
                     (10.0, 60.0, 2, 2, 1.949853515624999794797629e-06),
                     (10.0, 60.0, 3, 0, 0.1992397417478069601103625),
                     (10.0, 60.0, 3, 1, -0.0006215419902313299944947049),
                     (10.0, 60.0, 3, 2, 1.942529704488118285422755e-06),
                     (10.0, 60.0, 3, 3, 2.430241947880497288900196e-08),
                 })
        {
            double coefficient = Coefficient(order, kappa, t, i);
            Assert.True(
                Math.Abs((coefficient / digits) - 1.0) <= 1e-12,
                $"LPP{order} z{i} at kappa {kappa}, t {t}: {coefficient} against {digits}");
        }
    }

    [Fact]
    public void PastTheUpperCutThePricesMatchTheAnalyticEngine()
    {
        // QuantLib 1.44's LPP3 prices 32.480028059335794, 26.991911698395533 and 21.503795337455266 at kappa 10 over
        // twenty years, and both expansions 23.81010194224134, 16.40191973542416 and 8.99373752860698 at kappa 40 over
        // ten, the forward's discounted intrinsic values; its LPP2 throws at kappa 10 ("negative value (-nan(ind)) for
        // inf stdDev") [executed through the harness's HestonExpansionEngine::results]. The expansions' errors against
        // the analytic engine here: LPP2 up to 6.6e-7 relative, LPP3 up to 7.9e-8
        foreach ((double kappa, int years, double strike, double analytic) in new[]
                 {
                     (10.0, 20, 90.0, 42.133664423046774), (10.0, 20, 100.0, 39.24666073592091), (10.0, 20, 110.0, 36.60264044923597),
                     (40.0, 10, 90.0, 33.5597981528422), (40.0, 10, 100.0, 29.607252376646898), (40.0, 10, 110.0, 26.120627368108316),
                 })
        {
            // QuantLib 1.44's analytic engine at 192 points [executed on the wheel]
            HestonModel model = Model(kappa);
            var call = new VanillaOption(
                new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + new Period(years, TimeUnit.Years)));
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, Context, model, 192).NPV!.Value, 1e-12);

            double lpp2 = HestonExpansionEngine.Calculate(call, Context, model, HestonExpansionEngineHestonExpansionFormula.LPP2).NPV!.Value;
            double lpp3 = HestonExpansionEngine.Calculate(call, Context, model, HestonExpansionEngineHestonExpansionFormula.LPP3).NPV!.Value;
            Assert.True(Math.Abs((lpp2 / analytic) - 1.0) < 1e-6, $"LPP2 at kappa {kappa}, strike {strike}: {lpp2} against {analytic}");
            Assert.True(Math.Abs((lpp3 / analytic) - 1.0) < 1e-7, $"LPP3 at kappa {kappa}, strike {strike}: {lpp3} against {analytic}");
        }
    }

    [Fact]
    public void UpToTheUpperCutTheImpliedVolatilitiesAreQuantLibs()
    {
        // QuantLib 1.44's LPP2HestonExpansion and LPP3HestonExpansion at strikes 90, 100 and 110 on a forward of 100
        // [executed through the harness's HestonExpansion::impliedVolatility]
        foreach ((double kappa, double t, double[] lpp2, double[] lpp3) in new[]
                 {
                     (2.0, 10.0, new[] { 0.19668150003959978, 0.1948194335903817, 0.19316667630409656 },
                         new[] { 0.19673834590745334, 0.1949120720707651, 0.1932909854041178 }),
                     (2.0, 50.0, new[] { 0.19632554609580097, 0.19594088046875002, 0.19559423902491507 },
                         new[] { 0.1963498094859147, 0.19596667428066405, 0.19562139112163804 }),
                     (1.0, 100.0, new[] { 0.19225776253896726, 0.1918803546875, 0.19154027870029308 },
                         new[] { 0.1923838177782855, 0.19200947313300784, 0.19167211683882118 }),
                 })
        {
            var expansion2 = new LPP2HestonExpansion(kappa, 0.04, 0.3, 0.04, -0.5, t);
            var expansion3 = new LPP3HestonExpansion(kappa, 0.04, 0.3, 0.04, -0.5, t);
            double[] strikes = [90.0, 100.0, 110.0];
            for (int j = 0; j < strikes.Length; ++j)
            {
                Assert.Equal(lpp2[j], expansion2.ImpliedVolatility(strikes[j], 100.0));
                Assert.Equal(lpp3[j], expansion3.ImpliedVolatility(strikes[j], 100.0));
            }
        }
    }

    // the coefficients, internal to the expansions, at theta 0.04, sigma 0.3, v0 0.04, rho -0.5
    private static double Coefficient(int order, double kappa, double t, int i)
    {
        object expansion = order == 2
            ? new LPP2HestonExpansion(kappa, 0.04, 0.3, 0.04, -0.5, t)
            : new LPP3HestonExpansion(kappa, 0.04, 0.3, 0.04, -0.5, t);
        MethodInfo coefficient = expansion.GetType().GetMethod("Coefficient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (double)coefficient.Invoke(expansion, [i])!;
    }

    // v0 = theta 0.04, sigma 0.3, rho -0.5, r 3%, q 1%, spot 100
    private static HestonModel Model(double kappa) =>
        new(new HestonProcess(
            new FlatForward(Today, 0.03, new Actual365Fixed()), new FlatForward(Today, 0.01, new Actual365Fixed()), 100.0, 0.04, kappa, 0.04, 0.3, -0.5));
}
