// SPDX-FileCopyrightText: Copyright (C) 2009 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/solvers/fdmbackwardsolver.hpp ql/methods/finitedifferences/solvers/fdmbackwardsolver.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Methods;

/// <summary>
/// The description of a finite-difference scheme: its type and its two parameters, θ and μ, which the method of lines
/// and TR-BDF2 read as their own parameters.
/// </summary>
/// <param name="Type">The scheme's type.</param>
/// <param name="Theta">θ: the implicit weight; the method of lines' tolerance; TR-BDF2's α.</param>
/// <param name="Mu">μ: the second predictor's weight; the method of lines' relative first step; TR-BDF2's
/// tolerance.</param>
public readonly record struct FdmSchemeDesc(FdmSchemeDescFdmSchemeType Type, double Theta, double Mu)
{
    /// <summary>The Douglas scheme, θ = ½: Crank-Nicolson in one dimension.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc Douglas() => new(FdmSchemeDescFdmSchemeType.DouglasType, 0.5, 0.0);

    /// <summary>The Crank-Nicolson scheme, θ = ½.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc CrankNicolson() => new(FdmSchemeDescFdmSchemeType.CrankNicolsonType, 0.5, 0.0);

    /// <summary>The Craig-Sneyd scheme, θ = μ = ½.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc CraigSneyd() => new(FdmSchemeDescFdmSchemeType.CraigSneydType, 0.5, 0.5);

    /// <summary>The modified Craig-Sneyd scheme, θ = μ = ⅓.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc ModifiedCraigSneyd() =>
        new(FdmSchemeDescFdmSchemeType.ModifiedCraigSneydType, 1.0 / 3.0, 1.0 / 3.0);

    /// <summary>The Hundsdorfer-Verwer scheme, θ = ½ + √3/6, μ = ½.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc Hundsdorfer() =>
        new(FdmSchemeDescFdmSchemeType.HundsdorferType, 0.5 + (Math.Sqrt(3.0) / 6), 0.5);

    /// <summary>The modified Hundsdorfer-Verwer scheme, θ = 1 − √2/2, μ = ½.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc ModifiedHundsdorfer() =>
        new(FdmSchemeDescFdmSchemeType.HundsdorferType, 1.0 - (Math.Sqrt(2.0) / 2), 0.5);

    /// <summary>The explicit Euler scheme.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc ExplicitEuler() => new(FdmSchemeDescFdmSchemeType.ExplicitEulerType, 0.0, 0.0);

    /// <summary>The implicit Euler scheme.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc ImplicitEuler() => new(FdmSchemeDescFdmSchemeType.ImplicitEulerType, 0.0, 0.0);

    /// <summary>The method of lines.</summary>
    /// <param name="eps">The solver's tolerance.</param>
    /// <param name="relInitStepSize">The solver's first step, relative to the time step.</param>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc MethodOfLines(double eps = 0.001, double relInitStepSize = 0.01) =>
        new(FdmSchemeDescFdmSchemeType.MethodOfLinesType, eps, relInitStepSize);

    /// <summary>The TR-BDF2 scheme, α = 2 − √2, its BDF2 stage's tolerance 1e-8.</summary>
    /// <returns>The description.</returns>
    public static FdmSchemeDesc TrBDF2() => new(FdmSchemeDescFdmSchemeType.TrBDF2Type, 2 - MathConstants.M_SQRT2, 1e-8);
}
