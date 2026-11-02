// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using ExcelDna.Integration;

namespace NanoQL.Excel;

/// <summary>
/// An entry's RTD topic, keyed by the store's key (XLR3): it shows the entry while a cell holds the topic, and Excel's
/// disposal of the topic, when its last cell goes, releases the entry.
/// </summary>
internal sealed class XlHolder : IExcelObservable, IDisposable
{
    private readonly XlEntry entry;
    private int disposed;

    /// <summary>Initializes a new instance of the <see cref="XlHolder"/> class, holding the entry.</summary>
    /// <param name="entry">The entry.</param>
    public XlHolder(XlEntry entry)
    {
        this.entry = entry;
        entry.Hold();
    }

    /// <inheritdoc/>
    public IDisposable Subscribe(IExcelObserver observer)
    {
        observer.OnNext(entry.Shown);
        return this;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) == 0)
        {
            entry.Let();
        }
    }
}
