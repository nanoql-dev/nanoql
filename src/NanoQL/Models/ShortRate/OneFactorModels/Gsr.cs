// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/onefactormodels/gsr.hpp ql/models/shortrate/onefactormodels/gsr.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>
/// The one-factor GSR model, formulated in the forward measure of its numeraire time: piecewise constant volatilities
/// on the step dates, and a constant reversion or piecewise constant reversions on the same dates.
/// </summary>
/// <remarks>
/// <para>
/// Immutable, a <see cref="Gaussian1dModel"/> implementing <see cref="ICalibratedModel"/> over its two arguments, the
/// reversions and the volatilities, in QuantLib's order (the S-g1d plan's G1R2 (i); master plan §2.2's calibration
/// row): a calibration returns a new model. QuantLib's quotes are values (master plan §2's quote row), so its four
/// constructors are two. Its memo of the process's integrals lives for one call (G1R1 (iii)), so QuantLib's
/// <c>flushCache</c> and its observers have no counterpart.
/// </para>
/// <para>
/// <see cref="WithNumeraireTime"/> returns a copy, its process moved to the new forward measure time with its core, and
/// refuses a time below the last step time, as QuantLib's master does after the pin (#2842); QuantLib 1.44's moves the
/// process and leaves its core at the constructor's time. A curve given to the numeraire is read with extrapolation,
/// as the model's own is, where QuantLib's reads a given one at the numeraire time without it (the plan's G1R6 (i);
/// <c>Divergences/Gaussian1dNumeraireExtrapolationTests.cs</c>).
/// </para>
/// </remarks>
public sealed class Gsr : Gaussian1dModel, ICalibratedModel
{
    private readonly ImmutableArray<Date> volstepdates;
    private readonly ImmutableArray<double> volsteptimes;
    private readonly ImmutableArray<Parameter> arguments;
    private readonly int reversionCount;
    private readonly GsrProcess process;

    /// <summary>Initializes a new instance of the <see cref="Gsr"/> class, with a constant reversion.</summary>
    /// <param name="termStructure">The model's curve.</param>
    /// <param name="volstepdates">The volatilities' step dates, increasing, after the curve's reference date.</param>
    /// <param name="volatilities">The volatilities: one more than the step dates.</param>
    /// <param name="reversion">The constant reversion.</param>
    /// <param name="T">The numeraire time, the process's forward measure time; 60 by default, QuantLib's. It may lie
    /// below the last step date, as QuantLib accepts it: a step past it moves nothing, and nothing past it is read.</param>
    /// <exception cref="ArgumentNullException"><paramref name="termStructure"/>, <paramref name="volstepdates"/> or
    /// <paramref name="volatilities"/> is null.</exception>
    /// <exception cref="ArgumentException">
    /// A step date isn't after the reference date or after the date before it, or the volatilities aren't one more
    /// than the step dates.
    /// </exception>
    public Gsr(YieldTermStructure termStructure, IEnumerable<Date> volstepdates, IEnumerable<double> volatilities, double reversion, double T = 60.0)
        : this(Checked(termStructure), Copied(volstepdates), Copied(volatilities), [reversion], T)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="Gsr"/> class, with piecewise constant
    /// reversions.</summary>
    /// <param name="termStructure">The model's curve.</param>
    /// <param name="volstepdates">The step dates of the volatilities and reversions, increasing, after the curve's
    /// reference date.</param>
    /// <param name="volatilities">The volatilities: one more than the step dates.</param>
    /// <param name="reversions">The reversions: one, constant, or one more than the step dates.</param>
    /// <param name="T">The numeraire time, the process's forward measure time; 60 by default, QuantLib's. It may lie
    /// below the last step date, as QuantLib accepts it: a step past it moves nothing, and nothing past it is read.</param>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// A step date isn't after the reference date or after the date before it, or the volatilities or the reversions
    /// don't match the step dates.
    /// </exception>
    public Gsr(YieldTermStructure termStructure, IEnumerable<Date> volstepdates, IEnumerable<double> volatilities, IEnumerable<double> reversions, double T = 60.0)
        : this(Checked(termStructure), Copied(volstepdates), Copied(volatilities), Copied(reversions), T)
    {
    }

