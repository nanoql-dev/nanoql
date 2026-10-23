// SPDX-FileCopyrightText: Copyright (C) 2014 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2026 Aaditya Panikath
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/volatility/zabrsmilesection.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.TermStructures;

/// <summary>
/// A ZABR smile section: the volatilities and option prices of the ZABR model at given parameters, by the kernel
/// <typeparamref name="TEvaluation"/> names.
/// </summary>
/// <typeparam name="TEvaluation">The kernel: <see cref="ZabrShortMaturityLognormal"/>,
/// <see cref="ZabrShortMaturityNormal"/>, <see cref="ZabrLocalVolatility"/> or <see cref="ZabrFullFd"/>.</typeparam>
/// <remarks>
/// <para>
/// The section builds its <see cref="ZabrModel"/> from α, β, ν, ρ and γ at its exercise time and forward. With the
/// short-maturity lognormal kernel its volatility is the model's lognormal volatility at the strike, floored at 1e-6,
/// and its option prices are <see cref="SmileSection"/>'s. With the normal kernel its option prices are Bachelier's
/// with the model's normal volatility, and its volatility is the lognormal one implied from them, 0 where that can't be
/// implied, as QuantLib's catches every failure. The finite-difference kernels price calls at a strike grid, the
/// moneyness grid times the forward (by default 0.01 to 20, its non-positive strikes dropped) with
/// <c>fdRefinement</c> points between two of its strikes: by <see cref="ZabrModel.FdPrice(ReadOnlySpan{double})"/>,
/// or by <see cref="ZabrModel.FullFdPrice"/> at each strike. A cubic spline through those prices and the forward at
/// strike 0 prices up to the grid's last strike, an exponential fitted to its end beyond; puts by parity; and the
/// volatility is the lognormal one implied from those prices, as the normal kernel's. The short-maturity kernels don't
/// read the grid.
/// </para>
/// <para>
/// QuantLib's date constructor, whose section floats with the evaluation date, is <see cref="Create"/>, which takes the
/// evaluation context and fixes the date. QuantLib's short-maturity kernels read the five parameters unchecked; fewer
/// are refused here, for every kernel, with the text of QuantLib's local-volatility kernel.
/// </para>
/// <para>
/// Unsealed for its named classes, QuantLib-Python's (<see cref="ZabrShortMaturityLognormalSmileSection"/>,
/// <see cref="ZabrShortMaturityNormalSmileSection"/>, <see cref="ZabrLocalVolatilitySmileSection"/> and
/// <see cref="ZabrFullFdSmileSection"/>). Its constructor reads the exercise time, so
/// <see cref="ExerciseTime"/> is sealed here.
/// </para>
/// </remarks>
public class ZabrSmileSection<TEvaluation> : SmileSection, IXabrModelInstance
    where TEvaluation : struct, IZabrEvaluation
{
    // QuantLib's default moneyness grid, shared with SmileSectionUtils
    private static readonly double[] DefaultMoney =
        [0.0, 0.01, 0.05, 0.10, 0.25, 0.40, 0.50, 0.60, 0.70, 0.80, 0.90, 1.0, 1.25, 1.5, 1.75, 2.0, 5.0, 7.5, 10.0, 15.0, 20.0];

    private readonly ZabrModel model;

    // the finite-difference kernels' strike grid, call prices and spline; null for the short-maturity kernels
    private readonly FdGrid? fd;

    /// <summary>Initializes a new instance of the <see cref="ZabrSmileSection{TEvaluation}"/> class for an exercise
    /// time.</summary>
    /// <param name="timeToExpiry">The exercise time, positive.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="zabrParameters">α, β, ν, ρ and γ, in that order; values after the fifth are ignored.</param>
    /// <param name="moneyness">The finite-difference kernels' moneyness grid, empty for their default.</param>
    /// <param name="fdRefinement">The finite-difference kernels' points between two of the grid's strikes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fdRefinement"/> is negative.</exception>
    /// <exception cref="ArgumentException">The time isn't positive, there are fewer than five parameters, or the model
    /// refuses them or the forward.</exception>
    public ZabrSmileSection(
        double timeToExpiry,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        ReadOnlySpan<double> moneyness = default,
        int fdRefinement = 5)
        : base(timeToExpiry)
    {
        model = Init(forward, zabrParameters, fdRefinement);
        fd = FdGridOf(model, forward, moneyness, fdRefinement);
    }

    /// <summary>Initializes a new instance of the <see cref="ZabrSmileSection{TEvaluation}"/> class for an exercise
    /// date, the evaluation date of <see cref="Create"/> its reference date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="zabrParameters">α, β, ν, ρ and γ, in that order; values after the fifth are ignored.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for QuantLib's default,
    /// Actual/365 (Fixed).</param>
    /// <param name="moneyness">The finite-difference kernels' moneyness grid, empty for their default.</param>
    /// <param name="fdRefinement">The finite-difference kernels' points between two of the grid's strikes.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fdRefinement"/> is negative.</exception>
    /// <exception cref="ArgumentException">A date is <c>default(Date)</c>, the exercise date is before the reference
    /// date, there are fewer than five parameters, or the model refuses them, the forward or the exercise time.</exception>
    protected ZabrSmileSection(
        Date d,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        Date referenceDate,
        DayCounter? dc = null,
        ReadOnlySpan<double> moneyness = default,
        int fdRefinement = 5)
        : base(d, dc ?? new Actual365Fixed(), referenceDate)
    {
        model = Init(forward, zabrParameters, fdRefinement);
        fd = FdGridOf(model, forward, moneyness, fdRefinement);
    }

    /// <summary>Gets the exercise time.</summary>
    /// <remarks>Sealed: the constructor reads it.</remarks>
    public sealed override double ExerciseTime => base.ExerciseTime;

    /// <summary>Gets the section's model.</summary>
    public ZabrModel Model => model;

    /// <summary>A ZABR smile section for an exercise date, whose reference date is the evaluation date.</summary>
    /// <param name="d">The exercise date.</param>
    /// <param name="forward">The forward.</param>
    /// <param name="zabrParameters">α, β, ν, ρ and γ, in that order.</param>
    /// <param name="context">The evaluation context, whose evaluation date is the reference date.</param>
    /// <param name="dc">The day counter of the exercise time, or <see langword="null"/> for Actual/365 (Fixed).</param>
    /// <param name="moneyness">The finite-difference kernels' moneyness grid, empty for their default.</param>
    /// <param name="fdRefinement">The finite-difference kernels' points between two of the grid's strikes.</param>
    /// <returns>The section, with a fixed reference date.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="fdRefinement"/> is negative.</exception>
    /// <exception cref="ArgumentException">The section refuses its arguments.</exception>
    public static ZabrSmileSection<TEvaluation> Create(
        Date d,
        double forward,
        ReadOnlySpan<double> zabrParameters,
        EvaluationContext context,
        DayCounter? dc = null,
        ReadOnlySpan<double> moneyness = default,
        int fdRefinement = 5)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new ZabrSmileSection<TEvaluation>(
            d, forward, zabrParameters, context.EvaluationDate, dc, moneyness, fdRefinement);
    }

    /// <summary>The minimum strike: 0.</summary>
    /// <returns>The strike.</returns>
    public override double MinStrike() => 0.0;

    /// <summary>The maximum strike: the largest real.</summary>
    /// <returns>The strike.</returns>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <summary>The at-the-money level: the model's forward.</summary>
    /// <returns>The forward.</returns>
    public override double? AtmLevel() => model.Forward;

    /// <summary>The price of an option on the forward, by the section's kernel.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="type"/> isn't a member of its type.</exception>
    /// <exception cref="ArgumentException">The kernel's formula or the model refuses the strike.</exception>
    /// <exception cref="NanoQLException">The model's integration fails.</exception>
    public override double OptionPrice(double strike, OptionType type = OptionType.Call, double discount = 1.0)
    {
        EnumArguments.Check(type, nameof(type));
        return TEvaluation.OptionPrice(this, strike, type, discount);
    }

    /// <inheritdoc/>
    double IXabrModelInstance.Volatility(double x, VolatilityType volatilityType) => Volatility(x, volatilityType, 0.0);

    /// <summary><see cref="SmileSection"/>'s option price, which the lognormal kernel prices with.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    internal double SmileSectionOptionPrice(double strike, OptionType type, double discount) =>
        base.OptionPrice(strike, type, discount);

    /// <summary>The finite-difference kernels' option price: the call spline up to the grid's last strike, the
    /// exponential beyond, puts by parity.</summary>
    /// <param name="strike">The strike.</param>
    /// <param name="type">Call or put.</param>
    /// <param name="discount">The discount factor.</param>
    /// <returns>The price.</returns>
    internal double FdOptionPrice(double strike, OptionType type, double discount)
    {
        FdGrid grid = fd!;
        double call = strike <= grid.Strikes[^1] ? grid.CallPriceFct.Evaluate(strike) : Math.Exp((-grid.A * strike) + grid.B);
        if (type == OptionType.Call)
        {
            return call * discount;
        }
        else
        {
            return (call - (model.Forward - strike)) * discount;
        }
    }

    /// <inheritdoc/>
    protected override double VolatilityImpl(double strike) => TEvaluation.VolatilityImpl(this, strike);

    // QuantLib's init's strike grid for the finite-difference kernels and their init2 and init3: the call prices, the
    // spline through them and the forward at strike 0, and the exponential's parameters at the grid's end.
    private static FdGrid? FdGridOf(ZabrModel model, double forward, ReadOnlySpan<double> moneyness, int fdRefinement)
    {
        // set up strike grid for local vol or full fd flavour of this section
        // this is shared with SmileSectionUtils - unify later ?
        ReadOnlySpan<double> tmp = moneyness.IsEmpty ? DefaultMoney : moneyness;
        var strikes = new List<double>();
        double lastF = 0.0;
        bool firstStrike = true;
        foreach (double i in tmp)
        {
            double f = i * forward;
            if (f > 0.0)
            {
                if (!firstStrike)
                {
                    for (int j = 1; j <= fdRefinement; ++j)
                    {
                        strikes.Add(lastF + (((double)j) * (f - lastF) / (fdRefinement + 1)));
                    }
                }

                firstStrike = false;
                lastF = f;
                strikes.Add(f);
            }
        }

        double[]? callPrices = TEvaluation.CallPrices(model, [.. strikes]);
        if (callPrices is null)
        {
            return null;
        }

        // init3
        strikes.Insert(0, 0.0);
        double[] prices = [forward, .. callPrices];
        Interpolation callPriceFct = new CubicInterpolation(
            [.. strikes],
            prices,
            CubicInterpolationDerivativeApprox.Spline,
            true,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0,
            CubicInterpolationBoundaryCondition.SecondDerivative,
            0.0).EnableExtrapolation();

        // on the right side we extrapolate exponetially (because spline
        // does not make sense)
        // we precompute the necessary parameters here
        const double eps = 1E-5; // gap for first derivative computation

        double c0 = callPriceFct.Evaluate(strikes[^1]);
        double c0p = (callPriceFct.Evaluate(strikes[^1] - eps) - c0) / eps;

        double a = c0p / c0;
        double b = Math.Log(c0) + (a * strikes[^1]);
        return new FdGrid([.. strikes], [.. prices], callPriceFct, a, b);
    }

    // QuantLib's init, which builds the model; the finite-difference kernels' init, which checks the parameters' count
    // first, also builds their strike grid (FdGridOf).
    private ZabrModel Init(double forward, ReadOnlySpan<double> zabrParameters, int fdRefinement)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(fdRefinement);

        // QuantLib's short-maturity kernels read the five parameters unchecked.
        if (!(zabrParameters.Length >= 5))
        {
            throw new ArgumentException(
                $"zabr expects 5 parameters (alpha,beta,nu,rho,gamma) but ({zabrParameters.Length}) given",
                nameof(zabrParameters));
        }

        return new ZabrModel(
            ExerciseTime,
            forward,
            zabrParameters[0],
            zabrParameters[1],
            zabrParameters[2],
            zabrParameters[3],
            zabrParameters[4]);
    }

    // QuantLib's strikes_, callPrices_, callPriceFct_, a_ and b_.
    private sealed record FdGrid(ImmutableArray<double> Strikes, ImmutableArray<double> CallPrices, Interpolation CallPriceFct, double A, double B);
}
