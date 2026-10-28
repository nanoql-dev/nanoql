// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The integrals' conventions (the S-core plan's C4): exp-sinh's refusals, Boost's domain errors (CD13); Filon's
/// refusal of 0 intervals (CR7 (xxviii)) and QuantLib's of an odd number; the moment-based polynomial's memo, a
/// 12-node rule asking 24 moments in 27 calls, QuantLib's count (<c>probes\c4_memo.cpp</c>, <c>c4_memo.out</c>), where
/// the recursion alone asks 511,659,097 times (the plan's CP20), and one polynomial serving two threads, each reading
/// QuantLib's nodes (CR5 (d)); and the trigonometric polynomials' moments
/// by the loop, QuantLib's bits (round 1's O1-8). And §2.6's two routes: exp-sinh against tanh-sinh after a
/// substitution; Filon against Gauss-Kronrod; the two-dimensional integral against iterated one-dimensional rules.
/// QuantLib's bits are its own (this run's <c>probes\c4_facts.cpp</c>, <c>c4_facts.out</c>).
/// </summary>
public class IntegralConventionTests
{
    // QuantLib's GaussLaguerreCosinePolynomial<Real>(0.2)'s moments 0 to 31, bit for bit
    private static readonly ulong[] CosineMoments =
    [
        0x3FF0000000000000UL, 0x3FEECB19DECB19DFUL, 0x3FFD13839BD86FEAUL, 0x401433612C458E9DUL, 0x40325983335FB02CUL,
        0x40546B15CBA41522UL, 0x407AB2F0859C389AUL, 0x40A3EE10B616ACB0UL, 0x40D0A2C2CE7B5ABAUL, 0x40FE99EA3BE33DD5UL,
        0x412EBE53A99A3CDAUL, 0x4160DA4FE4320E0AUL, 0x41945E4648DE3C7FUL, 0x41CBCA79EC68B3AFUL, 0x420619ADB69D159FUL,
        0x4244E8AA242B070EUL, 0x428745AAA66C2A53UL, 0x42CD5C8F7D586FE5UL, 0x43142BE7D95A32B7UL, 0x435D532F7C8C1F56UL,
        0x43A62CD662FC7A3DUL, 0x43F14A06F599265AUL, 0x443BABDA14CB9A0EUL, 0x4486AD4AF900D22DUL, 0x44D3022030B09C88UL,
        0x452048FC0AFB2879UL, 0x456C814BD5925B72UL, 0x45B978F438A6ECF5UL, 0x46073B0B8149FD87UL, 0x46559C748DDAD1BFUL,
        0x46A47F56DB52C00FUL, 0x46F3CF8AD443B22EUL,
    ];

    // QuantLib's GaussLaguerreSinePolynomial<Real>(0.2)'s moments 0 to 31, bit for bit
    private static readonly ulong[] SineMoments =
    [
        0x3FF0000000000000UL, 0x3FF261D388935FECUL, 0x40047B555DAC3F22UL, 0x4020ABF8E36C0245UL, 0x4041AD7C4E250A9BUL,
        0x4066F0AAF103EDA2UL, 0x409183A20798E168UL, 0x40BEA04F6FCB2466UL, 0x40EE0F869BAC3C67UL, 0x41204F62DF9A6F77UL,
        0x4153547F0863ABFAUL, 0x4188C5B87117E6D1UL, 0x41C105BCA0E6A0BDUL, 0x41F8E9CA3FB40EA9UL, 0x42334DA2A0CCAE8BUL,
        0x426F879145C06D6DUL, 0x42AB0F55C9C03DB5UL, 0x42E862262D99D3DBUL, 0x43271E7754CAC90CUL, 0x436737741641AF24UL,
        0x43A8FD74EE489B64UL, 0x43ED4F632AF9645CUL, 0x443308D912135F47UL, 0x447B97B0B7E04A91UL, 0x44C62F072D57E24AUL,
        0x45137052B213CC21UL, 0x45622DA837D45557UL, 0x45B1D143F36573B2UL, 0x460211DF66C666C4UL, 0x4652CE7CAE47D502UL,
        0x46A3FCF035286959UL, 0x46F5A1A5BDBD91D4UL,
    ];

