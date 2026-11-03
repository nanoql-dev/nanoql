// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Bind.TestTypes;

/// <summary>A member that takes its time, so the Python replayer's sweep can be seen ending a call past its
/// limit.</summary>
public static class Sleeper
{
    /// <summary>Sleeps, then returns.</summary>
    /// <param name="seconds">How long, in seconds.</param>
    public static void Sleep(double seconds) => Thread.Sleep(TimeSpan.FromSeconds(seconds));
}
