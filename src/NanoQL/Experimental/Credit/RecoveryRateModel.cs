// SPDX-FileCopyrightText: Copyright (C) 2009 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/credit/recoveryratemodel.hpp ql/experimental/credit/recoveryratemodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>A model of the recovery rate: the future value of a recovery rate in the event of a default.</summary>
/// <remarks>An extension point; immutable.</remarks>
[Experimental("NQL0001")]
public abstract class RecoveryRateModel
{
    /// <summary>The expected recovery rate at a future date, conditional on a default event type and seniority.</summary>
    /// <param name="defaultDate">The default date; not checked.</param>
    /// <param name="defaultKey">The default key; QuantLib's empty key when <see langword="null"/>.</param>
    /// <returns>The recovery, or <see langword="null"/> where the model can't give one for the seniority.</returns>
    public virtual double? RecoveryValue(Date defaultDate, DefaultProbKey? defaultKey = null) =>

        // no check on dates...
        RecoveryValueImpl(defaultDate, defaultKey ?? new DefaultProbKey());

    /// <summary>Whether the model gives recovery rates for a seniority.</summary>
    /// <param name="seniority">The seniority.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    public abstract bool AppliesToSeniority(Seniority seniority);

    /// <summary>The recovery the model gives.</summary>
    /// <param name="defaultDate">The default date.</param>
    /// <param name="defaultKey">The default key.</param>
    /// <returns>The recovery, or <see langword="null"/> where the model can't give one for the seniority.</returns>
    protected abstract double? RecoveryValueImpl(Date defaultDate, DefaultProbKey defaultKey);
}
