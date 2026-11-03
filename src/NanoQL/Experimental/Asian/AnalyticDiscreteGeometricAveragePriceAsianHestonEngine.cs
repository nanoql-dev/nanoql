// SPDX-FileCopyrightText: Copyright (C) 2020 Jack Gillett
// SPDX-FileCopyrightText: Copyright (C) 2020, 2021 Jack Gillett
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/asian/analytic_discr_geom_av_price_heston.hpp ql/experimental/asian/analytic_discr_geom_av_price_heston.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// Kim, Kim, Kim and Wee's analytic engine for discrete geometric average-price Asian options under the Heston model:
/// the joint characteristic function of the log geometric average, QuantLib's <c>Phi</c>, inverted by a Fourier
/// integral.
/// </summary>
/// <remarks>
/// <para>
/// The value, with QuantLib's additional results ("dcf", "s0", "strike", "expiryTime", "term1", "term2",
/// "xiRightLimit", "fixingTimes", "tauK", "adjustedStrike", "prefactor", "kStar"); "term2" is the integral's term on the
/// route below, so the value is D × prefactor × (±term1 + term2), as QuantLib's. An arithmetic option is priced as
/// the geometric one on its fixings to come, unseasoned, QuantLib's use of the engine as the arithmetic Monte Carlo's
/// control variate.
/// </para>
/// <para>
/// Documented divergences. QuantLib cuts its integral at ξ 100 on 128 Gauss-Legendre points, before the integrand
/// decays at short maturities and small variance (−51% on a ten-day Asian at v₀ 0.0025;
/// <c>Divergences/HestonGeometricAsianCutoffTests.cs</c>), and leaves out [0, 1e-8 ξ_max]. Here:
/// </para>
/// <list type="bullet">
/// <item><description><c>Phi</c> in a stable form, ω̃ through tanh and the logs of the Riccati factors through log1p,
/// where QuantLib's terms of order 1/σ² cancel at small vol of vol and its cosh and sinh overflow at long maturities
/// (<c>Divergences/HestonGeometricAsianSmallParameterTests.cs</c>); below a vol of vol of 1e-4, a cubic in σ through
/// the Black-Scholes limit at σ = 0; at a vanishing square root, γ = 0, the forms' limits (κ = 0).</description></item>
/// <item><description>Before the integral, a switch: a point mass at v₀ = θ = 0, priced at its intrinsic; and the
/// discounted intrinsic where a Chernoff bound on the extrinsic value, from <c>Phi</c> at real s up to the moment
/// explosion, is at or below 1e-12 × max(the discounted intrinsic, D F_G/100)
/// (<c>Divergences/HestonGeometricAsianIntrinsicTests.cs</c>).</description></item>
/// <item><description>The integral on Carr and Madan's damped contour: the out-of-the-money option on its own strip
/// where that strip is at least 0.5 wide, else on Lewis's middle strip, the other by parity; Lord and Kahl's α inside
/// the half of the strip next to its pole; the explosions by the closed-form first-zero test of the Riccati factors
/// (<c>Divergences/HestonGeometricAsianExplosionTests.cs</c>); cut where <c>Phi</c>'s modulus falls below 1e-14 of its
/// value on the contour's axis, or at the engine's ξ_max where that is later; split at the decades of ξ and at the
/// peak's width below 1; on NanoQL's adaptive Kronrod rule at the price's tolerance scale, max(the price, the lower
/// bound, D F_G/100), in two passes, accepted where two breakpoint sets agree within it, a third on disagreement, each
/// set capped at 1e6 evaluations (<c>Divergences/HestonGeometricAsianStrikeScaleTests.cs</c>).</description></item>
/// <item><description>Refused, with "variance too small for the Heston geometric engine near the forward", where the
/// switch doesn't fire and the cut lies past ξ 1e7, which no checkable route reaches.</description></item>
/// <item><description>F_G exact, S₀ D_q(t)/D_r(t), where every fixing time is one t and none is past, where
/// <c>Phi</c>(1, 0) is 0/0; the call floored at D max(F_G − K, 0), the put at its mirror.</description></item>
/// </list>
/// <para>
/// Times run on the engine's clock, <see cref="FdmHestonOp.Clock"/>, from the earliest reference date of the process's
/// curves, each discount read at its instants. A fixing before the evaluation date is left out, as the Monte Carlo
/// engines leave it, where QuantLib reads its negative time into the intervals; one from the evaluation date to the
/// clock's origin is taken at the origin, as a past fixing at the spot
/// (<c>Divergences/AsianLateSettlementTests.cs</c>).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public static class AnalyticDiscreteGeometricAveragePriceAsianHestonEngine
{
    private const string TooSmall = "variance too small for the Heston geometric engine near the forward";
    private const string CapReached = "maximum number of function evaluations exceeded";
    private const double SmallSigma = 1e-4;
    private const double CutRefused = 1e7;
    private const int Cap = 1000000;

    /// <summary>Prices a discrete geometric average-price Asian option under the Heston model.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Heston process.</param>
    /// <param name="xiRightLimit">QuantLib's upper limit of the Fourier integral; the integral runs past it where the
    /// integrand hasn't decayed there.</param>
    /// <returns>The option's results: the value and QuantLib's additional results; the expired results when the option
    /// has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't plain vanilla, the running product isn't positive, the expiry is in
    /// the past, the option's past fixings fail its checks, the variance is too small near the forward, or the integral
    /// can't reach its tolerance.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option, EvaluationContext context, HestonProcess process, double xiRightLimit = 100.0)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        Date origin = FdmHestonOp.Clock(process).ReferenceDate;
        DiscreteAveragingAsianArguments arguments = option.Arguments(context).AtOrigin(
            context.EvaluationDate, d => d < origin, process.S0);
        return option.FetchResults(Body(option, arguments, process, xiRightLimit));
    }

    private static OneAssetOptionResults Body(
        DiscreteAveragingAsianOption option, DiscreteAveragingAsianArguments arguments, HestonProcess process, double xiRightLimit)
    {
        /* this engine cannot really check for the averageType==Geometric
           since it can be used as control variate for the Arithmetic version
        QL_REQUIRE(arguments_.averageType == Average::Geometric,
                   "not a geometric average option");
        */
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("not an European Option");
        }

        double runningLog;
        int pastFixings;
        if (arguments.AverageType == AverageType.Geometric)
        {
            // the product by its log where it leaves double's range
            if (!(arguments.RunningAccumulator > 0.0 || arguments.RunningLogSum > double.NegativeInfinity))
            {
                throw new NanoQLException(
                    $"positive running product required: {arguments.RunningAccumulator.ToString(CultureInfo.InvariantCulture)} not allowed");
            }

            runningLog = arguments.RunningLog;
            pastFixings = arguments.PastFixings;
        }
        else
        {
            // it is being used as control variate
            runningLog = 0.0;
            pastFixings = 0;
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        double strike = payoff.Strike;
        Date exercise = option.Exercise.LastDate();

        TermStructure clock = FdmHestonOp.Clock(process);
        double expiryTime = clock.TimeFromReference(exercise);
        if (!(expiryTime >= 0.0))
        {
            throw new NanoQLException("Expiry Date cannot be in the past");
        }

        YieldTermStructure riskFreeRate = process.RiskFreeRate;
        YieldTermStructure dividendYield = process.DividendYield;
        double expiryDcf = riskFreeRate.DiscountAt(clock, expiryTime);

        double startTime = 0.0;
        var fixingTimes = new List<double>(arguments.FixingDates.Length + pastFixings);
        foreach (Date fixingDate in arguments.FixingDates)
        {
            double t = clock.TimeFromReference(fixingDate);
            if (t >= 0.0)
            {
                fixingTimes.Add(t);
            }
        }

        fixingTimes.Sort();
        var tauK = new List<double>(fixingTimes.Count + pastFixings + 2) { startTime };
        tauK.AddRange(fixingTimes);
        tauK.Add(expiryTime);

        // In the paper, seasoned asians are dealt with by letting the start time variable be greater
        // than 0. We can achieve the same by fixing the start time to 0.0, but attaching 'dummy'
        // fixing times at t=-1 for each past fixing, at the front of the fixing times arrays
        fixingTimes.InsertRange(0, Enumerable.Repeat(-1.0, pastFixings));
        tauK.InsertRange(0, Enumerable.Repeat(-1.0, pastFixings));

        int kStar = pastFixings;

        // Need the log of some discount factors to calculate the r-adjusted a factor (Eq 16)
        double LogForward(double t) => -Math.Log(riskFreeRate.DiscountAt(clock, t) / dividendYield.DiscountAt(clock, t));
        double trT = LogForward(startTime);
        double bigTrT = LogForward(expiryTime);
        var tkrTk = new double[fixingTimes.Count];
        for (int i = 0; i < fixingTimes.Count; i++)
        {
            tkrTk[i] = fixingTimes[i] < 0 ? 1.0 : LogForward(fixingTimes[i]);
        }

        // To account for seasoning, we need to calculate an 'adjusted' strike (Eq 6)
        double prefactor = Math.Exp(runningLog / fixingTimes.Count);
        double adjustedStrike = strike / prefactor;

        var model = new CharacteristicFunction(
            process.V0, process.Kappa, process.Theta, process.Sigma, process.Rho, Math.Log(process.S0), [.. fixingTimes], [.. tauK],
            kStar, trT, bigTrT, tkrTk, startTime, expiryTime);

        // F_G exact where every fixing time is one t and none is past, where Phi(1, 0) is 0/0
        double forward;
        int future = fixingTimes.Count - kStar;
        if (kStar == 0 && future > 0 && fixingTimes[0] == fixingTimes[^1])
        {
            forward = process.S0 * dividendYield.DiscountAt(clock, fixingTimes[0]) / riskFreeRate.DiscountAt(clock, fixingTimes[0]);
        }
        else
        {
            forward = Math.Exp(model.LogPhi(1.0).Real);
        }

        bool call = payoff.OptionType == OptionType.Call;
        double undiscounted = new Route(model, adjustedStrike, expiryDcf, forward, call, xiRightLimit).Price() / expiryDcf;

        // Calculate the two terms in eq (23): QuantLib's first, the integral's term the rest of the price
        double term1 = 0.5 * (forward - adjustedStrike);
        double term2 = call ? undiscounted - term1 : undiscounted + term1;

        // Apply the payoff functions
        double value = expiryDcf * prefactor * (call ? term1 + term2 : -term1 + term2);

        return new OneAssetOptionResults
        {
            NPV = value,
            AdditionalResults = ImmutableSortedDictionary.CreateRange(new KeyValuePair<string, object>[]
            {
                new("dcf", expiryDcf),
                new("s0", process.S0),
                new("strike", strike),
                new("expiryTime", expiryTime),
                new("term1", term1),
                new("term2", term2),
                new("xiRightLimit", xiRightLimit),
                new("fixingTimes", fixingTimes.ToImmutableArray()),
                new("tauK", tauK.ToImmutableArray()),
                new("adjustedStrike", adjustedStrike),
                new("prefactor", prefactor),
                new("kStar", kStar),
            }),
        };
    }

    // The price on the damped contour, the switch before it, for one strike, discount and forward.
    private sealed class Route(CharacteristicFunction model, double strike, double discount, double forward, bool call, double xiRightLimit)
    {
        private readonly double logStrike = Math.Log(strike);

        public double Price()
        {
            double lower = call ? discount * Math.Max(forward - strike, 0.0) : discount * Math.Max(strike - forward, 0.0);

            // with v0 = theta = 0 the average is a point mass
            if (model.PointMass)
            {
                return lower;
            }

            double unit = discount * forward / 100.0;
            double sStar = model.Explosion(+1), sMinus = model.Explosion(-1);
            if (Chernoff(sStar, sMinus) <= 1e-12 * Math.Max(lower, unit))
            {
                return lower;
            }

            (bool otmCall, Strip strip, double a1) = Contour(sStar, sMinus);
            double lp0 = model.LogPhi(a1).Real;
            if (DecayCut(a1, lp0) > CutRefused)
            {
                throw new NanoQLException(TooSmall);
            }

            double cut = Math.Max(DecayCut(a1, lp0), xiRightLimit);
            double Assemble(double integral)
            {
                double otm = strip.Middle ? integral + (otmCall ? discount * forward : discount * strike) : integral;
                if (call == otmCall)
                {
                    return otm;
                }

                return call ? otm + (discount * (forward - strike)) : otm - (discount * (forward - strike));
            }

            double estimate = Assemble(Integral(a1, lp0, strip, cut, 1e-8 * discount * forward, 0.0));
            double r7 = 1e-10 * Math.Max(Math.Max(Math.Abs(estimate), lower), unit);
            var values = new List<double>(3);
            foreach (double offset in (double[])[0.0, 0.5, 0.25])
            {
                values.Add(Assemble(Integral(a1, lp0, strip, cut, 0.5 * r7, offset)));
                for (int i = 0; i < values.Count - 1; i++)
                {
                    if (Math.Abs(values[i] - values[^1]) <= r7)
                    {
                        return Math.Max(0.5 * (values[i] + values[^1]), lower);
                    }
                }
            }

            throw new NanoQLException(CapReached);
        }

        // D x the Chernoff bound on the out-of-the-money option's value, the extrinsic of either, in logs; infinite
        // where its strip is empty
        private double Chernoff(double sStar, double sMinus)
        {
            Func<double, double> f;
            double lo, hi;
            if (strike >= forward)
            {
                // E[(G - K)+] <= E[G^s](s-1)^(s-1)/(s^s K^(s-1)), s in (1, s*)
                double width = sStar - 1;
                if (width < 1e-12)
                {
                    return double.PositiveInfinity;
                }

                f = s => model.LogPhi(s).Real + ((s - 1) * Math.Log(s - 1)) - (s * Math.Log(s)) - ((s - 1) * logStrike);
                lo = 1 + (1e-9 * width);
                hi = 1 + ((1 - 1e-9) * width);
            }
            else
            {
                // E[(K - G)+] <= E[G^-s] K^(1+s) s^s/(1+s)^(1+s), s in (0, -s_-)
                double width = -sMinus;
                if (width < 1e-12)
                {
                    return double.PositiveInfinity;
                }

                f = s => model.LogPhi(-s).Real + ((1 + s) * logStrike) + (s * Math.Log(s)) - ((1 + s) * Math.Log(1 + s));
                lo = 1e-9 * width;
                hi = (1 - 1e-9) * width;
            }

            double best = double.PositiveInfinity;
            for (int x = 0; x <= 400; x++)
            {
                double quadratic = lo + ((hi - lo) * (x / 400.0) * (x / 400.0));
                double geometric = lo * Math.Pow(hi / lo, x / 400.0);
                foreach (double s in (double[])[quadratic, geometric])
                {
                    double v = f(s);
                    if (double.IsFinite(v) && v < best)
                    {
                        best = v;
                    }
                }
            }

            return double.IsFinite(best) ? discount * Math.Exp(best) : double.PositiveInfinity;
        }

        // The strip and Lord and Kahl's alpha + 1 on it
        private (bool OtmCall, Strip Strip, double A1) Contour(double sStar, double sMinus)
        {
            bool otmCall = strike >= forward;
            double own = otmCall ? sStar - 1 : -sMinus;
            Strip strip;
            double a, b;
            if (own >= 0.5)
            {
                if (otmCall)
                {
                    strip = new Strip(false, 1.0, sStar);
                    a = 1 + 1e-3;
                    b = 1 + (own / 2);
                }
                else
                {
                    strip = new Strip(false, sMinus, 0.0);
                    a = sMinus / 2;
                    b = -1e-3;
                }
            }
            else
            {
                strip = new Strip(true, 0.0, 1.0);
                a = 0.02;
                b = 0.98;
            }

            double Objective(double a1) => (-(a1 - 1) * logStrike) + model.LogPhi(a1).Real - Math.Log(Math.Abs((a1 - 1) * a1));

            var grid = new List<(double Value, double A1)>(201);
            for (int x = 0; x <= 200; x++)
            {
                double t = x / 200.0;
                double c = (b - a) < 10 || a * b <= 0
                    ? a + ((b - a) * t)
                    : Math.CopySign(Math.Exp(Math.Log(Math.Abs(a)) + ((Math.Log(Math.Abs(b)) - Math.Log(Math.Abs(a))) * t)), a);
                double v = Objective(c);
                if (double.IsFinite(v))
                {
                    grid.Add((v, c));
                }
            }

            int i = 0;
            for (int j = 1; j < grid.Count; j++)
            {
                if (grid[j].Value < grid[i].Value)
                {
                    i = j;
                }
            }

            double lo = grid[Math.Max(i - 1, 0)].A1, hi = grid[Math.Min(i + 1, grid.Count - 1)].A1;
            double a1 = lo == hi ? grid[i].A1 : GoldenSection(Objective, Math.Min(lo, hi), Math.Max(lo, hi), 1e-9);
            return (otmCall, strip, a1);
        }

        // where Phi's modulus on the contour falls below 1e-14 of its value on the axis
        private double DecayCut(double a1, double lp0)
        {
            double cut = 1.0;
            while (cut < 1e13)
            {
                double decay = model.LogPhi(new Complex(a1, cut)).Real - lp0;
                if (!double.IsFinite(decay) || decay < Math.Log(1e-14))
                {
                    break;
                }

                cut *= 1.5;
            }

            return cut;
        }

        // the damped integral over [0, cut], split at the decades of xi and at the peak's width below 1, the
        // breakpoints shifted by an offset in the exponent, each piece on its share of the tolerance, the set capped at
        // 1e6 evaluations
        private double Integral(double a1, double lp0, Strip strip, double cut, double tolerance, double offset)
        {
            double alpha = a1 - 1;
            double F(double x)
            {
                var z = new Complex(a1, x);
                Complex value = Complex.Exp(model.LogPhi(z) - lp0 - new Complex(0.0, x * logStrike))
                    / (new Complex(alpha, x) * z);
                return value.Real;
            }

            double w = Math.Min(Math.Abs(alpha), Math.Abs(a1));
            if (!strip.Middle)
            {
                w = Math.Min(w, strip.Lower < 0 ? a1 - strip.Lower : strip.Upper - a1);
            }

            var points = new SortedSet<double>();
            for (int k = -8; k < 30; k++)
            {
                double p = w * Math.Pow(10.0, k + offset);
                if (p < Math.Min(1.0, cut))
                {
                    points.Add(p);
                }
            }

            for (int k = -1; k < 14; k++)
            {
                double p = Math.Pow(10.0, k + offset);
                if (p >= 1.0 && p < cut)
                {
                    points.Add(p);
                }
            }

            double[] edges = [0.0, .. points, cut];
            double scale = Math.Exp(Math.Log(discount) - (alpha * logStrike) + lp0 - Math.Log(Math.PI));
            double pieceTolerance = tolerance / scale / edges.Length;
            double total = 0.0;
            int evaluations = 0;
            for (int i = 0; i + 1 < edges.Length; i++)
            {
                int left = Cap - evaluations;
                if (left < 15)
                {
                    throw new NanoQLException(CapReached);
                }

                // the rule's absolute tolerance is refused at or below machine epsilon: the piece on its own tolerance's
                // unit, the same decisions
                IntegrationResult piece = new GaussKronrodAdaptive(1.0, left).IntegrateWithStatistics(
                    x => F(x) / pieceTolerance, edges[i], edges[i + 1]);
                total += piece.Value * pieceTolerance;
                evaluations += piece.NumberOfEvaluations;
            }

            return scale * total;
        }
    }

    // a strip of the contour: the middle strip, alpha in (-1, 0), or an option's own between its bounds in alpha + 1
    private readonly record struct Strip(bool Middle, double Lower, double Upper);

    // the minimum of a unimodal function on [lo, hi] to a tolerance
    private static double GoldenSection(Func<double, double> f, double lo, double hi, double tolerance)
    {
        double ratio = (Math.Sqrt(5.0) - 1.0) / 2.0;
        double c = hi - (ratio * (hi - lo)), d = lo + (ratio * (hi - lo));
        double fc = f(c), fd = f(d);
        while (hi - lo > tolerance)
        {
            if (!(fd < fc))
            {
                hi = d;
                d = c;
                fd = fc;
                c = hi - (ratio * (hi - lo));
                fc = f(c);
            }
            else
            {
                lo = c;
                c = d;
                fc = fd;
                d = lo + (ratio * (hi - lo));
                fd = f(d);
            }
        }

        return 0.5 * (lo + hi);
    }

    // QuantLib's Phi on one option's fixings, in the stable form, the cubic bridge below a vol of vol of 1e-4 (a
    // per-call working object)
    private sealed class CharacteristicFunction
    {
        private readonly double v0, kappa, theta, sigma, rho, logS0;
        private readonly double[] tN, tauK, tkrTk;
        private readonly int kStar;
        private readonly double trT, bigTrT, t, bigT;
        private readonly CharacteristicFunction[]? bridge;

        public CharacteristicFunction(
            double v0,
            double kappa,
            double theta,
            double sigma,
            double rho,
            double logS0,
            double[] tN,
            double[] tauK,
            int kStar,
            double trT,
            double bigTrT,
            double[] tkrTk,
            double t,
            double bigT)
        {
            this.v0 = v0;
            this.kappa = kappa;
            this.theta = theta;
            this.sigma = sigma;
            this.rho = rho;
            this.logS0 = logS0;
            this.tN = tN;
            this.tauK = tauK;
            this.kStar = kStar;
            this.trT = trT;
            this.bigTrT = bigTrT;
            this.tkrTk = tkrTk;
            this.t = t;
            this.bigT = bigT;
            if (sigma < SmallSigma)
            {
                bridge =
                [
                    With(SmallSigma),
                    With(2 * SmallSigma),
                    With(3 * SmallSigma),
                ];
            }
        }

        // with v0 = theta = 0 the variance stays 0 and the average is a point mass
        public bool PointMass => v0 == 0.0 && theta == 0.0;

        public Complex LogPhi(Complex s) => LogPhi(s, Complex.Zero);

        // ln Phi(s, w): the stable form, or below sigma 1e-4 the cubic in sigma through the Black-Scholes limit at 0 and
        // the stable form at 1e-4, 2e-4 and 3e-4
        public Complex LogPhi(Complex s, Complex w)
        {
            if (bridge is null)
            {
                return StableLogPhi(s, w);
            }

            double[] nodes = [0.0, SmallSigma, 2 * SmallSigma, 3 * SmallSigma];
            Complex[] values = [BlackScholesLogPhi(s, w), bridge[0].StableLogPhi(s, w), bridge[1].StableLogPhi(s, w), bridge[2].StableLogPhi(s, w)];
            Complex result = Complex.Zero;
            for (int i = 0; i < 4; i++)
            {
                double weight = 1.0;
                for (int j = 0; j < 4; j++)
                {
                    if (j != i)
                    {
                        weight *= (sigma - nodes[j]) / (nodes[i] - nodes[j]);
                    }
                }

                result += weight * values[i];
            }

            return result;
        }

        // The moment explosion past 1 (side +1) or below 0 (side -1) at real s: the last admitted point on quarter
        // decades of s - 1 or -s from 1e-12 to 1e12, refined by bisection
        public double Explosion(int side)
        {
            double start = side > 0 ? 1.0 : 0.0;
            double last = start;
            for (int x = -48; x <= 48; x++)
            {
                double s = start + (side * Math.Pow(10.0, x / 4.0));
                if (Admitted(s))
                {
                    last = s;
                }
                else
                {
                    double lo = last, hi = s;
                    for (int i = 0; i < 80; i++)
                    {
                        double mid = 0.5 * (lo + hi);
                        if (Admitted(mid))
                        {
                            lo = mid;
                        }
                        else
                        {
                            hi = mid;
                        }
                    }

                    return lo;
                }
            }

            return last;
        }

        // the closed-form first zero of each interval's Riccati factor at real s: admitted where no interval's end
        // reaches it; under the cubic bridge, where every node's stable form admits it, the bridge reading them all
        private bool Admitted(double s) =>
            bridge is null ? AdmittedHere(s) : AdmittedHere(s) && bridge.All(node => node.AdmittedHere(s));

        private bool AdmittedHere(double s)
        {
            if (sigma == 0.0)
            {
                return true;
            }

            double s2 = sigma * sigma;
            int n = tN.Length;
            Complex omegaNext = Omega(s, 0, n + 1, n);
            for (int k = n; k >= kStar; k--)
            {
                double tau = tauK[k + 1] - tauK[k];
                Complex z1 = Z(s, 0, k + 1, n);
                Complex z2 = omegaNext;
                double z1r = z1.Real, z2r = z2.Real;
                if (!double.IsFinite(z2r) || !double.IsFinite(z1r))
                {
                    return false;
                }

                double c = kappa - (z2r * s2);
                double g2 = (kappa * kappa) - (2 * z1r * s2);
                if (g2 > 0)
                {
                    double gg = Math.Sqrt(g2);
                    if (c < -gg && tau * gg / 2 >= Math.Atanh(-gg / c))
                    {
                        return false;
                    }
                }
                else if (g2 < 0)
                {
                    double gg = Math.Sqrt(-g2);
                    if (tau * gg / 2 >= Math.Atan2(gg, -c))
                    {
                        return false;
                    }
                }
                else if (c < 0 && tau >= -2 / c)
                {
                    return false;
                }

                omegaNext = OmegaTilde(s, 0, k, n, z1, z2, tau);
            }

            return true;
        }

        private CharacteristicFunction With(double volOfVol) =>
            new(v0, kappa, theta, volOfVol, rho, logS0, tN, tauK, kStar, trT, bigTrT, tkrTk, t, bigT);

        private Complex Z(Complex s, Complex w, int k, int n)
        {
            Complex a = ((n - k + 1) * s) + (n * w);
            return ((2 * rho * kappa) - sigma) * a / (2 * sigma * n) + ((1 - (rho * rho)) * a * a / (2.0 * n * n));
        }

        private Complex Omega(Complex s, Complex w, int k, int n)
        {
            if (k == kStar)
            {
                return Complex.Zero;
            }
            else if (k == n + 1)
            {
                return rho * w / sigma;
            }
            else
            {
                return rho * s / (sigma * n);
            }
        }

        // QuantLib's omega_tilde(k) from omega_tilde(k + 1) = z2 and z1 = z(k + 1), in the overflow-safe form: omega +
        // [z2 + (2 z1 - kappa z2) tanh(h)/gamma]/[1 + c tanh(h)/gamma], h = tau gamma/2, c = kappa - z2 sigma^2,
        // tanh(h)/gamma -> tau/2 where gamma = 0
        private Complex OmegaTilde(Complex s, Complex w, int k, int n, Complex z1, Complex z2, double tau)
        {
            double s2 = sigma * sigma;
            Complex g = Gamma(z1);
            Complex c = kappa - (z2 * s2);
            Complex ratio = g == Complex.Zero ? new Complex(0.5 * tau, 0.0) : SafeTanh(0.5 * tau * g) / g;
            return Omega(s, w, k, n) + ((z2 + (((2 * z1) - (kappa * z2)) * ratio)) / (1 + (c * ratio)));
        }

        // gamma = sqrt(kappa^2 - 2 z1 sigma^2), its real part non-negative
        private Complex Gamma(Complex z1)
        {
            Complex g = Complex.Sqrt((kappa * kappa) - (2 * z1 * sigma * sigma));
            return g.Real < 0 ? -g : g;
        }

        // QuantLib's Phi in logs: a + v0 omega_tilde(k*) + (term3 - term4), the last as kappa theta sum[tau 2 z1/(kappa
        // + gamma) - (2/sigma^2) log1p(sigma^2 X)], X = (1 - e^(-tau gamma))(2 z1/(kappa + gamma) - z2)/(2 gamma), its
        // limits where gamma = 0
        private Complex StableLogPhi(Complex s, Complex w)
        {
            int n = tN.Length;
            double s2 = sigma * sigma;
            Complex omegaNext = Omega(s, w, n + 1, n);
            Complex sum = Complex.Zero;
            for (int k = n; k >= kStar; k--)
            {
                double tau = tauK[k + 1] - tauK[k];
                Complex z1 = Z(s, w, k + 1, n);
                Complex z2 = omegaNext;
                Complex g = Gamma(z1);
                Complex kg = kappa + g;
                Complex q1 = kg == Complex.Zero ? Complex.Zero : 2 * z1 / kg;
                Complex x = g == Complex.Zero
                    ? tau * (q1 - z2) / 2
                    : (1 - Complex.Exp(-tau * g)) * (q1 - z2) / (2 * g);
                sum += (tau * q1) - (2 / s2 * Log1p(s2 * x));
                omegaNext = OmegaTilde(s, w, k, n, z1, z2, tau);
            }

            return A(s, w) + (v0 * omegaNext) + (kappa * theta * sum);
        }

        // Eq (16) modified for non-constant rates
        private Complex A(Complex s, Complex w)
        {
            double nn = tN.Length;
            double temp = -rho * kappa * theta / sigma;
            double summation = 0.0, summation2 = 0.0;
            for (int i = kStar + 1; i <= tN.Length; i++)
            {
                summation += tN[i - 1];
                summation2 += tkrTk[i - 1];
            }

            Complex term1 = ((s * (nn - kStar) / nn) + w) * (logS0 - (rho * v0 / sigma) - (t * temp) - trT);
            Complex term2 = (temp * ((s * summation / nn) + (w * bigT))) + (w * bigTrT) + (summation2 * s / nn);
            return term1 + term2;
        }

        // the Black-Scholes limit at sigma = 0: a deterministic variance V(t) = theta t - (v0 - theta) expm1(-kappa t)/
        // kappa (v0 t at kappa = 0), ln G and ln S_T normal
        private Complex BlackScholesLogPhi(Complex s, Complex w)
        {
            double V(double time) => kappa == 0.0
                ? v0 * time
                : (theta * time) - ((v0 - theta) * Expm1Functions.StdExpm1(-kappa * time) / kappa);

            double nn = tN.Length;
            double mean = 0.0, variance = 0.0, covariance = 0.0;
            for (int i = kStar; i < tN.Length; i++)
            {
                mean += logS0 + tkrTk[i] - (0.5 * V(tN[i]));
                covariance += V(tN[i]);
                for (int j = kStar; j < tN.Length; j++)
                {
                    variance += V(Math.Min(tN[i], tN[j]));
                }
            }

            mean /= nn;
            variance /= nn * nn;
            covariance /= nn;
            double meanT = logS0 + bigTrT - (0.5 * V(bigT));
            return (s * mean) + (w * meanT) + (0.5 * ((s * s * variance) + (2 * s * w * covariance) + (w * w * V(bigT))));
        }

        // tanh(z) for Re z >= 0, (1 - e^(-2z))/(1 + e^(-2z)), which doesn't overflow
        private static Complex SafeTanh(Complex z)
        {
            Complex e = Complex.Exp(-2 * z);
            return (1 - e) / (1 + e);
        }

        // log(1 + z), its real part through log1p where |z| < 0.5, where 2x + |z|^2 > -1, the plain log elsewhere
        private static Complex Log1p(Complex z)
        {
            if (!(Complex.Abs(z) < 0.5))
            {
                return Complex.Log(1 + z);
            }

            double x = z.Real, y = z.Imaginary;
            double real = 0.5 * Log1pFunctions.Log1p((2 * x) + (x * x) + (y * y));
            double imaginary = Math.Atan2(y, 1 + x);
            return new Complex(real, imaginary);
        }
    }
}
