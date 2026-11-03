// SPDX-FileCopyrightText: Copyright (C) 2006 Banca Profilo S.p.A.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/forwardmeasureprocess.hpp ql/processes/forwardmeasureprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>A stochastic process whose dynamics are expressed in the forward measure of a time T.</summary>
/// <remarks>
/// <para>
/// QuantLib's <c>setForwardMeasureTime</c> changes the process; here <see cref="WithForwardMeasureTime"/> returns a
/// copy at the new time, which a derived process overrides with its own type, rebuilding there any state of its own
/// that reads the time. A process built without a forward measure time refuses to read one, where QuantLib's reads its
/// uninitialized member: a divergence (the playbook's undefined-behaviour row).
/// </para>
/// <para>An extension point, as in QuantLib: G2's forward process derives from it.</para>
/// </remarks>
public abstract class ForwardMeasureProcess : StochasticProcess
{
    // Written by the constructors, and by WithForwardMeasureTime on a fresh copy only.
    private double? forwardMeasureTime;

    /// <summary>Initializes a new instance of the <see cref="ForwardMeasureProcess"/> class, with no forward measure time.</summary>
    protected ForwardMeasureProcess()
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ForwardMeasureProcess"/> class at a forward measure time.</summary>
    /// <param name="forwardMeasureTime">The forward measure time T.</param>
    protected ForwardMeasureProcess(double forwardMeasureTime)
    {
        this.forwardMeasureTime = forwardMeasureTime;
    }

    /// <summary>Initializes a new instance of the <see cref="ForwardMeasureProcess"/> class with a discretization, with no forward measure time.</summary>
    /// <param name="discretization">The discretization.</param>
    /// <exception cref="ArgumentNullException"><paramref name="discretization"/> is null.</exception>
    protected ForwardMeasureProcess(StochasticProcessDiscretization discretization)
        : base(discretization)
    {
    }

    /// <summary>Gets the forward measure time T: QuantLib's <c>getForwardMeasureTime</c>.</summary>
    /// <exception cref="NanoQLException">The process has no forward measure time.</exception>
    public double ForwardMeasureTime => forwardMeasureTime ?? throw new NanoQLException("forward measure time not set");

    /// <summary>A copy of the process at another forward measure time: QuantLib's <c>setForwardMeasureTime</c>.</summary>
    /// <param name="forwardMeasureTime">The forward measure time T.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    public virtual ForwardMeasureProcess WithForwardMeasureTime(double forwardMeasureTime)
    {
        var copy = (ForwardMeasureProcess)MemberwiseClone();
        copy.forwardMeasureTime = forwardMeasureTime;
        return copy;
    }
}
