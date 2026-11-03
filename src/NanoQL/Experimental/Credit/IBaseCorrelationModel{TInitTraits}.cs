// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/basecorrelationlossmodel.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;

namespace NanoQL.Experimental;

/// <summary>
/// A base model of the base-correlation loss model: a loss model of one correlation, built per correlation the surface
/// gives, QuantLib's <c>setupModels</c> specialisations.
/// </summary>
/// <typeparam name="TInitTraits">The copula's initialization parameters.</typeparam>
/// <remarks>An extension point: QuantLib's four base models implement it, and a model of one's own can.</remarks>
[Experimental("NQL0001")]
public interface IBaseCorrelationModel<TInitTraits>
    where TInitTraits : struct
{
    /// <summary>The base model at a correlation over the given recoveries.</summary>
    /// <param name="correlation">The correlation.</param>
    /// <param name="recoveries">The names' recoveries.</param>
    /// <param name="traits">The copula's initialization parameters.</param>
    /// <returns>The model, unbound.</returns>
    static abstract IDefaultLossModel ForCorrelation(double correlation, ImmutableArray<double> recoveries, TInitTraits traits);
}
