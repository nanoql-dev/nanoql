// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/termstructures/globalbootstrapvars.hpp ql/termstructures/globalbootstrapvars.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.TermStructures;

/// <summary>
/// Additional bootstrap variables that are plain values, each with an initial guess and an optional lower bound:
/// QuantLib's <c>SimpleQuoteVariables</c>.
/// </summary>
/// <remarks>
/// <para>
/// A variable with a lower bound lb is optimized as log(v − lb), its value exp(x) + lb; one without is optimized as
/// itself. As QuantLib's <c>detail::get</c> reads them, a variable past the guesses given takes the last guess, or 0
/// where none is given, and one past the bounds given takes the last bound, or none where none is given.
/// </para>
/// <para>
/// Immutable: QuantLib's variables are quotes the optimizer writes and the helpers read; here
/// <see cref="AdditionalBootstrapVariables.Values"/> returns the values, which the global bootstrap's
/// <see cref="GlobalBootstrap.VariableHelpers"/> reads, and the curve keeps the optimum's. Since a nanoql curve is
/// always built afresh, <see cref="Initialize"/> gives the guesses whatever its argument, where QuantLib's reads the
/// quotes' values, a previous bootstrap's, on a valid curve.
/// </para>
/// </remarks>
public sealed class SimpleQuoteVariables : AdditionalBootstrapVariables
{
    private readonly int count;
    private readonly ImmutableArray<double> initialGuesses;
    private readonly ImmutableArray<double?> lowerBounds;

    /// <summary>Initializes a new instance of the <see cref="SimpleQuoteVariables"/> class.</summary>
    /// <param name="count">The number of variables, QuantLib's number of quotes.</param>
    /// <param name="initialGuesses">The first variables' guesses, the variables past them taking the last;
    /// <see langword="null"/> or empty for a guess of 0.</param>
    /// <param name="lowerBounds">The first variables' lower bounds, <see langword="null"/> for none, the variables past
    /// them taking the last; <see langword="null"/> or empty for none.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="count"/> is negative.</exception>
    /// <exception cref="ArgumentException">There are more guesses or bounds than variables.</exception>
    public SimpleQuoteVariables(
        int count, IEnumerable<double>? initialGuesses = null, IEnumerable<double?>? lowerBounds = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        this.count = count;
        this.initialGuesses = initialGuesses is null ? [] : [.. initialGuesses];
        this.lowerBounds = lowerBounds is null ? [] : [.. lowerBounds];
        if (!(this.initialGuesses.Length <= count))
        {
            throw new ArgumentException("too many initialGuesses", nameof(initialGuesses));
        }

        if (!(this.lowerBounds.Length <= count))
        {
            throw new ArgumentException("too many lowerBounds", nameof(lowerBounds));
        }
    }

    /// <summary>Gets the number of variables.</summary>
    public int Count => count;

    /// <inheritdoc/>
    public override Vector Initialize(bool validData)
    {
        var guesses = new Vector(count);
        for (int i = 0, size = guesses.Size; i < size; ++i)
        {
            double guess = initialGuesses.IsEmpty ? 0.0 : initialGuesses[Math.Min(i, initialGuesses.Length - 1)];
            guesses[i] = TransformInverse(guess, i);
        }

        return guesses;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="x"/> holds more values than there are variables.</exception>
    public override ImmutableArray<double> Values(ReadOnlySpan<double> x)
    {
        if (!(x.Length <= count))
        {
            throw new ArgumentException($"{x.Length} values given for {count} variables", nameof(x));
        }

        var values = new double[x.Length];
        for (int i = 0, size = x.Length; i < size; ++i)
        {
            values[i] = TransformDirect(x[i], i);
        }

        return ImmutableArray.Create(values);
    }

    private double TransformDirect(double x, int i) =>
        LowerBound(i) is double lb ? Math.Exp(x) + lb : x;

    private double TransformInverse(double x, int i) =>
        LowerBound(i) is double lb ? Math.Log(x - lb) : x;

    // QuantLib's detail::get(lowerBounds_, i, Null<Real>()): the last bound past them.
    private double? LowerBound(int i) => lowerBounds.IsEmpty ? null : lowerBounds[Math.Min(i, lowerBounds.Length - 1)];
}
