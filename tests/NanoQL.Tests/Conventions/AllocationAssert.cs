// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Tests.Conventions;

/// <summary>
/// Asserts that code allocates nothing, read from the thread's allocation counter
/// (<see cref="GC.GetAllocatedBytesForCurrentThread"/>). The counter can move once while the code runs, a few kilobytes
/// the runtime allocates on the thread beside it, which the suite's parallel load makes rare but possible: so the code
/// is measured up to five times, each on state of its own, and the assertion fails only when every measurement
/// allocates. An allocation of the code itself recurs at each measurement; the runtime's doesn't.
/// </summary>
internal static class AllocationAssert
{
    private const int Measurements = 5;

    /// <summary>
    /// Asserts that <paramref name="measured"/> allocates nothing on state <paramref name="prepare"/> makes.
    /// </summary>
    /// <typeparam name="T">The state's type.</typeparam>
    /// <param name="prepare">Makes fresh state for one measurement, outside it.</param>
    /// <param name="measured">The code measured, on that state.</param>
    /// <returns>The state of the measurement that allocated nothing.</returns>
    internal static T AllocatesNothing<T>(Func<T> prepare, Action<T> measured)
    {
        long[] allocated = new long[Measurements];
        for (int i = 0; i < Measurements; i++)
        {
            T state = prepare();
            long before = GC.GetAllocatedBytesForCurrentThread();
            measured(state);
            allocated[i] = GC.GetAllocatedBytesForCurrentThread() - before;
            if (allocated[i] == 0)
            {
                return state;
            }
        }

        Assert.Fail($"allocated {string.Join(", ", allocated)} bytes in {Measurements} measurements");
        return default!;
    }
}
