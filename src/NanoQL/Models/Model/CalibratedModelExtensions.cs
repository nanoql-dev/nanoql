// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>QuantLib's members of <c>CalibratedModel</c> that return a new model, for any <see cref="ICalibratedModel"/>.</summary>
public static class CalibratedModelExtensions
{
    /// <summary>A new model, of the receiver's type, with the given parameters: QuantLib's <c>setParams</c>.</summary>
    /// <typeparam name="TModel">The model's type.</typeparam>
    /// <param name="model">The model, which doesn't change.</param>
    /// <param name="parameters">The parameters, as many as the model's <see cref="ICalibratedModel.Params"/> gives.</param>
    /// <returns>The new model.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="model"/> or <paramref name="parameters"/> is null.</exception>
    /// <exception cref="ArgumentException">The parameters are too few or too many.</exception>
    /// <remarks>As QuantLib's, the new parameters aren't checked against the model's constraints.</remarks>
    public static TModel SetParams<TModel>(this TModel model, Vector parameters)
        where TModel : ICalibratedModel
    {
        ArgumentNullException.ThrowIfNull(model);
        return (TModel)model.WithParams(parameters);
    }

    /// <summary>
    /// Calibrates the model to a set of instruments: QuantLib's <c>calibrate</c>, returning the calibrated model, of
    /// the receiver's type, with QuantLib's record of the calibration.
    /// </summary>
    /// <typeparam name="TModel">The model's type.</typeparam>
    /// <param name="model">The model, whose parameters start the optimization; it doesn't change.</param>
    /// <param name="helpers">The instruments, which it copies.</param>
    /// <param name="method">The optimization method.</param>
    /// <param name="endCriteria">The criteria that end the optimization.</param>
    /// <param name="context">The evaluation context the helpers price under.</param>
    /// <param name="constraint">
    /// A constraint added to the model's own; <see langword="null"/>, the default, QuantLib's empty constraint, for the
    /// model's alone.
    /// </param>
    /// <param name="weights">One weight per helper; empty, the default, for all 1.</param>
    /// <param name="fixParameters">
    /// Whether each of the model's parameters is fixed at its value; empty, the default, for all free.
    /// </param>
    /// <returns>The calibrated model, the end criterion, the problem values and the number of function evaluations.</returns>
    /// <exception cref="ArgumentNullException">An argument but the constraint is null.</exception>
    /// <exception cref="ArgumentException">
    /// No helper is given, or a null one; the weights aren't one per helper; the flags aren't one per parameter; or
    /// every parameter is fixed.
    /// </exception>
    /// <exception cref="NanoQLException">The optimization or a helper's pricing fails.</exception>
    /// <remarks>
    /// <para>
    /// QuantLib's sequence and counts: the checks of the helpers, the weights and the flags, in that order; the
    /// projection of the fixed parameters and the projected constraint; the minimization of the square root of the
    /// weighted sum of the squared calibration errors, each evaluation pricing the helpers against a new model built
    /// from its trial parameters; the model built from the optimum; and one more evaluation of the cost function's
    /// values for the problem values, which the count of function evaluations includes, as QuantLib's does.
    /// </para>
    /// <para>
    /// QuantLib's writes the trial parameters, and then the result, into the model, whose helpers' engines hold it,
    /// and keeps the record in the model; here the receiver never changes, and the helpers price against the model
    /// they are given.
    /// </para>
    /// </remarks>
    public static CalibrationResults<TModel> Calibrate<TModel>(
        this TModel model,
        IEnumerable<CalibrationHelper> helpers,
        OptimizationMethod method,
        EndCriteria endCriteria,
        EvaluationContext context,
        Constraint? constraint = null,
        ReadOnlySpan<double> weights = default,
        ReadOnlySpan<bool> fixParameters = default)
        where TModel : ICalibratedModel
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(helpers);
        ArgumentNullException.ThrowIfNull(method);
        ArgumentNullException.ThrowIfNull(endCriteria);
        ImmutableArray<CalibrationHelper> instruments = Copied(helpers);