    // QuantLib's 16-node rule on the cosine polynomial: its nodes, bit for bit
    private static readonly ulong[] CosineNodes =
    [
        0x404BBE5C96942733UL, 0x4043F2D9497F961CUL, 0x404104024244C80DUL, 0x403CEC430BA46142UL, 0x403866FFA45E3BFBUL,
        0x403456A9BD14C0FBUL, 0x402DBAD87CB643B2UL, 0x40268E42C1E6AF6CUL, 0x40218256A27D1369UL, 0x401A674F9D503A84UL,
        0x401315C4CB36A051UL, 0x4009FC24F5FB7A77UL, 0x40002D46CECD494BUL, 0x3FF167DB797BFD84UL, 0x3FDC4F7A0545228BUL,
        0x3FB57C45097144B2UL,
    ];

    // and its weights, bit for bit
    private static readonly ulong[] CosineWeights =
    [
        0x40246ADDB6428FEBUL, 0x40195169FA4510F8UL, 0x4015CE66379D7ABEUL, 0x40132B2C3FAAA37EUL, 0x4011136AB6EA056CUL,
        0x400F28DD56AD36F1UL, 0x402C518D67D4A360UL, 0x4005FCC177ED3A43UL, 0x40029B89F54289E2UL, 0x3FFFCD4D128B8C08UL,
        0x3FFACA33BE0BFE68UL, 0x3FF5F9887993C464UL, 0x3FF1458216CB351FUL, 0x3FE943C0F26496C9UL, 0x3FE00E4D6AAE058EUL,
        0x3FCB923C8AA651FDUL,
    ];

    [Fact]
    public void ExpSinhRefusesWhatBoostRefuses()
    {
        var integrator = new ExpSinhIntegral();
        ArgumentOutOfRangeException finite = Assert.Throws<ArgumentOutOfRangeException>(
            () => integrator.Integrate(x => x, 0.0, 1.0));
        Assert.Contains("Use tanh_sinh quadrature for integration over finite domains", finite.Message, StringComparison.Ordinal);
        ArgumentOutOfRangeException whole = Assert.Throws<ArgumentOutOfRangeException>(
            () => integrator.Integrate(x => Math.Exp(-x * x), double.NegativeInfinity, double.PositiveInfinity));
        Assert.Contains("Use sinh_sinh quadrature", whole.Message, StringComparison.Ordinal);
        Assert.Throws<ArgumentOutOfRangeException>(() => integrator.Integrate(x => x, double.NaN, double.PositiveInfinity));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpSinhIntegral(2.0).Integrate(x => Math.Exp(-x)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ExpSinhIntegral(1e-8, -1));
    }

    [Fact]
    public void ExpSinhMeetsTanhSinhAfterASubstitution()
    {
        // ∫₀^∞ f(x) dx = ∫₀¹ f(u/(1 − u))/(1 − u)² du, by tanh-sinh on [0, 1]
        Func<double, double>[] integrands = [x => Math.Exp(-0.5 * x * x), x => x * Math.Exp(-x), x => 1.0 / (1.0 + (x * x))];
        foreach (Func<double, double> f in integrands)
        {
            double expSinh = new ExpSinhIntegral(1e-12).Integrate(f);
            double tanhSinh = new TanhSinhIntegral(1e-12).Integrate(
                u => u < 1.0 ? f(u / (1.0 - u)) / ((1.0 - u) * (1.0 - u)) : 0.0, 0.0, 1.0);
            Assert.True(Math.Abs(expSinh - tanhSinh) < 1e-12 * Math.Abs(tanhSinh), $"{expSinh:R} against {tanhSinh:R}");
        }
    }

