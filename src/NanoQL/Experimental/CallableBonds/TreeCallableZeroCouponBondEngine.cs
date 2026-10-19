// SPDX-FileCopyrightText: Copyright (C) 2008 Allen Kuo
// SPDX-FileCopyrightText: Copyright (C) 2021 Ralf Konrad Eckel
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/callablebonds/treecallablebondengine.hpp ql/experimental/callablebonds/treecallablebondengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Models;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The numerical lattice engine for callable zero-coupon bonds: QuantLib's subclass of the fixed-rate bonds' engine,
/// which only adds constructors, so each overload is <see cref="TreeCallableFixedRateBondEngine"/>'s.
/// </summary>
[Experimental("NQL0001")]
public static class TreeCallableZeroCouponBondEngine
{
    /// <summary>Prices a callable bond on the model's tree, with a number of time steps.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeSteps">The number of time steps of the tree, on a grid through the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the tree's short rates.</param>
    /// <returns>The bond's results, as <see cref="TreeCallableFixedRateBondEngine"/>'s.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="timeSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="timeSteps"/> is 0.</exception>
    /// <exception cref="NanoQLException">
    /// The bond's arguments are inconsistent; the model needs a term structure and none is given, QuantLib's empty
    /// handle; or the model's tree takes no spread.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        int timeSteps,
        YieldTermStructure? termStructure = null,
        double spread = 0.0) =>
        TreeCallableFixedRateBondEngine.Calculate(bond, context, model, timeSteps, termStructure, spread);

    /// <summary>Prices a callable bond on the model's tree, on a time grid.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">The short-rate model.</param>
    /// <param name="timeGrid">The tree's time grid, which must hold the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the tree's short rates.</param>
    /// <returns>The bond's results, as <see cref="TreeCallableFixedRateBondEngine"/>'s.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The grid can't hold a tree, or misses a mandatory time; the bond's arguments are inconsistent; the model needs a
    /// term structure and none is given, QuantLib's empty handle; or the model's tree takes no spread.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        TimeGrid timeGrid,
        YieldTermStructure? termStructure = null,
        double spread = 0.0) =>
        TreeCallableFixedRateBondEngine.Calculate(bond, context, model, timeGrid, termStructure, spread);

    /// <summary>Prices a callable bond on a lattice already built from the model.</summary>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The evaluation context, which gives the bond's settlement date.</param>
    /// <param name="model">The short-rate model the lattice was built from.</param>
    /// <param name="lattice">The lattice, whose grid must hold the bond's mandatory times.</param>
    /// <param name="termStructure">
    /// The discount curve when the model isn't term-structure consistent, or <see langword="null"/>.
    /// </param>
    /// <param name="spread">The continuously compounded spread added to the lattice's short rates.</param>
    /// <returns>The bond's results, as <see cref="TreeCallableFixedRateBondEngine"/>'s.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The grid misses a mandatory time; the bond's arguments are inconsistent; the model needs a term structure and
    /// none is given, QuantLib's empty handle; or a spread is given for a lattice that isn't a one-factor short-rate
    /// tree.
    /// </exception>
    public static CallableBondResults Calculate(
        CallableBond bond,
        EvaluationContext context,
        ShortRateModel model,
        Lattice lattice,
        YieldTermStructure? termStructure = null,
        double spread = 0.0) =>
        TreeCallableFixedRateBondEngine.Calculate(bond, context, model, lattice, termStructure, spread);
}
