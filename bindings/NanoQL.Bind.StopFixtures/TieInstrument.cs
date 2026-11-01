// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.StopFixtures;

/// <summary>An instrument of both shapes, whose member takes an engine: neither calculation is the more derived.</summary>
public sealed class TieInstrument : ITieA, ITieB
{
    /// <summary>The instrument with an engine.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The instrument.</returns>
    public TieInstrument WithEngine(Func<TieInstrument, EvaluationContext, InstrumentResults> engine) => this;
}
