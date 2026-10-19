// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/interpolations/extrapolation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>The base class of objects that may allow extrapolation.</summary>
/// <remarks>
/// Immutable: <see cref="ExtrapolatorExtensions.EnableExtrapolation{T}"/> and
/// <see cref="ExtrapolatorExtensions.DisableExtrapolation{T}"/> return a modified copy, of the receiver's own type.
/// </remarks>
public abstract class Extrapolator
{
    // Written only at construction (the initializer) and on a fresh copy (WithExtrapolation).
    private bool extrapolate;

    /// <summary>Gets a value indicating whether extrapolation is enabled.</summary>
    /// <remarks>
    /// A derived class of nanoql's own may set it in its constructor: a curve that follows another starts from the
    /// other's flag, as QuantLib's derived curves copy it when they are built.
    /// </remarks>
    public bool AllowsExtrapolation
    {
        get => extrapolate;
        private protected init => extrapolate = value;
    }

    // A copy of the object with extrapolation enabled or disabled.
    internal Extrapolator WithExtrapolation(bool allow)
    {
        var copy = (Extrapolator)MemberwiseClone();
        copy.extrapolate = allow;
        return copy;
    }
}
