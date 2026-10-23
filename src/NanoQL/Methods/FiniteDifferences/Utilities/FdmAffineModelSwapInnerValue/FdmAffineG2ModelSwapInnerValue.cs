// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.hpp ql/methods/finitedifferences/utilities/fdmaffinemodelswapinnervalue.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.Time;

namespace NanoQL.Methods;

/// <summary>QuantLib-Python's name for <see cref="FdmAffineModelSwapInnerValue{TModel}"/> on <see cref="G2"/>.</summary>
/// <remarks>A convenience (the playbook's named classes): nanoql's own code uses the generic type.</remarks>
public sealed class FdmAffineG2ModelSwapInnerValue : FdmAffineModelSwapInnerValue<G2>
{
    /// <summary>Initializes a new instance of the <see cref="FdmAffineG2ModelSwapInnerValue"/> class.</summary>
    /// <param name="disModel">The discounting model, on the discount curve.</param>
    /// <param name="fwdModel">The forwarding model, on the index's curve.</param>
    /// <param name="swap">The swap: a vanilla or an overnight-indexed swap, its nominal constant.</param>
    /// <param name="exerciseDates">The exercise dates by their times on the discount curve's day counter.</param>
    /// <param name="mesher">The mesher.</param>
    /// <param name="direction">The model's first direction in the mesher.</param>
    /// <param name="context">The evaluation context, whose fixings and settings the coupons' amounts read.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmAffineG2ModelSwapInnerValue(
        G2 disModel,
        G2 fwdModel,
        FixedVsFloatingSwap swap,
        IReadOnlyDictionary<double, Date> exerciseDates,
        FdmMesher mesher,
        int direction,
        EvaluationContext context)
        : base(disModel, fwdModel, swap, exerciseDates, mesher, direction, context)
    {
    }
}
