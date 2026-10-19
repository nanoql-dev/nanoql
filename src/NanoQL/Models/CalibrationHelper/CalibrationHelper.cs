// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/calibrationhelper.hpp ql/models/calibrationhelper.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>The abstract base class for calibration helpers: an instrument whose error a calibration minimizes.</summary>
/// <remarks>
/// QuantLib's helper prices against the model its engine holds; here it prices against the model it is given, so one
/// helper serves every trial model of a calibration. An extension point, as in QuantLib: the Black calibration
/// helpers derive from it.
/// </remarks>
public abstract class CalibrationHelper
{
    /// <summary>Initializes a new instance of the <see cref="CalibrationHelper"/> class.</summary>
    protected CalibrationHelper()
    {
    }

    /// <summary>The error resulting from the model's valuation of the instrument.</summary>
    /// <param name="model">The model the instrument is priced against.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The error.</returns>
    public abstract double CalibrationError(ICalibratedModel model, EvaluationContext context);
}
