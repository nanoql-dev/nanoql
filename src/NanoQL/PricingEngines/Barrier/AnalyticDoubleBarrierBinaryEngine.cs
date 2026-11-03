// SPDX-FileCopyrightText: Copyright (C) 2015 Thema Consulting SA
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/barrier/analyticdoublebarrierbinaryengine.hpp ql/pricingengines/barrier/analyticdoublebarrierbinaryengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The analytic engine for double barrier binary options: cash-or-nothing knock-outs and knock-ins paid at expiry by
/// Hui's eigenfunction series ("One-touch double barrier binary option values", Applied Financial Economics 6/1996),
/// and KIKO and KOKI options paid at the hit, by the same series' sums.
/// </summary>
/// <remarks>
/// <para>
/// The knock-out and knock-in take a European exercise; KIKO and KOKI an American one paying at the hit, its window
/// open from the start. Each rate is read on the volatility's clock and each time is the volatility's, as
/// <see cref="AnalyticBarrierEngine"/> reads them, where QuantLib reads every structure at the risk-free curve's times.
/// </para>
/// <para>
/// Documented divergences. The knock-out integrates the killed density over the payoff's region, [max(K, L), U] for a
/// call and [L, min(K, U)] for a put, where QuantLib's series integrates over the whole corridor whatever the payoff's
/// strike and type, so a put struck at 0 pays (<c>Divergences/DoubleBinaryStrikeTests.cs</c>); the knock-in is the
/// European cash-or-nothing less the knock-out, and one already knocked in is that European, where QuantLib pays the
/// cash undiscounted (<c>Divergences/DoubleBinaryKnockedInTests.cs</c>). QuantLib's value stands for a call at K ≤ L,
/// its own cases. Hui's series is taken where a bound on its dropped tail free of its sines, the terms' envelope, is
/// within 1e-11 of its sum, where QuantLib checks its last term, which carries a sine and passes unconverged at x/Z =
/// j/99 (<c>Divergences/DoubleBinaryHuiCheckTests.cs</c>); elsewhere, where QuantLib refuses ("serie did not converge
/// sufficiently fast") among them, the knock-out is the cash-or-nothing part of QuantLib's double-barrier image series,
/// <see cref="AnalyticDoubleBarrierEngine"/>'s callKO and putKO, which converges fastest there, its terms in logs
/// (<c>Divergences/DoubleBinarySmallVarianceTests.cs</c>). The rebate is priced as the double barrier engine prices it,
/// a knock-out's at the first touch of either barrier, now where one is already touched, a knock-in's at expiry where
/// neither is touched, where QuantLib's engine reads none; a KIKO's or KOKI's, for which no convention fixes the
/// payment, is refused (<c>Divergences/DoubleBinaryRebateTests.cs</c>). KIKO and KOKI are QuantLib's 999-term sums
/// where Abel's bound on their dropped tail is within 1e-11 of the sum, QuantLib's value and bits there; elsewhere,
/// where QuantLib's 999 terms run unconverged or it refuses, the absorption flux of QuantLib's own image-series killed
/// density at the barrier, integrated in log time between the roots that bound its mass
/// (<c>Divergences/DoubleBinaryKikoTests.cs</c>). Below machine-epsilon variance the underlying follows its forward.
/// </para>
/// <para>
/// The degenerate branches, where a barrier is already touched, set the value, delta, gamma, vega and rho as QuantLib
/// does, and no theta or dividend rho; one already knocked in takes those of the European cash-or-nothing it is.
/// </para>
/// <para>
/// The series are first-passage closed forms in equivalent-constant rates and a volatility, the variance and discounts
/// to expiry: no exact closed form exists for a first passage under general deterministic term structures, and on
/// shaped markets an independent Monte Carlo finds a knock-out +0.66% and +1.78% off, within two of its standard
/// errors; the term-structure-exact route is a Monte Carlo on the process.
/// </para>
/// </remarks>
public static class AnalyticDoubleBarrierBinaryEngine
{
    // QuantLib's series' lengths and its last-term check's threshold (analyticdoublebarrierbinaryengine.cpp:46-53)
    private const int HuiIterations = 100;
    private const int KikoIterations = 1000;

    // the relative bound on a series' dropped tail under which the series stands
    private const double TailBound = 1e-11;

    // the most envelope terms summed for a tail bound: past it the bound fails, the terms decaying too slowly
    private const int MaxEnvelopeTerms = 100000;

