// SPDX-FileCopyrightText: Copyright (C) 2006 Joseph Wang
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/volatilitymodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL;

/// <summary>
/// A local volatility estimator: a volatility for each date of a series of quotes, from the quotes around it.
/// </summary>
/// <remarks>
/// An extension point, as in QuantLib: each estimator derives from it. Estimators are immutable, so they answer the
/// same on every thread.
/// </remarks>
/// <typeparam name="T">The quote's type.</typeparam>
public abstract class LocalVolatilityEstimator<T>
{
    /// <summary>Initializes a new instance of the <see cref="LocalVolatilityEstimator{T}"/> class.</summary>
    protected LocalVolatilityEstimator()
    {
    }

    /// <summary>The volatilities the quotes give.</summary>
    /// <param name="quoteSeries">The quotes.</param>
    /// <returns>A volatility for each date the estimator reaches.</returns>
    public abstract TimeSeries<double> Calculate(TimeSeries<T> quoteSeries);
}
