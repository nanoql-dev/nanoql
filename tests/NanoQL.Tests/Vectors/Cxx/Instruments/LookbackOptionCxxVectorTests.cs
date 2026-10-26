// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's lookback option vectors, <c>tests/vectors/cxx/instruments/lookbackoption.json</c>: the validation
/// of the floating and fixed options' running extremum, the partial floating option's lambda and lookback end, and the
/// partial fixed option's lookback start, each option European in a year.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class LookbackOptionCxxVectorTests
{
    [Fact]
    public void LookbackOptionCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/lookbackoption");
        var check = new VectorAssert(file);
        var call = new FloatingTypePayoff(OptionType.Call);
        var struck = new PlainVanillaPayoff(OptionType.Call, 100.0);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var aYear = new EuropeanExercise(context.EvaluationDate + 365);
            check.Output(c, "valid", Lazy(() =>
            {
                OneAssetOption option = c.String("option") switch
                {
                    "floating" => new ContinuousFloatingLookbackOption(c.Double("minmax"), call, aYear),
                    "fixed" => new ContinuousFixedLookbackOption(c.Double("minmax"), struck, aYear),
                    "partial floating" => new ContinuousPartialFloatingLookbackOption(
                        100.0, c.Double("lambda"), c.DateInput("lookbackPeriodEnd"), new FloatingTypePayoff((OptionType)c.Int("optionType")), aYear),
                    _ => new ContinuousPartialFixedLookbackOption(c.DateInput("lookbackPeriodStart"), struck, aYear),
                };
                option.Validate();
                return true;
            }));
        }

        check.Complete();
    }
}
