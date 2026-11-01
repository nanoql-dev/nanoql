// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

import java.lang.foreign.MemorySegment;

/** Anything a nanoql object is: every class and interface of the layer, so an object passes as each it implements. */
public interface NanoHandle {
    /**
     * The object's handle, still the object's.
     *
     * @return the handle
     */
    MemorySegment handle();
}
