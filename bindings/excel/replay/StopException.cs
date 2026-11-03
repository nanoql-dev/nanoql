// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Excel.Replay;

/// <summary>A call the replay can't make in Excel's forms: its fact replays up to it, as the bindings' replayers
/// stop.</summary>
/// <param name="why">Why.</param>
internal sealed class StopException(string why) : Exception(why);
