// SPDX-FileCopyrightText: Copyright (C) 2008 J. Erik Radmall
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/commodities/quantity.hpp ql/experimental/commodities/quantity.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Utilities;

namespace NanoQL.Experimental;

/// <summary>An amount of a commodity, in a unit of measure.</summary>
/// <remarks>
/// <para>
/// A value: its zero value, <c>default(Quantity)</c>, is QuantLib's <c>Quantity()</c>, 0.0 of no commodity type in no
/// unit, both <see langword="null"/>.
/// </para>
/// <para>
/// The operators combine quantities in one unit as QuantLib's do, and never convert: QuantLib's operators read the
/// static <c>Quantity::conversionType</c>, and these see no context, so they behave as under its
/// <c>NoConversion</c>. On a unit mismatch every operator but <c>==</c> and <c>!=</c> throws QuantLib's "unitOfMeasure
/// mismatch and no conversion specified"; <c>==</c> returns <see langword="false"/> and <c>!=</c>
/// <see langword="true"/>, where QuantLib's throw, as <see cref="Money"/>'s do. The conversion modes are methods that
/// take the context, which holds the settings (<see cref="EvaluationContext.QuantitySettings"/>) and the conversions
/// (<see cref="EvaluationContext.UnitOfMeasureConversions"/>): <see cref="Add(Quantity, EvaluationContext)"/>,
/// <see cref="Subtract(Quantity, EvaluationContext)"/>, <see cref="Divide(Quantity, EvaluationContext)"/>,
/// <see cref="Equals(Quantity, EvaluationContext)"/>, <see cref="LessThan"/>, <see cref="LessThanOrEqual"/>,
/// <see cref="GreaterThan"/>, <see cref="GreaterThanOrEqual"/>, and <see cref="QuantityFunctions"/>' <c>Close</c> and
/// <c>CloseEnough</c>. A converted quantity is rounded by its unit's rounding, which leaves it as it is (<see
/// cref="UnitOfMeasure.Rounding"/>).
/// </para>
/// <para>
/// <see cref="Equals(Quantity)"/> and <see cref="GetHashCode"/> compare the commodity type, the unit and the amount,
/// the amount by <see cref="double.Equals(double)"/>, so that equality is reflexive.
/// </para>
/// </remarks>
[Experimental("NQL0001")]
public readonly struct Quantity : IEquatable<Quantity>
{
    /// <summary>Initializes a new instance of the <see cref="Quantity"/> struct.</summary>
    /// <param name="commodityType">The commodity type; <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="unitOfMeasure">The unit; <see langword="null"/> for QuantLib's empty one.</param>
    /// <param name="amount">The amount.</param>
    public Quantity(CommodityType? commodityType, UnitOfMeasure? unitOfMeasure, double amount)
    {
        CommodityType = commodityType;
        UnitOfMeasure = unitOfMeasure;
        Amount = amount;
    }

    /// <summary>Gets the commodity type; <see langword="null"/> for QuantLib's empty one.</summary>
    public CommodityType? CommodityType { get; }

    /// <summary>Gets the unit; <see langword="null"/> for QuantLib's empty one.</summary>
    public UnitOfMeasure? UnitOfMeasure { get; }

    /// <summary>Gets the amount.</summary>
    public double Amount { get; }

    /// <summary>The same quantity itself.</summary>
    /// <param name="q">The quantity.</param>
    /// <returns><paramref name="q"/>.</returns>
    public static Quantity operator +(Quantity q) => q;

    /// <summary>The negated quantity, in the same unit.</summary>
    /// <param name="q">The quantity.</param>
    /// <returns>The negated quantity.</returns>
    public static Quantity operator -(Quantity q) => new(q.CommodityType, q.UnitOfMeasure, -q.Amount);

    /// <summary>The sum of two quantities in one unit, of the first's commodity type.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns>The sum.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static Quantity operator +(Quantity q1, Quantity q2) => Plus(q1, q2, null);

    /// <summary>The difference of two quantities in one unit.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The quantity subtracted.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static Quantity operator -(Quantity q1, Quantity q2) => Minus(q1, q2, null);

    /// <summary>A quantity multiplied by a number.</summary>
    /// <param name="q">The quantity.</param>
    /// <param name="x">The number.</param>
    /// <returns>The product, in the quantity's unit.</returns>
    public static Quantity operator *(Quantity q, double x) => new(q.CommodityType, q.UnitOfMeasure, q.Amount * x);

    /// <summary>A quantity multiplied by a number.</summary>
    /// <param name="x">The number.</param>
    /// <param name="q">The quantity.</param>
    /// <returns>The product, in the quantity's unit.</returns>
    public static Quantity operator *(double x, Quantity q) => q * x;

    /// <summary>A quantity divided by a number.</summary>
    /// <param name="q">The quantity.</param>
    /// <param name="x">The number.</param>
    /// <returns>The quotient, in the quantity's unit.</returns>
    public static Quantity operator /(Quantity q, double x) => new(q.CommodityType, q.UnitOfMeasure, q.Amount / x);

    /// <summary>The ratio of two quantities in one unit.</summary>
    /// <param name="q1">The dividend.</param>
    /// <param name="q2">The divisor.</param>
    /// <returns>The ratio of their amounts.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static double operator /(Quantity q1, Quantity q2) => Apply(q1, q2, null, static (x, y) => x / y);

    /// <summary>Whether two quantities are equal: the same unit and equal amounts, QuantLib's test, whatever their
    /// commodity types.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns><see langword="true"/> when the units are the same and the amounts compare equal; <see langword="false"/>
    /// on a unit mismatch, where QuantLib's throws.</returns>
    public static bool operator ==(Quantity q1, Quantity q2) => q1.UnitOfMeasure == q2.UnitOfMeasure && q1.Amount == q2.Amount;

    /// <summary>Whether two quantities differ.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns>The negation of <c>==</c>.</returns>
    public static bool operator !=(Quantity q1, Quantity q2) => !(q1 == q2);

    /// <summary>Whether one quantity is less than another in the same unit.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns><see langword="true"/> when the first amount is less.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static bool operator <(Quantity q1, Quantity q2) => Apply(q1, q2, null, static (x, y) => x < y);

    /// <summary>Whether one quantity is at most another in the same unit.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns><see langword="true"/> when the first amount is at most the second.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static bool operator <=(Quantity q1, Quantity q2) => Apply(q1, q2, null, static (x, y) => x <= y);

    /// <summary>Whether one quantity is greater than another in the same unit: QuantLib's <c>q2 &lt; q1</c>.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns><see langword="true"/> when the first amount is greater.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static bool operator >(Quantity q1, Quantity q2) => q2 < q1;

    /// <summary>Whether one quantity is at least another in the same unit: QuantLib's <c>q2 &lt;= q1</c>.</summary>
    /// <param name="q1">The first quantity.</param>
    /// <param name="q2">The second quantity.</param>
    /// <returns><see langword="true"/> when the first amount is at least the second.</returns>
    /// <exception cref="NanoQLException">The units differ.</exception>
    public static bool operator >=(Quantity q1, Quantity q2) => q2 <= q1;

    /// <summary>The quantity rounded by its unit's rounding, which leaves it as it is.</summary>
    /// <returns>The rounded quantity.</returns>
    /// <exception cref="NanoQLException">The unit is QuantLib's empty one, a <see langword="null"/>.</exception>
    public Quantity Rounded() => new(CommodityType, UnitOfMeasure, UnitOf(this).Rounding.Evaluate(Amount));

    /// <summary>
    /// The sum of this quantity and another, converting as the context's <see cref="EvaluationContext.QuantitySettings"/>
    /// say: QuantLib's <c>operator+</c>.
    /// </summary>
    /// <param name="other">The quantity added.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>
    /// The sum: in this quantity's unit, or in the base unit under
    /// <see cref="QuantityConversionType.BaseUnitOfMeasureConversion"/> when the units differ.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The units differ under <see cref="QuantityConversionType.NoConversion"/>; no base unit is set under
    /// <see cref="QuantityConversionType.BaseUnitOfMeasureConversion"/>; or a conversion can't be found.
    /// </exception>
    public Quantity Add(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Plus(this, other, context);
    }

    /// <summary>
    /// The difference of this quantity and another, converting as <see cref="Add(Quantity, EvaluationContext)"/> does:
    /// QuantLib's <c>operator-</c>.
    /// </summary>
    /// <param name="other">The quantity subtracted.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>The difference.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public Quantity Subtract(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Minus(this, other, context);
    }

    /// <summary>The ratio of this quantity to another, converting as the context's quantity settings say.</summary>
    /// <param name="other">The divisor.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>The ratio of their amounts, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public double Divide(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x / y);
    }

    /// <summary>
    /// Whether this quantity equals another, converting as the context's quantity settings say: QuantLib's
    /// <c>operator==</c>.
    /// </summary>
    /// <param name="other">The other quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>
    /// Whether the amounts, once converted, compare equal; <see langword="false"/> on a unit mismatch under
    /// <see cref="QuantityConversionType.NoConversion"/>, as <c>==</c> returns, where QuantLib's throws.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">No base unit is set, or a conversion can't be found.</exception>
    public bool Equals(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (UnitOfMeasure != other.UnitOfMeasure
            && context.QuantitySettings.ConversionType == QuantityConversionType.NoConversion)
        {
            return false;
        }

        return Apply(this, other, context, static (x, y) => x == y);
    }

    /// <summary>Whether this quantity is less than another, converting as the context's quantity settings say.</summary>
    /// <param name="other">The other quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>Whether this amount is less, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public bool LessThan(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x < y);
    }

    /// <summary>Whether this quantity is at most another, converting as the context's quantity settings say.</summary>
    /// <param name="other">The other quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>Whether this amount is at most the other, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public bool LessThanOrEqual(Quantity other, EvaluationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return Apply(this, other, context, static (x, y) => x <= y);
    }

    /// <summary>
    /// Whether this quantity is greater than another: QuantLib's <c>q1 &gt; q2</c>, which is <c>q2 &lt; q1</c>, so an
    /// automated conversion converts this quantity into the other's unit.
    /// </summary>
    /// <param name="other">The other quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>Whether this amount is greater, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public bool GreaterThan(Quantity other, EvaluationContext context) => other.LessThan(this, context);

    /// <summary>
    /// Whether this quantity is at least another: QuantLib's <c>q1 &gt;= q2</c>, which is <c>q2 &lt;= q1</c>, so an
    /// automated conversion converts this quantity into the other's unit.
    /// </summary>
    /// <param name="other">The other quantity.</param>
    /// <param name="context">The evaluation context, whose quantity settings and unit conversions are read.</param>
    /// <returns>Whether this amount is at least the other, once converted.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="context"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">As <see cref="Add(Quantity, EvaluationContext)"/>.</exception>
    public bool GreaterThanOrEqual(Quantity other, EvaluationContext context) => other.LessThanOrEqual(this, context);

    /// <summary>Whether this quantity has another's commodity type, unit and amount, the amount by
    /// <see cref="double.Equals(double)"/>.</summary>
    /// <param name="other">The other quantity.</param>
    /// <returns><see langword="true"/> when all three are the same.</returns>
    public bool Equals(Quantity other) =>
        CommodityType == other.CommodityType && UnitOfMeasure == other.UnitOfMeasure && Amount.Equals(other.Amount);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Quantity other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(CommodityType, UnitOfMeasure, Amount);

    /// <summary>
    /// The quantity as QuantLib's <c>operator&lt;&lt;</c> writes it: the commodity type's code, the amount in C++'s
    /// default stream format, six significant digits, and the unit's code ("HO 1000 GAL").
    /// </summary>
    /// <returns>The text.</returns>
    /// <exception cref="NanoQLException">The commodity type or the unit is QuantLib's empty one.</exception>
    public override string ToString()
    {
        CommodityType type = CommodityType ?? throw new NanoQLException("no commodity type data provided");
        return $"{type.Code} {DataFormattersFunctions.General(Amount)} {UnitOf(this).Code}";
    }

    // QuantLib's apply on this quantity and another, for QuantityFunctions' close and close_enough.
    internal T Apply<T>(Quantity other, EvaluationContext context, Func<double, double, T> f) =>
        Apply(this, other, context, f);

    // QuantLib's UnitOfMeasure accessors on an empty unit.
    private static UnitOfMeasure UnitOf(Quantity q) =>
        q.UnitOfMeasure ?? throw new NanoQLException("no unit of measure data provided");

    // QuantLib's operator+=: within one unit the amounts' sum; otherwise converted as the settings say, or refused.
    private static Quantity Plus(Quantity q1, Quantity q2, EvaluationContext? context)
    {
        if (q1.UnitOfMeasure == q2.UnitOfMeasure)
        {
            return new Quantity(q1.CommodityType, q1.UnitOfMeasure, q1.Amount + q2.Amount);
        }

        switch (ConversionTypeOf(context))
        {
            case QuantityConversionType.BaseUnitOfMeasureConversion:
                return Plus(ConvertToBase(q1, context!), ConvertToBase(q2, context!), context);
            case QuantityConversionType.AutomatedConversion:
                return Plus(q1, ConvertTo(q2, q1.UnitOfMeasure, context!), context);
            default:
                throw Mismatch();
        }
    }

    // QuantLib's operator-=, which mirrors operator+=.
    private static Quantity Minus(Quantity q1, Quantity q2, EvaluationContext? context)
    {
        if (q1.UnitOfMeasure == q2.UnitOfMeasure)
        {
            return new Quantity(q1.CommodityType, q1.UnitOfMeasure, q1.Amount - q2.Amount);
        }

        switch (ConversionTypeOf(context))
        {
            case QuantityConversionType.BaseUnitOfMeasureConversion:
                return Minus(ConvertToBase(q1, context!), ConvertToBase(q2, context!), context);
            case QuantityConversionType.AutomatedConversion:
                return Minus(q1, ConvertTo(q2, q1.UnitOfMeasure, context!), context);
            default:
                throw Mismatch();
        }
    }

    // QuantLib's operator/, operator== and the comparisons: f of the amounts within one unit, otherwise converted as
    // the settings say, or refused.
    private static T Apply<T>(Quantity q1, Quantity q2, EvaluationContext? context, Func<double, double, T> f)
    {
        if (q1.UnitOfMeasure == q2.UnitOfMeasure)
        {
            return f(q1.Amount, q2.Amount);
        }

        switch (ConversionTypeOf(context))
        {
            case QuantityConversionType.BaseUnitOfMeasureConversion:
                return Apply(ConvertToBase(q1, context!), ConvertToBase(q2, context!), context, f);
            case QuantityConversionType.AutomatedConversion:
                return Apply(q1, ConvertTo(q2, q1.UnitOfMeasure, context!), context, f);
            default:
                throw Mismatch();
        }
    }

    private static QuantityConversionType ConversionTypeOf(EvaluationContext? context) =>
        context?.QuantitySettings.ConversionType ?? QuantityConversionType.NoConversion;

    private static NanoQLException Mismatch() => new("unitOfMeasure mismatch and no conversion specified");

    // QuantLib's convertTo: the quantity converted by the context's conversion, then rounded by its unit.
    private static Quantity ConvertTo(Quantity q, UnitOfMeasure? target, EvaluationContext context)
    {
        if (q.UnitOfMeasure != target)
        {
            UnitOfMeasureConversion rate = context.UnitOfMeasureConversions.Lookup(
                q.CommodityType ?? throw new NanoQLException("no commodity type data provided"),
                UnitOf(q),
                target ?? throw new NanoQLException("no unit of measure data provided"));
            return rate.Convert(q).Rounded();
        }

        return q;
    }

    // QuantLib's convertToBase.
    private static Quantity ConvertToBase(Quantity q, EvaluationContext context)
    {
        UnitOfMeasure baseUnit = context.QuantitySettings.BaseUnitOfMeasure
            ?? throw new NanoQLException("no base unitOfMeasure set");
        return ConvertTo(q, baseUnit, context);
    }
}
