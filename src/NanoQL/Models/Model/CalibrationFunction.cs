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
/// The cost function of a calibration: the helpers' calibration errors under the model with the trial parameters,
/// QuantLib's <c>CalibratedModel::CalibrationFunction</c>.
/// </summary>
/// <remarks>
/// QuantLib's sets each trial's parameters into the model it calibrates, whose helpers' engines hold it; here each
/// evaluation builds a new model with the trial parameters (<see cref="ICalibratedModel.WithParams"/>) and prices the
/// helpers against it. The cost is the square root of the weighted sum of the squared errors, with no division by the
/// number of helpers, unlike <see cref="CostFunction"/>'s default.
/// </remarks>
internal sealed class CalibrationFunction : CostFunction
{
    private readonly ICalibratedModel model;
    private readonly ImmutableArray<CalibrationHelper> instruments;
    private readonly ImmutableArray<double> weights;
    private readonly Projection projection;
    private readonly EvaluationContext context;

    /// <summary>Initializes a new instance of the <see cref="CalibrationFunction"/> class.</summary>
    /// <param name="model">The model being calibrated, whose trial copies the helpers price against.</param>
    /// <param name="instruments">The helpers.</param>
    /// <param name="weights">One weight per helper.</param>
    /// <param name="projection">The projection of the model's parameters onto the free ones.</param>
    /// <param name="context">The evaluation context the helpers price under.</param>
    internal CalibrationFunction(
        ICalibratedModel model,
        ImmutableArray<CalibrationHelper> instruments,
        ImmutableArray<double> weights,
        Projection projection,
        EvaluationContext context)
    {
        this.model = model;
        this.instruments = instruments;
        this.weights = weights;
        this.projection = projection;
        this.context = context;
    }

    /// <summary>Gets the step of the finite differences: 1e-6, as QuantLib's calibration function sets it.</summary>
    public override double FiniteDifferenceEpsilon => 1e-6;

    /// <summary>The cost at the free parameters given: the square root of the weighted sum of the squared errors.</summary>
    /// <param name="x">The free parameters.</param>
    /// <returns>The cost.</returns>
    public override double Value(Vector x)
    {
        ICalibratedModel trial = model.WithParams(projection.Include(x));
        double value = 0.0;
        for (int i = 0; i < instruments.Length; i++)
        {
            double diff = instruments[i].CalibrationError(trial, context);
            value += diff * diff * weights[i];
        }

        return Math.Sqrt(value);
    }

    /// <summary>The values at the free parameters given: each helper's error times the square root of its weight.</summary>
    /// <param name="x">The free parameters.</param>
    /// <returns>A new vector, one value per helper.</returns>
    public override Vector Values(Vector x)
    {
        ICalibratedModel trial = model.WithParams(projection.Include(x));
        var values = new Vector(instruments.Length);
        for (int i = 0; i < instruments.Length; i++)
        {
            values[i] = instruments[i].CalibrationError(trial, context) * Math.Sqrt(weights[i]);
        }

        return values;
    }
}
