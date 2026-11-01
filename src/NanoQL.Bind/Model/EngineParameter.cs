// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Model;

/// <summary>
/// A member's parameter that takes one of nanoql's engines as a delegate: the delegate's instrument (none for the
/// composite shape, which takes the context alone), the arguments the member supplies after the context, and the result.
/// </summary>
/// <param name="Delegate">The delegate type, a generic method's argument closed at its constraint.</param>
/// <param name="Instrument">The instrument the delegate takes first, or null for the composite shape.</param>
/// <param name="Supplied">The arguments the member supplies after the instrument and the context.</param>
/// <param name="Cast">Per supplied argument, whether it is a generic argument closed at its constraint, cast to the engine parameter's type at each call.</param>
/// <param name="Result">The delegate's result type.</param>
/// <param name="Index">The adapter's index in the native library.</param>
internal sealed record EngineParameter(Type Delegate, Type? Instrument, IReadOnlyList<Type> Supplied, IReadOnlyList<bool> Cast, Type Result, int Index);
