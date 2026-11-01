// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.StopFixtures;

/// <summary>An engine with a calculation for each of the instrument's two shapes: they tie for it.</summary>
public static class TieEngine
{
    /// <summary>The calculation for the first shape.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="context">The context.</param>
    /// <returns>Nothing: never run.</returns>
    public static InstrumentResults Calculate(ITieA instrument, EvaluationContext context) => throw new NotSupportedException();

    /// <summary>The calculation for the second shape.</summary>
    /// <param name="instrument">The instrument.</param>
    /// <param name="context">The context.</param>
    /// <returns>Nothing: never run.</returns>
    public static InstrumentResults Calculate(ITieB instrument, EvaluationContext context) => throw new NotSupportedException();
}
