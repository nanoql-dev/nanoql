// SPDX-FileCopyrightText: Copyright (C) 2007 Mark Joshi
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/marketmodels/models/alphaform.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Models;

/// <summary>
/// A one-parameter family of each rate's volatility multiplier, the alpha calibration's form: <see cref="SetAlpha"/>
/// chooses the member, <see cref="Evaluate"/> reads its value at a rate.
/// </summary>
/// <remarks>
/// A working type (the S-mm plan's MR2 (b)): <see cref="SetAlpha"/> changes it, as QuantLib's <c>setAlpha</c> does,
/// and it isn't thread-safe; <see cref="Clone"/> is deep, and the alpha calibration copies its form for each
/// calibration. An extension point, as in QuantLib.
/// </remarks>
[WorkingType]
public abstract class AlphaForm
{
    /// <summary>Initializes a new instance of the <see cref="AlphaForm"/> class.</summary>
    protected AlphaForm()
    {
    }

    /// <summary>The form's value at the <paramref name="i"/>-th rate, at the current α.</summary>
    /// <param name="i">The rate.</param>
    /// <returns>The multiplier.</returns>
    public abstract double Evaluate(int i);

    /// <summary>Sets the form's α.</summary>
    /// <param name="alpha">The α.</param>
    public abstract void SetAlpha(double alpha);

    /// <summary>Copies the form deeply, its α with it.</summary>
    /// <returns>The copy.</returns>
    public abstract AlphaForm Clone();
}
