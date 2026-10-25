// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The SmileSectionRNDCalculator vectors,
/// <c>tests/vectors/methods/finitedifferences/utilities/smilesectionrndcalculator.json</c>: the Breeden-Litzenberger
/// density, distribution and inverse at flat, SVI, SABR and interpolated sections' expiries, and the inverse's
/// refusal without an at-the-money level.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SmileSectionRNDCalculatorVectorTests
{
    [Fact]
    public void SmileSectionRNDCalculatorVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/smilesectionrndcalculator");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            SmileSection section = SectionOf(c.Input("section"), c);
            var calculator = new SmileSectionRNDCalculator(section, c.Int("nStrikes"), c.Double("nStd"));
            double[] x = c.Doubles("x"), probabilities = c.Doubles("probabilities");
            check.Output(c, "pdf", (object?[])[.. x.Select(u => Lazy(() => calculator.Pdf(u)))]);
            check.Output(c, "cdf", (object?[])[.. x.Select(u => Lazy(() => calculator.Cdf(u)))]);
            check.Output(c, "invcdf", (object?[])[.. probabilities.Select(p => Lazy(() => calculator.InvCdf(p)))]);
        }

        check.Complete();
    }

    private static SmileSection SectionOf(JsonElement spec, VectorCase c) => spec.GetProperty("type").GetString() switch
    {
        "SviSmileSection" => new SviSmileSection(
            spec.GetProperty("exerciseTime").GetDouble(), spec.GetProperty("forward").GetDouble(), Doubles(spec.GetProperty("sviParameters"))),
        "FlatSmileSection" or "SabrSmileSection" => SmileSectionInputs.SectionOf(spec, c.Context()),
        _ => InterpolatedSmileSectionVectorTests.SectionOf(spec, c.Context()),
    };
}