        if (instruments.IsEmpty)
        {
            throw new ArgumentException("no instruments provided", nameof(helpers));
        }

        Constraint c = constraint is null ? model.Constraint : new CompositeConstraint(model.Constraint, constraint);

        if (!(weights.IsEmpty || weights.Length == instruments.Length))
        {
            throw new ArgumentException(
                $"mismatch between number of instruments ({instruments.Length}) and weights ({weights.Length})",
                nameof(weights));
        }

        ImmutableArray<double> w = weights.IsEmpty ? ImmutableArray.CreateRange(Enumerable.Repeat(1.0, instruments.Length)) : [.. weights];

        Vector prms = model.Params();
        if (!(fixParameters.IsEmpty || fixParameters.Length == prms.Size))
        {
            throw new ArgumentException(
                $"mismatch between number of parameters ({prms.Size}) and fixed-parameter specs ({fixParameters.Length})",
                nameof(fixParameters));
        }

        if (prms.Size == 0)
        {
            // QuantLib's projection's refusal, of a model with no parameters.
            throw new ArgumentException("numberOfFreeParameters==0", nameof(model));
        }

        bool[] all = new bool[prms.Size];
        var proj = new Projection(prms.AsSpan(), !fixParameters.IsEmpty ? fixParameters : all);
        var f = new CalibrationFunction(model, instruments, w, proj, context);
        var pc = new ProjectedConstraint(c, proj);
        var prob = new Problem(f, pc, proj.Project(prms).AsSpan());
        var state = new ProblemState(prob);
        EndCriteriaType endCriteriaType = method.Minimize(state, endCriteria);
        Vector result = state.CurrentValue.Clone();
        TModel calibrated = model.SetParams(proj.Include(result));
        Vector problemValues = state.Values(result);
        return new CalibrationResults<TModel>(
            calibrated, endCriteriaType, [.. problemValues.AsSpan()], state.FunctionEvaluation);
    }

    /// <summary>
    /// The calibration's cost at the given parameters, with unit weights: QuantLib's <c>value</c>, which leaves the
    /// model at the parameters, while here nothing changes.
    /// </summary>
    /// <param name="model">The model, which doesn't change.</param>
    /// <param name="parameters">The parameters, as many as the model's <see cref="ICalibratedModel.Params"/> gives.</param>
    /// <param name="helpers">The instruments; none gives a cost of 0.</param>
    /// <param name="context">The evaluation context the helpers price under.</param>
    /// <returns>The square root of the sum of the squared calibration errors under a model with the parameters.</returns>
    /// <exception cref="ArgumentNullException">An argument is null.</exception>
    /// <exception cref="ArgumentException">
    /// The parameters are empty, too few or too many, or a helper is null.
    /// </exception>
    /// <exception cref="NanoQLException">A helper's pricing fails.</exception>
    public static double Value(
        this ICalibratedModel model, Vector parameters, IEnumerable<CalibrationHelper> helpers, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(model);
        ArgumentNullException.ThrowIfNull(parameters);
        ArgumentNullException.ThrowIfNull(helpers);
        ImmutableArray<CalibrationHelper> instruments = Copied(helpers);
        if (parameters.Size == 0)
        {
            // QuantLib's projection's refusal, of the parameters given.
            throw new ArgumentException("numberOfFreeParameters==0", nameof(parameters));
        }

        ImmutableArray<double> w = ImmutableArray.CreateRange(Enumerable.Repeat(1.0, instruments.Length));
        var p = new Projection(parameters.AsSpan());
        var f = new CalibrationFunction(model, instruments, w, p, context);
        return f.Value(parameters);
    }

    // The helpers, copied; QuantLib's vector may hold a null one, which its cost function dereferences.
    private static ImmutableArray<CalibrationHelper> Copied(IEnumerable<CalibrationHelper> helpers)
    {
        ImmutableArray<CalibrationHelper> copied = [.. helpers];
        foreach (CalibrationHelper helper in copied)
        {
            if (helper is null)
            {
                throw new ArgumentException("null calibration helper", nameof(helpers));
            }
        }

        return copied;
    }
}
