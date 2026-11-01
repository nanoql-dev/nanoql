// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.lang.ref.Cleaner;
import java.lang.ref.Reference;
import java.util.concurrent.atomic.AtomicReference;
import org.nanoql.ffi.nanoql_h;
import org.nanoql.internal.Native;
import org.nanoql.internal.Types;

/**
 * An immutable nanoql object behind a handle the object owns: released by {@link #close()} (a try-with-resources
 * block), or by a cleaner once the object is unreachable. A closed object refuses a call before it reaches nanoql.
 * Equality, the hash code and the string are nanoql's.
 */
public class NanoObject implements NanoHandle, AutoCloseable {
    private static final Cleaner CLEANER = Cleaner.create();

    private final AtomicReference<MemorySegment> handle;
    private final Cleaner.Cleanable cleanable;

    /**
     * Adopts a handle nanoql returned.
     *
     * @param tag the adoption's tag
     * @param h the handle, the object's now
     */
    public NanoObject(Native.Adopt tag, MemorySegment h) {
        AtomicReference<MemorySegment> held = new AtomicReference<>(h);
        this.handle = held;
        this.cleanable = CLEANER.register(this, () -> {
            MemorySegment x = held.getAndSet(null);
            if (x != null) {
                Native.release(x);
            }
        });
    }

    /**
     * The handle, still the object's.
     *
     * @return the handle
     * @throws NanoQLException code 9, once the object is closed
     */
    @Override
    public final MemorySegment handle() {
        MemorySegment h = handle.get();
        if (h == null) {
            throw new NanoQLException(9, "the " + getClass().getSimpleName() + " is closed", null, null);
        }
        return h;
    }

    /**
     * Whether the object is closed: its handle released.
     *
     * @return whether it is
     */
    public final boolean isClosed() {
        return handle.get() == null;
    }

    /** Releases the handle; later calls on the object refuse. Closing twice does nothing. */
    @Override
    public final void close() {
        cleanable.clean();
    }

    /**
     * The object as another class of the layer, as C++'s {@code object_cast}: always a new object of that class holding a
     * new handle to the same nanoql object, for the caller to close, the object itself left open. Unchecked until a member
     * is called: a member of a class the object isn't refuses with code 10.
     *
     * @param <T> the class
     * @param cls the class, or an interface
     * @return the object as that class
     * @throws NanoQLException code 9, once the object is closed
     */
    public final <T extends NanoHandle> T as(Class<T> cls) {
        try {
            return Types.adopt(cls, Native.retain(handle()));
        } finally {
            Reference.reachabilityFence(this);
        }
    }

    /**
     * The object's .NET type.
     *
     * @return the type's full name
     */
    public final String typeName() {
        try {
            return Native.typeName(handle());
        } finally {
            Reference.reachabilityFence(this);
        }
    }

    @Override
    public final boolean equals(Object o) {
        if (this == o) {
            return true;
        }
        if (!(o instanceof NanoObject other)) {
            return false;
        }
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.JAVA_BOOLEAN);
            Native.check(nanoql_h.nql_equals(handle(), other.handle(), r));
            return r.get(ValueLayout.JAVA_BOOLEAN, 0);
        } finally {
            Reference.reachabilityFence(this);
            Reference.reachabilityFence(other);
        }
    }

    @Override
    public final int hashCode() {
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.JAVA_INT);
            Native.check(nanoql_h.nql_hash(handle(), r));
            return r.get(ValueLayout.JAVA_INT, 0);
        } finally {
            Reference.reachabilityFence(this);
        }
    }

    @Override
    public final String toString() {
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.ADDRESS);
            Native.check(nanoql_h.nql_to_string(handle(), r));
            String s = Native.takeString(r.get(ValueLayout.ADDRESS, 0));
            return s == null ? "" : s;
        } finally {
            Reference.reachabilityFence(this);
        }
    }
}
