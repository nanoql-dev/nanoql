// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: Copyright (C) 2012 Liquidnet Holdings, Inc.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/volatility/garch.hpp ql/models/volatility/garch.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The GARCH(1,1) volatility model: σ²ₜ = ω + α u²ₜ₋₁ + β σ²ₜ₋₁, ω = γ V_L with γ = 1 − α − β and V_L the long-term
/// variance. Volatilities are assumed to be expressed on an annual basis.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: a model built from a series is calibrated at construction, and each <c>Calibrate</c> returns a new model
/// with the receiver's mode, where QuantLib's change the object. The statics are QuantLib's:
/// <see cref="Calculate(TimeSeries{double}, double, double, double)"/>, <see cref="ToR2"/>, <see cref="CostFunction"/>
/// and <c>CalibrateR2</c>'s six overloads, each returning the parameters and the minimization's results that QuantLib
/// writes through its references and returns.
/// </para>
/// <para>
/// Documented divergences:
/// </para>
/// <list type="bullet">
/// <item><description>A calibration whose minimizer doesn't converge is refused, "GARCH(1,1) calibration did not
/// converge: optimizer stopped with end criterion …", which the mode-based calibrations catch and fall back to their
/// initial guess: QuantLib's master (#2844), which 1.44 doesn't check
/// (<c>Divergences/GarchConvergenceTests.cs</c>).</description></item>
/// <item><description>The cost's gradient is the gradient of its value, the recursive ∂σ²ₜ/∂θ = (1, u²ₜ₋₁, σ²ₜ₋₁) + β
/// ∂σ²ₜ₋₁/∂θ, where QuantLib's takes the direct term alone, 0.13 to 0.80 times the true gradient; QuantLib's own paths,
/// the Simplex and the ACF fit, never read it (<c>Divergences/GarchGradientTests.cs</c>).</description></item>
/// <item><description>The cost refuses a conditional variance that isn't positive, which only the overloads that
/// subtract the mean from the squared returns can produce, where QuantLib takes its log, NaN
/// (<c>Divergences/GarchDemeanedVarianceTests.cs</c>).</description></item>
/// <item><description>A series of fewer than two points is refused by <see cref="Calculate(TimeSeries{double}, double,
/// double, double)"/>, where QuantLib reads before its start.</description></item>
/// <item><description>The mode-based initial guesses fit the autocorrelations by <see cref="NonLinearLeastSquare"/> on
/// the conjugate gradient, whose returned point is the last one its line search accepted
/// (<c>Divergences/LineSearchReturnTests.cs</c>), so the Simplex starts a hair from QuantLib's guess and the calibrated
/// parameters end within its tolerance, 1e-8, of QuantLib's, the log-likelihood the same.</description></item>
/// <item><description>The second initial guess (the ACF fit's, which <see cref="Garch11Mode.GammaGuess"/> and
/// <see cref="Garch11Mode.BestOfTwo"/> take) sets α = γ − β, as the first does, where the fit fails or its guess misses
/// the constraint; QuantLib leaves α there as its caller's, an uninitialized element of its guess array
/// (<c>garch.cpp:318-368</c>, <c>:435-439</c>), so its value is undefined.</description></item>
/// </list>
/// </remarks>
public sealed class Garch11 : VolatilityCompositor
{
    private const double TolLevel = 1.0e-8;

    private readonly double gamma;
    private readonly double vl;

    /// <summary>Initializes a new instance of the <see cref="Garch11"/> class from its parameters.</summary>
    /// <param name="a">α.</param>
    /// <param name="b">β.</param>
    /// <param name="vl">The long-term variance V_L.</param>
    public Garch11(double a, double b, double vl)
        : this(a, b, 1 - a - b, vl, 0.0, Garch11Mode.BestOfTwo)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Garch11"/> class calibrated to a series.</summary>
    /// <param name="qs">The series of returns.</param>
    /// <param name="mode">The initial guess.</param>
    /// <exception cref="ArgumentNullException"><paramref name="qs"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> isn't a <see cref="Garch11Mode"/>
    /// member.</exception>
    /// <exception cref="NanoQLException">The series is too short or constant.</exception>
    public Garch11(TimeSeries<double> qs, Garch11Mode mode = Garch11Mode.BestOfTwo)
        : this(Calibrated(Values(qs), mode, null, null))
    {
    }

