// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Extensions.Conventions;

namespace NanoQL.Tests.Extensions;

/// <summary>Conventions documents as text, for the extensions' tests.</summary>
internal static class ConventionsText
{
    /// <summary>The conventions' namespace declaration, for a test document's root.</summary>
    public const string Xmlns = "xmlns=\"" + ConventionsXml.Namespace + "\"";

    /// <summary>A document's snapshot.</summary>
    /// <param name="text">The document.</param>
    /// <param name="source">The source its records report.</param>
    /// <returns>Its records.</returns>
    public static ConventionSnapshot Read(string text, string? source = null)
    {
        using var reader = new StringReader(text);
        return ConventionsXml.Read(reader, source);
    }

    /// <summary>A document of records, between a root element in the conventions' namespace.</summary>
    /// <param name="records">The records' elements.</param>
    /// <returns>Its records.</returns>
    public static ConventionSnapshot Records(string records) => Read($"<Conventions {Xmlns}>{records}</Conventions>");

    /// <summary>A snapshot's canonical text.</summary>
    /// <param name="snapshot">The snapshot.</param>
    /// <returns>The text the writer writes.</returns>
    public static string Write(ConventionSnapshot snapshot)
    {
        using var writer = new StringWriter { NewLine = "\n" };
        ConventionsXml.Write(snapshot, writer);
        return writer.ToString();
    }

    /// <summary>The set QuantLib's presets and a document's records make.</summary>
    /// <param name="records">The records' elements.</param>
    /// <returns>The set.</returns>
    public static ConventionSet OnQuantLib(string records) => ConventionSet.QuantLib.With(Records(records));
}
