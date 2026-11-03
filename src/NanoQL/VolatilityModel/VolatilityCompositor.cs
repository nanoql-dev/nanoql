// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/volatilitymodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// A volatility compositor: a volatility series from a series of returns or volatilities, by a model it can calibrate
/// to the series.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: each model derives from it. Immutable: QuantLib's <c>calibrate</c> changes the
/// object, where <see cref="Calibrate"/> returns the calibrated model and changes nothing.
/// </remarks>
public abstract class VolatilityCompositor
{
    /// <summary>Initializes a new instance of the <see cref="VolatilityCompositor"/> class.</summary>
    protected VolatilityCompositor()
    {
    }

    /// <summary>The volatilities the model gives on the series.</summary>
    /// <param name="volatilitySeries">The series.</param>
    /// <returns>The volatility series.</returns>
    public abstract TimeSeries<double> Calculate(TimeSeries<double> volatilitySeries);

    /// <summary>The model calibrated to the series.</summary>
    /// <param name="volatilitySeries">The series.</param>
    /// <returns>A new model, or this one where calibration changes nothing.</returns>
    public abstract VolatilityCompositor Calibrate(TimeSeries<double> volatilitySeries);
}
