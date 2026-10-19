// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2005, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/model.hpp ql/models/model.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: calibrate and value, with their cost function, which wait for the calibration helpers and the cases that calibrate

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
}
