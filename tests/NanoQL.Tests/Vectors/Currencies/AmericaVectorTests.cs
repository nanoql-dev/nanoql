// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The American currencies' vectors, <c>tests/vectors/currencies/america.json</c>: each of the 16 currencies'
/// name, codes, symbols, fractions per unit, triangulation currency and rounding.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AmericaVectorTests
{
    private static readonly Dictionary<string, Func<Currency>> Currencies = new()
    {
        ["ARSCurrency"] = () => new ARSCurrency(),
        ["BRLCurrency"] = () => new BRLCurrency(),
        ["CADCurrency"] = () => new CADCurrency(),
        ["CLPCurrency"] = () => new CLPCurrency(),
        ["COPCurrency"] = () => new COPCurrency(),
        ["MXNCurrency"] = () => new MXNCurrency(),
        ["PENCurrency"] = () => new PENCurrency(),
        ["PEICurrency"] = () => new PEICurrency(),
        ["PEHCurrency"] = () => new PEHCurrency(),
        ["TTDCurrency"] = () => new TTDCurrency(),
        ["USDCurrency"] = () => new USDCurrency(),
        ["VEBCurrency"] = () => new VEBCurrency(),
        ["MXVCurrency"] = () => new MXVCurrency(),
        ["COUCurrency"] = () => new COUCurrency(),
        ["CLFCurrency"] = () => new CLFCurrency(),
        ["UYUCurrency"] = () => new UYUCurrency(),
    };

    [Fact]
    public void AmericaVector()
    {
        var file = VectorFile.Load("currencies/america");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            Currency currency = Currencies[c.String("currency")]();
            check.Output(c, "name", currency.Name);
            check.Output(c, "code", currency.Code);
            check.Output(c, "numericCode", currency.NumericCode);
            check.Output(c, "symbol", currency.Symbol);
            check.Output(c, "fractionSymbol", currency.FractionSymbol);
            check.Output(c, "fractionsPerUnit", currency.FractionsPerUnit);
            check.Output(c, "triangulationCurrency", currency.TriangulationCurrency?.Code);
            check.Output(c, "rounding", c.Doubles("rounded").Select(x => (object?)currency.Rounding.Evaluate(x)));
        }

        check.Complete();
    }
}