    private Garch11(double alpha, double beta, double gamma, double vl, double logLikelihood, Garch11Mode mode)
    {
        Alpha = alpha;
        Beta = beta;
        this.gamma = gamma;
        this.vl = vl;
        LogLikelihood = logLikelihood;
        Mode = mode;
    }

    private Garch11(Garch11 calibrated)
        : this(calibrated.Alpha, calibrated.Beta, calibrated.gamma, calibrated.vl, calibrated.LogLikelihood, calibrated.Mode)
    {
    }

    /// <summary>Gets α.</summary>
    public double Alpha { get; }

    /// <summary>Gets β.</summary>
    public double Beta { get; }

    /// <summary>Gets ω = γ V_L.</summary>
    public double Omega => vl * gamma;

    /// <summary>Gets the long-term variance V_L.</summary>
    public double LtVol => vl;

    /// <summary>Gets the log-likelihood of the calibration, 0 for a model built from its parameters.</summary>
    public double LogLikelihood { get; }

    /// <summary>Gets the initial guess the model's calibrations take.</summary>
    public Garch11Mode Mode { get; }

    /// <inheritdoc/>
    /// <exception cref="ArgumentNullException"><paramref name="volatilitySeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series has fewer than two points.</exception>
    public override TimeSeries<double> Calculate(TimeSeries<double> volatilitySeries) => Calculate(volatilitySeries, Alpha, Beta, Omega);

    /// <inheritdoc/>
    /// <returns>The model calibrated by its mode's initial guess and QuantLib's Simplex.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="volatilitySeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The series is too short or constant.</exception>
    public override Garch11 Calibrate(TimeSeries<double> volatilitySeries) => Calibrated(Values(volatilitySeries), Mode, null, null);

    /// <summary>The model calibrated by its mode's initial guess and an optimization method.</summary>
    /// <param name="quoteSeries">The series of returns.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <returns>A new model, with this one's mode.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The series is too short or constant.</exception>
    public Garch11 Calibrate(TimeSeries<double> quoteSeries, OptimizationMethod method, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(endCriteria);
        return Calibrated(Values(quoteSeries), Mode, method, endCriteria);
    }

