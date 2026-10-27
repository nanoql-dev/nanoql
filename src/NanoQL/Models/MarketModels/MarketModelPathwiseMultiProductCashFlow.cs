// SPDX-FileCopyrightText: Copyright (C) 2006, 2008 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/pathwisemultiproduct.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A cash flow a <see cref="MarketModelPathwiseMultiProduct"/> generates at a step: the index of its payment time and
/// its amount followed by the amount's derivatives with respect to each forward rate. QuantLib's nested
/// <c>MarketModelPathwiseMultiProduct::CashFlow</c> (R2's top-level name).
/// </summary>
/// <remarks>
/// A working type (the S-mmp plan's MMPR1 (iii)): a product writes it in place, into the caller's buffer, whose
/// <see cref="Amount"/> the caller sizes to 1 + the number of rates. <see cref="CopyFrom"/> copies another cash flow's
/// values where QuantLib assigns one cash flow to another.
/// </remarks>
public sealed class MarketModelPathwiseMultiProductCashFlow
{
    private readonly double[] amount;

    /// <summary>Initializes a new instance of the <see cref="MarketModelPathwiseMultiProductCashFlow"/>
    /// class.</summary>
    /// <param name="size">The size of <see cref="Amount"/>: 1 + the number of rates.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    public MarketModelPathwiseMultiProductCashFlow(int size)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(size);
        amount = new double[size];
    }

    /// <summary>Gets or sets the index of the cash flow's payment time.</summary>
    public int TimeIndex { get; set; }

    /// <summary>
    /// Gets the amount, then its derivative with respect to each forward rate: a view of the cash flow's own storage.
    /// </summary>
    public Span<double> Amount => amount;

    /// <summary>Copies another cash flow's time index and amounts into this one, QuantLib's assignment.</summary>
    /// <param name="other">The cash flow copied, of this one's size.</param>
    /// <exception cref="ArgumentException">The two cash flows' sizes differ.</exception>
    public void CopyFrom(MarketModelPathwiseMultiProductCashFlow other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (other.amount.Length != amount.Length)
        {
            throw new ArgumentException(
                $"a cash flow of {other.amount.Length} amounts can't be copied into one of {amount.Length}", nameof(other));
        }

        TimeIndex = other.TimeIndex;
        other.amount.CopyTo(amount, 0);
    }
}
