// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

import java.lang.foreign.MemorySegment;
import org.nanoql.internal.Native;

/** An engine bound with its inputs (an engine's {@code bind} or {@code bindOn}), for the members that take an engine. */
public final class BoundEngine extends NanoObject {
    /**
     * Adopts a handle nanoql returned.
     *
     * @param tag the adoption's tag
     * @param h the handle
     */
    public BoundEngine(Native.Adopt tag, MemorySegment h) {
        super(tag, h);
    }
}
