// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Student t density at large degrees of freedom and the cumulative function at large ν: QuantLib takes each gamma
/// function as the exponential of its logarithm, which overflows from n 342 (0 there, NaN from 343), and its
/// denominator's product overflows earlier at large x (0 at n 330, x 30); nanoql takes the gammas' ratio as Boost's
/// tgamma_delta_ratio and the power by log1p wherever a factor overflows, to n = 2³¹ − 1. QuantLib's cumulative
/// function takes the incomplete beta's complement at 1 − n/(n + x²), which loses the argument's digits as n grows
/// (6.4e-10 relative at 1e6, 4.6e-9 at 1e8); nanoql takes the complementary argument x²/(n + x²) directly where n/(n +
/// x²) exceeds ½. Below the centre QuantLib's ½ − ½·(1 − I) cancels as the probability falls (1.3e-6 relative at n 10,
/// x −30, and 0 for 3.1e-24 at n 30); nanoql takes ½·I directly. Both through Boost's incomplete beta, as QuantLib's
/// own loses digits in its log-gamma prefactor as n grows; at n 1 below the centre, ½·(1 − I), as Boost's complement
/// there rounds 1 − z. And the quantile at n 4 and 6, whose Boost forms cancel near ½ (±0 within 1e-9 of it), takes one
/// Newton step on the cumulative function there. The references are 50-digit evaluations of the definitions; QuantLib's
/// values beside them are its own.
/// </summary>
public class StudentDistributionTests
{
    private static void Near(double exact, double actual, double relative) =>
        Assert.True(Math.Abs(actual - exact) <= relative * Math.Abs(exact), $"{actual:R} for {exact:R}");

    [Fact]
    public void TheDensityAtLargeDegreesOfFreedomIsFinite()
    {
        Near(0.35169560888273665, new StudentDistribution(342).Evaluate(0.5), 1e-12);    // QuantLib 0
        Near(0.35169668609708477, new StudentDistribution(343).Evaluate(0.5), 1e-12);    // QuantLib NaN
        Near(0.351938830615914, new StudentDistribution(1000).Evaluate(0.5), 1e-12);     // QuantLib NaN
        Near(0.35206406153226333, new StudentDistribution(100000).Evaluate(0.5), 1e-12); // QuantLib NaN
        Near(1.0847812298624628e-95, new StudentDistribution(330).Evaluate(30.0), 1e-12); // QuantLib 0
        Near(0.35206520024084999, new StudentDistribution(1000000).Evaluate(0.5), 1e-12);
        Near(0.352065326637776, new StudentDistribution(1000000000).Evaluate(0.5), 1e-12);
        Near(1.0981244250153700e-195, new StudentDistribution(100000).Evaluate(30.0), 5e-13);
    }

    [Fact]
    public void TheDensityComputesAtTheLargestDegreesOfFreedom()
    {
        // n + 1 overflows an int at 2^31 - 1; QuantLib's n_ + 1 is undefined there
        Near(0.35206532670538239, new StudentDistribution(int.MaxValue).Evaluate(0.5), 1e-12);
        Near(0.0044318484439259809, new StudentDistribution(int.MaxValue).Evaluate(3.0), 1e-12);
    }

    [Fact]
    public void WhereQuantLibsFactorsAreFiniteItsDensityIsKept()
    {
        // QuantLib's own values, bit for bit
        Assert.Equal(0.35169343545651904, new StudentDistribution(340).Evaluate(0.5));
        Assert.Equal(0.35169452535451234, new StudentDistribution(341).Evaluate(0.5));
        Assert.Equal(1.2584163368974994e-51, new StudentDistribution(100).Evaluate(30.0));
    }

    [Fact]
    public void TheCumulativeFunctionKeepsItsDigitsAtLargeNu()
    {
        var c6 = new CumulativeStudentDistribution(1000000);
        Near(0.9331926408816036, c6.Evaluate(1.5), 1e-14);      // QuantLib 0.93319264023976833
        Near(0.06680735911839639, c6.Evaluate(-1.5), 1e-13);    // QuantLib 0.06680735976023161
        Near(0.5398278272539784, c6.Evaluate(0.1), 1e-14);      // QuantLib 0.53982782727296352
        var c8 = new CumulativeStudentDistribution(100000000);
        Near(0.9331927971526462, c8.Evaluate(1.5), 1e-14);      // QuantLib 0.9331928017458988
        Near(0.06680720284735377, c8.Evaluate(-1.5), 1e-13);    // QuantLib 0.066807198254101252
        Near(0.5398278371767985, c8.Evaluate(0.1), 1e-14);      // QuantLib 0.53982783922882382
        var c4 = new CumulativeStudentDistribution(10000);
        Near(0.9331770140881902, c4.Evaluate(1.5), 1e-14);      // QuantLib 0.93317701408907083
        Near(0.5398268349844626, c4.Evaluate(0.1), 1e-14);      // QuantLib 0.53982683498553341
    }