    // QuantLib's initialize(T): the step times, the counts, the two parameters and the process.
    private Gsr(YieldTermStructure termStructure, ImmutableArray<Date> volstepdates, ImmutableArray<double> volatilities, ImmutableArray<double> reversions, double T)
        : base(termStructure)
    {
        this.volstepdates = volstepdates;
        volsteptimes = UpdateTimes(termStructure, volstepdates);

        if (!(volatilities.Length == volsteptimes.Length + 1))
        {
            throw new ArgumentException(
                $"there must be n+1 volatilities ({volatilities.Length}) for n volatility step times ({volsteptimes.Length})",
                nameof(volatilities));
        }

        if (!(reversions.Length == 1 || reversions.Length == volsteptimes.Length + 1))
        {
            throw new ArgumentException(
                $"there must be 1 or n+1 reversions ({reversions.Length}) for n volatility step times ({volsteptimes.Length})",
                nameof(reversions));
        }

        Parameter reversion;
        if (reversions.Length == 1)
        {
            reversion = new ConstantParameter(reversions[0], new NoConstraint());
        }
        else
        {
            reversion = new PiecewiseConstantParameter(volsteptimes, new NoConstraint());
            for (int i = 0; i < reversion.Size; i++)
            {
                reversion = reversion.SetParam(i, reversions[i]);
            }
        }

        Parameter sigma = new PiecewiseConstantParameter(volsteptimes, new NoConstraint());
        for (int i = 0; i < sigma.Size; i++)
        {
            sigma = sigma.SetParam(i, volatilities[i]);
        }

        arguments = [reversion, sigma];
        reversionCount = reversions.Length;
        process = new GsrProcess(new GsrProcessCore(volsteptimes, ParamsOf(sigma), ParamsOf(reversion), T), null, null);
        SetStateProcess(process);
    }

    // A copy with other arguments or another process, its step dates and times kept.
    private Gsr(Gsr other, ImmutableArray<Parameter> arguments, GsrProcess process)
        : base(other.TermStructure)
    {
        volstepdates = other.volstepdates;
        volsteptimes = other.volsteptimes;
        reversionCount = other.reversionCount;
        this.arguments = arguments;
        this.process = process;
        SetStateProcess(process);
    }

    /// <inheritdoc/>
    public ImmutableArray<Parameter> Arguments => arguments;

    /// <inheritdoc/>
    public Constraint Constraint => new CalibratedModel.PrivateConstraint(arguments);

    /// <summary>Gets the reversions: QuantLib's <c>reversion()</c>, the reversion argument's parameters.</summary>
    public ImmutableArray<double> Reversion => ParamsOf(arguments[0]);

    /// <summary>Gets the volatilities: QuantLib's <c>volatility()</c>, the volatility argument's parameters.</summary>
    public ImmutableArray<double> Volatility => ParamsOf(arguments[1]);

    /// <summary>Gets the numeraire time, the state process's forward measure time.</summary>
    public double NumeraireTime => process.ForwardMeasureTime;

    /// <inheritdoc/>
    public Vector Params() => CalibratedModel.FlattenParams(arguments);

    /// <inheritdoc/>
    /// <remarks>
    /// QuantLib's <c>setParams</c> with its <c>generateArguments</c>: the process takes the new volatilities and
    /// reversions, its step times and forward measure time kept. As QuantLib's, the parameters aren't checked against
    /// the arguments' constraints, and the refusals of a wrong size come before any write. The copy's core takes its
    /// zero-reversion flags from the new reversions, the model built at them; QuantLib's recomputes them from the old
    /// ones before it sets the new, so its model keeps the previous parameters' flags
    /// (<c>Divergences/GsrWithParamsTests.cs</c>).
    /// </remarks>
    public ICalibratedModel WithParams(Vector parameters)
    {
        ImmutableArray<Parameter> newArguments = CalibratedModel.ArgumentsWithParams(arguments, parameters);
        var core = process.Core.WithParameters(ParamsOf(newArguments[1]), ParamsOf(newArguments[0]));
        return new Gsr(this, newArguments, new GsrProcess(core, null, null));
    }

    /// <summary>A copy of the model at another numeraire time: QuantLib's <c>numeraireTime(T)</c>.</summary>
    /// <param name="T">The numeraire time.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    /// <exception cref="ArgumentException"><paramref name="T"/> is below the last step time.</exception>
    public Gsr WithNumeraireTime(double T)
    {
        var core = process.Core.WithForwardMeasureTime(T, nameof(T));
        return new Gsr(this, arguments, new GsrProcess(core, null, null));
    }

    /// <summary>The calibration flags that fix the reversions, the volatilities free.</summary>
    /// <returns>One flag per parameter: the reversions' set, the volatilities' clear.</returns>
    public ImmutableArray<bool> FixedReversions() => Flags(reversionCount, true, Volatility.Length, false);

    /// <summary>The calibration flags that fix the volatilities, the reversions free.</summary>
    /// <returns>One flag per parameter: the reversions' clear, the volatilities' set.</returns>
    public ImmutableArray<bool> FixedVolatilities() => Flags(reversionCount, false, Volatility.Length, true);

