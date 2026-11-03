// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's HestonSLVFDMModel log vectors, <c>tests/vectors/cxx/models/equity/hestonslvfdmmodel.json</c>: the
/// finite-difference SLV calibration's log with logging on, its number of entries and its first, second, eleventh and
/// last entries' times and meshers, the outputs stable under one-ulp moves of the inputs (the densities aren't).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class HestonSLVFDMModelCxxVectorTests
{
    [Fact]
    public void HestonSLVFDMModelCxxVector()
    {
        var file = VectorFile.Load("cxx/models/equity/hestonslvfdmmodel");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement spec = c.Input("model");
            JsonElement process = spec.GetProperty("process");
            JsonElement p = spec.GetProperty("params");
            JsonElement scheme = spec.GetProperty("scheme");
            var parameters = new HestonSLVFokkerPlanckFdmParams(
                p[0].GetInt32(),
                p[1].GetInt32(),
                p[2].GetInt32(),
                p[3].GetInt32(),
                p[4].GetDouble(),
                p[5].GetInt32(),
                p[6].GetInt32(),
                p[7].GetDouble(),
                p[8].GetDouble(),
                p[9].GetInt32(),
                p[10].GetDouble(),
                p[11].GetDouble(),
                p[12].GetDouble(),
                p[13].GetDouble(),
                p[14].GetDouble(),
                p[15].GetDouble(),
                p[16].GetDouble(),
                (FdmHestonGreensFctAlgorithm)p[17].GetInt32(),
                (FdmSquareRootFwdOpTransformationType)p[18].GetInt32(),
                new FdmSchemeDesc((FdmSchemeDescFdmSchemeType)scheme[0].GetInt32(), scheme[1].GetDouble(), scheme[2].GetDouble()));
            var localVol = new LocalConstantVol(
                new Date(process.GetProperty("referenceDate").GetInt32()),
                spec.GetProperty("volatility").GetDouble(),
                CashFlowInputs.DayCounterOf(process.GetProperty("dayCounter")));
            var model = new HestonSLVFDMModel(
                localVol,
                new HestonModel(HestonCxxInputs.ProcessOf(process)),
                new Date(spec.GetProperty("endDate").GetInt32()),
                parameters,
                true,
                null,
                spec.GetProperty("mixingFactor").GetDouble());
            ImmutableArray<HestonSLVFDMModelLogEntry> log = model.LogEntries;
            check.Output(c, "size", log.Length);
            foreach (JsonElement key in c.Input("kept").EnumerateArray())
            {
                string name = key.GetString()!;
                HestonSLVFDMModelLogEntry entry = log[int.Parse(name[1..], System.Globalization.CultureInfo.InvariantCulture)];
                check.Output(
                    c,
                    name,
                    name[0] switch
                    {
                        't' => entry.T,
                        'x' => entry.Mesher.GetFdm1dMeshers()[0].Locations().ToArray(),
                        'v' => entry.Mesher.GetFdm1dMeshers()[1].Locations().ToArray(),
                        _ => (object)entry.Prob.ToArray(),
                    });
            }
        }

        check.Complete();
    }
}
