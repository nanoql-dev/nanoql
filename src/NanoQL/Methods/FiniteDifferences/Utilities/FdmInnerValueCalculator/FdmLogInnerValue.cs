// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp ql/methods/finitedifferences/utilities/fdminnervaluecalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The cell-averaged inner value of a payoff on a grid in the logarithm of the underlying.</summary>
/// <remarks>A working type, as its base is.</remarks>
[WorkingType]
public sealed class FdmLogInnerValue : FdmCellAveragingInnerValue
{
    /// <summary>Initializes a new instance of the <see cref="FdmLogInnerValue"/> class.</summary>
    /// <param name="payoff">The payoff.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="direction">The direction of the underlying's logarithm.</param>
    /// <exception cref="ArgumentNullException">The payoff or the mesher is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The direction isn't one of the layout's.</exception>
    public FdmLogInnerValue(Payoff payoff, FdmMesher mesher, int direction)
        : base(payoff, mesher, direction, Math.Exp)
    {
    }
}