    // The integration's tolerance and cap for the flux integral
    private const double FluxTolerance = 1e-12;
    private const int MaxFluxEvaluations = 100000;

    /// <summary>Prices a double barrier binary option.</summary>
    /// <param name="option">The option, with a cash-or-nothing payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <returns>The option's results: the value, and the Greeks QuantLib sets where a barrier is already touched; the
    /// expired results when the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// A KIKO or KOKI option's exercise isn't American or opens after the volatility's reference date; another's isn't
    /// European; the payoff isn't cash-or-nothing; the spot or a barrier isn't positive; the lower barrier isn't below
    /// the upper one.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DoubleBarrierOption option, EvaluationContext context, GeneralizedBlackScholesProcess process)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Body(option, process));
    }

    // The value at unit cash of a payment at the first touch of the lower barrier (KIKO) or the upper one (KOKI) before
    // the other and before the date, the variance read at a strike: the double-barrier engine's rebate
    internal static double HitValue(
        GeneralizedBlackScholesProcess process, Date lastDate, double barrierLo, double barrierHi, double volStrike,
        DoubleBarrierType barrierType) =>
        new Market(process, lastDate, barrierLo, barrierHi, volStrike).Kiko(1.0, barrierType);

    // The value at unit cash of a payment at the date where neither barrier is touched: the double-barrier engine's
    // knock-in rebate
    internal static double NoTouchValue(
        GeneralizedBlackScholesProcess process, Date lastDate, double barrierLo, double barrierHi, double volStrike) =>
        new Market(process, lastDate, barrierLo, barrierHi, volStrike).KnockOut(1.0, OptionType.Call, 0.0);

    private static OneAssetOptionResults Body(DoubleBarrierOption option, GeneralizedBlackScholesProcess process)
    {
        DoubleBarrierType barrierType = option.BarrierType;
        if (barrierType is DoubleBarrierType.KIKO or DoubleBarrierType.KOKI)
        {
            if (option.Exercise is not AmericanExercise ex)
            {
                throw new NanoQLException("KIKO/KOKI options must have American exercise");
            }

            if (!(ex.Dates[0] <= process.BlackVolatility.ReferenceDate))
            {
                throw new NanoQLException("American option with window exercise not handled yet");
            }

            // no convention fixes when a KIKO's or KOKI's rebate is paid, at the knock-out's touch or at expiry if the
            // knock-in never comes
            if (option.Rebate != 0.0)
            {
                throw new NanoQLException("rebate not supported for KIKO/KOKI options");
            }
        }
        else if (option.Exercise.Type != ExerciseType.European)
        {
            throw new NanoQLException("non-European exercise given");
        }

        if (option.Payoff is not CashOrNothingPayoff payoff)
        {
            throw new NanoQLException("a cash-or-nothing payoff must be given");
        }

        double spot = process.StateVariable;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double barrierLo = option.BarrierLo;
        double barrierHi = option.BarrierHi;
        if (!(barrierLo > 0.0))
        {
            throw new NanoQLException("positive low barrier value required");
        }

        if (!(barrierHi > 0.0))
        {
            throw new NanoQLException("positive high barrier value required");
        }

        if (!(barrierLo < barrierHi))
        {
            throw new NanoQLException("barrier_lo must be < barrier_hi");
        }

        // check if the barriers are already touched
        switch (barrierType)
        {
            case DoubleBarrierType.KnockOut:
                if (spot <= barrierLo || spot >= barrierHi)
                {
                    // knocked out: its rebate, paid at the hit, now, where QuantLib's has no value
                    return Touched(option.Rebate);
                }

                break;

            case DoubleBarrierType.KnockIn:
                if (spot <= barrierLo || spot >= barrierHi)
                {
                    // knocked in: the European cash-or-nothing, paid at expiry
                    OneAssetOptionResults european = AnalyticEuropeanEngine.Body(
                        new VanillaOption(payoff, option.Exercise), process, process.RiskFreeRate);
                    return new OneAssetOptionResults
                    {
                        NPV = european.NPV,
                        Delta = european.Delta,
                        Gamma = european.Gamma,
                        Vega = european.Vega,
                        Rho = european.Rho,
                    };
                }

                break;

            case DoubleBarrierType.KIKO:
                if (spot >= barrierHi)
                {
                    // knocked out, no value
                    return Touched(0.0);
                }
                else if (spot <= barrierLo)
                {
                    // knocked in, pays
                    return Touched(payoff.CashPayoff);
                }

                break;

            case DoubleBarrierType.KOKI:
                if (spot <= barrierLo)
                {
                    // knocked out, no value
                    return Touched(0.0);
                }
                else if (spot >= barrierHi)
                {
                    // knocked in, pays
                    return Touched(payoff.CashPayoff);
                }

                break;
        }

        var market = new Market(process, option.Exercise.LastDate(), barrierLo, barrierHi, payoff.Strike);
        double value = barrierType switch
        {
            DoubleBarrierType.KnockOut => market.KnockOut(payoff.CashPayoff, payoff.OptionType, payoff.Strike),
            DoubleBarrierType.KnockIn => market.KnockIn(payoff),
            _ => market.Kiko(payoff.CashPayoff, barrierType),
        };

        // the rebate QuantLib's engine drops, the double barrier engine's convention: a knock-out's paid at the first
        // touch of either barrier, R × (KIKO + KOKI) at unit cash; a knock-in's at expiry where neither is touched, R ×
        // the unit-cash knock-out at strike 0
        if (option.Rebate != 0.0)
        {
            value += barrierType == DoubleBarrierType.KnockOut
                ? option.Rebate * (market.Kiko(1.0, DoubleBarrierType.KIKO) + market.Kiko(1.0, DoubleBarrierType.KOKI))
                : option.Rebate * market.KnockOut(1.0, OptionType.Call, 0.0);
        }

        return new OneAssetOptionResults { NPV = value };
    }

    // QuantLib's degenerate branches: the value, and delta, gamma, vega and rho at 0
    private static OneAssetOptionResults Touched(double value) => new()
    {
        NPV = value,
        Delta = 0.0,
        Gamma = 0.0,
        Vega = 0.0,
        Rho = 0.0,
    };

    // The market as the series read it, on the volatility's clock, with the routes
    private sealed class Market
    {
        private readonly GeneralizedBlackScholesProcess process;
        private readonly Date lastDate;
        private readonly double spot;
        private readonly double barrierLo;
        private readonly double barrierHi;
        private readonly double residualTime;
        private readonly double variance;
        private readonly double sigmaq;
        private readonly double r;
        private readonly double q;
        private readonly double discount;
        private readonly double dividendDiscount;

        internal Market(
            GeneralizedBlackScholesProcess process, Date lastDate, double barrierLo, double barrierHi, double volStrike)
        {
            this.process = process;
            this.lastDate = lastDate;
            this.barrierLo = barrierLo;
            this.barrierHi = barrierHi;
            spot = process.StateVariable;
            if (!(spot > 0.0))
            {
                throw new NanoQLException("positive spot value required");
            }

            BlackVolTermStructure vol = process.BlackVolatility;
            variance = vol.AccruedVariance(lastDate, volStrike);
            if (!(variance >= 0.0))
            {
                throw new NanoQLException("negative variance not allowed");
            }

            residualTime = vol.TimeFromReference(lastDate);
            if (!(residualTime > 0.0))
            {
                throw new NanoQLException("expiration time must be > 0");
            }

            sigmaq = variance / residualTime;
            r = AnalyticBarrierEngine.RateOnVolatilityClock(process.RiskFreeRate, vol, lastDate);
            q = AnalyticBarrierEngine.RateOnVolatilityClock(process.DividendYield, vol, lastDate);
            discount = process.RiskFreeRate.AccruedDiscount(lastDate);
            dividendDiscount = process.DividendYield.AccruedDiscount(lastDate);
        }

        private bool Deterministic => variance < NumericLimits.QL_EPSILON;

        // The cash-or-nothing knock-out paying on terminal spots in the payoff's region, floored at 0 as QuantLib's
        internal double KnockOut(double cash, OptionType type, double strike) => Max(KnockOutSum(cash, type, strike), 0.0);

        // The knock-out before QuantLib's floor: Hui's series under its tail bound, the image series elsewhere
        private double KnockOutSum(double cash, OptionType type, double strike)
        {
            double lo = type == OptionType.Call ? Max(strike, barrierLo) : barrierLo;
            double hi = type == OptionType.Call ? barrierHi : Min(strike, barrierHi);
            if (!(lo < hi))
            {
                return 0.0;
            }

            if (Deterministic)
            {
                double forward = spot * dividendDiscount / discount;
                return forward > barrierLo && forward < barrierHi && forward >= lo && forward <= hi
                    ? cash * discount
                    : 0.0;
            }

            bool full = lo == barrierLo && hi == barrierHi;
            (double hui, bool converged) = full ? HuiFull(cash) : HuiRegion(cash, lo, hi);
            if (converged)
            {
                return hui;
            }

            return cash * discount * ImageCash(lo, hi);
        }

        // The cash-or-nothing knock-in: the European less the knock-out; QuantLib's cash × D − KO for its strike-0 call
        internal double KnockIn(CashOrNothingPayoff payoff)
        {
            double ko = KnockOutSum(payoff.CashPayoff, payoff.OptionType, payoff.Strike);
            double european;
            if (payoff.OptionType == OptionType.Call && payoff.Strike == 0.0)
            {
                european = payoff.CashPayoff * discount;
            }
            else
            {
                double forward = spot * dividendDiscount / discount;
                european = new BlackCalculator(payoff, forward, Math.Sqrt(variance), discount).Value;
            }

            return Max(european - ko, 0.0);
        }

        // KIKO or KOKI: QuantLib's 999-term sum under Abel's bound, the absorption flux elsewhere
        internal double Kiko(double cash, DoubleBarrierType barrierType)
        {
            if (Deterministic)
            {
                return DeterministicHit(cash, barrierType);
            }

            (double quantLib, bool converged) = QuantLibKiko(cash, barrierType);
            if (converged)
            {
                return quantLib;
            }

            return cash * Flux(barrierType);
        }

        // QuantLib's payoffAtExpiry for the knock-out, its tot before the max, and whether the sine-free bound on its
        // dropped tail is within 1e-11 of it
        private (double Tot, bool Converged) HuiFull(double cash)
        {
            double z = Math.Log(barrierHi / barrierLo);
            double b = r - q;
            double alpha = -0.5 * ((2 * b / sigmaq) - 1);
            double beta = (-0.25 * Math.Pow((2 * b / sigmaq) - 1, 2)) - (2 * r / sigmaq);
            double factor = 2 * Math.PI * cash / Math.Pow(z, 2); // common factor
            double loAlpha = Math.Pow(spot / barrierLo, alpha);
            double hiAlpha = Math.Pow(spot / barrierHi, alpha);

            double tot = 0;
            for (int i = 1; i < HuiIterations; ++i)
            {
                double term1 = (loAlpha - (Math.Pow(-1.0, i) * hiAlpha)) / (Math.Pow(alpha, 2) + Math.Pow(i * Math.PI / z, 2));
                double term2 = Math.Sin(i * Math.PI / z * Math.Log(spot / barrierLo));
                double term3 = Math.Exp(-0.5 * (Math.Pow(i * Math.PI / z, 2) - beta) * variance);
                double term = factor * i * term1 * term2 * term3;
                tot += term;
            }

            // the envelope of the dropped terms, |lo^α ∓ hi^α| <= lo^α + hi^α and |sin| <= 1
            double TailTerm(int i)
            {
                double w = i * Math.PI / z;
                return factor * i * (loAlpha + hiAlpha) / ((alpha * alpha) + (w * w)) * Math.Exp(-0.5 * ((w * w) - beta) * variance);
            }

            return (tot, double.IsFinite(tot) && EnvelopeTail(TailTerm, HuiIterations, Math.Abs(alpha) * z / Math.PI) <= TailBound * Math.Abs(tot));
        }

        // Hui's series over the region [lo, hi] of terminal spots: ∫ e^(−αy) sin(ωy) dy in closed form over
        // y ∈ [ln(lo/L), ln(hi/L)] in place of QuantLib's whole corridor, with its envelope's tail bound
        private (double Tot, bool Converged) HuiRegion(double cash, double lo, double hi)
        {
            double z = Math.Log(barrierHi / barrierLo);
            double b = r - q;
            double alpha = -0.5 * ((2 * b / sigmaq) - 1);
            double beta = (-0.25 * Math.Pow((2 * b / sigmaq) - 1, 2)) - (2 * r / sigmaq);
            double factor = 2 * Math.PI * cash / Math.Pow(z, 2);
            double x = Math.Log(spot / barrierLo);
            double ya = Math.Log(lo / barrierLo);
            double yb = Math.Log(hi / barrierLo);
            double aAlpha = Math.Pow(spot / lo, alpha);
            double bAlpha = Math.Pow(spot / hi, alpha);

            double tot = 0;
            for (int i = 1; i < HuiIterations; ++i)
            {
                double w = i * Math.PI / z;
                double bracket = (aAlpha * ((alpha * Math.Sin(w * ya)) + (w * Math.Cos(w * ya))))
                    - (bAlpha * ((alpha * Math.Sin(w * yb)) + (w * Math.Cos(w * yb))));
                double term = factor * i / w * bracket / ((alpha * alpha) + (w * w)) * Math.Sin(w * x)
                    * Math.Exp(-0.5 * ((w * w) - beta) * variance);
                tot += term;
            }

            double TailTerm(int i)
            {
                double w = i * Math.PI / z;
                return factor * i / w * (aAlpha + bAlpha) * (Math.Abs(alpha) + w) / ((alpha * alpha) + (w * w))
                    * Math.Exp(-0.5 * ((w * w) - beta) * variance);
            }

            return (tot, double.IsFinite(tot) && EnvelopeTail(TailTerm, HuiIterations, Math.Abs(alpha) * z / Math.PI) <= TailBound * Math.Abs(tot));
        }

        // QuantLib's payoffKIKO, and whether Abel's bound on its dropped tail is within 1e-11 of its sum
        private (double Value, bool Converged) QuantLibKiko(double cash, DoubleBarrierType barrierType)
        {
            double barrierLo = this.barrierLo;
            double barrierHi = this.barrierHi;
            if (barrierType == DoubleBarrierType.KOKI)
            {
                (barrierLo, barrierHi) = (barrierHi, barrierLo);
            }

            double b = r - q;
            double alpha = -0.5 * ((2 * b / sigmaq) - 1);
            double beta = (-0.25 * Math.Pow((2 * b / sigmaq) - 1, 2)) - (2 * r / sigmaq);
            double z = Math.Log(barrierHi / barrierLo);
            double logSL = Math.Log(spot / barrierLo);

            double tot = 0;
            for (int i = 1; i < KikoIterations; ++i)
            {
                double factor = Math.Pow(i * Math.PI / z, 2) - beta;
                double term1 = (beta - (Math.Pow(i * Math.PI / z, 2) * Math.Exp(-0.5 * factor * variance))) / factor;
                double term2 = Math.Sin(i * Math.PI / z * logSL);
                double term = 2.0 / (i * Math.PI) * term1 * term2;
                tot += term;
            }

            tot += 1 - (logSL / z);
            double sum = tot;
            tot *= cash * Math.Pow(spot / barrierLo, alpha);
            double value = Max(tot, 0.0);
            if (!double.IsFinite(tot))
            {
                return (value, false);
            }

            // Abel's bound on the β/(ω² − β) part, whose coefficients decrease, |Σ aᵢ sin(iθ)| <= |a_N| / |sin(θ/2)|,
            // and the envelope of the Gaussian part; on QuantLib's sum before its factor
            double theta = Math.PI * logSL / z;
            double wN = KikoIterations * Math.PI / z;
            if (!((wN * wN) > 2.0 * beta))
            {
                return (value, false);
            }

            double aN = Math.Abs(2.0 / (KikoIterations * Math.PI) * beta / ((wN * wN) - beta));
            double abel = aN / Math.Abs(Math.Sin(theta / 2.0));
            double Gaussian(int i)
            {
                double w = i * Math.PI / z;
                return 2.0 / (i * Math.PI) * (w * w) * Math.Exp(-0.5 * ((w * w) - beta) * variance) / Math.Abs((w * w) - beta);
            }

            double gaussian = EnvelopeTail(Gaussian, KikoIterations, 0.0);
            return (value, abel + gaussian <= TailBound * Math.Abs(sum));
        }

        // The sum of an envelope's terms from the first dropped one on, past its peak (the index past which it is
        // decreasing) until a term is below machine epsilon of the sum; +∞ where it doesn't settle within its cap
        private static double EnvelopeTail(Func<int, double> term, int first, double peak)
        {
            double sum = 0.0;
            for (int i = first; i < first + MaxEnvelopeTerms; ++i)
            {
                double t = term(i);
                if (!double.IsFinite(t))
                {
                    return double.PositiveInfinity;
                }

                sum += t;
                if (i > peak && t <= NumericLimits.QL_EPSILON * sum)
                {
                    return sum;
                }
            }

            return double.PositiveInfinity;
        }

        // The image route: the cash part of QuantLib's double-barrier image series over [lo, hi], its terms in logs and
        // its normal differences from the small tail
        private double ImageCash(double lo, double hi)
        {
            double sd = Math.Sqrt(variance);
            double b = r - q;
            double mu1 = (2 * b / sigmaq) + 1;
            double bsigma = (b + (sigmaq / 2.0)) * residualTime / sd;
            return AnalyticDoubleBarrierEngine.ImageSum(
                Math.Log(spot), Math.Log(barrierLo), Math.Log(barrierHi), lo, hi, sd, bsigma, mu1 - 2, -sd, 5);
        }

        // The absorption flux of QuantLib's image-series killed density at the barrier: KIKO at the lower barrier, ∫₀ᵀ
        // e^(−rt) (σ²/2) ∂ᵧp(t; x, 0) dt, each image paired with its reflection, so nothing cancels; KOKI the same
        // mirrored, Z − x for x and −μ for μ. The time integral runs in log time between the two roots of (d − m t)² =
        // 1400σ²t, d the spot's log distance to the barrier and m the drift toward it, past which the nearest image is
        // below e^(−700), on the first-estimate scale at 1e-12
        private double Flux(DoubleBarrierType barrierType)
        {
            double z = Math.Log(barrierHi / barrierLo);
            double x0 = Math.Log(spot / barrierLo);
            double mu = r - q - (sigmaq / 2.0);
            bool kiko = barrierType == DoubleBarrierType.KIKO;
            double x = kiko ? x0 : z - x0;
            double drift = kiko ? mu : -mu;

            // the roots, with d = x and m the drift toward the barrier, −drift
            double d = x;
            double m = -drift;
            double bigB = (2 * d * m) + (1400 * sigmaq);
            double disc = (bigB * bigB) - (4 * m * m * d * d);
            if (!(bigB > 0.0) || disc < 0.0)
            {
                return 0.0;
            }

            double root = Math.Sqrt(disc);
            double tLo = 2 * d * d / (bigB + root);
            double tHi = m == 0.0 ? double.PositiveInfinity : (bigB + root) / (2 * m * m);
            if (!(tLo < residualTime))
            {
                return 0.0;
            }

            double upper = Min(tHi, residualTime);
            double Integrand(double u)
            {
                double t = Math.Exp(u);
                return t * FluxDensity(t, x, z, drift);
            }

            return GaussKronrodAdaptive.IntegrateOnFirstEstimate(
                Integrand, Math.Log(tLo), Math.Log(upper), FluxTolerance, MaxFluxEvaluations);
        }

        // e^(−rt) Σₙ (x + 2nZ)/√(2πσ²t³) exp(−(x + 2nZ + μt)²/(2σ²t) + 2nZμ/σ²), the images out to ±(10σ√t + Z)
        private double FluxDensity(double t, double x, double z, double drift)
        {
            double s2t = sigmaq * t;
            int images = (int)Math.Ceiling(((10.0 * Math.Sqrt(s2t)) + z) / (2.0 * z));
            double logNorm = -0.5 * Math.Log(2.0 * Math.PI * sigmaq * t * t * t);
            double sum = 0.0;
            for (int n = -images; n <= images; ++n)
            {
                double c = x + (2 * n * z);
                double e = (-((c + (drift * t)) * (c + (drift * t))) / (2.0 * s2t)) + (2 * n * z * drift / sigmaq);
                sum += c * Math.Exp(e + logNorm);
            }

            return Math.Exp(-r * t) * sum;
        }

        // Below machine-epsilon variance the log-spot moves at the carry, so it reaches a barrier, if at all, at
        // ln(H/S)/(r − q): KIKO pays at the lower one's touch, KOKI at the upper's, where the forward reaches it
        private double DeterministicHit(double cash, DoubleBarrierType barrierType)
        {
            double forward = spot * dividendDiscount / discount;
            double barrier;
            if (barrierType == DoubleBarrierType.KIKO)
            {
                if (!(forward <= barrierLo))
                {
                    return 0.0;
                }

                barrier = barrierLo;
            }
            else
            {
                if (!(forward >= barrierHi))
                {
                    return 0.0;
                }

                barrier = barrierHi;
            }

            double hitTime = Math.Log(barrier / spot) / (r - q);
            return cash * Math.Exp(-r * hitTime);
        }

        // std::min and std::max, which return their first argument where neither is less than the other
        private static double Min(double a, double b) => b < a ? b : a;

        private static double Max(double a, double b) => a < b ? b : a;
    }
}
