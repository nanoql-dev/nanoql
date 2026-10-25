// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/fdmmesherintegral.hpp ql/methods/finitedifferences/utilities/fdmmesherintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// The integral of values on a composite mesher: a one-dimensional rule along the last direction of the integrals
/// over the other directions, recursively.
/// </summary>
/// <remarks>
/// Immutable. QuantLib's class holds a reference to the caller's function object, which a temporary, as its SLV model
/// passes, leaves dangling; here the delegate is held.
/// </remarks>
public sealed class FdmMesherIntegral
{
    private readonly ImmutableArray<Fdm1dMesher> meshers;
    private readonly Func<Vector, Vector, double> integrator1d;

    /// <summary>Initializes a new instance of the <see cref="FdmMesherIntegral"/> class.</summary>
    /// <param name="mesher">The composite mesher.</param>
    /// <param name="integrator1d">The one-dimensional rule: the integral of values at abscissas, given the abscissas
    /// and the values.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public FdmMesherIntegral(FdmMesherComposite mesher, Func<Vector, Vector, double> integrator1d)
        : this(Checked(mesher).GetFdm1dMeshers(), integrator1d)
    {
    }

    private FdmMesherIntegral(ImmutableArray<Fdm1dMesher> meshers, Func<Vector, Vector, double> integrator1d)
    {
        ArgumentNullException.ThrowIfNull(integrator1d);
        this.meshers = meshers;
        this.integrator1d = integrator1d;
    }

    /// <summary>The integral of values on the mesher.</summary>
    /// <param name="f">The values, one per point of the mesher's layout.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="f"/> isn't one value per point, where QuantLib reads past a shorter one.
    /// </exception>
    public double Integrate(Vector f)
    {
        ArgumentNullException.ThrowIfNull(f);
        int size = 1;
        foreach (Fdm1dMesher m in meshers)
        {
            size *= m.Size;
        }

        if (f.Size != size)
        {
            throw new ArgumentException($"{f.Size} values given for a mesher of {size} points", nameof(f));
        }

        return IntegrateChecked(f);
    }

    private static FdmMesherComposite Checked(FdmMesherComposite mesher)
    {
        ArgumentNullException.ThrowIfNull(mesher);
        return mesher;
    }

    private double IntegrateChecked(Vector f)
    {
        var x = new Vector(meshers[^1].Locations().AsSpan());

        if (meshers.Length == 1)
        {
            return integrator1d(x, f);
        }

        var subMesherIntegral = new FdmMesherIntegral(meshers[..^1], integrator1d);
        int subSize = 1;
        for (int i = 0; i < meshers.Length - 1; ++i)
        {
            subSize *= meshers[i].Size;
        }

        var g = new Vector(x.Size);
        var fSub = new Vector(subSize);

        for (int i = 0; i < x.Size; ++i)
        {
            f.AsSpan().Slice(i * subSize, subSize).CopyTo(fSub.AsSpan());
            g[i] = subMesherIntegral.IntegrateChecked(fSub);
        }

        return integrator1d(x, g);
    }
}
