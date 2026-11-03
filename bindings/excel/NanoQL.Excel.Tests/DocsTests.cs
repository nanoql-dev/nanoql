// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection;
using System.Xml.Linq;
using ExcelDna.Integration;
using static NanoQL.Excel.Tests.Xl;

namespace NanoQL.Excel.Tests;

/// <summary>The help text: a function's description its member's XML summary, an argument's its parameter's with its form.</summary>
[Collection(TestSettings.Name)]
public sealed class DocsTests
{
    /// <summary>A known function's description is its XML summary; an argument's its parameter's text and its Excel form.</summary>
    [Fact]
    public void AFunctionsDescriptionIsItsSummary()
    {
        MethodInfo m = Xl.Experimental.Methods["nqlEvaluationContextEvaluationDate"];
        string description = m.GetCustomAttribute<ExcelFunctionAttribute>()!.Description;
        XDocument xml = XDocument.Load(Path.ChangeExtension(typeof(EvaluationContext).Assembly.Location, ".xml"));
        XElement member = xml.Descendants("member").Single(e => (string)e.Attribute("name")! == "P:NanoQL.EvaluationContext.EvaluationDate");
        string summary = System.Text.RegularExpressions.Regex.Replace(member.Element("summary")!.Value, @"\s+", " ").Trim();
        Assert.Equal(summary, description);
        Assert.Equal("a date", Xl.Experimental.Methods["nqlEvaluationContext"].GetParameters()[0].GetCustomAttribute<ExcelArgumentAttribute>()!.Description);
        string forward = Xl.Experimental.Methods["nqlFlatForward"].GetParameters()[1].GetCustomAttribute<ExcelArgumentAttribute>()!.Description;
        Assert.EndsWith(" (a number)", forward, StringComparison.Ordinal);
        Assert.All(Xl.Experimental.Methods.Values, x => Assert.True(x.GetCustomAttribute<ExcelFunctionAttribute>()!.Description.Length <= 253));
    }
}