    /// <summary>The calibration flags that leave only one volatility free.</summary>
    /// <param name="i">The volatility's index.</param>
    /// <returns>One flag per parameter, every one set but the volatility's.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="ArgumentException">No volatility has the index.</exception>
    public ImmutableArray<bool> MoveVolatility(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        int volatilities = Volatility.Length;
        if (!(i < volatilities))
        {
            throw new ArgumentException($"volatility with index {i} does not exist (0...{volatilities - 1})", nameof(i));
        }

        bool[] res = Enumerable.Repeat(true, reversionCount + volatilities).ToArray();
        res[reversionCount + i] = false;
        return [.. res];
    }

    /// <summary>The calibration flags that leave only one reversion free.</summary>
    /// <param name="i">The reversion's index.</param>
    /// <returns>One flag per parameter, every one set but the reversion's.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="i"/> is negative.</exception>
    /// <exception cref="ArgumentException">No reversion has the index.</exception>
    public ImmutableArray<bool> MoveReversion(int i)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(i);
        if (!(i < reversionCount))
        {
            throw new ArgumentException($"reversion with index {i} does not exist (0...{reversionCount - 1})", nameof(i));
        }

        bool[] res = Enumerable.Repeat(true, reversionCount + Volatility.Length).ToArray();
        res[i] = false;
        return [.. res];
    }

    /// <summary>
    /// Calibrates the volatilities one by one to the given helpers, the reversions fixed: QuantLib's
    /// <c>calibrateVolatilitiesIterative</c>, each step calibrating one volatility to its own helper.
    /// </summary>
    /// <param name="helpers">The helpers, one per volatility to move, in order; the step dates should be their fixing
    /// dates but the last.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">The criteria that end each step.</param>
    /// <param name="context">The evaluation context the helpers price under.</param>
    /// <param name="constraint">A constraint added to the model's own; <see langword="null"/>, QuantLib's empty
    /// constraint, by default.</param>
    /// <param name="weights">One weight per helper, each step weighted by its own helper's; empty, the default, for all
    /// 1.</param>
    /// <returns>The last step's record, its model the calibrated one: what QuantLib's model reports after the
    /// loop.</returns>
    /// <exception cref="ArgumentNullException">An argument but the constraint is null.</exception>
    /// <exception cref="ArgumentException">
    /// No helper is given, or a null one; the weights aren't one per helper; or a helper has no volatility to move.
    /// </exception>
    /// <exception cref="NanoQLException">An optimization or a helper's pricing fails.</exception>
    /// <remarks>
    /// QuantLib's passes the whole weight vector to each one-helper step, which its calibration refuses for every
    /// non-empty vector of more than one weight: a documented divergence (the plan's G1R6 (xiv);
    /// <c>Divergences/GsrIterativeWeightsTests.cs</c>). With no helper, QuantLib's loop does nothing and leaves the
    /// model's last record; here there is no record to return, so it is refused with its calibration's text.
    /// </remarks>
    public CalibrationResults<Gsr> CalibrateVolatilitiesIterative(
        IEnumerable<BlackCalibrationHelper> helpers,
        OptimizationMethod method,
        EndCriteria endCriteria,
        EvaluationContext context,
        Constraint? constraint = null,
        ReadOnlySpan<double> weights = default) =>
        CalibrateIterative(helpers, method, endCriteria, context, constraint, weights, (model, i) => model.MoveVolatility(i));

    /// <summary>
    /// Calibrates the reversions one by one to the given helpers, the volatilities fixed: QuantLib's
    /// <c>calibrateReversionsIterative</c>, each step calibrating one reversion to its own helper.
    /// </summary>
    /// <param name="helpers">The helpers, one per reversion to move, in order; the step dates should follow their
    /// maturities.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">The criteria that end each step.</param>
    /// <param name="context">The evaluation context the helpers price under.</param>
    /// <param name="constraint">A constraint added to the model's own; <see langword="null"/>, QuantLib's empty
    /// constraint, by default.</param>
    /// <param name="weights">One weight per helper, each step weighted by its own helper's; empty, the default, for all
    /// 1.</param>
    /// <returns>The last step's record, its model the calibrated one: what QuantLib's model reports after the
    /// loop.</returns>
    /// <exception cref="ArgumentNullException">An argument but the constraint is null.</exception>
    /// <exception cref="ArgumentException">
    /// No helper is given, or a null one; the weights aren't one per helper; or a helper has no reversion to move.
    /// </exception>
    /// <exception cref="NanoQLException">An optimization or a helper's pricing fails.</exception>
    /// <remarks>As <see cref="CalibrateVolatilitiesIterative"/>'s, for the weights and an empty set of
    /// helpers.</remarks>
    public CalibrationResults<Gsr> CalibrateReversionsIterative(
        IEnumerable<BlackCalibrationHelper> helpers,
        OptimizationMethod method,
        EndCriteria endCriteria,
        EvaluationContext context,
        Constraint? constraint = null,
        ReadOnlySpan<double> weights = default) =>
        CalibrateIterative(helpers, method, endCriteria, context, constraint, weights, (model, i) => model.MoveReversion(i));

    internal override double NumeraireCore(double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo)
    {
        if (t == 0)
        {
            return Discount(yts, process.ForwardMeasureTime, null);
        }

        return ZerobondCore(process.ForwardMeasureTime, null, t, tDate, y, yts, memo);
    }

    internal override double ZerobondCore(double T, Date? maturity, double t, Date? tDate, double y, YieldTermStructure? yts, Gaussian1dMemo memo)
    {
        if (t == 0.0)
        {
            return Discount(yts, T, maturity);
        }

        GsrProcessCoreMemo m = memo.For(process.Core);
        double x = (y * process.StdDeviation(0.0, 0.0, t, m)) + process.Expectation(0.0, 0.0, t, m);
        double gtT = process.G(t, T, m);

        double d = Discount(yts, T, maturity) / Discount(yts, t, tDate);

        return d * Math.Exp((-x * gtT) - (0.5 * process.Y(t, m) * gtT * gtT));
    }

    internal override double StateExpectation(double t0, double x0, double dt, Gaussian1dMemo memo) =>
        process.Expectation(t0, x0, dt, memo.For(process.Core));

    internal override double StateStdDeviation(double t0, double x0, double dt, Gaussian1dMemo memo) =>
        process.StdDeviation(t0, x0, dt, memo.For(process.Core));

    /// <inheritdoc/>
    protected override double NumeraireImpl(double t, double y, YieldTermStructure? yts) => NumeraireCore(t, null, y, yts, new Gaussian1dMemo());

    /// <inheritdoc/>
    protected override double ZerobondImpl(double T, double t, double y, YieldTermStructure? yts) => ZerobondCore(T, null, t, null, y, yts, new Gaussian1dMemo());

    private static ImmutableArray<T> Copied<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [.. values];
    }

    // QuantLib's "yield term structure handle is empty", checked first, as QuantLib's constructors check it.
    private static YieldTermStructure Checked(YieldTermStructure termStructure)
    {
        ArgumentNullException.ThrowIfNull(termStructure);
        return termStructure;
    }

    private static ImmutableArray<double> ParamsOf(Parameter parameter) => [.. parameter.Params().AsSpan()];

    private static ImmutableArray<bool> Flags(int first, bool firstValue, int second, bool secondValue) =>
        [.. Enumerable.Repeat(firstValue, first), .. Enumerable.Repeat(secondValue, second)];

    // QuantLib's updateTimes: each step date's time on the model curve, positive and increasing.
    private static ImmutableArray<double> UpdateTimes(YieldTermStructure termStructure, ImmutableArray<Date> volstepdates)
    {
        var times = new double[volstepdates.Length];
        for (int j = 0; j < volstepdates.Length; j++)
        {
            times[j] = termStructure.TimeFromReference(volstepdates[j]);
            if (j == 0)
            {
                if (!(times[0] > 0.0))
                {
                    throw new ArgumentException(
                        string.Create(CultureInfo.InvariantCulture, $"volsteptimes must be positive ({times[0]})"),
                        nameof(volstepdates));
                }
            }
            else if (!(times[j] > times[j - 1]))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"volsteptimes must be strictly increasing ({times[j - 1]}@{j - 1}, {times[j]}@{j})"),
                    nameof(volstepdates));
            }
        }

        return [.. times];
    }

    // QuantLib's iterative loop: one calibration per helper, each moving one parameter of the model the step before
    // returned, weighted by its own helper's weight (G1R6 (xiv)).
    private CalibrationResults<Gsr> CalibrateIterative(
        IEnumerable<BlackCalibrationHelper> helpers,
        OptimizationMethod method,
        EndCriteria endCriteria,
        EvaluationContext context,
        Constraint? constraint,
        ReadOnlySpan<double> weights,
        Func<Gsr, int, ImmutableArray<bool>> move)
    {
        ArgumentNullException.ThrowIfNull(helpers);
        ImmutableArray<BlackCalibrationHelper> instruments = [.. helpers];
        if (instruments.IsEmpty)
        {
            throw new ArgumentException("no instruments provided", nameof(helpers));
        }

        if (!(weights.IsEmpty || weights.Length == instruments.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of instruments ({instruments.Length}) and weights ({weights.Length})",
                nameof(weights));
        }

        Gsr model = this;
        CalibrationResults<Gsr>? results = null;
        for (int i = 0; i < instruments.Length; i++)
        {
            ReadOnlySpan<double> weight = weights.IsEmpty ? default : weights.Slice(i, 1);
            results = model.Calibrate([instruments[i]], method, endCriteria, context, constraint, weight, move(model, i).AsSpan());
            model = results.Model;
        }

        return results!;
    }
}
