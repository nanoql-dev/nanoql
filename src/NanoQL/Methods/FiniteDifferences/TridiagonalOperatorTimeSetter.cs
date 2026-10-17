// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2003, 2004, 2005, 2006 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2002, 2003, 2011 Ferdinando Ametrano
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/tridiagonaloperator.hpp ql/methods/finitedifferences/tridiagonaloperator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The time-setting logic of a time-dependent <see cref="TridiagonalOperator"/>: QuantLib's nested
/// <c>TridiagonalOperator::TimeSetter</c>.
/// </summary>
/// <remarks>
/// An extension point: a derived operator sets its protected time setter to one, and <see cref="TridiagonalOperator.SetTime"/>
/// calls it. It should hold no state of its own that a call changes, since copies of an operator share it.
/// </remarks>
public abstract class TridiagonalOperatorTimeSetter
{
    /// <summary>Sets the operator's coefficients for a time.</summary>
    /// <param name="t">The time.</param>
    /// <param name="L">The operator, which the setter changes through its row setters.</param>
    public abstract void SetTime(double t, TridiagonalOperator L);
}
