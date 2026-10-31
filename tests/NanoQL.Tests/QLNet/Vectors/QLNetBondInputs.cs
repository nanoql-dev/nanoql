// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Extensions.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.QLNet;

/// <summary>The QLNet bond vectors' inputs: calendars, day counters and prepayment models by their names.</summary>
internal static class QLNetBondInputs
{
    /// <summary>The calendar a case names: <c>TARGET</c> or <c>NullCalendar</c>.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The calendar.</returns>
    public static Calendar Calendar(VectorCase c) => c.String("calendar") switch
    {
        "TARGET" => new TARGET(),
        "NullCalendar" => new NullCalendar(),
        string other => throw new InvalidOperationException($"{c.Name}: no calendar {other}"),
    };

    /// <summary>The day counter a case names.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The day counter.</returns>
    public static DayCounter DayCounter(VectorCase c) => c.String("dayCounter") switch
    {
        "Thirty360USA" => new Thirty360(Thirty360Convention.USA),
        "Thirty360BondBasis" => new Thirty360(Thirty360Convention.BondBasis),
        "ActualActualISMA" => new ActualActual(ActualActualConvention.ISMA),
        string other => throw new InvalidOperationException($"{c.Name}: no day counter {other}"),
    };

    /// <summary>The prepayment model a case gives, or <see langword="null"/> for none.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The model.</returns>
    public static IPrepayModel? Model(VectorCase c)
    {
        JsonElement m = c.Input("model");
        if (m.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return m.GetProperty("type").GetString() switch
        {
            "PSACurve" => new PSACurve(
                new Date(m.GetProperty("startDate").GetInt32()), m.GetProperty("multiplier").GetDouble()),
            "ConstantCPR" => new ConstantCPR(m.GetProperty("cpr").GetDouble()),
            var other => throw new InvalidOperationException($"{c.Name}: no model {other}"),
        };
    }
}