    [Fact]
    public void FilonRefusesZeroIntervalsAndAnOddNumber()
    {
        Assert.Equal(
            "intervals", Assert.Throws<ArgumentOutOfRangeException>(() => new FilonIntegral(FilonIntegralType.Sine, 1.0, 0)).ParamName);
        ArgumentException odd = Assert.Throws<ArgumentException>(() => new FilonIntegral(FilonIntegralType.Cosine, 1.0, 3));
        Assert.Equal("intervals", odd.ParamName);
        Assert.StartsWith("number of intervals must be even", odd.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FilonMeetsGaussKronrod()
    {
        // ∫₀^{2π} e^(−x/2) cos(100x) dx and its sine twin, QuantLib's test's integrands
        const double t = 100.0;
        var kronrod = new GaussKronrodAdaptive(1e-13, 100000000);
        double filonCosine = new FilonIntegral(FilonIntegralType.Cosine, t, 4096).Integrate(x => Math.Exp(-0.5 * x), 0.0, 2 * Math.PI);
        double kronrodCosine = kronrod.Integrate(x => Math.Exp(-0.5 * x) * Math.Cos(t * x), 0.0, 2 * Math.PI);
        Assert.True(Math.Abs(filonCosine - kronrodCosine) < 1e-12, $"{filonCosine:R} against {kronrodCosine:R}");
        double filonSine = new FilonIntegral(FilonIntegralType.Sine, t, 4096).Integrate(x => Math.Exp(-0.5 * x), 0.0, 2 * Math.PI);
        double kronrodSine = kronrod.Integrate(x => Math.Exp(-0.5 * x) * Math.Sin(t * x), 0.0, 2 * Math.PI);
        Assert.True(Math.Abs(filonSine - kronrodSine) < 1e-12, $"{filonSine:R} against {kronrodSine:R}");
    }

    [Fact]
    public void TheTwoDimensionalIntegralIsTheIteratedOne()
    {
        static double F(double x, double y) => Math.Exp(-x) * Math.Sin(x * y);
        var lobatto = new GaussLobattoIntegral(100000, 1e-13);
        double twoDimensional = new TwoDimensionalIntegral(lobatto, lobatto).Integrate(F, (0.0, 0.5), (2.0, 1.5));
        double iterated = lobatto.Integrate(x => lobatto.Integrate(y => F(x, y), 0.5, 1.5), 0.0, 2.0);
        Assert.Equal(iterated, twoDimensional);

        // against a fine Gauss-Legendre product rule, the nested adaptive rules' accuracy
        var legendre = new GaussLegendreIntegration(64);
        double product = 0.0;
        for (int i = 0; i < legendre.Order; ++i)
        {
            for (int j = 0; j < legendre.Order; ++j)
            {
                double x = 1.0 + legendre.X()[i], y = 1.0 + (0.5 * legendre.X()[j]);
                product += legendre.Weights()[i] * legendre.Weights()[j] * 0.5 * F(x, y);
            }
        }

        Assert.True(Math.Abs(twoDimensional - product) < 1e-11, $"{twoDimensional:R} against {product:R}");
    }

    [Fact]
    public void OnePolynomialServesTwoThreadsWithQuantLibsNodes()
    {
        var polynomial = new GaussLaguerreCosinePolynomial(0.2);
        var rules = new GaussianQuadrature[2];
        Parallel.For(0, 2, i => rules[i] = new GaussianQuadrature(16, polynomial));
        foreach (GaussianQuadrature rule in rules)
        {
            Assert.Equal(CosineNodes.Select(BitConverter.UInt64BitsToDouble), rule.X().ToArray());
            Assert.Equal(CosineWeights.Select(BitConverter.UInt64BitsToDouble), rule.Weights().ToArray());
        }
    }

    [Fact]
    public void TheMemoAsksEachMomentOnce()
    {
        var polynomial = new CountingLaguerrePolynomial();
        _ = new GaussianQuadrature(12, polynomial);
        Assert.Equal(27, polynomial.MomentCalls);
        Assert.Equal(24, polynomial.MomentsAsked.Count);
    }

    [Fact]
    public void TheTrigonometricMomentsAreQuantLibs()
    {
        var cosine = new GaussLaguerreCosinePolynomial(0.2);
        var sine = new GaussLaguerreSinePolynomial(0.2);
        for (int i = 0; i < CosineMoments.Length; ++i)
        {
            Assert.Equal(BitConverter.UInt64BitsToDouble(CosineMoments[i]), cosine.Moment(i));
            Assert.Equal(BitConverter.UInt64BitsToDouble(SineMoments[i]), sine.Moment(i));
        }
    }

    // Laguerre's weight e^(−x), its moments i!, counting the calls: those of moments asked before, and every call
    // past 1,000 refused, so a recursion without its memo fails here at once
    private sealed class CountingLaguerrePolynomial : MomentBasedGaussianPolynomial
    {
        public HashSet<int> MomentsAsked { get; } = [];

        public int MomentCalls { get; private set; }

        public int RepeatedCalls { get; private set; }

        public override double Moment(int i)
        {
            if (++MomentCalls > 1000)
            {
                throw new InvalidOperationException($"{MomentCalls} moment calls");
            }

            if (!MomentsAsked.Add(i))
            {
                ++RepeatedCalls;
            }

            double factorial = 1.0;
            for (int k = 2; k <= i; ++k)
            {
                factorial *= k;
            }

            return factorial;
        }

        public override double W(double x) => Math.Exp(-x);
    }
}
