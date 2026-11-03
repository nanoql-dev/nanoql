// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2008 Lorella Fatone
// SPDX-FileCopyrightText: Copyright (C) 2008 Maria Cristina Recchioni
// SPDX-FileCopyrightText: Copyright (C) 2008 Francesco Zirilli
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/barrieroption/perturbativebarrieroptionengine.hpp ql/experimental/barrieroption/perturbativebarrieroptionengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The perturbative engine for up-and-out put options on time-dependent Black-Scholes markets (Fatone, Recchioni and
/// Zirilli): the barrier price on the market's averages, corrected to first or second order in the drift and
/// variance's departures from them.
/// </summary>
/// <remarks>
/// <para>
/// QuantLib's algorithm, its midpoint rules in the time variables (<c>npoint</c> outer and <c>npoint2</c> inner points)
/// and its local distribution helpers: Hart's normal (algorithm 5666 by Recchioni), and Genz's bivariate normal
/// (Drezner and Wesolowsky's method) and trivariate normal (Plackett's formula by adaptive Kronrod integration), as
/// QuantLib's port of Genz's TVPACK has them. QuantLib's warning: the engine was reported to fail tests on Mac OS X
/// 10.8.4.
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/PerturbativeBarrierEngineTests.cs</c>), each leaving QuantLib's bits where it
/// doesn't apply, order 0's among them. Three lines of QuantLib's TVPACK port are Genz's: Plackett's integrand squares
/// <c>BA − R·BB</c> where QuantLib 1.44 takes its square root (upstream's own fix after v1.44, #2935); its Student
/// branch belongs to the degrees-of-freedom test, where QuantLib's attaches it to the normal integrand's cut, so a
/// normal integrand past the cut returns 0, not QuantLib's 0.5 or NaN; and the bivariate normal's |ρ| ≥ 0.925 branch
/// takes √(1 − x²), where QuantLib's takes (1 − x²)². QuantLib's first 12-point weight, short of Genz's in the last
/// digits, stands. The second-order term's derivative helpers are their functions' derivatives in a, term by term from
/// the identity ∂Φ₂(h, k; ρ)/∂h = φ(h) Φ((k − ρh)/√(1 − ρ²)) their own terms apply: the F helper's factor at <c>tt −
/// s</c>, its function's, where QuantLib's reads <c>tt − p</c>; and the E helper's limits √(τ(p − s)/(2ps)) and √(τ(p −
/// s)/(2(τ − p)(τ − s))) with the sign of its derivative through the first limit, where QuantLib's are mistranscribed.
/// The rate, carry and variance integrals run on the engine's clock, <see cref="FdmBlackScholesOp.Clock"/>, each
/// structure read at the instants its times denote on its own clock; where every structure shares the risk-free curve's
/// times, QuantLib's.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class PerturbativeBarrierOptionEngine
{
    private const double PI = 3.14159265358979324;
    private const double HalfPi = PI / 2.0;
    private const double TwoPi = 2.0 * PI;
    private const double SqrtTwo = 1.4142135623730950488;
    private const double SqrtPi = 1.7724538509055160273;
    private const double SqrtTwoPi = SqrtTwo * SqrtPi;

    /// <summary>Prices an up-and-out put option.</summary>
    /// <param name="option">The option: up-and-out, no rebate, a plain-vanilla put.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="order">The approximation's order: 0, 1 or 2.</param>
    /// <param name="zeroGamma">Whether the approximation's γ is 0, where it is the market's average carry over half
    /// its average variance by default.</param>
    /// <param name="npoint">The number of points of the outer time integrals, positive.</param>
    /// <param name="npoint2">The number of points of the second-order term's inner time integral, positive.</param>
    /// <returns>The option's value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="order"/> is negative.</exception>
    /// <exception cref="ArgumentException">The order is above 2, or a number of points isn't positive.</exception>
    /// <exception cref="NanoQLException">
    /// The option isn't an up-and-out ("this engine only manages up-and-out options"), has a rebate, or isn't a
    /// plain-vanilla put.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        BarrierOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int order = 1,
        bool zeroGamma = false,
        int npoint = 1000,
        int npoint2 = 100)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(order);
        if (!(order <= 2))
        {
            throw new ArgumentException("order must be <= 2", nameof(order));
        }

        if (!(npoint > 0 && npoint2 > 0))
        {
            throw new ArgumentException("integration point counts must be positive", nameof(npoint));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process, order, zeroGamma, npoint, npoint2));
    }

    private static OneAssetOptionResults Body(
        BarrierOption option, GeneralizedBlackScholesProcess process, int order, bool zeroGamma, int npoint, int npoint2)
    {
        if (!(option.BarrierType == BarrierType.UpOut))
        {
            throw new NanoQLException("this engine only manages up-and-out options");
        }

        if (!(option.Rebate == 0.0))
        {
            throw new NanoQLException("this engine does not manage non-null rebates");
        }

        if (!(option.Payoff is PlainVanillaPayoff payoff && payoff.OptionType == OptionType.Put))
        {
            throw new NanoQLException("this engine only manages put options");
        }

        double stock = process.X0;
        double kprice = payoff.Strike;
        double hbarr = option.Barrier;

        // the engine's clock: each structure read at the instants its times denote on its own clock
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        var market = new Market(process, clock);

        double tauMin = 0.0;
        double tauMax = clock.TimeFromReference(option.Exercise.LastDate());

        int igm = zeroGamma ? 0 : 1;

        double value = BarrierUPD(kprice, stock, hbarr, tauMin, tauMax, order, igm, npoint, npoint2, market);
        return new OneAssetOptionResults { NPV = value };
    }

    private static double BarrierUPD(
        double kprice,
        double stock,
        double hbarr,
        double taumin,
        double taumax,
        int iord,
        int igm,
        int npoint,
        int npoint2,
        Market market)
    {
        double v0, v1, v1p, v2pp, gm;
        double e1, e2, e3, e4;
        double xstar, s0;
        double sigmat, disc, d1, d2, d3, d4;
        double et, tt, dt, p;
        double dsqpi;
        double caux, ccaux;
        double auxnew;
        double x, b, c;

        if (igm == 0)
        {
            gm = 0.0;
        }
        else if (igm == 1)
        {
            gm = market.IntegralAlpha(taumin, taumax) / (0.5 * market.IntegralVariance(taumin, taumax));
        }
        else
        {
            gm = 0.0;
        }

        // xstar=min(0,log(kprice/hbarr))
        xstar = Math.Log(kprice / hbarr);

        if (xstar > 0.0)
        {
            xstar = 0.0;
        }

        sigmat = market.IntegralVariance(taumin, taumax);
        disc = -market.IntegralRate(taumin, taumax);
        double oneMinusGamma = 1.0 - gm;
        double onePlusGamma = 1.0 + gm;
        double barrierFactor = (hbarr * Math.Exp(0.5 * onePlusGamma * xstar))
            - (kprice * Math.Exp(-0.5 * oneMinusGamma * xstar));

        // Change of variable
        s0 = stock / hbarr;

        // Compute the zero-th order term P_0
        x = Math.Log(s0);
        double invSqrtSigmat = 1.0 / Math.Sqrt(sigmat);
        double halfSigmat = 0.5 * sigmat;
        double minusGammaNumerator = xstar + (oneMinusGamma * halfSigmat);
        double plusGammaNumerator = xstar - (onePlusGamma * halfSigmat);
        double scaledLogS0 = x * invSqrtSigmat;
        double scaledMinusGammaNumerator = minusGammaNumerator * invSqrtSigmat;
        double scaledPlusGammaNumerator = plusGammaNumerator * invSqrtSigmat;
        d1 = scaledMinusGammaNumerator - scaledLogS0;
        d2 = scaledMinusGammaNumerator + scaledLogS0;
        d3 = scaledPlusGammaNumerator - scaledLogS0;
        d4 = scaledPlusGammaNumerator + scaledLogS0;

        e1 = Phid(d1);
        e2 = Phid(d2);
        e3 = Phid(d3);
        e4 = Phid(d4);

        v0 = (kprice * e1) - (kprice * Math.Exp(oneMinusGamma * x) * e2);
        v0 = v0 + (Math.Exp(gm * halfSigmat) * ((-hbarr * s0 * e3) + (hbarr * Math.Exp(-gm * x) * e4)));
        v0 = v0 * Math.Exp(disc);

        if (iord == 0)
        {
            return v0;
        }

        // Compute the first order term  P_1
        dt = (taumax - taumin) / (double)npoint;

        tt = 0.5 * market.IntegralVariance(taumin, taumax);

        et = Math.Exp(0.5 * (1.0 - gm) * x);

        dsqpi = SqrtPi;

        v1 = 0.0;
        for (int i = 1; i <= npoint; i++)
        {
            v1p = 0.0;
            double tmp = taumin + (dt * (double)((2 * i) - 1) * 0.5);
            p = 0.5 * market.IntegralVariance(tmp, taumax);
            double expGammaP = Math.Exp(gm * p);

            // Function E(p,tt,a,b,gm)
            ccaux = V(p, tt, x, xstar, gm) + V(p, tt, x, -xstar, gm) - V(p, tt, -x, xstar, gm) - V(p, tt, -x, -xstar, gm);
            auxnew = ccaux * ((-kprice * Math.Exp(-xstar * 0.5 * (1.0 - gm))) + (hbarr * Math.Exp(xstar * 0.5 * (1.0 + gm))));
            v1p = v1p + auxnew;

            // Function L(p,tt,a,b,c,gm)
            b = gm - 1.0;
            c = -xstar;
            ccaux = Llold(p, tt, x, b, c, gm) - Llold(p, tt, -x, b, c, gm);
            auxnew = kprice * (1.0 - gm) * ccaux;
            v1p = v1p + auxnew;

            b = -(gm + 1.0);
            c = xstar;
            ccaux = Llold(p, tt, x, b, c, gm) - Llold(p, tt, -x, b, c, gm);
            auxnew = -expGammaP * hbarr * ccaux;
            v1p = v1p + auxnew;

            b = gm + 1.0;
            c = -xstar;
            ccaux = Llold(p, tt, x, b, c, gm) - Llold(p, tt, -x, b, c, gm);
            auxnew = expGammaP * hbarr * gm * ccaux;
            v1p = v1p + auxnew;

            // Function F(p,tt,a,b,c,gm)
            b = gm - 1.0;
            auxnew = -kprice * (1.0 - gm) * (Ff(p, tt, x, b, gm) - Ff(p, tt, -x, b, gm));
            v1p = v1p + auxnew;

            b = gm + 1.0;
            auxnew = -expGammaP * gm * hbarr * (Ff(p, tt, x, b, gm) - Ff(p, tt, -x, b, gm));
            v1p = v1p + auxnew;

            v1 = v1 + ((market.Alpha(tmp) - (gm * 0.5 * market.SigmaSquared(tmp))) * v1p);
        }

        v1 = Math.Exp(disc) * et * v1 * dt / (dsqpi * 2.0);

        if (iord == 1)
        {
            return v0 + v1;
        }

        // Compute the second order term P_2
        double v2 = 0.0;

        for (int i = 1; i <= npoint; i++)
        {
            double v2p = 0.0;
            double tmp = taumin + (dt * (double)((2 * i) - 1) * 0.5);
            p = 0.5 * market.IntegralVariance(tmp, taumax);

            double dtp = (taumax - tmp) / (double)npoint2;
            double outerPerturbation = market.Alpha(tmp) - (gm * 0.5 * market.SigmaSquared(tmp));

            for (int j = 1; j <= npoint2; j++)
            {
                double tmp1 = tmp + (dtp * (double)((2 * j) - 1) * 0.50);
                double s = 0.50 * market.IntegralVariance(tmp1, taumax);
                double expGammaS = Math.Exp(gm * s);
                double innerPerturbation = market.Alpha(tmp1) - (gm * 0.5 * market.SigmaSquared(tmp1));

                caux = Dll(s, p, tt, -x, -1.0 + gm, -xstar, gm) - Dll(s, p, tt, x, -1.0 + gm, -xstar, gm);
                v2pp = caux * kprice * (1.0 - gm);

                caux = Dll(s, p, tt, -x, -1.0 - gm, xstar, gm) - Dll(s, p, tt, x, -1.0 - gm, xstar, gm);
                v2pp = v2pp - (expGammaS * hbarr * caux);

                caux = Dll(s, p, tt, -x, 1.0 + gm, -xstar, gm) - Dll(s, p, tt, x, 1.0 + gm, -xstar, gm);
                v2pp = v2pp + (expGammaS * gm * hbarr * caux);

                caux = +Dvv(s, p, tt, -x, xstar, gm) - Dvv(s, p, tt, x, xstar, gm);
                caux = caux + (Dvv(s, p, tt, -x, -xstar, gm) - Dvv(s, p, tt, x, -xstar, gm));
                v2pp = v2pp + (barrierFactor * caux);

                caux = Dff(s, p, tt, -x, -1.0 + gm, gm) - Dff(s, p, tt, x, -1.0 + gm, gm);
                v2pp = v2pp - ((1.0 - gm) * kprice * caux);

                caux = Dff(s, p, tt, -x, 1.0 + gm, gm) - Dff(s, p, tt, x, 1.0 + gm, gm);
                v2pp = v2pp - (expGammaS * gm * hbarr * caux);

                v2pp = v2pp * 0.5 * (1.0 - gm);

                caux = -Ddll(s, p, tt, -x, -1.0 + gm, -xstar, gm) + Ddll(s, p, tt, x, -1.0 + gm, -xstar, gm);
                v2pp = v2pp + (caux * kprice * (1.0 - gm));

                caux = -Ddll(s, p, tt, -x, -1.0 - gm, xstar, gm) + Ddll(s, p, tt, x, -1.0 - gm, xstar, gm);
                v2pp = v2pp - (expGammaS * hbarr * caux);

                caux = -Ddll(s, p, tt, -x, 1.0 + gm, -xstar, gm) + Ddll(s, p, tt, x, 1.0 + gm, -xstar, gm);
                v2pp = v2pp + (expGammaS * gm * hbarr * caux);

                caux = -Ddvv(s, p, tt, -x, xstar, gm) + Ddvv(s, p, tt, x, xstar, gm);
                caux = caux + (-Dvv(s, p, tt, -x, -xstar, gm) + Dvv(s, p, tt, x, -xstar, gm));

                v2pp = v2pp + (barrierFactor * caux);

                caux = -Ddff(s, p, tt, -x, -1 + gm, gm) + Ddff(s, p, tt, x, -1 + gm, gm);
                v2pp = v2pp - ((1.0 - gm) * kprice * caux);

                caux = -Ddff(s, p, tt, -x, 1.0 + gm, gm) + Ddff(s, p, tt, x, 1.0 + gm, gm);
                v2pp = v2pp - (expGammaS * gm * hbarr * caux);

                v2p = v2p + (innerPerturbation * v2pp);
            }

            v2 = v2 + (v2p * outerPerturbation * dtp);
        }

        v2 = Math.Exp(disc) * et * v2 * dt;

        return v0 + v1 + v2;
    }

    // Normal distribution probabilities accurate to 1D-15, Z the number of standard deviations from the mean:
    // Recchioni's port of algorithm 5666 (Alan Miller) for the error function, from Hart et al., "Computer
    // Approximations", Wiley, 1968
    internal static double Phid(double z)
    {
        const double p0 = 220.2068679123761;
        const double p1 = 221.2135961699311;
        const double p2 = 112.0792914978709;
        const double p3 = 33.91286607838300;
        const double p4 = 6.373962203531650;
        const double p5 = 0.7003830644436881;
        const double p6 = 0.03526249659989109;

        const double q0 = 440.4137358247522;
        const double q1 = 793.8265125199484;
        const double q2 = 637.3336333788311;
        const double q3 = 296.5642487796737;
        const double q4 = 86.78073220294608;
        const double q5 = 16.064177579206950;
        const double q6 = 1.7556671631826420;
        const double q7 = 0.088388347648318440;
        const double cutoff = 7.071067811865475;

        double p;
        double zabs = Math.Abs(z);

        // |Z| > 37
        if (zabs > 37)
        {
            p = 0;
        }
        else
        {
            // |Z| <= 37
            double expntl = Math.Exp(-zabs * zabs / 2);

            // |Z| < CUTOFF = 10/SQRT(2)
            if (zabs < cutoff)
            {
                p = expntl * ((((((((((((p6 * zabs) + p5) * zabs) + p4) * zabs) + p3) * zabs) + p2) * zabs) + p1) * zabs) + p0)
                    / ((((((((((((((q7 * zabs) + q6) * zabs) + q5) * zabs) + q4) * zabs) + q3) * zabs) + q2) * zabs) + q1) * zabs)
                        + q0);
            }

            // |Z| >= CUTOFF.
            else
            {
                p = expntl / (zabs + (1 / (zabs + (2 / (zabs + (3 / (zabs + (4 / (zabs + 0.65))))))))) / SqrtTwoPi;
            }
        }

        if (z > 0)
        {
            p = 1 - p;
        }

        return p;
    }

    // Function F(p,tt,a,b,gm)
    private static double Ff(double p, double tt, double a, double b, double gm)
    {
        double aa = -((b * p) - (b * tt) + a) / Math.Sqrt(2.0 * (tt - p));
        double caux = 2.0 * SqrtPi * Phid(aa);
        aa = (b * b) - ((1.0 - gm) * (1.0 - gm));
        aa = aa / 4.0;
        return Math.Exp(-0.5 * a * b) * Math.Exp(aa * (tt - p)) * caux;
    }

    // Function E(p,tt,a,b,gm)
    private static double V(double p, double tt, double a, double b, double gm)
    {
        double aa = -((p * (a - b)) + (b * tt)) / Math.Sqrt(2.0 * p * tt * (tt - p));
        double caux = Phid(aa);
        aa = Math.Exp((a - b) * (a - b) / (4.0 * tt)) * Math.Exp((1.0 - gm) * (1.0 - gm) * tt / 4.0) * Math.Sqrt(tt);
        return caux / aa;
    }

    // Function L(p,tt,a,b,c,gm)
    private static double Llold(double p, double tt, double a, double b, double c, double gm)
    {
        double xx = (-a + (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        double yy = (-a + (b * tt) + c) / Math.Sqrt(2.0 * tt);
        double rho = Math.Sqrt((tt - p) / tt);
        double aa = (b * b) - ((1.0 - gm) * (1.0 - gm));
        aa = aa / 4.0;
        double caux = Nd2(-xx, -yy, rho);
        return 2.0 * SqrtPi * Math.Exp(-a * b * 0.5) * Math.Exp(aa * (tt - p)) * caux;
    }

    // Function D_E(s,p,tt,a,b,gm)
    internal static double Dvv(double s, double p, double tt, double a, double b, double gm)
    {
        double aa = ((a * p) + (b * (tt - p))) / Math.Sqrt(2.0 * p * tt * (tt - p));
        double caux = Phid(aa);

        aa = Math.Exp((a - b) * (a - b) / (4.0 * tt)) * Math.Exp((1.0 - gm) * (1.0 - gm) * tt / 4.0) * Math.Sqrt(tt);
        caux = -caux / aa;

        double xx = ((a * p) + (b * (tt - p))) / Math.Sqrt(2.0 * tt * p * (tt - p));
        double yy = ((a * s) + (b * (tt - s))) / Math.Sqrt(2.0 * tt * s * (tt - s));
        double rho = Math.Sqrt(s * (tt - p) / (p * (tt - s)));
        double caux1 = Nd2(-xx, -yy, rho);
        caux1 = caux1 / aa;

        aa = Math.Exp((a + b) * (a + b) / (4.0 * tt)) * Math.Exp((1.0 - gm) * (1.0 - gm) * tt / 4.0) * Math.Sqrt(tt);

        xx = ((a * p) - (b * (tt - p))) / Math.Sqrt(2.0 * tt * p * (tt - p));
        yy = ((a * s) - (b * (tt - s))) / Math.Sqrt(2.0 * tt * s * (tt - s));
        rho = Math.Sqrt(s * (tt - p) / (p * (tt - s)));
        double caux2 = Nd2(-xx, -yy, rho);
        caux2 = caux2 / aa;
        return (caux + caux1 + caux2) / (2.0 * SqrtPi);
    }

    // Function D_F(s,p,tt,a,b,gm)
    internal static double Dff(double s, double p, double tt, double a, double b, double gm)
    {
        double xx = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        double caux = -Phid(xx) * Math.Exp(-0.5 * a * b);

        xx = (a + (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        double yy = (a + (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        double rho = Math.Sqrt((tt - p) / (tt - s));
        double caux1 = Nd2(-xx, -yy, rho);
        caux1 = Math.Exp(0.5 * a * b) * caux1;

        xx = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        yy = (a - (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        rho = Math.Sqrt((tt - p) / (tt - s));
        double caux2 = Nd2(-xx, -yy, rho);
        caux2 = Math.Exp(-0.5 * a * b) * caux2;

        double aa = Math.Exp(((b * b) - ((1.0 - gm) * (1.0 - gm))) * (tt - s) / 4.0);

        return (caux + caux1 + caux2) * aa;
    }

    // Function D_L(s,p,a,b,c,gm)
    internal static double Dll(double s, double p, double tt, double a, double b, double c, double gm)
    {
        Span<double> sigmarho = stackalloc double[4];
        Span<double> limit = stackalloc double[4];
        double epsi = 1.0e-12;
        limit[1] = (a + (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        limit[2] = (a + (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        limit[3] = (a + (b * tt) + c) / Math.Sqrt(2.0 * tt);
        sigmarho[1] = Math.Sqrt((tt - p) / (tt - s));
        sigmarho[2] = Math.Sqrt((tt - p) / tt);
        sigmarho[3] = Math.Sqrt((tt - s) / tt);

        double caux = Math.Exp(0.5 * a * b) * Tvtl(0, limit, sigmarho, epsi);

        limit[1] = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        limit[2] = (-a + (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        limit[3] = (-a + (b * tt) + c) / Math.Sqrt(2.0 * tt);
        sigmarho[1] = -Math.Sqrt((tt - p) / (tt - s));
        sigmarho[2] = -Math.Sqrt((tt - p) / tt);
        sigmarho[3] = Math.Sqrt((tt - s) / tt);

        double caux1 = -Math.Exp(-0.5 * a * b) * Tvtl(0, limit, sigmarho, epsi);

        double aa = Math.Exp(((b * b) - ((1.0 - gm) * (1.0 - gm))) * (tt - s) / 4.0);

        return (caux + caux1) * aa;
    }

    // Derivative with respect to a of the function D_F(s,p,tt,a,b,gm): QuantLib's terms, its factor at tt − s, the
    // function's, where QuantLib's reads tt − p
    internal static double Ddff(double s, double p, double tt, double a, double b, double gm)
    {
        double xx = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        double caux = Phid(xx) * Math.Exp(-0.5 * a * b);

        xx = (a + (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        double yy = (a + (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        double rho = Math.Sqrt((tt - p) / (tt - s));
        double caux1 = Nd2(-xx, -yy, rho);
        caux1 = Math.Exp(0.5 * a * b) * caux1;

        xx = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        yy = (a - (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        rho = Math.Sqrt((tt - p) / (tt - s));
        double caux2 = Nd2(-xx, -yy, rho);
        caux2 = -Math.Exp(-0.5 * a * b) * caux2;

        caux = 0.5 * b * (caux + caux1 + caux2);

        xx = (a + (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        yy = b * Math.Sqrt(p - s) / SqrtTwo;
        caux1 = Math.Exp(-0.5 * xx * xx) * Math.Exp(0.5 * a * b) * Phid(yy) / (2.0 * Math.Sqrt(PI * (tt - p)));

        xx = (a + (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        yy = a * Math.Sqrt(p - s) / Math.Sqrt(2.0 * (tt - p) * (tt - s));
        caux2 = Math.Exp(-0.5 * xx * xx) * Math.Exp(0.5 * a * b) * Phid(yy) / (2.0 * Math.Sqrt(PI * (tt - s)));

        xx = (a - (b * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        yy = b * Math.Sqrt(p - s) / SqrtTwo;
        double caux3 = -Math.Exp(-0.5 * xx * xx) * Math.Exp(-0.5 * a * b) * Phid(yy) / (2.0 * Math.Sqrt(PI * (tt - p)));

        xx = (a - (b * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        yy = a * Math.Sqrt(p - s) / Math.Sqrt(2.0 * (tt - p) * (tt - s));
        double caux4 = Math.Exp(-0.5 * xx * xx) * Math.Exp(-0.5 * a * b) * Phid(yy) / (2.0 * Math.Sqrt(PI * (tt - s)));

        double aa = Math.Exp(((b * b) - ((1.0 - gm) * (1.0 - gm))) * (tt - s) / 4.0);

        return (caux + caux1 + caux2 + caux3 + caux4) * aa;
    }

    // Derivative with respect to a of the function D_L(s,p,tt,a,b,c,gm)
    internal static double Ddll(double s, double p, double tt, double ax, double bx, double c, double gm)
    {
        Span<double> sigmarho = stackalloc double[4];
        Span<double> sigma = stackalloc double[4];
        Span<double> limit = stackalloc double[4];
        int idx;
        double epsi = 1.0e-12;
        limit[1] = (ax + (bx * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        limit[2] = (ax + (bx * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        limit[3] = (ax + (bx * tt) + c) / Math.Sqrt(2.0 * tt);
        sigmarho[1] = Math.Sqrt((tt - p) / (tt - s));
        sigmarho[2] = Math.Sqrt((tt - p) / tt);
        sigmarho[3] = Math.Sqrt((tt - s) / tt);
        sigma[1] = Math.Sqrt(1.0 - (sigmarho[1] * sigmarho[1]));
        sigma[2] = Math.Sqrt(1.0 - (sigmarho[2] * sigmarho[2]));
        sigma[3] = Math.Sqrt(1.0 - (sigmarho[3] * sigmarho[3]));

        double caux = 0.5 * bx * Tvtl(0, limit, sigmarho, epsi);

        idx = 1;
        caux = caux + (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * (tt - p)));

        idx = 2;
        caux = caux + (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * (tt - s)));

        idx = 3;
        caux = caux + (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * tt));

        caux = Math.Exp(0.5 * ax * bx) * caux;

        limit[1] = (ax - (bx * (tt - p))) / Math.Sqrt(2.0 * (tt - p));
        limit[2] = (-ax + (bx * (tt - s))) / Math.Sqrt(2.0 * (tt - s));
        limit[3] = (-ax + (bx * tt) + c) / Math.Sqrt(2.0 * tt);
        sigmarho[1] = -Math.Sqrt((tt - p) / (tt - s));
        sigmarho[2] = -Math.Sqrt((tt - p) / tt);
        sigmarho[3] = Math.Sqrt((tt - s) / tt);
        sigma[1] = Math.Sqrt(1.0 - (sigmarho[1] * sigmarho[1]));
        sigma[2] = Math.Sqrt(1.0 - (sigmarho[2] * sigmarho[2]));
        sigma[3] = Math.Sqrt(1.0 - (sigmarho[3] * sigmarho[3]));

        double caux1 = 0.5 * bx * Tvtl(0, limit, sigmarho, epsi);

        idx = 1;
        caux1 = caux1 - (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * (tt - p)));

        idx = 2;
        caux1 = caux1 + (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * (tt - s)));

        idx = 3;
        caux1 = caux1 + (Derivn3(limit, sigmarho, sigma, idx) / Math.Sqrt(2.0 * tt));

        caux1 = Math.Exp(-0.5 * ax * bx) * caux1;

        double aa = Math.Exp(((bx * bx) - ((1.0 - gm) * (1.0 - gm))) * (tt - s) / 4.0);

        return (caux + caux1) * aa;
    }

    // Derivative with respect to a of the function D_E(s,p,tt,a,b,gm): QuantLib's terms, each normal's limit
    // (y − ρx)/√(1 − ρ²) as the identity gives it, ∓b√((p − s)/(2ps)) and a√((p − s)/(2(τ − p)(τ − s))), and the
    // E term's x-derivative negative, d/dx [Φ₂(x, y) − Φ(x)] = −φ(x) Φ(−(y − ρx)/√(1 − ρ²)), where QuantLib's limits
    // read √((p − s)/√(2ps)) and √((p − s)/√(2(τ − p)(τ − s))) and that term is positive
    internal static double Ddvv(double s, double p, double tt, double a, double b, double gm)
    {
        double aa = ((a * p) + (b * (tt - p))) / Math.Sqrt(2.0 * p * tt * (tt - p));
        double caux = Phid(aa);

        aa = Math.Exp(-(a - b) * (a - b) / (4.0 * tt)) / tt;

        caux = 0.5 * aa * caux * (a - b);

        double xx = ((a * p) + (b * (tt - p))) / Math.Sqrt(2.0 * tt * p * (tt - p));
        double yy = ((a * s) + (b * (tt - s))) / Math.Sqrt(2.0 * tt * s * (tt - s));
        double rho = Math.Sqrt(s * (tt - p) / (p * (tt - s)));
        double caux1 = Nd2(-xx, -yy, rho);
        caux1 = -0.5 * aa * caux1 * (a - b);

        aa = Math.Exp(-(a + b) * (a + b) / (4.0 * tt)) / tt;

        xx = ((a * p) - (b * (tt - p))) / Math.Sqrt(2.0 * tt * p * (tt - p));
        yy = ((a * s) - (b * (tt - s))) / Math.Sqrt(2.0 * tt * s * (tt - s));
        rho = Math.Sqrt(s * (tt - p) / (p * (tt - s)));
        double caux2 = Nd2(-xx, -yy, rho);
        caux2 = -0.5 * aa * caux2 * (a + b);

        aa = -b * Math.Sqrt((p - s) / (2.0 * p * s));
        double aux = Math.Sqrt(p / (PI * tt * (tt - p))) * Phid(aa);

        xx = (a + b) * (a + b) / (4.0 * tt);
        yy = ((a * p) - (b * (tt - p))) * ((a * p) - (b * (tt - p))) / (4.0 * p * tt * (tt - p));
        double caux3 = aux * Math.Exp(-xx) * Math.Exp(-yy) / 2.0;

        xx = (a - b) * (a - b) / (4.0 * tt);
        yy = ((a * p) + (b * (tt - p))) * ((a * p) + (b * (tt - p))) / (4.0 * p * tt * (tt - p));
        double caux4 = -aux * Math.Exp(-xx) * Math.Exp(-yy) / 2.0;

        aa = a * Math.Sqrt((p - s) / (2.0 * (tt - p) * (tt - s)));
        aux = Math.Sqrt(s / (PI * tt * (tt - s))) * Phid(aa);

        xx = (a + b) * (a + b) / (4.0 * tt);
        yy = ((a * s) - (b * (tt - s))) * ((a * s) - (b * (tt - s))) / (4.0 * s * tt * (tt - s));
        double caux5 = aux * Math.Exp(-xx) * Math.Exp(-yy) / 2.0;

        xx = (a - b) * (a - b) / (4.0 * tt);
        yy = ((a * s) + (b * (tt - s))) * ((a * s) + (b * (tt - s))) / (4.0 * s * tt * (tt - s));
        double caux6 = aux * Math.Exp(-xx) * Math.Exp(-yy) / 2.0;

        aux = Math.Exp((1.0 - gm) * (1.0 - gm) * tt / 4.0) * Math.Sqrt(tt);

        return (caux + caux1 + caux2 + caux3 + caux4 + caux5 + caux6) / (aux * 2.0 * SqrtPi);
    }

    // Derivn3 computes the derivatives of the trivariate cumulative normal
    // distribution with respect to one of the integration limits
    private static double Derivn3(ReadOnlySpan<double> limit, ReadOnlySpan<double> sigmarho, ReadOnlySpan<double> sigma, int idx)
    {
        double aa, xx, yy, rho;
        double sc = SqrtTwoPi;

        if (idx == 1)
        {
            aa = Math.Exp(-0.5 * limit[1] * limit[1]);
            xx = (limit[3] - (sigmarho[2] * limit[1])) / sigma[2];
            yy = (limit[2] - (sigmarho[1] * limit[1])) / sigma[1];
            rho = (sigmarho[3] - (sigmarho[1] * sigmarho[2])) / (sigma[1] * sigma[2]);
        }
        else if (idx == 2)
        {
            aa = Math.Exp(-0.5 * limit[2] * limit[2]);
            xx = (limit[1] - (sigmarho[1] * limit[2])) / sigma[1];
            yy = (limit[3] - (sigmarho[3] * limit[2])) / sigma[3];
            rho = (sigmarho[2] - (sigmarho[1] * sigmarho[3])) / (sigma[1] * sigma[3]);
        }
        else
        {
            // idx=3
            aa = Math.Exp(-0.5 * limit[3] * limit[3]);
            xx = (limit[1] - (sigmarho[2] * limit[3])) / sigma[2];
            yy = (limit[2] - (sigmarho[3] * limit[3])) / sigma[3];
            rho = (sigmarho[1] - (sigmarho[2] * sigmarho[3])) / (sigma[2] * sigma[3]);
        }

        return aa * Nd2(-xx, -yy, rho) / sc;
    }

    // Trivariate normal and t probabilities P(X(I) < H(I)), I = 1, 2, 3, Genz's TVTL ("Numerical Computation of
    // Rectangular Bivariate and Trivariate Normal and t Probabilities", Statistics and Computing 14 (2004) 251-260;
    // Plackett, Biometrika 41 (1954) 351-360; Drezner, Math. Comp. 62 (1994) 289-294) as Recchioni ported it: limit and
    // sigmarho 1-based, sigmarho holding r21, r31, r32; nu 0 for the normal case; epsi the required absolute accuracy
    internal static double Tvtl(int nu, ReadOnlySpan<double> limit, ReadOnlySpan<double> sigmarho, double epsi)
    {
        const double one = 1.0, zro = 0.0;
        double eps = Math.Max(1.0e-14, epsi);
        double pt = HalfPi;

        double h1 = limit[1];
        double h2 = limit[2];
        double h3 = limit[3];
        double r12 = sigmarho[1];
        double r13 = sigmarho[2];
        double r23 = sigmarho[3];

        // Sort R's and check for special cases
        if (Math.Abs(r12) > Math.Abs(r13))
        {
            h2 = h3;
            h3 = limit[2];
            r12 = r13;
            r13 = sigmarho[1];
        }

        if (Math.Abs(r13) > Math.Abs(r23))
        {
            h1 = h2;
            h2 = limit[1];
            r23 = r13;
            r13 = sigmarho[3];
        }

        double tvt = 0.0;
        if ((Math.Abs(h1) + Math.Abs(h2) + Math.Abs(h3)) < eps)
        {
            tvt = (1 + ((Math.Asin(r12) + Math.Asin(r13) + Math.Asin(r23)) / pt)) / 8.0;
        }
        else if ((nu < 1) && ((Math.Abs(r12) + Math.Abs(r13)) < eps))
        {
            tvt = Phid(h1) * Bvtl(nu, h2, h3, r23);
        }
        else if ((nu < 1) && ((Math.Abs(r13) + Math.Abs(r23)) < eps))
        {
            tvt = Phid(h3) * Bvtl(nu, h1, h2, r12);
        }
        else if ((nu < 1) && ((Math.Abs(r12) + Math.Abs(r23)) < eps))
        {
            tvt = Phid(h2) * Bvtl(nu, h1, h3, r13);
        }
        else if ((1.0 - r23) < eps)
        {
            tvt = Bvtl(nu, h1, Math.Min(h2, h3), r12);
        }
        else if ((r23 + 1.0) < eps)
        {
            if (h2 > -h3)
            {
                tvt = Bvtl(nu, h1, h2, r12) - Bvtl(nu, h1, -h3, r12);
            }
        }
        else
        {
            // Compute singular TVT value
            if (nu < 1)
            {
                tvt = Bvtl(nu, h2, h3, r23) * Phid(h1);
            }
            else if (r23 > 0)
            {
                tvt = Bvtl(nu, h1, Math.Min(h2, h3), zro);
            }
            else if (h2 > -h3)
            {
                tvt = Bvtl(nu, h1, h2, zro) - Bvtl(nu, h1, -h3, zro);
            }

            // Use numerical integration to compute probability
            double rua = Math.Asin(r12);
            double rub = Math.Asin(r13);
            double ar = Math.Asin(r23);
            double ruc = Sign(pt, ar) - ar;
            var integrand = new PlackettIntegrand(h1, h2, h3, r23, rua, rub, ar, ruc, nu);
            tvt = tvt + (Adonet(zro, one, eps, integrand) / (4.0 * pt));
        }

        return Math.Max(zro, Math.Min(tvt, one));
    }

    // Computes SIN(X), COS(X)^2, with series approx. for |X| near PI/2
    private static void Sincs(double x, out double sx, out double cs)
    {
        const double pt = 1.57079632679489661923132169163975;
        double ee = (pt - Math.Abs(x)) * (pt - Math.Abs(x));

        if (ee < 5e-5)
        {
            sx = Sign(1 - (ee * (1 - (ee / 12)) / 2), x);
            cs = ee * (1 - (ee * (1 - (2 * ee / 15)) / 3));
        }
        else
        {
            sx = Math.Sin(x);
            cs = 1 - (sx * sx);
        }
    }

    // One Dimensional Globally Adaptive Integration Function
    private static double Adonet<TIntegrand>(double a, double b, double tol, in TIntegrand integrand)
        where TIntegrand : struct, IPlackettIntegrand
    {
        const int nl = 100;
        Span<double> ei = stackalloc double[101];
        Span<double> ai = stackalloc double[101];
        Span<double> bi = stackalloc double[101];
        Span<double> fi = stackalloc double[101];
        double fin = 0.0;
        double err;
        int ip, im;
        ai[1] = a;
        bi[1] = b;
        err = 1;
        ip = 1;
        im = 1;
        while (((4 * err) > tol) && (im < nl))
        {
            im = im + 1;
            bi[im] = bi[ip];
            ai[im] = (ai[ip] + bi[ip]) / 2.0;
            bi[ip] = ai[im];
            fi[ip] = Krnrdt(ai[ip], bi[ip], integrand, out ei[ip]);
            fi[im] = Krnrdt(ai[im], bi[im], integrand, out ei[im]);

            err = 0.0;
            fin = 0.0;
            for (int i = 1; i <= im; i++)
            {
                if (ei[i] > ei[ip])
                {
                    ip = i;
                }

                fin = fin + fi[i];
                err = err + (ei[i] * ei[i]);
            }

            err = Math.Sqrt(err);
        }

        return fin;
    }

    // Kronrod Rule, its abscissae and weights for (-1,1), the positive ones only: XGK the 2N+1-point Kronrod rule's,
    // XGK(2), XGK(4), ... the N-point Gauss rule's and XGK(1), XGK(3), ... the optimally added ones; WGK the 2N+1-point
    // Kronrod rule's weights, WG the N-point Gauss rule's
    private static double Krnrdt<TIntegrand>(double a, double b, in TIntegrand integrand, out double err)
        where TIntegrand : struct, IPlackettIntegrand
    {
        const int n = 11;
        ReadOnlySpan<double> wg =
        [
            0.0, 0.2729250867779007, 0.05566856711617449,
            0.1255803694649048, 0.1862902109277352,
            0.2331937645919914, 0.2628045445102478,
        ];
        ReadOnlySpan<double> xgk =
        [
            0.0, 0.0000000000000000, 0.9963696138895427,
            0.9782286581460570, 0.9416771085780681,
            0.8870625997680953, 0.8160574566562211,
            0.7301520055740492, 0.6305995201619651,
            0.5190961292068118, 0.3979441409523776,
            0.2695431559523450, 0.1361130007993617,
        ];
        ReadOnlySpan<double> wgk =
        [
            0.0, 0.1365777947111183, 0.00976544104596129,
            0.02715655468210443, 0.04582937856442671,
            0.06309742475037484, 0.07866457193222764,
            0.09295309859690074, 0.1058720744813894,
            0.1167395024610472, 0.1251587991003195,
            0.1312806842298057, 0.1351935727998845,
        ];

        // CEN the mid point of the interval, WID its half-length, RESG the N-point Gauss formula's result, RESK the
        // 2N+1-point Kronrod formula's: the Kronrod approximation to the integral, and its estimated absolute error
        double wid = (b - a) / 2.0;
        double cen = (b + a) / 2.0;

        double fc = integrand.Evaluate(cen);

        double resg = fc * wg[0 + 1];
        double resk = fc * wgk[0 + 1];

        for (int j = 1; j <= n; j++)
        {
            double t = wid * xgk[j + 1];
            fc = integrand.Evaluate(cen - t) + integrand.Evaluate(cen + t);
            resk = resk + (wgk[j + 1] * fc);
            if ((j - (2 * (j / 2))) == 0)
            {
                resg = resg + (wg[1 + (j / 2)] * fc);
            }
        }

        err = Math.Abs(wid * (resk - resg));
        return wid * resk;
    }

    // Student t Distribution Function
    private static double Studnt(int nu, double t)
    {
        const double zro = 0.0, one = 1.0;
        double result;

        if (nu < 1)
        {
            result = Phid(t);
        }
        else if (nu == 1)
        {
            result = (1 + (2.0 * Math.Atan(t) / PI)) / 2.0;
        }
        else if (nu == 2)
        {
            result = (1 + (t / Math.Sqrt(2.0 + (t * t)))) / 2.0;
        }
        else
        {
            double tt = t * t;
            double cssthe = 1 / (1 + (tt / (double)nu));
            double polyn = 1;
            for (int j = nu - 2; j >= 2; j -= 2)
            {
                polyn = 1.0 + ((j - 1.0) * cssthe * polyn / (double)j);
            }

            if ((nu - (2 * (nu / 2))) == 1)
            {
                double rn = nu;
                double ts = t / Math.Sqrt(rn);
                result = (1.0 + (2.0 * (Math.Atan(ts) + (ts * cssthe * polyn)) / PI)) / 2.0;
            }
            else
            {
                double snthe = t / Math.Sqrt(nu + tt);
                result = (1 + (snthe * polyn)) / 2.0;
            }

            result = Math.Max(zro, Math.Min(result, one));
        }

        return result;
    }

    // Bivariate t probabilities P(X < DH, Y < DK) with correlation R, Dunnett and Sobel's method ("A bivariate
    // generalization of Student's t-distribution with tables for certain special cases", Biometrika 41 (1954) 153-169)
    // as Genz coded it and Recchioni ported it
    private static double Bvtl(int nu, double dh, double dk, double r)
    {
        double result;
        double one = 1;
        double eps = 1e-15;
        if (nu < 1)
        {
            result = Nd2(-dh, -dk, r);
        }
        else if ((1 - r) <= eps)
        {
            result = Studnt(nu, Math.Min(dh, dk));
        }
        else if ((r + 1) <= eps)
        {
            result = dh > -dk ? Studnt(nu, dh) - Studnt(nu, -dk) : 0.0;
        }
        else
        {
            int hs = (int)Sign(one, dh - (r * dk));
            int ks = (int)Sign(one, dk - (r * dh));
            double tpi = TwoPi;
            double snu = (double)nu;
            snu = Math.Sqrt(snu);
            double ors = 1.0 - (r * r);
            double hrk = dh - (r * dk);
            double krh = dk - (r * dh);
            double xnhk, xnkh;
            if ((Math.Abs(hrk) + ors) > 0)
            {
                xnhk = hrk * hrk / ((hrk * hrk) + (ors * (nu + (dk * dk))));
                xnkh = krh * krh / ((krh * krh) + (ors * (nu + (dh * dh))));
            }
            else
            {
                xnhk = 0.0;
                xnkh = 0.0;
            }

            double bvt, gmph, gmpk, btnckh, btpdkh, btnchk, btpdhk;
            if ((nu - (2 * (nu / 2))) == 0)
            {
                bvt = Math.Atan2(Math.Sqrt(ors), -r) / tpi;
                gmph = dh / Math.Sqrt(16 * (nu + (dh * dh)));
                gmpk = dk / Math.Sqrt(16 * (nu + (dk * dk)));
                btnckh = 2 * Math.Atan2(Math.Sqrt(xnkh), Math.Sqrt(1 - xnkh)) / PI;
                btpdkh = 2 * Math.Sqrt(xnkh * (1 - xnkh)) / PI;
                btnchk = 2 * Math.Atan2(Math.Sqrt(xnhk), Math.Sqrt(1 - xnhk)) / PI;
                btpdhk = 2 * Math.Sqrt(xnhk * (1 - xnhk)) / PI;
                for (int j = 1; j <= nu / 2; ++j)
                {
                    bvt = bvt + (gmph * (1 + (ks * btnckh)));
                    bvt = bvt + (gmpk * (1 + (hs * btnchk)));
                    btnckh = btnckh + btpdkh;
                    btpdkh = 2 * j * btpdkh * (1 - xnkh) / ((2 * j) + 1);
                    btnchk = btnchk + btpdhk;
                    btpdhk = 2 * j * btpdhk * (1 - xnhk) / ((2 * j) + 1);
                    gmph = gmph * ((2 * j) - 1) / (2 * j * (1 + (dh * dh / nu)));
                    gmpk = gmpk * ((2 * j) - 1) / (2 * j * (1 + (dk * dk / nu)));
                }
            }
            else
            {
                double qhrk = Math.Sqrt((dh * dh) + (dk * dk) - (2 * r * dh * dk) + (nu * ors));
                double hkrn = (dh * dk) + (r * nu);
                double hkn = (dh * dk) - nu;
                double hpk = dh + dk;
                bvt = Math.Atan2(-snu * ((hkn * qhrk) + (hpk * hkrn)), (hkn * hkrn) - (nu * hpk * qhrk)) / tpi;
                if (bvt < -eps)
                {
                    bvt = bvt + 1;
                }

                gmph = dh / (tpi * snu * (1 + (dh * dh / nu)));
                gmpk = dk / (tpi * snu * (1 + (dk * dk / nu)));
                btnckh = Math.Sqrt(xnkh);
                btpdkh = btnckh;
                btnchk = Math.Sqrt(xnhk);
                btpdhk = btnchk;
                for (int j = 1; j <= (nu - 1) / 2; ++j)
                {
                    bvt = bvt + (gmph * (1 + (ks * btnckh)));
                    bvt = bvt + (gmpk * (1 + (hs * btnchk)));
                    btpdkh = ((2 * j) - 1) * btpdkh * (1 - xnkh) / (2 * j);
                    btnckh = btnckh + btpdkh;
                    btpdhk = ((2 * j) - 1) * btpdhk * (1 - xnhk) / (2 * j);
                    btnchk = btnchk + btpdhk;
                    gmph = 2 * j * gmph / (((2 * j) + 1) * (1 + (dh * dh / nu)));
                    gmpk = 2 * j * gmpk / (((2 * j) + 1) * (1 + (dk * dk / nu)));
                }
            }

            result = bvt;
        }

        return result;
    }

    // Computes Plackett formula integrand: Genz's PNTGND, its quadratic form squared (#2935) and its Student branch
    // under the degrees-of-freedom test, where QuantLib 1.44 takes a square root and attaches the branch to the normal
    // integrand's cut
    private static double Pntgnd(int nuc, double ba, double bb, double bc, double ra, double rb, double r, double rr)
    {
        double result = 0.0;
        double dt = rr * (rr - ((ra - rb) * (ra - rb)) - (2 * ra * rb * (1 - r)));
        if (dt > 0)
        {
            double bt = ((bc * rr) + (ba * ((r * rb) - ra)) + (bb * ((r * ra) - rb))) / Math.Sqrt(dt);
            double ft = ((ba - (r * bb)) * (ba - (r * bb)) / rr) + (bb * bb);
            if (nuc < 1)
            {
                if ((bt > -10) && (ft < 100))
                {
                    result = Math.Exp(-ft / 2);
                    if (bt < 10)
                    {
                        result = result * Phid(bt);
                    }
                }
            }
            else
            {
                ft = Math.Sqrt(1 + (ft / nuc));
                result = Studnt(nuc, bt / ft) / Math.Pow(ft, nuc);
            }
        }

        return result;
    }

    // Bivariate normal probabilities P(X > DH, Y > DK), so that P(X < DH, Y < DK) = ND2(-DH, -DK, R): Drezner and
    // Wesolowsky's method ("On the computation of the bivariate normal integral", Journal of Statist. Comput. Simul. 35
    // (1989) 101-107) with Genz's modifications for double precision and |R| close to 1, as Recchioni ported it; its
    // |R| >= 0.925 branch Genz's BVND, √(1 − XS), where QuantLib's port takes (1 − XS)²
    internal static double Nd2(double a, double b, double rho)
    {
        ReadOnlySpan<double> xl =
        [
            0.0, 0.0, 0.0, 0.0,
            0.0, -0.9324695142031522, -0.9815606342467191, -0.9931285991850949,
            0.0, -0.6612093864662647, -0.9041172563704750, -0.9639719272779138,
            0.0, -0.2386191860831970, -0.7699026741943050, -0.9122344282513259,
            0.0, 0.0, -0.5873179542866171, -0.8391169718222188,
            0.0, 0.0, -0.3678314989981802, -0.7463319064601508,
            0.0, 0.0, -0.1252334085114692, -0.6360536807265150,
            0.0, 0.0, 0.0, -0.5108670019508271,
            0.0, 0.0, 0.0, -0.3737060887154196,
            0.0, 0.0, 0.0, -0.2277858511416451,
            0.0, 0.0, 0.0, -0.07652652113349733,
        ];
        ReadOnlySpan<double> wl =
        [
            0.0, 0.0, 0.0, 0.0,
            0.0, 0.1713244923791705, 0.0471753363865111, 0.01761400713915212,
            0.0, 0.3607615730481384, 0.1069393259953183, 0.04060142980038694,
            0.0, 0.4679139345726904, 0.1600783285433464, 0.06267204833410906,
            0.0, 0.0, 0.2031674267230659, 0.08327674157670475,
            0.0, 0.0, 0.2334925365383547, 0.1019301198172404,
            0.0, 0.0, 0.2491470458134029, 0.1181945319615184,
            0.0, 0.0, 0.0, 0.1316886384491766,
            0.0, 0.0, 0.0, 0.1420961093183821,
            0.0, 0.0, 0.0, 0.1491729864726037,
            0.0, 0.0, 0.0, 0.1527533871307259,
        ];
        int ng, lg;
        double r = rho;
        double dh = a;
        double dk = b;

        if (Math.Abs(r) < 0.3)
        {
            ng = 1;
            lg = 3;
        }
        else if (Math.Abs(r) < 0.75)
        {
            ng = 2;
            lg = 6;
        }
        else
        {
            ng = 3;
            lg = 10;
        }

        double h = dh;
        double k = dk;
        double hk = h * k;

        double bvn = 0.0;
        if (Math.Abs(r) < 0.925)
        {
            if (Math.Abs(r) > 0)
            {
                double hs = ((h * h) + (k * k)) / 2;
                double asr = Math.Asin(r);
                for (int i = 1; i <= lg; i++)
                {
                    for (int @is = -1; @is <= 1; @is = @is + 2)
                    {
                        double sn = Math.Sin(asr * ((@is * xl[(4 * i) + ng]) + 1) / 2);
                        bvn = bvn + (wl[(4 * i) + ng] * Math.Exp(((sn * hk) - hs) / (1.0 - (sn * sn))));
                    }
                }

                bvn = bvn * asr / (2 * TwoPi);
            }

            bvn = bvn + (Phid(-h) * Phid(-k));
        }
        else
        {
            if (r < 0)
            {
                k = -k;
                hk = -hk;
            }

            if (Math.Abs(r) < 1)
            {
                double @as = (1 - r) * (1 + r);
                double aa = Math.Sqrt(@as);

                double bs = (h - k) * (h - k);
                double c = (4 - hk) / 8;
                double d = (12 - hk) / 16;
                double asr = -((bs / @as) + hk) / 2;
                if (asr > -100)
                {
                    bvn = aa * Math.Exp(asr) * (1 - (c * (bs - @as) * (1 - (d * bs / 5)) / 3) + (c * d * @as * @as / 5));
                }

                if (-hk < 100)
                {
                    double bb = Math.Sqrt(bs);
                    bvn = bvn - (Math.Exp(-hk / 2) * SqrtTwoPi * Phid(-bb / aa) * bb * (1 - (c * bs * (1 - (d * bs / 5)) / 3)));
                }

                aa = aa / 2;
                for (int i = 1; i <= lg; i++)
                {
                    for (int @is = -1; @is <= 1; @is = @is + 2)
                    {
                        double xs = aa * ((@is * xl[(4 * i) + ng]) + 1) * (aa * ((@is * xl[(4 * i) + ng]) + 1));
                        double rs = Math.Sqrt(1 - xs);
                        asr = -((bs / xs) + hk) / 2;
                        if (asr > -100)
                        {
                            bvn = bvn + (aa * wl[(4 * i) + ng] * Math.Exp(asr)
                                * ((Math.Exp(-hk * (1 - rs) / (2 * (1 + rs))) / rs) - (1 + (c * xs * (1 + (d * xs))))));
                        }
                    }
                }

                bvn = -bvn / TwoPi;
            }

            if (r > 0)
            {
                bvn = bvn + Phid(-Math.Max(h, k));
            }
            else
            {
                bvn = -bvn;
                if (k > h)
                {
                    bvn = bvn + Phid(k) - Phid(h);
                }
            }
        }

        return bvn;
    }

    private static double Sign(double a, double b) => b > 0.0 ? Math.Abs(a) : -Math.Abs(a);

    // The Plackett formula integrand TVTL integrates, a value so that the Kronrod rule's calls allocate nothing.
    private interface IPlackettIntegrand
    {
        double Evaluate(double x);
    }

    // Computes Plackett formula integrands: TVTMFN at fixed limits and correlations
    private readonly struct PlackettIntegrand(
        double h1, double h2, double h3, double r23, double rua, double rub, double ar, double ruc, int nuc)
        : IPlackettIntegrand
    {
        public double Evaluate(double x)
        {
            const double zro = 0.0;
            double result = 0.0;

            Sincs(rua * x, out double r12, out double rr2);
            Sincs(rub * x, out double r13, out double rr3);

            if (Math.Abs(rua) > 0)
            {
                result += rua * Pntgnd(nuc, h1, h2, h3, r13, r23, r12, rr2);
            }

            if (Math.Abs(rub) > 0)
            {
                result += rub * Pntgnd(nuc, h1, h3, h2, r12, r23, r13, rr3);
            }

            if (nuc > 0)
            {
                Sincs(ar + (ruc * x), out double r, out double rr);
                result -= ruc * Pntgnd(nuc, h2, h3, h1, zro, zro, r, rr);
            }

            return result;
        }
    }

    // QuantLib's adapters of the process: the integrated rate, the integrated carry and variance, the instantaneous
    // carry and variance, each structure read at the instants the clock's times denote; QuantLib's reads where a
    // structure shares the clock's times.
    private sealed class Market(GeneralizedBlackScholesProcess process, TermStructure clock)
    {
        private readonly YieldTermStructure r = process.RiskFreeRate;
        private readonly YieldTermStructure q = process.DividendYield;
        private readonly BlackVolTermStructure v = process.BlackVolatility;
        private readonly double s = process.X0;

        // r->forwardRate(t1,t2,Continuous) * (t2-t1)
        internal double IntegralRate(double t1, double t2) => r.ContinuousForwardRateAt(clock, t1, t2) * (t2 - t1);

        // the carry's forward rate times (t2-t1)
        internal double IntegralAlpha(double t1, double t2)
        {
            double alpha = r.ContinuousForwardRateAt(clock, t1, t2) - q.ContinuousForwardRateAt(clock, t1, t2);
            return alpha * (t2 - t1);
        }

        // the carry's instantaneous forward rate
        internal double Alpha(double t) => r.ContinuousForwardRateAt(clock, t, t) - q.ContinuousForwardRateAt(clock, t, t);

        // the square of the instantaneous forward volatility, v->blackForwardVol(t,t,s,true), per unit of the clock's
        // time
        internal double SigmaSquared(double t)
        {
            if (v.SharesTimes(clock))
            {
                double sigma = v.BlackForwardVol(t, t, s, true);
                return sigma * sigma;
            }

            // QuantLib's centred difference on the clock's time
            if (t == 0.0)
            {
                double epsilon = 1.0e-5;
                return v.BlackForwardVarianceAt(clock, 0.0, epsilon, s, true) / epsilon;
            }
            else
            {
                double epsilon = Math.Min(1.0e-5, t);
                return v.BlackForwardVarianceAt(clock, t - epsilon, t + epsilon, s, true) / (2 * epsilon);
            }
        }

        // v->blackForwardVariance(t1,t2,s,true)
        internal double IntegralVariance(double t1, double t2) => v.BlackForwardVarianceAt(clock, t1, t2, s, true);
    }
}
