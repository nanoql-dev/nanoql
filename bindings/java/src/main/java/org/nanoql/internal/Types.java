// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.internal;

import java.lang.foreign.MemorySegment;
import java.lang.invoke.MethodHandle;
import java.lang.invoke.MethodHandles;
import java.lang.invoke.MethodType;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;
import java.util.function.Function;
import org.nanoql.NanoHandle;

/**
 * Each class and its adopting constructor by its .NET type's full name, so a result wraps as the most derived class the
 * layer knows: nanoql's classes register from the generated {@code NanoqlTypes}, a user's package's from its own. Not
 * part of nanoql's API.
 */
public final class Types {
    private record Entry(Class<?> cls, Function<MemorySegment, ? extends NanoHandle> make) {
    }

    private static final Map<String, Entry> BY_NAME = new ConcurrentHashMap<>();

    // Each class's adopting constructor, an interface's through its Of.
    private static final ClassValue<MethodHandle> ADOPTING = new ClassValue<>() {
        @Override
        protected MethodHandle computeValue(Class<?> cls) {
            try {
                Class<?> c = cls.isInterface() ? Class.forName(cls.getName() + "$Of", false, cls.getClassLoader()) : cls;
                return MethodHandles.publicLookup().findConstructor(c, MethodType.methodType(void.class, Native.Adopt.class, MemorySegment.class));
            } catch (ReflectiveOperationException e) {
                return null;
            }
        }
    };

    static {
        NanoqlTypes.register();
    }

    private Types() {
    }

    /**
     * Registers a class and its adopting constructor.
     *
     * @param <T> the class
     * @param name the .NET type's full name
     * @param cls the class
     * @param make the constructor
     */
    public static <T extends NanoHandle> void register(String name, Class<T> cls, Function<MemorySegment, T> make) {
        BY_NAME.put(name, new Entry(cls, make));
    }

    /**
     * An object of a class for a handle, whatever the handle's .NET type: the class's adopting constructor called.
     *
     * @param <T> the class
     * @param cls the class, or an interface with its {@code Of}
     * @param h the handle, the object's now
     * @return the object
     * @throws IllegalArgumentException for a class that adopts no handle, the handle released
     */
    public static <T extends NanoHandle> T adopt(Class<T> cls, MemorySegment h) {
        MethodHandle make = ADOPTING.get(cls);
        if (make == null) {
            Native.release(h);
            throw new IllegalArgumentException(cls.getName() + " adopts no handle");
        }
        try {
            return cls.cast(make.invoke(Native.ADOPT, h));
        } catch (Throwable e) {
            Native.release(h);
            throw new IllegalStateException(e);
        }
    }

    /**
     * An object for a handle: of its .NET type's class where the layer knows it and it is the declared class's, else of
     * the declared class.
     *
     * @param <T> the declared class
     * @param name the .NET type's full name
     * @param h the handle, the object's now
     * @param declared the declared class
     * @param make the declared class's adopting constructor
     * @return the object
     */
    static <T extends NanoHandle> T make(String name, MemorySegment h, Class<T> declared, Function<MemorySegment, T> make) {
        Entry e = BY_NAME.get(name);
        if (e != null && declared.isAssignableFrom(e.cls())) {
            return declared.cast(e.make().apply(h));
        }
        return make.apply(h);
    }
}
