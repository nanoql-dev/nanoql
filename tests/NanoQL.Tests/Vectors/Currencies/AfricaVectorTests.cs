// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The African currencies' vectors, <c>tests/vectors/currencies/africa.json</c>: each of the 14 currencies'
/// name, codes, symbols, fractions per unit, triangulation currency and rounding.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AfricaVectorTests
{
    private static readonly Dictionary<string, Func<Currency>> Currencies = new()
    {
        ["AOACurrency"] = () => new AOACurrency(),
        ["BWPCurrency"] = () => new BWPCurrency(),
        ["EGPCurrency"] = () => new EGPCurrency(),
        ["ETBCurrency"] = () => new ETBCurrency(),
        ["GHSCurrency"] = () => new GHSCurrency(),
        ["KESCurrency"] = () => new KESCurrency(),
        ["MADCurrency"] = () => new MADCurrency(),
        ["MURCurrency"] = () => new MURCurrency(),
        ["NGNCurrency"] = () => new NGNCurrency(),
        ["TNDCurrency"] = () => new TNDCurrency(),
        ["UGXCurrency"] = () => new UGXCurrency(),
        ["XOFCurrency"] = () => new XOFCurrency(),
        ["ZARCurrency"] = () => new ZARCurrency(),
        ["ZMWCurrency"] = () => new ZMWCurrency(),
    };

    [Fact]
    public void AfricaVector()
    {
        var file = VectorFile.Load("currencies/africa");
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
