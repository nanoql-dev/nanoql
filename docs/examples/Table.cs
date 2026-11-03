// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Globalization;
using System.Text;

namespace NanoQL.Examples;

/// <summary>An example's output: a table the docs' sync writes as Markdown beside the page.</summary>
/// <param name="headers">The columns' headers; a header starting with '&gt;' is right-aligned, the mark dropped.</param>
public sealed class Table(params string[] headers)
{
    private readonly List<string[]> rows = [];

    /// <summary>Gets the rows, each a cell per column.</summary>
    public IReadOnlyList<string[]> Rows => rows;

    /// <summary>Formats a number with grouping and the given decimals, culture-invariant.</summary>
    /// <param name="value">The number.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text.</returns>
    public static string N(double value, int decimals = 2) =>
        value.ToString("N" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    /// <summary>Formats a rate as a percentage with the given decimals, culture-invariant.</summary>
    /// <param name="rate">The rate.</param>
    /// <param name="decimals">The decimals.</param>
    /// <returns>The text.</returns>
    public static string Percent(double rate, int decimals = 4) =>
        (rate * 100).ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture) + "%";

    /// <summary>Formats a date as ISO 8601's year, month and day.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The text.</returns>
    public static string D(Time.Date date) =>
        string.Create(CultureInfo.InvariantCulture, $"{date.Year:D4}-{(int)date.Month:D2}-{date.DayOfMonth:D2}");

    /// <summary>Adds a row.</summary>
    /// <param name="cells">The row's cells, as many as the columns.</param>
    /// <returns>This table.</returns>
    public Table Add(params string[] cells)
    {
        if (cells.Length != headers.Length)
        {
            throw new ArgumentException($"{cells.Length} cells for {headers.Length} columns", nameof(cells));
        }

        rows.Add(cells);
        return this;
    }

    /// <summary>The table as Markdown.</summary>
    /// <returns>The text, a line per row.</returns>
    public string ToMarkdown()
    {
        var text = new StringBuilder();
        text.Append("| ").Append(string.Join(" | ", headers.Select(h => h.TrimStart('>')))).Append(" |\n");
        text.Append('|').Append(string.Join("|", headers.Select(h => h.StartsWith('>') ? "---:" : "---"))).Append("|\n");
        foreach (string[] row in rows)
        {
            text.Append("| ").Append(string.Join(" | ", row)).Append(" |\n");
        }

        return text.ToString();
    }
}
