// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import org.nanoql.ffi.nanoql_h;
import org.nanoql.internal.Library;
import org.nanoql.internal.Native;

/** nanoql's library: which one is loaded, its versions, and the handles it has issued. */
public final class NanoQL {
    static {
        Library.load();
    }

    private NanoQL() {
    }

    /**
     * Loads nanoql's library from a path in place of the platform's own, such as a build holding a user's assembly;
     * before any object is made, since objects of one library are foreign to another. The system property
     * {@code nanoql.library} does the same.
     *
     * @param path the library's path
     * @throws IllegalStateException once a library is loaded
     */
    public static void useLibrary(String path) {
        Library.use(path);
    }

    /**
     * The loaded library's path, loading it first: the natives jar's copy (on Linux and macOS deleted once loaded, on
     * Windows kept in the temporary folder under its hash), or the path {@code nanoql.library} or {@link #useLibrary}
     * named.
     *
     * @return the path
     */
    public static String libraryPath() {
        return Library.path();
    }

    /**
     * The library's version: {@code major.minor}, or {@code major.minor.patch} for a fix.
     *
     * @return the version
     */
    public static String version() {
        return nanoql_h.nql_version().reinterpret(Long.MAX_VALUE).getString(0);
    }

    /**
     * The library's ABI version, the major times 1,000 plus the minor.
     *
     * @return the version
     */
    public static int abiVersion() {
        return nanoql_h.nql_abi_version();
    }

    /**
     * The handles the library has issued and nobody released: a diagnostic.
     *
     * @return the count
     */
    public static long liveHandles() {
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.JAVA_LONG);
            Native.check(nanoql_h.nql_live_handles(r));
            return r.get(ValueLayout.JAVA_LONG, 0);
        }
    }
}
