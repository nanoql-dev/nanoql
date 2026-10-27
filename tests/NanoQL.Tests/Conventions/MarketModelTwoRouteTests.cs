// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Models;
using NanoQL.Numerics;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Two routes to one value in the market-model framework (the S-mm plan's §2.6, the planner's MP3 and MP14): the drifts
/// with and without factor reduction at full factors; the constant-maturity state and drifts spanning one forward
/// against the LIBOR ones; the three curve states on one curve; and the coterminal Jacobian against central bumps.
/// </summary>
public class MarketModelTwoRouteTests
{
    private static readonly double[] RateTimes = [0.5, 1.0, 1.5, 2.0, 2.5, 3.0];
    private static readonly double[] Forwards = [0.03, 0.033, 0.036, 0.034, 0.038];

    [Fact]
    public void PlainAndReducedDriftsAgreeAtFullFactors()
    {
        Matrix pseudo = Pseudo(5);
        foreach (int numeraire in new[] { 5, 2, 0 })
        {
            var c = new LMMDriftCalculator(pseudo, [0.0, 0.01, 0.0, 0.02, 0.0], [0.5, 0.5, 0.5, 0.5, 0.5], numeraire, 0);
            double[] plain = new double[5], reduced = new double[5];
            c.ComputePlain(Forwards, plain);
            c.ComputeReduced(Forwards, reduced);
            for (int i = 0; i < 5; ++i)
            {
                Assert.Equal(plain[i], reduced[i], 1e-16);
            }
        }
    }

    [Theory]
    [InlineData(5)]
    [InlineData(3)]
    public void EachCurveStateOverloadIsItsForwardsOverload(int factors)
    {
        // a state set on discount ratios, so its forwards are its own reads
        var cs = new LMMCurveState(RateTimes);
        var setOn = new LMMCurveState(RateTimes);
        setOn.SetOnForwardRates(Forwards);
        cs.SetOnDiscountRatios([.. Enumerable.Range(0, 6).Select(i => setOn.DiscountRatio(i, 0))]);
        double[] fwds = cs.ForwardRates().ToArray(), taus = [0.5, 0.5, 0.5, 0.5, 0.5];
        Matrix pseudo = Pseudo(factors);
        var lmm = new LMMDriftCalculator(pseudo, [0.0, 0.01, 0.0, 0.02, 0.0], taus, 2, 0);
        var normal = new LMMNormalDriftCalculator(pseudo, taus, 2, 0);
        var routes = new (Action<double[]> State, Action<double[]> Forwards)[]
        {
            (d => lmm.Compute(cs, d), d => lmm.Compute(fwds, d)),
            (d => lmm.ComputePlain(cs, d), d => lmm.ComputePlain(fwds, d)),
            (d => lmm.ComputeReduced(cs, d), d => lmm.ComputeReduced(fwds, d)),
            (d => normal.Compute(cs, d), d => normal.Compute(fwds, d)),
            (d => normal.ComputePlain(cs, d), d => normal.ComputePlain(fwds, d)),
            (d => normal.ComputeReduced(cs, d), d => normal.ComputeReduced(fwds, d)),
        };
        foreach ((Action<double[]> state, Action<double[]> forwards) in routes)
        {
            double[] a = new double[5], b = new double[5];
            state(a);
            forwards(b);
            Assert.Equal(b, a);
            Assert.Contains(a, x => x != 0.0);
        }
    }

    [Fact]
    public void ConstantMaturityOfOneForwardIsTheLiborModel()
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var cms = new CMSwapCurveState(RateTimes, 1);
        cms.SetOnCMSwapRates(Forwards);
        for (int i = 0; i <= 5; ++i)
        {
            Assert.Equal(lmm.DiscountRatio(i, 5), cms.DiscountRatio(i, 5), 1e-15);
        }

        Matrix pseudo = Pseudo(3);
        double[] taus = [0.5, 0.5, 0.5, 0.5, 0.5], d = new double[5], l = new double[5], m = new double[5];
        new LMMDriftCalculator(pseudo, d, taus, 5, 0).Compute(lmm, l);
        new CMSMMDriftCalculator(pseudo, d, taus, 5, 0, 1).Compute(cms, m);
        for (int i = 0; i < 5; ++i)
        {
            Assert.Equal(l[i], m[i], 1e-16);
        }
    }

    [Fact]
    public void TheThreeStatesOnOneCurveAgree()
    {
        var lmm = new LMMCurveState(RateTimes);
        lmm.SetOnForwardRates(Forwards);
        var cot = new CoterminalSwapCurveState(RateTimes);
        cot.SetOnCoterminalSwapRates(lmm.CoterminalSwapRates());
        var cms = new CMSwapCurveState(RateTimes, 3);
        cms.SetOnCMSwapRates(lmm.CmSwapRates(3));
        for (int i = 0; i <= 5; ++i)
        {
            double expected = lmm.DiscountRatio(i, 0);
            Assert.Equal(expected, cot.DiscountRatio(i, 0), 1e-15);
            Assert.Equal(expected, cms.DiscountRatio(i, 0), 1e-15);
        }
    }

    [Fact]
    public void TheCoterminalJacobianIsTheBumpedSwapRates()
    {
        var cs = new LMMCurveState(RateTimes);
        cs.SetOnForwardRates(Forwards);
        Matrix j = SwapForwardMappings.CoterminalSwapForwardJacobian(cs);
        const double h = 1e-6;
        for (int f = 0; f < 5; ++f)
        {
            double[] up = (double[])Forwards.Clone(), down = (double[])Forwards.Clone();
            up[f] += h;
            down[f] -= h;
            var u = new LMMCurveState(RateTimes);
            u.SetOnForwardRates(up);
            var w = new LMMCurveState(RateTimes);
            w.SetOnForwardRates(down);
            for (int i = 0; i < 5; ++i)
            {
                Assert.Equal((u.CoterminalSwapRate(i) - w.CoterminalSwapRate(i)) / (2 * h), j[i, f], 1e-8);
            }
        }
    }

    private static Matrix Pseudo(int factors)
    {
        var m = new Matrix(5, factors);
        for (int i = 0; i < 5; ++i)
        {
            for (int k = 0; k < factors; ++k)
            {
                m[i, k] = 0.05 + (0.01 * i) - (0.02 * k) + (0.003 * i * k);
            }
        }

        return m;
    }
}
