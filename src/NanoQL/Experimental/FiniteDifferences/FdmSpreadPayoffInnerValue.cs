// SPDX-FileCopyrightText: Copyright (C) 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/finitedifferences/fdmspreadpayoffinnervalue.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Instruments;
using NanoQL.Methods;

namespace NanoQL.Experimental;

/// <summary>The inner value of a basket payoff on two inner values, a spread's two legs.</summary>
[Experimental("NQL0001")]
public sealed class FdmSpreadPayoffInnerValue : FdmInnerValueCalculator
{
    private readonly BasketPayoff payoff;
    private readonly FdmInnerValueCalculator calc1;
    private readonly FdmInnerValueCalculator calc2;

    // the two legs' values, held
    private readonly double[] a = new double[2];

    /// <summary>Initializes a new instance of the <see cref="FdmSpreadPayoffInnerValue"/> class.</summary>
    /// <param name="payoff">The basket payoff on the two legs.</param>
    /// <param name="calc1">The first leg's inner value.</param>
    /// <param name="calc2">The second leg's inner value.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmSpreadPayoffInnerValue(BasketPayoff payoff, FdmInnerValueCalculator calc1, FdmInnerValueCalculator calc2)
    {
        ArgumentNullException.ThrowIfNull(payoff);
        ArgumentNullException.ThrowIfNull(calc1);
        ArgumentNullException.ThrowIfNull(calc2);
        this.payoff = payoff;
        this.calc1 = calc1;
        this.calc2 = calc2;
    }

    /// <inheritdoc/>
    public override double InnerValue(FdmLinearOpIterator iter, double t)
    {
        a[0] = calc1.InnerValue(iter, t);
        a[1] = calc2.InnerValue(iter, t);
        return payoff.Evaluate(a);
    }

    /// <inheritdoc/>
    public override double AvgInnerValue(FdmLinearOpIterator iter, double t) => InnerValue(iter, t);
}
