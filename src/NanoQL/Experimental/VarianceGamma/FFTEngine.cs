// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/fftengine.hpp ql/experimental/variancegamma/fftengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// The fast-Fourier-transform engine for European options (Carr and Madan, 1998, "Option valuation using the fast
/// Fourier transform", Journal of Computational Finance 2, 61-73): one transform prices every option of an expiry.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: an engine for another model derives it and returns, for each expiry, the model's
/// state there: its characteristic function, its discount factors and the strip of exponents with a finite moment.
/// Immutable: QuantLib's cache of precalculated prices is the list form's result (the S-xeq plan's XER1), so
/// <see cref="Calculate(IReadOnlyList{VanillaOption}, EvaluationContext)"/> returns each option's results in the list's
/// order, QuantLib's <c>precalculate</c> followed by each option's <c>NPV()</c>, one transform per expiry on the grid
/// QuantLib's map would hold; a single option is a list of one, QuantLib's uncached route. An expired option gets its
/// expired results without joining a transform. QuantLib's <c>update()</c> and <c>clone()</c> aren't ported: there is
/// no state to clear or separate.
/// </para>
/// <para>
/// Documented divergences (<c>Divergences/VarianceGammaFftTests.cs</c>). The grid is sized from the largest |log K| of the
/// expiry's strikes and at least 2^14 points, QuantLib's size wherever its own is at least 2^14 (every QuantLib case),
/// where QuantLib's size is undefined when every strike is below e^(−λ), and below 2^14 points, as for any strike
/// between 1 and about 60, its frequency spacing 2π/(λn) is too coarse for the transform to price (XER5 (xix); the
/// user's ruling at the S-xeq run's stop 6). The damping is QuantLib's 1.25, and its grid, wherever E[S^2.25] is finite
/// (every Black-Scholes market, every QuantLib case), the grid doubled at the same spacing until two successive grids
/// agree within 5e-5 of the spot at every strike, QuantLib's prices where its own grid agrees with the first doubling
/// and the finer grid's where a later one agrees, up to 2^22, past which a refusal: QuantLib's period λn aliases the
/// damped call's tail once σ√T passes about 1.2, an at-the-money call pricing −298.9 against 66.80 at σ√T 2 (the user's
/// ruling at the S-xeq run's stop 9); the same refusal where the damped tail's growth, ln E[S^2.25] − 2.25 ln E[S]
/// (α(α + 1)σ²T/2 under Black-Scholes), passes 18, σ√T 3.58, where the recovered prices carry a rounding error no grid
/// changes, so two grids agree while both are off (the code review's O2-1); otherwise half the strip's upper bound with the grid doubled
/// until α·nλ/2 ≥ 20, while the grid stays within 2^22 points; past that the put side, α + 1 = max(u_lo/2, −0.25)
/// inside the strip's lower part, the grid doubled from QuantLib's until two successive grids agree within 1e-8 of each
/// strike's larger of call and put, up to 2^22, the calls by QuantLib's parity; past that a refusal (XER5 (xviii), the
/// user's rulings of 2026-10-28 at about 10:22, 10:55 and 11:27).
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public abstract class FFTEngine
{
    private const double QuantLibAlpha = 1.25;
    private const int MaxPoints = 1 << 22;
    private const int MinPoints = 1 << 14;
    private const double DampedTail = 20.0;
    private const double PutSideFloor = -0.25;
    private const double GridAgreement = 1e-8;

    // the agreement of two successive grids at the damping 1.25, relative to the spot, where QuantLib's grid stands: its
    // own change on doubling is at most 4.0e-5 up to σ√T 1.1, 3.6e-4 and more where its period aliases (stop 9)
    private const double TailAgreement = 5e-5;

    // the damped tail's growth past which two grids agree on a rounding floor: 17.2 at σ√T 3.5 prices within 5e-5 of the
    // spot, 19.8 at 3.75 errs 3.3e-4 at a spot of 1 (the code review's O2-1)
    private const double TailGrowthBound = 18.0;

    private const string TailRefusal =
        "the FFT grid doesn't converge within 2^22 points at the damping 1.25: use the analytic engine";

    /// <summary>Initializes a new instance of the <see cref="FFTEngine"/> class.</summary>
    /// <param name="logStrikeSpacing">The grid's log-strike spacing λ.</param>
    protected FFTEngine(double logStrikeSpacing)
    {
        LogStrikeSpacing = logStrikeSpacing;
    }

    /// <summary>Gets the grid's log-strike spacing λ.</summary>
    public double LogStrikeSpacing { get; }

    /// <summary>Gets the underlying's value today.</summary>
    protected abstract double X0 { get; }

    /// <summary>Prices a European option: QuantLib's uncached route, a list of one.</summary>
    /// <param name="option">The option: European, on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException"> The exercise isn't European, the payoff isn't striked, the model refuses the
    /// market, or the grid can't resolve it. </exception>
    public OneAssetOptionResults Calculate(VanillaOption option, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(option);
        return Calculate([option], context)[0];
    }

    /// <summary>
    /// Prices a list of European options: QuantLib's <c>precalculate</c> on the list, then each option's <c>NPV()</c>,
    /// one transform per expiry.
    /// </summary>
    /// <param name="options">The options, each European on a striked payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>Each option's results in the list's order: the value; the expired results for an expired
    /// option.</returns>
    /// <exception cref="ArgumentNullException">An argument or an option is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException"> An exercise isn't European, a payoff isn't striked, the model refuses the
    /// market, or the grid can't resolve it. </exception>
    public ImmutableArray<OneAssetOptionResults> Calculate(IReadOnlyList<VanillaOption> options, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(context);

        // QuantLib's precalculate: the live options grouped by expiry date, each checked as QuantLib checks it
        var results = new OneAssetOptionResults?[options.Count];
        var byExpiry = new SortedDictionary<Date, List<int>>();
        for (int k = 0; k < options.Count; k++)
        {
            VanillaOption option = options[k] ?? throw new ArgumentNullException(nameof(options));
            if (option.IsExpired(context))
            {
                results[k] = option.SetupExpired();
                continue;
            }

            option.Validate();
            if (!(option.Exercise.Type == ExerciseType.European))
            {
                throw new NanoQLException("not an European Option");
            }

            if (option.Payoff is not StrikedTypePayoff)
            {
                throw new NanoQLException("non-striked payoff given");
            }

            Date expiry = option.Exercise.LastDate();
            if (!byExpiry.TryGetValue(expiry, out List<int>? members))
            {
                members = [];
                byExpiry.Add(expiry, members);
            }

            members.Add(k);
        }

        foreach ((Date expiryDate, List<int> members) in byExpiry)
        {
            double[] strikes = [.. members.Select(k => ((StrikedTypePayoff)options[k].Payoff!).Strike)];
            double[] values = PriceExpiry(expiryDate, context, strikes, [.. members.Select(k => ((StrikedTypePayoff)options[k].Payoff!).OptionType)]);
            for (int m = 0; m < members.Count; m++)
            {
                VanillaOption option = options[members[m]];
                results[members[m]] = option.FetchResults(new OneAssetOptionResults { NPV = values[m] });
            }
        }

        return [.. results.Select(r => r!)];
    }

    /// <summary>The model's state at an expiry: QuantLib's <c>precalculateExpiry</c>.</summary>
    /// <param name="d">The expiry date.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The state, its characteristic function and discount factors.</returns>
    protected abstract ExpiryState PrecalculateExpiry(Date d, EvaluationContext context);

    // One expiry's prices: the damping and grid by XER5 (xviii) and (xix), then QuantLib's transform and interpolation
    private double[] PriceExpiry(Date expiryDate, EvaluationContext context, double[] strikes, OptionType[] types)
    {
        double lambda = LogStrikeSpacing;
        ExpiryState state = PrecalculateExpiry(expiryDate, context);
        double df = state.DiscountFactor;
        double div = state.DividendYield;
        int n0 = QuantLibPoints(strikes, lambda);

        double[] calls;
        double[] puts;
        if (state.StripUpper is not double hi || hi > QuantLibAlpha + 1.0)
        {
            // QuantLib's damping and grid, doubled where its period aliases the damped tail (the ruling at stop 9)
            calls = TailAgreedGrid(state, strikes, n0, df);
            puts = Parity(calls, strikes, df, div, toPuts: true);
        }
        else
        {
            double alpha = 0.5 * (hi - 1.0);
            int n = n0;
            while (alpha * n * lambda / 2.0 < DampedTail && n <= MaxPoints)
            {
                n *= 2;
            }

            if (n <= MaxPoints)
            {
                // the call side, inside the strip
                calls = Grid(state, strikes, alpha, n, df);
                puts = Parity(calls, strikes, df, div, toPuts: true);
            }
            else
            {
                // the put side, inside the strip's lower part
                double u = Math.Max(0.5 * state.StripLower!.Value, PutSideFloor);
                n = n0;
                double[] previous = Grid(state, strikes, u - 1.0, n, df);
                double[]? converged = null;
                while (n < MaxPoints)
                {
                    n *= 2;
                    double[] current = Grid(state, strikes, u - 1.0, n, df);
                    double[] currentCalls = Parity(current, strikes, df, div, toPuts: false);
                    bool agree = true;
                    for (int m = 0; m < strikes.Length; m++)
                    {
                        double scale = Math.Max(Math.Abs(current[m]), Math.Abs(currentCalls[m]));
                        if (!(Math.Abs(current[m] - previous[m]) <= GridAgreement * scale))
                        {
                            agree = false;
                            break;
                        }
                    }

                    if (agree)
                    {
                        converged = current;
                        break;
                    }

                    previous = current;
                }

                puts = converged ?? throw new NanoQLException(
                    "the FFT grid doesn't converge within 2^22 points near the model's moment boundary: use the analytic engine");
                calls = Parity(puts, strikes, df, div, toPuts: false);
            }
        }

        var values = new double[strikes.Length];
        for (int m = 0; m < strikes.Length; m++)
        {
            values[m] = types[m] switch
            {
                OptionType.Call => calls[m],
                OptionType.Put => puts[m],
                _ => throw new NanoQLException("Invalid option type"),
            };
        }

        return values;
    }

    // QuantLib's grid at the damping 1.25 where its first doubling agrees within 5e-5 of the spot at every strike, the
    // finer grid of the first two successive grids that agree otherwise, up to 2^22 points (the ruling at stop 9)
    private double[] TailAgreedGrid(ExpiryState state, double[] strikes, int n0, double df)
    {
        // ln E[S^(α+1)] − (α+1) ln E[S], the transform at −(α+1)i and at −i
        double growth = Math.Log(state.ComplexFourierTransform(new Complex(0.0, -(QuantLibAlpha + 1.0))).Real)
            - ((QuantLibAlpha + 1.0) * Math.Log(state.ComplexFourierTransform(new Complex(0.0, -1.0)).Real));
        if (!(growth <= TailGrowthBound))
        {
            throw new NanoQLException(TailRefusal);
        }

        double[] quantLibs = Grid(state, strikes, QuantLibAlpha, n0, df);
        double[] previous = quantLibs;
        int n = n0;
        while (n < MaxPoints)
        {
            n *= 2;
            double[] current = Grid(state, strikes, QuantLibAlpha, n, df);
            bool agree = true;
            for (int m = 0; m < strikes.Length; m++)
            {
                if (!(Math.Abs(current[m] - previous[m]) <= TailAgreement * X0))
                {
                    agree = false;
                    break;
                }
            }

            if (agree)
            {
                return n == 2 * n0 ? quantLibs : current;
            }

            previous = current;
        }

        throw new NanoQLException(TailRefusal);
    }

    // QuantLib's put from the call, put = call − x0·div + K·df, or the call from the put by the same parity reversed
    private double[] Parity(double[] prices, double[] strikes, double df, double div, bool toPuts)
    {
        var result = new double[prices.Length];
        for (int m = 0; m < prices.Length; m++)
        {
            result[m] = toPuts
                ? prices[m] - (X0 * div) + (strikes[m] * df)
                : prices[m] + (X0 * div) - (strikes[m] * df);
        }

        return result;
    }

    // QuantLib's n, from the expiry's largest |log K| (XER5 (xix)): its largest strike's log wherever that dominates, and
    // at least 2^14 (the ruling at stop 6)
    private static int QuantLibPoints(double[] strikes, double lambda)
    {
        double maxStrike = 0.0;
        double minStrike = double.PositiveInfinity;
        foreach (double strike in strikes)
        {
            if (strike > maxStrike)
            {
                maxStrike = strike;
            }

            minStrike = Math.Min(minStrike, strike);
        }

        double logSpan = Math.Log(maxStrike);
        if (!(logSpan >= -Math.Log(minStrike)))
        {
            logSpan = -Math.Log(minStrike);
        }

        double nR = 2.0 * (logSpan + lambda) / lambda;
        int log2N = (int)(Math.Log(nR) / Math.Log(2.0)) + 1;
        return Math.Max(1 << log2N, MinPoints);
    }

    // QuantLib's transform at damping alpha on n points, the damped prices interpolated at the strikes: calls for
    // alpha > 0, puts for alpha + 1 < 0
    private double[] Grid(ExpiryState state, double[] strikes, double alpha, int n, double df)
    {
        double lambda = LogStrikeSpacing;
        var i1 = new Complex(0, 1);
        int log2N = BitOperations.Log2((uint)n);

        // Strike range (equation 19,20)
        double b = n * lambda / 2.0;

        // Grid spacing (equation 23)
        double eta = 2.0 * Math.PI / (lambda * n);

        // Input to fourier transform
        var fti = new Complex[n];
        for (int i = 0; i < n; i++)
        {
            double vJ = eta * i;
            double sw = eta * (3.0 + ((i % 2) == 0 ? -1.0 : 1.0) - ((i == 0) ? 1.0 : 0.0)) / 3.0;
            Complex psi = df * state.ComplexFourierTransform(vJ - ((alpha + 1) * i1));
            psi /= (alpha * alpha) + alpha - (vJ * vJ) + (i1 * ((2 * alpha) + 1.0) * vJ);
            fti[i] = Complex.Exp(i1 * b * vJ) * sw * psi;
        }

        // Perform fft
        var results = new Complex[n];
        var fft = new FastFourierTransform(log2N);
        fft.Transform(fti, results);

        // Call prices
        var prices = new double[n];
        var gridStrikes = new double[n];
        for (int i = 0; i < n; i++)
        {
            double kU = -b + (lambda * i);
            prices[i] = Math.Exp(-alpha * kU) / Math.PI * results[i].Real;
            gridStrikes[i] = Math.Exp(kU);
        }

        var interpolation = new LinearInterpolation(gridStrikes, prices);
        var values = new double[strikes.Length];
        for (int m = 0; m < strikes.Length; m++)
        {
            values[m] = interpolation.Evaluate(strikes[m]);
        }

        return values;
    }

    /// <summary>
    /// The model's state at one expiry: its characteristic function, its discount factors and the strip of exponents u
    /// with E[S^u] finite.
    /// </summary>
    protected abstract class ExpiryState
    {
        /// <summary>Gets the risk-free discount factor to the expiry.</summary>
        public abstract double DiscountFactor { get; }

        /// <summary>Gets the dividend discount factor to the expiry.</summary>
        public abstract double DividendYield { get; }

        /// <summary>Gets the strip's lower bound, the smallest u with E[S^u] finite, or <see langword="null"/> where
        /// every moment is finite.</summary>
        public virtual double? StripLower => null;

        /// <summary>Gets the strip's upper bound, the largest u with E[S^u] finite, or <see langword="null"/> where
        /// every moment is finite.</summary>
        public virtual double? StripUpper => null;

        /// <summary>The characteristic function of the log of the underlying at the expiry.</summary>
        /// <param name="u">The argument.</param>
        /// <returns>The characteristic function's value.</returns>
        public abstract Complex ComplexFourierTransform(Complex u);
    }
}
