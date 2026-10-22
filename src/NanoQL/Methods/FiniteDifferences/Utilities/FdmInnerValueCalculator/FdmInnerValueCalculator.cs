// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdminnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: FdmLogBasketInnerValue, which waits for S-exo with the basket option's payoff

namespace NanoQL.Methods;

/// <summary>The inner value of an instrument at a grid point: its payoff there, or the payoff's average over the
/// point's cell.</summary>
/// <remarks>
/// A working type (the S-eq plan's SEQR1): a calculator may fill a cache on its first call, as QuantLib's cell
/// averages do. An extension point, as in QuantLib: later units derive their calculators from it.
/// </remarks>
public abstract class FdmInnerValueCalculator
{
    /// <summary>The inner value at a point and time.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="t">The time.</param>
    /// <returns>The inner value.</returns>
    public abstract double InnerValue(FdmLinearOpIterator iter, double t);

    /// <summary>The inner value averaged over a point's cell, at a time.</summary>
    /// <param name="iter">The point.</param>
    /// <param name="t">The time.</param>
    /// <returns>The average inner value.</returns>
    public abstract double AvgInnerValue(FdmLinearOpIterator iter, double t);
}