    [Fact]
    public void TheLowerTailKeepsItsDigits()
    {
        Near(1.980896171015662e-11, new CumulativeStudentDistribution(10).Evaluate(-30.0), 1e-13);      // QuantLib 1.9808987783420662e-11
        Near(3.125895815304444e-24, new CumulativeStudentDistribution(30).Evaluate(-30.0), 1e-13);      // QuantLib 0
        Near(4.190166279344146e-52, new CumulativeStudentDistribution(100).Evaluate(-30.0), 1e-13);     // QuantLib 0
        Near(6.010047116831719e-198, new CumulativeStudentDistribution(1000000).Evaluate(-30.0), 1e-11); // QuantLib 0
        Near(1.2250867067519001e-06, new CumulativeStudentDistribution(100).Evaluate(-5.0), 1e-13);     // QuantLib 1.225086706746481e-06
    }

    [Fact]
    public void AtOneDegreeOfFreedomBelowTheCentreTheCauchyDistributionKeepsItsDigits()
    {
        var c1 = new CumulativeStudentDistribution(1);
        Near(0.49999999681690114, c1.Evaluate(-1e-8), 5e-14);   // QuantLib 0.5
        Near(0.49999968169011382, c1.Evaluate(-1e-6), 5e-14);   // QuantLib 0.49999968167596515
        Near(0.35241638234956673, c1.Evaluate(-0.5), 5e-14);    // QuantLib 0.35241638234957939
        Assert.True(Math.Abs(c1.Evaluate(-1e-6) + c1.Evaluate(1e-6) - 1.0) < 1e-13);
    }

    [Fact]
    public void AtFourAndSixDegreesOfFreedomTheQuantileNearTheCentreIsItsCumulativeFunctionsInverse()
    {
        // within the quantile's conditioning, epsilon/(2|p - 1/2|), where QuantLib's value is +-0 within 1e-9 of 1/2
        (int N, double P, double Exact)[] cases =
        [
            (4, 0.5 - 1e-8, -2.6666666652630515e-8),    // QuantLib -4.2146848510894035e-08
            (4, 0.5 + 1e-9, 2.6666665912481828e-9),     // QuantLib 0
            (4, 0.5 - 1e-9, -2.6666667392779194e-9),    // QuantLib -0
            (4, 0.4999, -0.00026666667061725468),       // QuantLib -0.00026666667103737794
            (6, 0.5 - 1e-8, -2.6127890575934669e-8),    // QuantLib -2.6127890607201404e-08
            (6, 0.5 + 1e-9, 2.6127889850740022e-9),     // QuantLib 0
            (6, 0.5 - 1e-9, -2.6127891301129308e-9),    // QuantLib -0
            (6, 0.4999, -0.00026127890936507919),       // QuantLib -0.00026127890936506512
        ];
        foreach ((int n, double p, double exact) in cases)
        {
            Near(exact, new InverseCumulativeStudent(n).Evaluate(p), 2.2e-16 / (2.0 * Math.Abs(p - 0.5)));
        }
    }

    [Fact]
    public void AboveTheCentreQuantLibsFormulaIsKept()
    {
        // where n/(n + x²) is at most ½ and x is positive, QuantLib's values, bit for bit
        Assert.Equal(0.99999999998019096, new CumulativeStudentDistribution(10).Evaluate(30.0));
        Assert.Equal(1.0, new CumulativeStudentDistribution(30).Evaluate(30.0));
    }

    [Fact]
    public void AtSmallNuTheComplementaryArgumentAgreesWithQuantLib()
    {
        // QuantLib's value at small ν where its argument's complement loses little: the two routes within 1e-14
        Assert.True(Math.Abs(new CumulativeStudentDistribution(5).Evaluate(1.5) - 0.90304815987876741) < 1e-14);
        Assert.True(Math.Abs(new CumulativeStudentDistribution(5).Evaluate(0.1) - 0.53788492942266752) < 1e-14);
    }
}
