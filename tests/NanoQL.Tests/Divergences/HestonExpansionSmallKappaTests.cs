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
/// The LPP2 and LPP3 expansions at small κt, from a small κ or a short term. QuantLib's coefficients
/// (<c>hestonexpansionengine.cpp:128-720</c>) are expansions in e^{κt} that cancel in double as κt falls: LPP3's z₃ is
/// −0.196 for 1.310 at κt = 5e-3 and 3.6e8 for 0.478 at a term of 52 minutes at κ 1.5, where its volatility three
/// standard deviations up is 142, and its price at κ = 1e-4 is 9.7% below the analytic engine's; LPP2's z₁ is −73.1 for
/// −0.189 at κt = 5e-7. nanoql evaluates each coefficient below its cut (LPP2's 0.5, 0.2 and 0.5, LPP3's 2, 1, 1 and
/// 1), where QuantLib's double first departs from the expression's exact value by more than 1e-12 relative, in
/// double-double, QuantLib's expression unchanged; below double-double's own floor (LPP2's 2e-6, 5e-7 and 2e-6, LPP3's
/// 1e-3, 5e-4, 5e-5 and 5e-3) in quad-double; below quad-double's floor (LPP2's 1e-12 each, LPP3's 5e-10, 2e-10, 1e-12
/// and 5e-8) by the cubic in κ through four quad-double values there; and in QuantLib's double, and so its bits, from
/// the cut up. The check: QuantLib's expressions in Boost's <c>cpp_bin_float</c> at 100 digits, and at 250 below the
/// double-double floors, QuantLib's implied volatilities at the cuts, QuantLib's analytic engine, and at short terms
/// QuantLib's Forde expansion.
/// </summary>
public class HestonExpansionSmallKappaTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void AtAndBelowTheCutLpp2sCoefficientsAreTheReferenceValues()
    {
        // set 1: theta 0.04, sigma 0.3, v0 0.04, rho -0.5 at t = 1; set 2: theta 0.09, sigma 0.5, v0 0.0625, rho 0.3 at
        // t = 0.5; the reference values at kappa t, QuantLib 1.44's double beside each above the double-double floor
        // (QuantLib's expressions in double), quad-double and the cubic below it
        AssertCoefficients(2, new[]
        {
            (0, 1, 0.2, 0.1809881274943440844515792), // QuantLib 0.18098812749433965
            (0, 1, 1e-2, 0.1787082114839202589443463), // QuantLib 0.17870821151716457
            (0, 1, 1e-4, 0.1785794311959705551822884), // QuantLib 0.17852769445171524
            (0, 1, 2e-6, 0.1785781511249783911857889), // QuantLib 0.20679695536588608
            (0, 2, 1e-4, 0.2318502954043967963445708), // QuantLib 0.23184510188343715
            (1, 1, 0.1, -0.1823553045675551593865533), // QuantLib -0.18235530456756041
            (1, 1, 1e-3, -0.1886070483946882049424065), // QuantLib -0.18860705319716553
            (1, 1, 1e-5, -0.1886712265642148310105944), // QuantLib -0.17775668488766924
            (1, 1, 5e-7, -0.188671842578129278119518), // QuantLib -73.071450058658755
            (1, 2, 1e-4, 0.1495236199276144791688094), // QuantLib 0.14952037674669735
            (2, 1, 0.2, 0.1543063321040105388697517), // QuantLib 0.1543063321041136
            (2, 1, 1e-2, 0.1746146289685062690283812), // QuantLib 0.17461462812048059
            (2, 1, 1e-4, 0.1757695317773242008757571), // QuantLib 0.17706501470835581
            (2, 1, 2e-6, 0.1757810156252109188445858), // QuantLib -0.52734863742135663
            (2, 2, 1e-4, 0.5166069282461632995799741), // QuantLib 0.51675625684654025
            (0, 1, 1e-8, 0.1785781251306250031310671),
            (0, 1, 1e-14, 0.1785781250000001342963796),
            (0, 2, 1e-9, 0.2318457031709251300613708),
            (1, 1, 1e-9, -0.1886718749351562410277572),
            (1, 1, 1e-14, -0.1886718749999993425731087),
            (1, 2, 1e-9, 0.1495312499236953070232179),
            (2, 1, 1e-8, 0.175781248828124986774238),
            (2, 1, 1e-14, 0.1757812499999988096258004),
            (2, 2, 1e-12, 0.5166666666660692444355641),
        });

        // QuantLib 1.44's LPP2 at kappa t 0.5 on set 1, each coefficient at or above its cut, at strikes 90, 100 and
        // 110 on a forward of 100 (the C++ harness's HestonExpansion::impliedVolatility)
        var expansion = new LPP2HestonExpansion(0.5, 0.04, 0.3, 0.04, -0.5, 1.0);
        Assert.Equal(0.20224835058738697, expansion.ImpliedVolatility(90.0, 100.0));
        Assert.Equal(0.18395521876579807, expansion.ImpliedVolatility(100.0, 100.0));
        Assert.Equal(0.16986038214175905, expansion.ImpliedVolatility(110.0, 100.0));
    }

    [Fact]
    public void AtAndBelowTheCutLpp3sCoefficientsAreTheReferenceValues()
    {
        // the sets and the probe as above
        AssertCoefficients(3, new[]
        {
            (0, 1, 1.0, 0.1878138871597257536800651), // QuantLib 0.18781388715972577
            (0, 1, 0.1, 0.1801142587327016087783963), // QuantLib 0.18011425873285869
            (0, 1, 1e-2, 0.1790010294466902065648942), // QuantLib 0.17900103336035139
            (0, 1, 1e-3, 0.178885058718429299992189), // QuantLib 0.17895110574375761
            (0, 2, 1e-2, 0.2321171156408159432894121), // QuantLib 0.23211711289744932
            (1, 1, 0.5, -0.1776354578522521786183059), // QuantLib -0.17763545785225304
            (1, 1, 0.1, -0.2184059696782549678223171), // QuantLib -0.21840596967364698
            (1, 1, 5e-3, -0.230910624831138878639722), // QuantLib -0.23090567055293884
            (1, 1, 5e-4, -0.2315366853447988043438267), // QuantLib -0.17870617938187813
            (1, 2, 1e-2, 0.1829547349405495640937791), // QuantLib 0.18295465263621266
            (2, 1, 0.5, 0.1289234973314947876464425), // QuantLib 0.12892349733149763
            (2, 1, 1e-2, 0.1757023246755624105088419), // QuantLib 0.17570230845391574
            (2, 1, 5e-4, 0.1768207530508155260613382), // QuantLib 0.17462466493923531
            (2, 1, 5e-5, 0.1768739686389287872536754), // QuantLib 6.6248970470641284
            (2, 2, 1e-2, 0.5065640269653176296365597), // QuantLib 0.50656403978368547
            (3, 1, 0.5, 0.7428703387921793675051381), // QuantLib 0.74287033879245823
            (3, 1, 0.1, 1.170535750041257731028659), // QuantLib 1.1705358004612336
            (3, 1, 1e-2, 1.302647639879290119735641), // QuantLib 1.1850265408489618
            (3, 1, 5e-3, 1.31047643213704622593846), // QuantLib -0.19588304444619078
            (3, 2, 1e-2, -1.678397512193060553389427), // QuantLib -1.386567928923339
            (0, 1, 1e-4, 0.1788734135855076253102405),
            (0, 1, 1e-8, 0.178872119270074905438177),
            (0, 1, 1e-10, 0.1788721191419195026592369),
            (0, 2, 1e-6, 0.2316593859144432279327624),
            (1, 1, 1e-5, -0.2316050497247257515885069),
            (1, 1, 1e-10, -0.2316064452985440278746534),
            (1, 2, 1e-9, 0.1845800779609102305112533),
            (2, 1, 1e-6, 0.176879764526420397584558),
            (2, 1, 1e-14, 0.1768798828124987984831947),
            (2, 2, 1e-9, 0.5123854160797858657591185),
            (3, 1, 1e-4, 1.318201182787529976812906),
            (3, 1, 1e-6, 1.318357792969841093061349),
            (3, 1, 1e-8, 1.318359359179687394153899),
            (3, 1, 1e-12, 1.318359374998417753773039),
            (3, 2, 1e-6, -1.711996597588259055338061),
            (3, 2, 1e-9, -1.711999996597583962218656),
        });

        // QuantLib 1.44's LPP3 at kappa t 2 on set 1, each coefficient at or above its cut (the C++ harness's
        // HestonExpansion::impliedVolatility)
        var expansion = new LPP3HestonExpansion(2.0, 0.04, 0.3, 0.04, -0.5, 1.0);
        Assert.Equal(0.2037715936007213, expansion.ImpliedVolatility(90.0, 100.0));
        Assert.Equal(0.19220590976755317, expansion.ImpliedVolatility(100.0, 100.0));
        Assert.Equal(0.1828404641621433, expansion.ImpliedVolatility(110.0, 100.0));
    }

    [Fact]
    public void AtTheCutTheCoefficientsAreContinuous()
    {
        // each coefficient at its cut, in double, against the next kappa down, in double-double, on both sets and two
        // terms
        foreach ((int order, int i, double cut) in new[] { (2, 0, 0.5), (2, 1, 0.2), (2, 2, 0.5), (3, 0, 2.0), (3, 1, 1.0), (3, 2, 1.0), (3, 3, 1.0) })
        {
            foreach (int set in new[] { 1, 2 })
            {
                foreach (double t in new[] { 0.5, 2.0 })
                {
                    double atTheCut = Coefficient(order, set, cut / t, t, i);
                    double below = Coefficient(order, set, Math.BitDecrement(cut / t), t, i);
                    Assert.True(
                        Math.Abs((below / atTheCut) - 1.0) <= 1e-12, $"LPP{order} z{i}, set {set}, t {t}: {below} against {atTheCut}");
                }
            }
        }
    }

    [Fact]
    public void AtASmallKappaThePricesMatchTheAnalyticEngine()
    {
        // a year's call at 100 on set 1, r = q = 3%: QuantLib 1.44's LPP3 prices 6.920815491486161, 6.918890259239631
        // and 6.231209669919503 at kappa 1e-2, 1e-3 and 1e-4, and its LPP2 6.909524082935676, 6.905010408240162 and
        // 6.902563149041442 (the C++ harness's HestonExpansionEngine::results). Both expansions here within their order
        // of accuracy at kappa 1, where LPP2 is 2.9e-3 and LPP3 1.9e-3 below the analytic engine
        var context = new EvaluationContext { EvaluationDate = Today };
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));
        foreach ((double kappa, double analytic) in new[]
                 {
                     (1e-2, 6.907740487801554), (1e-3, 6.90245360436016), (1e-4, 6.901921862817677), (1e-5, 6.9018686579504145),
                 })
        {
            // QuantLib 1.44's analytic engine at 192 points (QuantLib-Python 1.44)
            HestonModel model = Model(kappa);
            Assert.Equal(analytic, AnalyticHestonEngine.Calculate(call, context, model, 192).NPV!.Value, 1e-13);
            foreach (HestonExpansionEngineHestonExpansionFormula formula in new[]
                     {
                         HestonExpansionEngineHestonExpansionFormula.LPP2, HestonExpansionEngineHestonExpansionFormula.LPP3,
                     })
            {
                double price = HestonExpansionEngine.Calculate(call, context, model, formula).NPV!.Value;
                Assert.True(Math.Abs((price / analytic) - 1.0) < 3e-3, $"{formula} at kappa {kappa}: {price} against {analytic}");
            }
        }
    }

    [Fact]
    public void AtShortTermsTheCoefficientsAreTheReferenceValues()
    {
        // theta 0.04, sigma 0.3, v0 0.06, rho -0.5 at kappa 1.5 and 5, terms of 52 minutes, 8.8 hours and a day; the
        // 100-digit values and QuantLib 1.44's double as above
        foreach ((int order, int i, double kappa, double t, double digits) in new[]
                 {
                     (2, 0, 1.5, 1e-4, 0.2449440181578244417899917), // QuantLib 0.24494401916491904
                     (2, 1, 1.5, 1e-4, -0.1530887393567868520611619), // QuantLib -0.15308874229643632
                     (2, 2, 1.5, 1e-4, 0.09567721312557259042961927), // QuantLib 0.095509403056743075
                     (3, 2, 1.5, 1e-4, 0.09567727292173814383977608), // QuantLib 0.095536806115303782
                     (2, 0, 1.5, 1e-3, 0.2448994372665023144971754), // QuantLib 0.24489943727576235
                     (2, 1, 1.5, 1e-3, -0.1530494101378446009994481), // QuantLib -0.1530494101356287
                     (2, 2, 1.5, 1e-3, 0.09562341584189526834677245), // QuantLib 0.095623261263646078
                     (3, 0, 1.5, 1e-3, 0.2448994374996250373852086), // QuantLib 0.24489943751071799
                     (3, 1, 1.5, 1e-3, -0.1530726907218561118895637), // QuantLib -0.1530726709614792
                     (3, 2, 1.5, 1e-3, 0.09562401327935631597143048), // QuantLib 0.095623740730154852
                     (2, 0, 1.5, 1.0 / 365, 0.2448133844435090666389454), // QuantLib 0.24481338444447823
                     (2, 1, 1.5, 1.0 / 365, -0.1529733698192199482246976), // QuantLib -0.1529733698202819
                     (2, 2, 1.5, 1.0 / 365, 0.09551953774213552417981114), // QuantLib 0.095519531892105797
                     (3, 0, 1.5, 1.0 / 365, 0.2448133861919786587772073), // QuantLib 0.24481338619270662
                     (3, 1, 1.5, 1.0 / 365, -0.1530369515044837804295213), // QuantLib -0.15303695054288
                     (3, 2, 1.5, 1.0 / 365, 0.09552117178816668102050216), // QuantLib 0.095521161534629001
                     (2, 0, 5.0, 1e-4, 0.2449368754845750789990564), // QuantLib 0.24493687547530291
                     (2, 1, 5.0, 1e-4, -0.1530783203464775187100904), // QuantLib -0.15307832054644643
                     (2, 2, 5.0, 1e-4, 0.09566326183061701553585676), // QuantLib 0.095664790116344101
                     (3, 1, 5.0, 1e-4, -0.1530806512155172628119592), // QuantLib -0.15307998548113783
                     (3, 2, 5.0, 1e-4, 0.09566332161318342217562649), // QuantLib 0.095660521899826767
                     (2, 0, 5.0, 1e-3, 0.2448281608160697474609681), // QuantLib 0.24482816081620601
                     (2, 1, 5.0, 1e-3, -0.1529452046036727210290812), // QuantLib -0.15294520460633224
                     (2, 2, 5.0, 1e-3, 0.09548412799952073160540884), // QuantLib 0.095484125677100393
                     (3, 0, 5.0, 1e-3, 0.2448281610489234382239782), // QuantLib 0.24482816104909264
                     (3, 1, 5.0, 1e-3, -0.1529683867931749159724008), // QuantLib -0.15296838620179495
                     (3, 2, 5.0, 1e-3, 0.09548472408226487097350717), // QuantLib 0.095484718071820462
                     (3, 3, 5.0, 1e-3, 0.4768827872371977369107666), // QuantLib 0.31473129226914864
                     (2, 0, 5.0, 1.0 / 365, 0.2446188998337886994043234), // QuantLib 0.24461889983380847
                     (2, 1, 5.0, 1.0 / 365, -0.1526877963346380851790294), // QuantLib -0.15268779633487284
                     (2, 2, 5.0, 1.0 / 365, 0.09513911579854461680301649), // QuantLib 0.095139115688287049
                     (3, 0, 5.0, 1.0 / 365, 0.2446189015767232501442052), // QuantLib 0.2446189015767459
                     (3, 1, 5.0, 1.0 / 365, -0.1527506432861654796205141), // QuantLib -0.15275064326075549
                     (3, 2, 5.0, 1.0 / 365, 0.09514073975026135977335952), // QuantLib 0.095140739518662018
                     (3, 3, 5.0, 1.0 / 365, 0.4742237830539604883011149), // QuantLib 0.47284893292396962
                     (3, 0, 1.5, 1e-4, 0.2449440181601566145772428), // QuantLib 0.24494401924488005
                     (3, 1, 1.5, 1e-4, -0.1530910712124150300183913), // QuantLib -0.15304805513960731
                     (3, 3, 1.5, 1e-4, 0.4783699194155183623636484), // QuantLib 357218683.5347718
                     (3, 3, 1.5, 1e-3, 0.4779556465310411623511991), // QuantLib -234.71153457004147
                     (3, 3, 1.5, 1.0 / 365, 0.4771555641930210931324193), // QuantLib 0.43011615967082462
                     (3, 0, 5.0, 1e-4, 0.2449368754869069828194651), // QuantLib 0.24493687547786214
                     (3, 3, 5.0, 1e-4, 0.4782624909863776129037944), // QuantLib -300358.0551613078
                 })
        {
            double coefficient = Coefficient(Expansion(order, kappa, 0.04, 0.3, 0.06, -0.5, t), i);
            Assert.True(
                Math.Abs((coefficient / digits) - 1.0) <= 1e-12, $"LPP{order} z{i} at kappa {kappa}, t {t}: {coefficient} against {digits}");
        }

        // the volatilities at three standard deviations either side of a forward of 100 against QuantLib's Forde
        // expansion (the C++ harness's HestonExpansion::impliedVolatility): LPP2 within 5e-6, the most QuantLib's own
        // LPP2 and Forde differ by here, and LPP3 within that and its cubic term at a term of 0, ρ(8ρ² − 5)σ³/(96
        // v₀^{5/2}) x³; QuantLib 1.44's LPP3 at kappa 1.5 is 1e-8 and 141.99439171164155 at 52 minutes,
        // 0.2514534314842224 and 0.23844871717720223 at 8.8 hours
        double z3AtZero = Math.Abs(-0.5 * ((8.0 * 0.25) - 5.0) * 0.027 / (96.0 * Math.Pow(0.06, 2.5)));
        foreach ((double kappa, double t, double[] forde) in new[]
                 {
                     (1.5, 1e-4, new[] { 0.24607416949252306, 0.24494401783877065, 0.24382419943096792 }),
                     (1.5, 1e-3, new[] { 0.24850813676332753, 0.24489940536881669, 0.24139395792181612 }),
                     (1.5, 1.0 / 365, new[] { 0.2508408795451156, 0.24481314512786534, 0.23906812336148484 }),
                     (5.0, 1e-4, new[] { 0.2460669475846207, 0.24493687324492408, 0.2438171306440673 }),
                     (5.0, 1e-3, new[] { 0.248434172536568, 0.24482793702925326, 0.24132483466882557 }),
                     (5.0, 1.0 / 365, new[] { 0.25063341894386504, 0.24461722264885266, 0.23888260577771547 }),
                 })
        {
            HestonExpansion lpp2 = Expansion(2, kappa, 0.04, 0.3, 0.06, -0.5, t);
            HestonExpansion lpp3 = Expansion(3, kappa, 0.04, 0.3, 0.06, -0.5, t);
            for (int j = -1; j <= 1; ++j)
            {
                double x = 3.0 * j * Math.Sqrt(0.06 * t);
                double strike = 100.0 * Math.Exp(x);
                double vol2 = lpp2.ImpliedVolatility(strike, 100.0);
                double vol3 = lpp3.ImpliedVolatility(strike, 100.0);
                Assert.True(Math.Abs(vol2 - forde[j + 1]) <= 5e-6, $"LPP2 at kappa {kappa}, t {t}, x {x}: {vol2} against {forde[j + 1]}");
                Assert.True(
                    Math.Abs(vol3 - forde[j + 1]) <= 5e-6 + (z3AtZero * Math.Abs(x * x * x)),
                    $"LPP3 at kappa {kappa}, t {t}, x {x}: {vol3} against {forde[j + 1]}");
            }
        }
    }

    // each (coefficient, set, kappa t, reference) against the coefficient on the set at kappa t / t
    private static void AssertCoefficients(int order, (int I, int Set, double Kt, double Digits)[] points)
    {
        foreach ((int i, int set, double kt, double digits) in points)
        {
            double t = set == 1 ? 1.0 : 0.5;
            double coefficient = Coefficient(order, set, kt / t, t, i);
            Assert.True(Math.Abs((coefficient / digits) - 1.0) <= 1e-12, $"LPP{order} z{i}, set {set}, kappa t {kt}: {coefficient} against {digits}");
        }
    }

    // set 1: theta 0.04, sigma 0.3, v0 0.04, rho -0.5; set 2: theta 0.09, sigma 0.5, v0 0.0625, rho 0.3
    private static double Coefficient(int order, int set, double kappa, double t, int i) =>
        Coefficient(set == 1 ? Expansion(order, kappa, 0.04, 0.3, 0.04, -0.5, t) : Expansion(order, kappa, 0.09, 0.5, 0.0625, 0.3, t), i);

    // the coefficients, internal to the expansions
    private static double Coefficient(HestonExpansion expansion, int i)
    {
        MethodInfo coefficient = expansion.GetType().GetMethod("Coefficient", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (double)coefficient.Invoke(expansion, [i])!;
    }

    private static HestonExpansion Expansion(int order, double kappa, double theta, double sigma, double v0, double rho, double t) =>
        order == 2 ? new LPP2HestonExpansion(kappa, theta, sigma, v0, rho, t) : new LPP3HestonExpansion(kappa, theta, sigma, v0, rho, t);

    // set 1, r = q = 3%, spot 100
    private static HestonModel Model(double kappa)
    {
        var rTS = new FlatForward(Today, 0.03, new Actual365Fixed());
        return new HestonModel(new HestonProcess(rTS, rTS, 100.0, 0.04, kappa, 0.04, 0.3, -0.5));
    }
}
