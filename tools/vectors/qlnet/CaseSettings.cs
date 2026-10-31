// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using QLNet;

namespace NanoQL.QLNetVectors;

/// <summary>
/// Every case's evaluation context (docs/porting-playbook.md §7.2): QLNet's global settings, set explicitly before the
/// case and written into its <c>inputs.settings</c> under <c>vectors.py</c>'s keys.
/// </summary>
internal static class CaseSettings
{
    /// <summary>Sets QLNet's settings for a case and returns them as its <c>inputs.settings</c>.</summary>
    /// <param name="evaluationDate">The evaluation date.</param>
    /// <param name="includeReferenceDateEvents">Whether events on the reference date count as occurred.</param>
    /// <param name="includeTodaysCashFlows">
    /// Whether today's cash flows count, or <see langword="null"/> for unset.
    /// </param>
    /// <param name="enforcesTodaysHistoricFixings">Whether today's fixing must be a historic one.</param>
    /// <returns>The settings, as the case writes them.</returns>
    public static Obj Set(
        Date evaluationDate,
        bool includeReferenceDateEvents = false,
        bool? includeTodaysCashFlows = null,
        bool enforcesTodaysHistoricFixings = false)
    {
        Settings.setEvaluationDate(evaluationDate);
        Settings.includeReferenceDateEvents = includeReferenceDateEvents;
        Settings.includeTodaysCashFlows = includeTodaysCashFlows;
        Settings.enforcesTodaysHistoricFixings = enforcesTodaysHistoricFixings;
        return new Obj
        {
            ["evaluationDate"] = evaluationDate.serialNumber(),
            ["includeReferenceDateEvents"] = includeReferenceDateEvents,
            ["includeTodaysCashFlows"] = includeTodaysCashFlows,
            ["enforcesTodaysHistoricFixings"] = enforcesTodaysHistoricFixings,
        };
    }
}
