// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The COS Heston engine's cumulants at small κt (the S-hes plan's HR7 (ix), under R8, by HR6). QuantLib's c₁ to c₄
/// (<c>coshestonengine.cpp:177-286</c>) are expansions in e^{κt} that cancel in double as κt falls: c₄ is off by 2.7%
/// at κt = 5e-3, c₃ by 0.23% at 1e-3, c₂ by 600% at 1e-6 and c₁ by 8e-8 at 1e-9. nanoql evaluates each below its cut
/// (2e-3, 0.2, 0.5 and 1), where QuantLib's double first departs from the expression's exact value by more than 1e-12
/// relative, in double-double, QuantLib's expression unchanged; below double-double's own floor (1e-9, 1e-6, 1e-3 and
/// 5e-3) in quad-double; below quad-double's floor (1e-12, 1e-12, 5e-10 and 2e-7) by the cubic in κ through four
/// quad-double values there; and in QuantLib's double, and so its bits, from the cut up. The check: QuantLib's
/// expressions in Boost's <c>cpp_bin_float</c> at 100 digits, and at 250 below the double-double floors [executed at the
/// run], QuantLib's cumulants at the cuts, and QuantLib's analytic engine.
/// </summary>
public class CosHestonSmallKappaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void AtAndBelowTheCutTheCumulantsAreTheReferenceValues()
    {
        // set 1: theta 0.04, sigma 0.3, v0 0.04, rho -0.5 at t = 1; set 2: theta 0.09, sigma 0.5, v0 0.0625, rho 0.3 at
        // t = 0.5; the reference values at kappa t, QuantLib 1.44's double beside each above the double-double floor
        // [executed in the same probe, QuantLib's expressions in double], quad-double and the cubic below it
        foreach ((int i, (int set, double kt, double digits)[] points) in new[]
                 {
                     (1, new[]
                     {
                         (1, 1e-3, -0.02000000000000000041633363), // QuantLib -0.019999999999999865
                         (1, 1e-5, -0.02000000000000000041633363), // QuantLib -0.020000000000007994
                         (1, 1e-7, -0.02000000000000000041633363), // QuantLib -0.020000000001752016
                         (1, 1e-9, -0.02000000000000000041633363), // QuantLib -0.020000001634807415
                         (2, 1e-5, -0.01562503437488541695312308), // QuantLib -0.015625034374964576
                         (2, 1e-11, -0.01562500000003437499999989),
                         (2, 1e-14, -0.015625000000000034375),
                     }),
                     (2, new[]
                     {
                         (1, 0.1, 0.04318096440121113995716461), // QuantLib 0.043180964401211164
                         (1, 1e-2, 0.04328778541269364886693332), // QuantLib 0.043287785412355881
                         (1, 1e-4, 0.04329987750354991327007294), // QuantLib 0.043300011751959612
                         (1, 1e-6, 0.04329999877500035576804814), // QuantLib -0.21684000081666557
                         (2, 1e-4, 0.03024158435239313104377233), // QuantLib 0.030241673789672362
                         (1, 1e-8, 0.04329999998775000080363556),
                         (1, 1e-14, 0.04329999999999998851813556),
                         (2, 1e-9, 0.03024088542365625004110734),
                     }),
                     (3, new[]
                     {
                         (1, 0.2, -0.01050929302424538985229089), // QuantLib -0.010509293024246644
                         (1, 1e-2, -0.01134618508730545979064527), // QuantLib -0.011346185522430918
                         (1, 1e-3, -0.01138839489212173753026836), // QuantLib -0.01141487888642715
                         (2, 1e-2, 0.002484488495303199121084398), // QuantLib 0.0024844886662972821
                         (1, 1e-5, -0.01139305293390175323229829),
                         (1, 1e-11, -0.01139309999995293347137928),
                         (2, 1e-12, 0.002484741210937476756963017),
                     }),
                     (4, new[]
                     {
                         (1, 0.5, 0.00476020719522195469421616), // QuantLib 0.0047602071952224344
                         (1, 0.1, 0.006238422607567512455635792), // QuantLib 0.0062384226072258317
                         (1, 1e-2, 0.006648754233220652280305748), // QuantLib 0.0066488989143201425
                         (1, 5e-3, 0.006672538314571122427594925), // QuantLib 0.0068559065556168053
                         (2, 1e-2, 0.002034685785914975245761331), // QuantLib 0.0020338997195917274
                         (1, 1e-4, 0.006695951177386782352017312),
                         (1, 1e-6, 0.006696425282273834147376762),
                         (1, 1e-9, 0.006696430066639414118946987),
                         (2, 1e-6, 0.002046569539423122506209406),
                         (2, 1e-9, 0.002046570731286946366953312),
                     }),
                 })
        {
            foreach ((int set, double kt, double digits) in points)
            {
                double t = set == 1 ? 1.0 : 0.5;
                double cumulant = Cumulant(Engine(set, kt / t), i, t);
                Assert.True(Math.Abs((cumulant / digits) - 1.0) <= 1e-12, $"c{i}, set {set}, kappa t {kt}: {cumulant} against {digits}");
            }
        }

        // QuantLib 1.44's c1 to c4 at each one's cut on set 1 [executed through the harness's COSHestonEngine::members]
        Assert.Equal(-0.01999999999999883, Engine(1, 2e-3).C1(1.0));
        Assert.Equal(0.043068529814738486, Engine(1, 0.2).C2(1.0));
        Assert.Equal(-0.009372745273513682, Engine(1, 0.5).C3(1.0));
        Assert.Equal(0.003488006696694215, Engine(1, 1.0).C4(1.0));
    }

    [Fact]
    public void AtTheCutTheCumulantsAreContinuous()
    {
        // each cumulant at its cut, in double, against the next kappa down, in double-double, on both sets and two terms
        foreach ((int i, double cut) in new[] { (1, 2e-3), (2, 0.2), (3, 0.5), (4, 1.0) })
        {
            foreach (int set in new[] { 1, 2 })
            {
                foreach (double t in new[] { 0.5, 2.0 })
                {
                    double atTheCut = Cumulant(Engine(set, cut / t), i, t);
                    double below = Cumulant(Engine(set, Math.BitDecrement(cut / t)), i, t);
                    Assert.True(Math.Abs((below / atTheCut) - 1.0) <= 1e-12, $"c{i}, set {set}, t {t}: {below} against {atTheCut}");
                }
            }
        }
    }

    [Fact]
    public void AtASmallKappaThePriceMatchesTheAnalyticEngine()
    {
        // a year's call at 100 on set 1, r = q = 3%: QuantLib 1.44's COS engine prices 6.907740835233824,
        // 6.902453996086095, 6.901922259219731 and 6.901869044519003 [executed on the wheel], 5.7e-8 above its analytic
        // engine as nanoql's, which the cumulants move only through the truncation range
        var context = new EvaluationContext { EvaluationDate = Today };
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        foreach ((double kappa, double analytic) in new[]
                 {
                     (1e-2, 6.907740487801554), (1e-3, 6.90245360436016), (1e-4, 6.901921862817677), (1e-5, 6.9018686579504145),
                 })
        {
            // QuantLib 1.44's analytic engine at 192 points [executed on the wheel]
            HestonModel model = Model(1, kappa);
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, context, model, 192).NPV!.Value, 1e-13);
            double cos = COSHestonEngine.Calculate(call, context, model).NPV!.Value;
            Assert.True(Math.Abs((cos / analytic) - 1.0) < 1e-7, $"kappa {kappa}: {cos} against {analytic}");
        }
    }

    private static double Cumulant(COSHestonEngine engine, int i, double t) => i switch
    {
        1 => engine.C1(t),
        2 => engine.C2(t),
        3 => engine.C3(t),
        _ => engine.C4(t),
    };

    private static COSHestonEngine Engine(int set, double kappa) => new(Model(set, kappa));

    // set 1: v0 = theta 0.04, sigma 0.3, rho -0.5; set 2: v0 0.0625, theta 0.09, sigma 0.5, rho 0.3; r = q = 3%, spot 100
    private static HestonModel Model(int set, double kappa)
    {
        var rTS = new FlatForward(Today, 0.03, new Actual365Fixed());
        return set == 1
            ? new HestonModel(new HestonProcess(rTS, rTS, 100.0, 0.04, kappa, 0.04, 0.3, -0.5))
            : new HestonModel(new HestonProcess(rTS, rTS, 100.0, 0.0625, kappa, 0.09, 0.5, 0.3));
    }
}
