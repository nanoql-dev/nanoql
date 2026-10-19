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

/// <summary>
/// A calibration's result: the calibrated model, and QuantLib's record of the calibration that produced it, which
/// QuantLib's <c>CalibratedModel</c> keeps in the model it calibrates.
/// </summary>
/// <typeparam name="TModel">The model's type.</typeparam>
/// <remarks>
/// Immutable, and compared by reference, as the model it holds is. QuantLib's record is unset before a calibration (its
/// evaluation count has no initial value); here it exists only once a calibration has run.
/// </remarks>
public sealed class CalibrationResults<TModel>
    where TModel : ICalibratedModel
{
    internal CalibrationResults(
        TModel model, EndCriteriaType endCriteria, ImmutableArray<double> problemValues, int functionEvaluation)
    {
        Model = model;
        EndCriteria = endCriteria;
        ProblemValues = problemValues;
        FunctionEvaluation = functionEvaluation;
    }

    /// <summary>Gets the calibrated model: the receiver's type, with the parameters the optimization found.</summary>
    public TModel Model { get; }

    /// <summary>Gets the criterion that ended the optimization: QuantLib's <c>endCriteria()</c>.</summary>
    public EndCriteriaType EndCriteria { get; }

    /// <summary>
    /// Gets the cost function's values at the calibrated parameters, each helper's calibration error times the square
    /// root of its weight: QuantLib's <c>problemValues()</c>.
    /// </summary>
    public ImmutableArray<double> ProblemValues { get; }

    /// <summary>
    /// Gets the number of evaluations of the cost function: QuantLib's <c>functionEvaluation()</c>, which counts the
    /// evaluation of the problem values after the optimization.
    /// </summary>
    public int FunctionEvaluation { get; }
}
