// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.Emitting;

/// <summary>One C parameter of a function: its C type, its name, and the shim's C# type for it.</summary>
/// <param name="CType">The C type, as <c>nanoql.h</c> declares it.</param>
/// <param name="Name">The parameter's C name.</param>
/// <param name="CsType">The C# type the shim receives it as.</param>
internal sealed record CSlot(string CType, string Name, string CsType);
