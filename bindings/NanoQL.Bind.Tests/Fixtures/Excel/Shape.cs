// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Tests.Fixtures.Excel;

/// <summary>A base with instance members a listed derived type inherits, a static member and a static getter it doesn't (XLD33).</summary>
public abstract class Shape
{
    /// <summary>Gets a static instance, which a derived type doesn't inherit.</summary>
    public static Shape Unit => new Square(1);

    /// <summary>Gets the area, inherited by a listed derived type.</summary>
    public double Area => Side() * Side();

    /// <summary>A static member, which stays with its own type.</summary>
    /// <param name="s">A shape.</param>
    /// <returns>Its side.</returns>
    public static double SideOf(Shape s) => s.Side();

    /// <summary>The side, overridden by the derived type, so it reaches the derived type once.</summary>
    /// <returns>The side.</returns>
    public abstract double Side();

    /// <summary>A scaled side, overridden too.</summary>
    /// <param name="k">The scale.</param>
    /// <returns>The scaled side.</returns>
    public virtual double Scaled(double k) => k * Side();
}
