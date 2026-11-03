// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Examples.Rates;

/// <summary>Indexes and fixings: a stored past fixing beside fixings forecast from the curve.</summary>
public static class IndexesAndFixings
{
    /// <summary>The example's outputs.</summary>
    /// <returns>Each fixing date's fixing and where it comes from.</returns>
    public static IEnumerable<(string Name, Table Table)> Outputs()
    {
        YieldTermStructure curve = Curves.Build();

        // <fixings>
        var euribor = new Euribor6M(curve);
        Date yesterday = euribor.FixingCalendar.Advance(Curves.Today, -1, TimeUnit.Days);
        EvaluationContext context = Curves.Context with
        {
            Fixings = Curves.Context.Fixings.AddFixing(euribor, yesterday, 0.0212),
        };

        Date[] fixingDates =
        [
            yesterday,
            Curves.Today,
            euribor.FixingCalendar.Advance(Curves.Today, new Period(6, TimeUnit.Months)),
        ];
        // </fixings>
        var table = new Table("Fixing date", "Value date", "Maturity", ">Fixing", "Source");
        foreach (Date d in fixingDates)
        {
            // <read>
            double fixing = euribor.Fixing(d, context, forecastTodaysFixing: true);
            Date valueDate = euribor.ValueDate(d);
            Date maturity = euribor.MaturityDate(valueDate);
            // </read>
            table.Add(Table.D(d), Table.D(valueDate), Table.D(maturity), Table.Percent(fixing),
                d < Curves.Today ? "stored" : "forecast");
        }

        yield return ("indexes-fixings", table);
    }
}
