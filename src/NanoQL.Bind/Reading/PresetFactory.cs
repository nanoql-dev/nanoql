// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;

namespace NanoQL.Bind.Reading;

/// <summary>
/// One coded preset of the extensions' preset factories: its dictionary, its name, and the constructor its lambda calls
/// last, with each of that constructor's arguments a constant or one of the lambda's parameters.
/// </summary>
/// <param name="Dictionary">The preset factories' field.</param>
/// <param name="Name">The preset's name.</param>
/// <param name="Lambda">The lambda that builds it.</param>
/// <param name="Constructors">Each constructor of the preset's type the lambda calls, with its arguments.</param>
internal sealed record PresetFactory(string Dictionary, string Name, MethodInfo Lambda, IReadOnlyList<PresetConstruction> Constructors)
{
    /// <summary>Gets the preset's type: the type of the lambda's outermost, last, constructor call.</summary>
    public Type PresetType => Constructors[^1].Constructor.DeclaringType!;
}
