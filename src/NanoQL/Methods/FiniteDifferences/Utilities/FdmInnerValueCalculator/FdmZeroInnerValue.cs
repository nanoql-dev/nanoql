// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdminnervaluecalculator.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>The inner value that is zero everywhere.</summary>
public sealed class FdmZeroInnerValue : FdmInnerValueCalculator
{
    /// <inheritdoc/>
    public override double InnerValue(FdmLinearOpIterator iter, double t) => 0.0;

    /// <inheritdoc/>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => 0.0;
}
