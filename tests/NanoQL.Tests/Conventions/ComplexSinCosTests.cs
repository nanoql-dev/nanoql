// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The library takes a complex exponential, power, sine or cosine from its own <c>ComplexFunctions</c>, which computes
/// the angle's sine and cosine apart: no call in <c>NanoQL.dll</c> or <c>NanoQL.Extensions.dll</c> reaches
/// <see cref="Math.SinCos"/> or <see cref="double.SinCos"/>, or one of <see cref="System.Numerics.Complex"/>'s
/// functions that take both from it (<c>FromPolarCoordinates</c>, <c>Exp</c>, <c>Pow</c>, <c>Sin</c>, <c>Cos</c>,
/// <c>Tan</c>, <c>Sinh</c>, <c>Cosh</c> and <c>Tanh</c>). On macOS, and on Linux x64 under a glibc older than 2.36,
/// .NET's JIT computes <see cref="Math.SinCos"/> by the platform's combined routine and Native AOT computes the two
/// apart, an ulp apart for some angles, so such a call gave different last bits in .NET and through the native
/// library.
/// </summary>
public class ComplexSinCosTests
{
    private static readonly HashSet<string> ComplexUsers =
        ["FromPolarCoordinates", "Exp", "Pow", "Sin", "Cos", "Tan", "Sinh", "Cosh", "Tanh"];

    [Fact]
    public void NoLibraryCallReachesSinCos()
    {
        System.Reflection.Assembly[] assemblies =
            [typeof(NanoQL.Time.Date).Assembly, typeof(NanoQL.Extensions.Conventions.ConventionSet).Assembly];
        var calls = new List<string>();
        foreach (System.Reflection.Assembly a in assemblies)
        {
            using var pe = new PEReader(File.OpenRead(a.Location));
            MetadataReader md = pe.GetMetadataReader();
            foreach (MemberReferenceHandle h in md.MemberReferences)
            {
                MemberReference m = md.GetMemberReference(h);
                if (m.Parent.Kind != HandleKind.TypeReference)
                {
                    continue;
                }

                TypeReference t = md.GetTypeReference((TypeReferenceHandle)m.Parent);
                string type = md.GetString(t.Namespace) + "." + md.GetString(t.Name);
                string name = md.GetString(m.Name);
                if (((type == "System.Math" || type == "System.Double") && name == "SinCos")
                    || (type == "System.Numerics.Complex" && ComplexUsers.Contains(name)))
                {
                    calls.Add(a.GetName().Name + ": " + type + "." + name);
                }
            }
        }

        Assert.Empty(calls);
    }
}