    /// <summary>The model calibrated from an initial guess by an optimization method.</summary>
    /// <param name="quoteSeries">The series of returns.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <param name="initialGuess">The initial ω, α and β.</param>
    /// <returns>A new model, with this one's mode.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The minimizer doesn't converge.</exception>
    public Garch11 Calibrate(TimeSeries<double> quoteSeries, OptimizationMethod method, EndCriteria endCriteria, ReadOnlySpan<double> initialGuess)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(endCriteria);
        ImmutableArray<double> values = Values(quoteSeries);
        (ImmutableArray<double> r2, _) = ToR2(values.AsSpan());
        return FromCalibration(CalibrateR2(r2.AsSpan(), method, endCriteria, initialGuess), values, Mode);
    }

    /// <summary>The variance forecast after a return r at a variance σ², γ V_L + α r² + β σ².</summary>
    /// <param name="r">The return.</param>
    /// <param name="sigma2">The variance.</param>
    /// <returns>The next variance.</returns>
    public double Forecast(double r, double sigma2) => (gamma * vl) + (Alpha * r * r) + (Beta * sigma2);

    /// <summary>
    /// The model's volatilities on a series of returns: at each date after the first, the square root of the variance
    /// the returns before it give, and one more at the last date plus its spacing.
    /// </summary>
    /// <param name="quoteSeries">The series of returns.</param>
    /// <param name="alpha">α.</param>
    /// <param name="beta">β.</param>
    /// <param name="omega">ω.</param>
    /// <returns>The volatility series.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="quoteSeries"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The series has fewer than two points.</exception>
    public static TimeSeries<double> Calculate(TimeSeries<double> quoteSeries, double alpha, double beta, double omega)
    {
        ArgumentNullException.ThrowIfNull(quoteSeries);
        if (quoteSeries.Size < 2)
        {
            throw new ArgumentException("a GARCH series needs at least two points", nameof(quoteSeries));
        }

        ImmutableArray<Date> dates = quoteSeries.Dates();
        ImmutableArray<double> values = quoteSeries.Values();
        var outDates = new Date[dates.Length];
        var outValues = new double[dates.Length];
        double u = values[0];
        double sigma2 = u * u;
        int cur = 0;
        while (++cur != dates.Length)
        {
            sigma2 = omega + (alpha * u * u) + (beta * sigma2);
            outDates[cur - 1] = dates[cur];
            outValues[cur - 1] = Math.Sqrt(sigma2);
            u = values[cur];
        }

        sigma2 = omega + (alpha * u * u) + (beta * sigma2);
        --cur;
        int prev = cur - 1;
        outDates[^1] = dates[cur] + (dates[cur] - dates[prev]);
        outValues[^1] = Math.Sqrt(sigma2);
        return new TimeSeries<double>(outDates, outValues);
    }

    /// <summary>The squared returns and their mean, QuantLib's <c>to_r2</c>.</summary>
    /// <param name="values">The returns.</param>
    /// <returns>The squared returns, and their running mean.</returns>
    public static (ImmutableArray<double> R2, double MeanR2) ToR2(ReadOnlySpan<double> values)
    {
        double u2, meanR2 = 0.0, w = 1.0;
        ImmutableArray<double>.Builder r2 = ImmutableArray.CreateBuilder<double>(values.Length);
        foreach (double v in values)
        {
            u2 = v;
            u2 *= u2;
            meanR2 = ((1.0 - w) * meanR2) + (w * u2);
            r2.Add(u2);
            w /= w + 1.0;
        }

        return (r2.MoveToImmutable(), meanR2);
    }

    /// <summary>The model's cost on a series of returns: Σ(ln σ²ₜ + u²ₜ/σ²ₜ)/(2N), 0 on an empty series.</summary>
    /// <param name="values">The returns.</param>
    /// <param name="alpha">α.</param>
    /// <param name="beta">β.</param>
    /// <param name="omega">ω.</param>
    /// <returns>The cost, the negated log-likelihood.</returns>
    public static double CostFunction(ReadOnlySpan<double> values, double alpha, double beta, double omega)
    {
        double retval = 0.0;
        double u2 = 0.0, sigma2 = 0.0;
        int n = 0;
        foreach (double v in values)
        {
            sigma2 = omega + (alpha * u2) + (beta * sigma2);
            u2 = v;
            u2 *= u2;
            retval += Math.Log(sigma2) + (u2 / sigma2);
            ++n;
        }

        return n > 0 ? retval / (2 * n) : 0.0;
    }

    /// <summary>Calibrates GARCH for r² by a mode's initial guess and QuantLib's Simplex.</summary>
    /// <param name="mode">The initial guess.</param>
    /// <param name="r2">The squared returns.</param>
    /// <param name="meanR2">Their mean.</param>
    /// <returns>The parameters and the minimization's results, <see langword="null"/> where it fell back to its
    /// guess.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> isn't a <see cref="Garch11Mode"/>
    /// member.</exception>
    /// <exception cref="NanoQLException">The series is too short or constant.</exception>
    public static Garch11Calibration CalibrateR2(Garch11Mode mode, ReadOnlySpan<double> r2, double meanR2)
    {
        var endCriteria = new EndCriteria(10000, 500, TolLevel, TolLevel, TolLevel);
        var method = new Simplex(0.001);
        return CalibrateR2(mode, r2, meanR2, method, endCriteria);
    }

    /// <summary>Calibrates GARCH for r² by a mode's initial guess and an optimization method.</summary>
    /// <param name="mode">The initial guess.</param>
    /// <param name="r2">The squared returns.</param>
    /// <param name="meanR2">Their mean.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <returns>The parameters and the minimization's results, <see langword="null"/> where it fell back to its
    /// guess.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="endCriteria"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="mode"/> isn't a <see cref="Garch11Mode"/>
    /// member.</exception>
    /// <exception cref="NanoQLException">The series is too short or constant.</exception>
    public static Garch11Calibration CalibrateR2(
        Garch11Mode mode, ReadOnlySpan<double> r2, double meanR2, OptimizationMethod method, EndCriteria endCriteria)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(endCriteria);
        EnumArguments.Check(mode, nameof(mode));
        double dataSize = r2.Length;
        double alpha = 0.0, beta = 0.0, omega;
        if (!(dataSize >= 4))
        {
            throw new NanoQLException("Data series is too short to fit GARCH model");
        }

        if (!(meanR2 > 0))
        {
            throw new NanoQLException("Data series is constant");
        }

        omega = meanR2 * dataSize / (dataSize - 1);

        // ACF
        int maxLag = (int)Math.Sqrt(dataSize);
        var acf = new double[maxLag + 1];
        var tmp = new double[r2.Length];
        for (int i = 0; i < r2.Length; ++i)
        {
            tmp[i] = r2[i] - meanR2;
        }

        AutocovarianceFunctions.Autocovariances(tmp, acf, maxLag);
        if (!(acf[0] > 0))
        {
            throw new NanoQLException("Data series is constant");
        }

        var cost = new Garch11CostFunction(r2.ToArray());

        // two initial guesses based on fitting ACF
        double gammaLower = 0.0;
        var opt1 = new Vector(3);
        double fCost1 = NumericLimits.QL_MAX_REAL;
        if (mode != Garch11Mode.GammaGuess)
        {
            (gammaLower, opt1[1], opt1[2], opt1[0]) = InitialGuess1(acf, meanR2);
            fCost1 = cost.Value(opt1);
        }

        var opt2 = new Vector(3);
        double fCost2 = NumericLimits.QL_MAX_REAL;
        if (mode != Garch11Mode.MomentMatchingGuess)
        {
            (gammaLower, opt2[1], opt2[2], opt2[0]) = InitialGuess2(acf, meanR2);
            fCost2 = cost.Value(opt2);
        }

        var constraints = new Garch11Constraint(gammaLower, 1.0 - TolLevel);

        ProblemResults? ret = null;
        if (mode != Garch11Mode.DoubleOptimization)
        {
            try
            {
                Garch11Calibration c = CalibrateR2(r2, method, constraints, endCriteria, (fCost1 <= fCost2 ? opt1 : opt2).AsSpan());
                (alpha, beta, omega, ret) = (c.Alpha, c.Beta, c.Omega, c.Problem);
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                if (fCost1 <= fCost2)
                {
                    alpha = opt1[1];
                    beta = opt1[2];
                    omega = opt1[0];
                }
                else
                {
                    alpha = opt2[1];
                    beta = opt2[2];
                    omega = opt2[0];
                }
            }
        }
        else
        {
            ProblemResults? ret1 = null, ret2 = null;
            try
            {
                Garch11Calibration c = CalibrateR2(r2, method, constraints, endCriteria, opt1.AsSpan());
                (alpha, beta, omega, ret1) = (c.Alpha, c.Beta, c.Omega, c.Problem);
                opt1[1] = alpha;
                opt1[2] = beta;
                opt1[0] = omega;
                if (constraints.Test(opt1))
                {
                    fCost1 = Math.Min(fCost1, cost.Value(opt1));
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                fCost1 = NumericLimits.QL_MAX_REAL;
            }

            try
            {
                Garch11Calibration c = CalibrateR2(r2, method, constraints, endCriteria, opt2.AsSpan());
                (alpha, beta, omega, ret2) = (c.Alpha, c.Beta, c.Omega, c.Problem);
                opt2[1] = alpha;
                opt2[2] = beta;
                opt2[0] = omega;
                if (constraints.Test(opt2))
                {
                    fCost2 = Math.Min(fCost2, cost.Value(opt2));
                }
            }
            catch (Exception e) when (e is NanoQLException or ArgumentException)
            {
                fCost2 = NumericLimits.QL_MAX_REAL;
            }

            if (fCost1 <= fCost2)
            {
                alpha = opt1[1];
                beta = opt1[2];
                omega = opt1[0];
                ret = ret1;
            }
            else
            {
                alpha = opt2[1];
                beta = opt2[2];
                omega = opt2[0];
                ret = ret2;
            }
        }

        return new Garch11Calibration(alpha, beta, omega, ret);
    }

    /// <summary>
    /// Calibrates GARCH for r² less its mean from an initial guess, by an optimization method under the constraint
    /// ω &gt; 0, α, β ≥ 0, α + β &lt; 1 − 1e-8.
    /// </summary>
    /// <param name="r2">The squared returns.</param>
    /// <param name="meanR2">Their mean, subtracted from each.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <param name="initialGuess">The initial ω, α and β.</param>
    /// <returns>The parameters and the minimization's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="endCriteria"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The minimizer doesn't converge, or the cost reaches a conditional variance that
    /// isn't positive.</exception>
    public static Garch11Calibration CalibrateR2(
        ReadOnlySpan<double> r2, double meanR2, OptimizationMethod method, EndCriteria endCriteria, ReadOnlySpan<double> initialGuess) =>
        CalibrateR2(Demeaned(r2, meanR2), method, endCriteria, initialGuess);

    /// <summary>
    /// Calibrates GARCH for r² from an initial guess, by an optimization method under the constraint ω &gt; 0, α, β ≥ 0,
    /// α + β &lt; 1 − 1e-8.
    /// </summary>
    /// <param name="r2">The squared returns.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <param name="initialGuess">The initial ω, α and β.</param>
    /// <returns>The parameters and the minimization's results.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="method"/> or <paramref name="endCriteria"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The minimizer doesn't converge.</exception>
    public static Garch11Calibration CalibrateR2(
        ReadOnlySpan<double> r2, OptimizationMethod method, EndCriteria endCriteria, ReadOnlySpan<double> initialGuess)
    {
        var constraints = new Garch11Constraint(0.0, 1.0 - TolLevel);
        return CalibrateR2(r2, method, constraints, endCriteria, initialGuess);
    }

    /// <summary>Calibrates GARCH for r² less its mean from an initial guess, by an optimization method under a
    /// constraint.</summary>
    /// <param name="r2">The squared returns.</param>
    /// <param name="meanR2">Their mean, subtracted from each.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="constraints">The constraint.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <param name="initialGuess">The initial ω, α and β.</param>
    /// <returns>The parameters and the minimization's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The minimizer doesn't converge, or the cost reaches a conditional variance that
    /// isn't positive.</exception>
    public static Garch11Calibration CalibrateR2(
        ReadOnlySpan<double> r2,
        double meanR2,
        OptimizationMethod method,
        Constraint constraints,
        EndCriteria endCriteria,
        ReadOnlySpan<double> initialGuess) =>
        CalibrateR2(Demeaned(r2, meanR2), method, constraints, endCriteria, initialGuess);

    /// <summary>Calibrates GARCH for r² from an initial guess, by an optimization method under a constraint.</summary>
    /// <param name="r2">The squared returns.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="constraints">The constraint.</param>
    /// <param name="endCriteria">Its end criteria.</param>
    /// <param name="initialGuess">The initial ω, α and β.</param>
    /// <returns>The parameters and the minimization's results.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The minimizer doesn't converge (#2844).</exception>
    public static Garch11Calibration CalibrateR2(
        ReadOnlySpan<double> r2, OptimizationMethod method, Constraint constraints, EndCriteria endCriteria, ReadOnlySpan<double> initialGuess)
    {
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(constraints);
        ArgumentNullException.ThrowIfNull(endCriteria);
        var cost = new Garch11CostFunction(r2.ToArray());
        var problem = new Problem(cost, constraints, initialGuess);
        ProblemResults results = method.Minimize(problem, endCriteria);

        // QuantLib's master (#2844): a minimizer that doesn't converge is refused
        if (!EndCriteria.Succeeded(results.EndCriteriaType))
        {
            throw new NanoQLException($"GARCH(1,1) calibration did not converge: optimizer stopped with end criterion {results.EndCriteriaType}");
        }

        ImmutableArray<double> optimum = results.CurrentValue;
        return new Garch11Calibration(optimum[1], optimum[2], optimum[0], results);
    }

    // QuantLib's calibrate(begin, end[, method, endCriteria]) on a new object of the given mode
    private static Garch11 Calibrated(ImmutableArray<double> values, Garch11Mode mode, OptimizationMethod? method, EndCriteria? endCriteria)
    {
        (ImmutableArray<double> r2, double meanR2) = ToR2(values.AsSpan());
        Garch11Calibration c = method is null
            ? CalibrateR2(mode, r2.AsSpan(), meanR2)
            : CalibrateR2(mode, r2.AsSpan(), meanR2, method, endCriteria!);
        return FromCalibration(c, values, mode);
    }

    // gamma = 1 - alpha - beta, vl = omega/gamma, and the log-likelihood from the problem or, where the calibration fell
    // back to its guess, from the cost at the new parameters, QuantLib's omega() = vl gamma
    private static Garch11 FromCalibration(Garch11Calibration c, ImmutableArray<double> values, Garch11Mode mode)
    {
        double gamma = 1 - c.Alpha - c.Beta;
        double vl = c.Omega / gamma;
        double logLikelihood = c.Problem is ProblemResults p
            ? -(p.FunctionValue ?? double.NaN)
            : -CostFunction(values.AsSpan(), c.Alpha, c.Beta, vl * gamma);
        return new Garch11(c.Alpha, c.Beta, gamma, vl, logLikelihood, mode);
    }

    private static ImmutableArray<double> Values(TimeSeries<double> series)
    {
        ArgumentNullException.ThrowIfNull(series);
        return series.Values();
    }

    private static double[] Demeaned(ReadOnlySpan<double> r2, double meanR2)
    {
        var tmp = new double[r2.Length];
        for (int i = 0; i < r2.Length; ++i)
        {
            tmp[i] = r2[i] - meanR2;
        }

        return tmp;
    }

    // Initial guess based on fitting ACF - initial guess for fitting acf is a moment matching estimates for mean(r2),
    // acf(0), and acf(1).
    private static (double GammaLower, double Alpha, double Beta, double Omega) InitialGuess1(double[] acf, double meanR2)
    {
        double a21 = acf[1];
        double a4 = acf[0] + (meanR2 * meanR2);

        double a = meanR2 * meanR2 / a4; // 1/sigma^2
        double b = a21 / a4; // rho(1)

        double gammaLower = a <= (1.0 / 3.0) - TolLevel ? Math.Sqrt((1 - (3 * a)) / (3 - (3 * a))) + TolLevel : TolLevel;
        var constraints = new Garch11Constraint(gammaLower, 1.0 - TolLevel);

        double gamma = gammaLower + ((1 - gammaLower) * 0.5);
        double beta = Math.Min(gamma, Math.Max((gamma * (1 - a)) - b, 0.0));
        double alpha = gamma - beta;
        double omega = meanR2 * (1 - gamma);

        if (Math.Abs(a - 0.5) < NumericLimits.QL_EPSILON)
        {
            gamma = Math.Max(gammaLower, -(1 + (4 * b * b)) / (4 * b));
            beta = Math.Min(gamma, Math.Max((gamma * (1 - a)) - b, 0.0));
            alpha = gamma - beta;
            omega = meanR2 * (1 - gamma);
        }
        else
        {
            if (a > 1.0 - NumericLimits.QL_EPSILON)
            {
                gamma = Math.Max(gammaLower, -(1 + (b * b)) / (2 * b));
                beta = Math.Min(gamma, Math.Max((gamma * (1 - a)) - b, 0.0));
                alpha = gamma - beta;
                omega = meanR2 * (1 - gamma);
            }
            else
            {
                double d = ((3 * a) - 1) * ((2 * b * b) + ((1 - a) * ((2 * a) - 1)));
                if (d >= 0)
                {
                    double sd = Math.Sqrt(d);
                    double bb = (b - sd) / ((2 * a) - 1);
                    double g = 0;
                    if (bb >= TolLevel && bb <= 1.0 - TolLevel)
                    {
                        g = (bb + b) / (1 - a);
                    }

                    if (g < gammaLower)
                    {
                        bb = (b + sd) / ((2 * a) - 1);
                        if (bb >= TolLevel && bb <= 1.0 - TolLevel)
                        {
                            g = (bb + b) / (1 - a);
                        }
                    }

                    if (g >= gammaLower)
                    {
                        gamma = g;
                        beta = Math.Min(gamma, Math.Max((gamma * (1 - a)) - b, 0.0));
                        alpha = gamma - beta;
                        omega = meanR2 * (1 - gamma);
                    }
                }
            }
        }

        var idx = new List<int>();
        int nCov = acf.Length - 1;
        for (int i = 0; i <= nCov; ++i)
        {
            if (i < 2 || (acf[i] > 0 && acf[i - 1] > 0 && acf[i - 1] > acf[i]))
            {
                idx.Add(i);
            }
        }

        return FitAcf(acf, meanR2, idx, gammaLower, constraints, gamma, beta, alpha, omega);
    }

    // Initial guess based on fitting ACF - initial guess for fitting acf is an estimate of gamma = alpfa+beta based on
    // the property: acf(i+1) = gamma*acf(i) for i > 1.
    private static (double GammaLower, double Alpha, double Beta, double Omega) InitialGuess2(double[] acf, double meanR2)
    {
        double a21 = acf[1];
        double a4 = acf[0] + (meanR2 * meanR2);
        double a = meanR2 * meanR2 / a4; // 1/sigma^2
        double b = a21 / a4; // rho(1)
        double gammaLower = a <= (1.0 / 3.0) - TolLevel ? Math.Sqrt((1 - (3 * a)) / (3 - (3 * a))) + TolLevel : TolLevel;
        var constraints = new Garch11Constraint(gammaLower, 1.0 - TolLevel);

        // ACF
        double gamma = 0;
        int nn = 0;
        var idx = new List<int>();
        int nCov = acf.Length - 1;
        for (int i = 0; i <= nCov; ++i)
        {
            if (i < 2)
            {
                idx.Add(i);
            }

            if (i > 1 && acf[i] > 0 && acf[i - 1] > 0 && acf[i - 1] > acf[i])
            {
                gamma += acf[i] / acf[i - 1];
                nn++;
                idx.Add(i);
            }
        }

        if (nn > 0)
        {
            gamma /= nn;
        }

        if (gamma < gammaLower)
        {
            gamma = gammaLower;
        }

        double beta = Math.Min(gamma, Math.Max((gamma * (1 - a)) - b, 0.0));
        double omega = meanR2 * (1 - gamma);

        // QuantLib leaves alpha as its caller's, an uninitialized element of its guess array; here gamma - beta, as
        // initialGuess1 sets it
        return FitAcf(acf, meanR2, idx, gammaLower, constraints, gamma, beta, gamma - beta, omega);
    }

    // the ACF fit from (gamma, beta) by QuantLib's least squares; the fit's guess where it meets the GARCH constraint,
    // else the guess given; a failed fit returns the guess given
    private static (double GammaLower, double Alpha, double Beta, double Omega) FitAcf(
        double[] acf, double meanR2, List<int> idx, double gammaLower, Garch11Constraint constraints, double gamma, double beta, double alpha, double omega)
    {
        try
        {
            var c = new FitAcfConstraint(gammaLower, 1.0 - TolLevel);
            var nnls = new NonLinearLeastSquare(c);
            var pr = new FitAcfProblem(meanR2, acf, idx);
            NonLinearLeastSquareResults fit = nnls.Perform(pr, [gamma, beta]);
            ImmutableArray<double> x = fit.Results;
            var guess = new Vector(3);
            guess[0] = meanR2 * (1 - x[0]);
            guess[1] = x[0] - x[1];
            guess[2] = x[1];
            if (constraints.Test(guess))
            {
                omega = guess[0];
                alpha = guess[1];
                beta = guess[2];
            }
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            // failed -- returning initial values
        }

        return (gammaLower, alpha, beta, omega);
    }

    // ω > 0, α, β ≥ 0, gammaLower ≤ α + β < gammaUpper, on (ω, α, β)
    private sealed class Garch11Constraint(double gammaLower, double gammaUpper) : Constraint
    {
        public override bool Test(Vector p)
        {
            ArgumentNullException.ThrowIfNull(p);
            if (!(p.Size >= 3))
            {
                throw new NanoQLException("size of parameters vector < 3");
            }

            return p[0] > 0 && p[1] >= 0 && p[2] >= 0 && p[1] + p[2] < gammaUpper && p[1] + p[2] >= gammaLower;
        }
    }

    // gammaLower ≤ γ < gammaUpper, 0 ≤ β ≤ γ, on (γ, β)
    private sealed class FitAcfConstraint(double gammaLower, double gammaUpper) : Constraint
    {
        public override bool Test(Vector p)
        {
            ArgumentNullException.ThrowIfNull(p);
            if (!(p.Size >= 2))
            {
                throw new NanoQLException("size of parameters vector < 2");
            }

            return p[0] >= gammaLower && p[0] < gammaUpper && p[1] >= 0 && p[1] <= p[0];
        }
    }

    // The negated log-likelihood on (ω, α, β) over the squared returns, its gradient the recursive one, and a
    // conditional variance that isn't positive refused
    private sealed class Garch11CostFunction(double[] r2) : CostFunction
    {
        public override double Value(Vector x)
        {
            ArgumentNullException.ThrowIfNull(x);
            double retval = 0.0;
            double sigma2 = 0;
            double u2 = 0;
            foreach (double r in r2)
            {
                sigma2 = x[0] + (x[1] * u2) + (x[2] * sigma2);
                Positive(sigma2);
                u2 = r;
                retval += Math.Log(sigma2) + (u2 / sigma2);
            }

            return retval / (2.0 * r2.Length);
        }

        public override Vector Values(Vector x)
        {
            ArgumentNullException.ThrowIfNull(x);
            var retval = new Vector(r2.Length);
            double sigma2 = 0;
            double u2 = 0;
            int i = 0;
            foreach (double r in r2)
            {
                sigma2 = x[0] + (x[1] * u2) + (x[2] * sigma2);
                Positive(sigma2);
                u2 = r;
                retval[i++] = (Math.Log(sigma2) + (u2 / sigma2)) / (2.0 * r2.Length);
            }

            return retval;
        }

        public override void Gradient(Vector grad, Vector x) => ValueAndGradient(grad, x);

        public override double ValueAndGradient(Vector grad, Vector x)
        {
            ArgumentNullException.ThrowIfNull(grad);
            ArgumentNullException.ThrowIfNull(x);
            double g0 = 0.0, g1 = 0.0, g2 = 0.0;
            double retval = 0.0;
            double sigma2 = 0;
            double u2 = 0;
            double sigma2prev = sigma2;
            double u2prev = u2;
            double d0 = 0.0, d1 = 0.0, d2 = 0.0;
            double norm = 2.0 * r2.Length;
            foreach (double r in r2)
            {
                sigma2 = x[0] + (x[1] * u2) + (x[2] * sigma2);
                Positive(sigma2);
                u2 = r;
                retval += Math.Log(sigma2) + (u2 / sigma2);

                // ∂σ²ₜ/∂θ = (1, u²ₜ₋₁, σ²ₜ₋₁) + β ∂σ²ₜ₋₁/∂θ, where QuantLib takes the direct term alone
                d0 = 1.0 + (x[2] * d0);
                d1 = u2prev + (x[2] * d1);
                d2 = sigma2prev + (x[2] * d2);
                double w = (sigma2 - u2) / (sigma2 * sigma2);
                g0 += d0 * w;
                g1 += d1 * w;
                g2 += d2 * w;
                u2prev = u2;
                sigma2prev = sigma2;
            }

            grad[0] = g0 / norm;
            grad[1] = g1 / norm;
            grad[2] = g2 / norm;
            return retval / norm;
        }

        private static void Positive(double sigma2)
        {
            if (!(sigma2 > 0.0))
            {
                throw new NanoQLException("the GARCH(1,1) conditional variance isn't positive: squared returns less their mean");
            }
        }
    }

    // QuantLib's FitAcfProblem: the ACF's targets and the GARCH(1,1) ACF on (γ, β)
    private sealed class FitAcfProblem(double a2, double[] acf, List<int> idx) : LeastSquareProblem
    {
        public override int Size => idx.Count;

        public override void TargetAndValue(Vector x, Vector target, Vector fct2fit)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(fct2fit);
            double a4 = acf[0] + (a2 * a2);
            double gamma = x[0];
            double beta = x[1];
            target[0] = a2 * a2 / a4;
            fct2fit[0] = (1 - (3 * gamma * gamma) - (2 * beta * beta) + (4 * beta * gamma)) / (3 * (1 - (gamma * gamma)));
            target[1] = acf[1] / a4;
            fct2fit[1] = (gamma * (1 - fct2fit[0])) - beta;
            for (int i = 2; i < idx.Count; ++i)
            {
                target[i] = acf[idx[i]] / a4;
                fct2fit[i] = Math.Pow(gamma, idx[i] - 1) * fct2fit[1];
            }
        }

        public override void TargetValueAndGradient(Vector x, Matrix gradFct2fit, Vector target, Vector fct2fit)
        {
            ArgumentNullException.ThrowIfNull(x);
            ArgumentNullException.ThrowIfNull(gradFct2fit);
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(fct2fit);
            double a4 = acf[0] + (a2 * a2);
            double gamma = x[0];
            double beta = x[1];
            target[0] = a2 * a2 / a4;
            double w1 = 1 - (3 * gamma * gamma) - (2 * beta * beta) + (4 * beta * gamma);
            double w2 = 1 - (gamma * gamma);
            fct2fit[0] = w1 / (3 * w2);
            gradFct2fit[0, 0] = 2.0 / 3.0 * ((((2 * beta) - (3 * gamma)) * w2) + (2 * w1 * gamma)) / (w2 * w2);
            gradFct2fit[0, 1] = 4.0 / 3.0 * (gamma - beta) / w2;
            target[1] = acf[1] / a4;
            fct2fit[1] = (gamma * (1 - fct2fit[0])) - beta;
            gradFct2fit[1, 0] = (1 - fct2fit[0]) - (gamma * gradFct2fit[0, 0]);
            gradFct2fit[1, 1] = (-gamma * gradFct2fit[0, 1]) - 1;
            for (int i = 2; i < idx.Count; ++i)
            {
                target[i] = acf[idx[i]] / a4;
                w1 = Math.Pow(gamma, idx[i] - 1);
                fct2fit[i] = w1 * fct2fit[1];
                gradFct2fit[i, 0] = ((idx[i] - 1) * (w1 / gamma) * fct2fit[1]) + (w1 * gradFct2fit[1, 0]);
                gradFct2fit[i, 1] = w1 * gradFct2fit[1, 1];
            }
        }
    }
}
