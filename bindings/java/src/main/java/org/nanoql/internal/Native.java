// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.internal;

import java.lang.foreign.Arena;
import java.lang.foreign.MemorySegment;
import java.lang.foreign.ValueLayout;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.function.Function;
import java.util.function.IntFunction;
import org.nanoql.NanoHandle;
import org.nanoql.NanoObject;
import org.nanoql.NanoQLArgumentException;
import org.nanoql.NanoQLException;
import org.nanoql.ffi.nanoql_h;
import org.nanoql.ffi.nql_complex;
import org.nanoql.ffi.nql_period;

/**
 * The conversions the generated layer calls (docs/bindings.md, "Java"): each value in its C form for a call and back,
 * the refusals as exceptions, the objects' handles. Not part of nanoql's API.
 */
public final class Native {
    private Native() {
    }

    /** The tag of the constructor that adopts a handle nanoql returned. */
    public static final class Adopt {
        private Adopt() {
        }
    }

    /** The one tag. */
    public static final Adopt ADOPT = new Adopt();

    private static final long EPOCH = LocalDate.of(1899, 12, 30).toEpochDay();

    // ---- refusals ----

    /**
     * Throws the refusal a status code names.
     *
     * @param status the status a C function returned
     */
    public static void check(int status) {
        if (status == 0) {
            return;
        }
        String message = text(nanoql_h.nql_last_error_message());
        String param = text(nanoql_h.nql_last_error_param());
        String type = text(nanoql_h.nql_last_error_type());
        if (message == null) {
            message = "nanoql refused the call (status " + status + ")";
        }
        throw switch (status) {
            case 2, 3, 4 -> new NanoQLArgumentException(status, message, param, type);
            default -> new NanoQLException(status, message, param, type);
        };
    }

    private static String text(MemorySegment p) {
        return p.address() == 0 ? null : p.reinterpret(Long.MAX_VALUE).getString(0);
    }

    // ---- objects ----

    /**
     * An object's handle, NULL for null.
     *
     * @param o the object
     * @return its handle
     */
    public static MemorySegment h(NanoHandle o) {
        return o == null ? MemorySegment.NULL : o.handle();
    }

    /**
     * A new object's handle, from a C function's out-parameter.
     *
     * @param f the call, given its arena
     * @return the handle the caller now owns
     */
    public static MemorySegment make(Function<Arena, MemorySegment> f) {
        try (Arena a = Arena.ofConfined()) {
            return f.apply(a);
        }
    }

    /**
     * An object for a handle the caller owns, of the most derived class the layer knows, or null for NULL.
     *
     * @param <T> the declared class
     * @param h the handle
     * @param declared the declared class
     * @param make the declared class's adopting constructor
     * @return the object
     */
    public static <T extends NanoHandle> T wrap(MemorySegment h, Class<T> declared, Function<MemorySegment, T> make) {
        if (h.address() == 0) {
            return null;
        }
        return Types.make(typeName(h), h, declared, make);
    }

    /**
     * An object's .NET type.
     *
     * @param h its handle
     * @return the type's full name
     */
    public static String typeName(MemorySegment h) {
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.ADDRESS);
            check(nanoql_h.nql_type_name(h, r));
            return takeString(r.get(ValueLayout.ADDRESS, 0));
        }
    }

    // ---- strings, dates, periods, complex numbers ----

    /**
     * A string in UTF-8, NULL for null.
     *
     * @param a the call's arena
     * @param s the string
     * @return its C form
     */
    public static MemorySegment str(Arena a, String s) {
        return s == null ? MemorySegment.NULL : a.allocateFrom(s);
    }

    /**
     * A string nanoql returned, freed.
     *
     * @param p the string, or NULL
     * @return it, or null
     */
    public static String takeString(MemorySegment p) {
        if (p.address() == 0) {
            return null;
        }
        String s = p.reinterpret(Long.MAX_VALUE).getString(0);
        nanoql_h.nql_free(p);
        return s;
    }

    /**
     * A date's serial: QuantLib's (Excel's), 0 for none.
     *
     * @param d the date, or null
     * @return its serial
     */
    public static int date(LocalDate d) {
        return d == null ? 0 : (int) (d.toEpochDay() - EPOCH);
    }

    /**
     * A serial's date.
     *
     * @param serial the serial
     * @return the date, or null for 0
     */
    public static LocalDate toDate(int serial) {
        return serial == 0 ? null : LocalDate.ofEpochDay(EPOCH + serial);
    }

    /**
     * A period's C form, a struct by value.
     *
     * @param a the call's arena
     * @param length its length
     * @param units its units' value
     * @return the struct
     */
    public static MemorySegment period(Arena a, int length, int units) {
        MemorySegment s = nql_period.allocate(a);
        nql_period.length(s, length);
        nql_period.units(s, units);
        return s;
    }

    /**
     * A complex number's C form.
     *
     * @param a the call's arena
     * @param re its real part
     * @param im its imaginary part
     * @return the struct
     */
    public static MemorySegment complex(Arena a, double re, double im) {
        MemorySegment s = nql_complex.allocate(a);
        nql_complex.re(s, re);
        nql_complex.im(s, im);
        return s;
    }

    // ---- nullable values: a pointer, NULL for none ----

    public static MemorySegment optDouble(Arena a, Double v) {
        return v == null ? MemorySegment.NULL : a.allocateFrom(ValueLayout.JAVA_DOUBLE, v);
    }

    public static MemorySegment optInt(Arena a, Integer v) {
        return v == null ? MemorySegment.NULL : a.allocateFrom(ValueLayout.JAVA_INT, v);
    }

    public static MemorySegment optLong(Arena a, Long v) {
        return v == null ? MemorySegment.NULL : a.allocateFrom(ValueLayout.JAVA_LONG, v);
    }

    public static MemorySegment optBool(Arena a, Boolean v) {
        if (v == null) {
            return MemorySegment.NULL;
        }
        MemorySegment s = a.allocate(ValueLayout.JAVA_BOOLEAN);
        s.set(ValueLayout.JAVA_BOOLEAN, 0, v);
        return s;
    }

    // ---- collections in: a pointer and a count, NULL for null and never NULL for empty ----

    private static MemorySegment nonNull(Arena a, MemorySegment s) {
        return s.byteSize() == 0 ? a.allocate(8) : s;
    }

    public static MemorySegment doubles(Arena a, double[] v) {
        return v == null ? MemorySegment.NULL : nonNull(a, a.allocateFrom(ValueLayout.JAVA_DOUBLE, v));
    }

    public static MemorySegment ints(Arena a, int[] v) {
        return v == null ? MemorySegment.NULL : nonNull(a, a.allocateFrom(ValueLayout.JAVA_INT, v));
    }

    public static MemorySegment longs(Arena a, long[] v) {
        return v == null ? MemorySegment.NULL : nonNull(a, a.allocateFrom(ValueLayout.JAVA_LONG, v));
    }

    public static MemorySegment bools(Arena a, boolean[] v) {
        if (v == null) {
            return MemorySegment.NULL;
        }
        MemorySegment s = a.allocate(Math.max(1, v.length));
        for (int i = 0; i < v.length; i++) {
            s.set(ValueLayout.JAVA_BOOLEAN, i, v[i]);
        }
        return s;
    }

    /**
     * A list's elements in a C form of four or eight bytes each (dates, enums, strings, handles), NULL for null.
     *
     * @param <T> the elements' type
     * @param a the call's arena
     * @param v the list
     * @param layout the C element's layout
     * @param put writes element i into the segment
     * @return the elements
     */
    public static <T> MemorySegment list(Arena a, List<T> v, ValueLayout layout, ElementWriter<T> put) {
        if (v == null) {
            return MemorySegment.NULL;
        }
        MemorySegment s = a.allocate(layout, Math.max(1, v.size()));
        for (int i = 0; i < v.size(); i++) {
            put.write(s, i, v.get(i));
        }
        return s;
    }

    /**
     * A list's elements in a C form of two ints each (periods), NULL for null.
     *
     * @param <T> the elements' type
     * @param a the call's arena
     * @param v the list
     * @param put writes element i's two ints at 2i and 2i + 1
     * @return the elements
     */
    public static <T> MemorySegment pairs(Arena a, List<T> v, ElementWriter<T> put) {
        if (v == null) {
            return MemorySegment.NULL;
        }
        MemorySegment s = a.allocate(ValueLayout.JAVA_INT, Math.max(2, 2L * v.size()));
        for (int i = 0; i < v.size(); i++) {
            put.write(s, i, v.get(i));
        }
        return s;
    }

    /**
     * Complex numbers in their C form, two doubles each, NULL for null.
     *
     * @param a the call's arena
     * @param v the numbers
     * @return the elements
     */
    public static MemorySegment complexes(Arena a, List<org.nanoql.Complex> v) {
        if (v == null) {
            return MemorySegment.NULL;
        }
        MemorySegment s = a.allocate(ValueLayout.JAVA_DOUBLE, Math.max(2, 2L * v.size()));
        for (int i = 0; i < v.size(); i++) {
            s.setAtIndex(ValueLayout.JAVA_DOUBLE, 2L * i, v.get(i).re());
            s.setAtIndex(ValueLayout.JAVA_DOUBLE, 2L * i + 1, v.get(i).im());
        }
        return s;
    }

    /**
     * Doubles as a list.
     *
     * @param v the doubles
     * @return the list
     */
    public static List<Double> boxed(double[] v) {
        List<Double> r = new ArrayList<>(v.length);
        for (double x : v) {
            r.add(x);
        }
        return r;
    }

    /** Writes one element into its C form. */
    @FunctionalInterface
    public interface ElementWriter<T> {
        void write(MemorySegment s, int i, T v);
    }

    /**
     * A list's count, 0 for null.
     *
     * @param v the list
     * @return its count
     */
    public static int count(List<?> v) {
        return v == null ? 0 : v.size();
    }

    public static int count(double[] v) {
        return v == null ? 0 : v.length;
    }

    public static int count(int[] v) {
        return v == null ? 0 : v.length;
    }

    public static int count(long[] v) {
        return v == null ? 0 : v.length;
    }

    public static int count(boolean[] v) {
        return v == null ? 0 : v.length;
    }

    /**
     * A matrix's values by rows, NULL for null.
     *
     * @param a the call's arena
     * @param m the matrix, rows of one length
     * @return the values
     */
    public static MemorySegment matrix(Arena a, double[][] m) {
        if (m == null) {
            return MemorySegment.NULL;
        }
        int rows = m.length, columns = rows == 0 ? 0 : m[0].length;
        MemorySegment s = a.allocate(ValueLayout.JAVA_DOUBLE, Math.max(1, (long) rows * columns));
        for (int i = 0; i < rows; i++) {
            if (m[i].length != columns) {
                throw new IllegalArgumentException("a matrix's rows have one length");
            }
            MemorySegment.copy(m[i], 0, s, ValueLayout.JAVA_DOUBLE, (long) i * columns * 8, columns);
        }
        return s;
    }

    public static int rows(double[][] m) {
        return m == null ? 0 : m.length;
    }

    public static int columns(double[][] m) {
        return m == null || m.length == 0 ? 0 : m[0].length;
    }

    /**
     * A jagged array's values flat by rows.
     *
     * @param a the call's arena
     * @param rows the rows
     * @return the values
     */
    public static MemorySegment jagged(Arena a, List<double[]> rows) {
        if (rows == null) {
            return MemorySegment.NULL;
        }
        int total = 0;
        for (double[] r : rows) {
            total += r.length;
        }
        MemorySegment s = a.allocate(ValueLayout.JAVA_DOUBLE, Math.max(1, total));
        long at = 0;
        for (double[] r : rows) {
            MemorySegment.copy(r, 0, s, ValueLayout.JAVA_DOUBLE, at * 8, r.length);
            at += r.length;
        }
        return s;
    }

    public static int jaggedCount(List<double[]> rows) {
        int total = 0;
        if (rows != null) {
            for (double[] r : rows) {
                total += r.length;
            }
        }
        return total;
    }

    public static MemorySegment rowLengths(Arena a, List<double[]> rows) {
        if (rows == null) {
            return MemorySegment.NULL;
        }
        int[] l = new int[rows.size()];
        for (int i = 0; i < l.length; i++) {
            l[i] = rows.get(i).length;
        }
        return ints(a, l);
    }

    /**
     * Copies an in-out array's values back after the call: a span, or a vector or matrix nanoql writes into.
     *
     * @param s the C buffer
     * @param v the caller's array
     */
    public static void copyBack(MemorySegment s, double[] v) {
        MemorySegment.copy(s, ValueLayout.JAVA_DOUBLE, 0, v, 0, v.length);
    }

    public static void copyBack(MemorySegment s, int[] v) {
        MemorySegment.copy(s, ValueLayout.JAVA_INT, 0, v, 0, v.length);
    }

    public static void copyBack(MemorySegment s, double[][] m) {
        int columns = columns(m);
        for (int i = 0; i < m.length; i++) {
            MemorySegment.copy(s, ValueLayout.JAVA_DOUBLE, (long) i * columns * 8, m[i], 0, columns);
        }
    }

    // ---- collections out: copied, then freed ----

    private static MemorySegment items(MemorySegment p, long bytes) {
        return p.reinterpret(Math.max(1, bytes));
    }

    public static double[] takeDoubles(MemorySegment p, int n) {
        double[] r = items(p, 8L * n).asSlice(0, 8L * n).toArray(ValueLayout.JAVA_DOUBLE);
        nanoql_h.nql_free(p);
        return r;
    }

    public static int[] takeInts(MemorySegment p, int n) {
        int[] r = items(p, 4L * n).asSlice(0, 4L * n).toArray(ValueLayout.JAVA_INT);
        nanoql_h.nql_free(p);
        return r;
    }

    public static long[] takeLongs(MemorySegment p, int n) {
        long[] r = items(p, 8L * n).asSlice(0, 8L * n).toArray(ValueLayout.JAVA_LONG);
        nanoql_h.nql_free(p);
        return r;
    }

    public static boolean[] takeBools(MemorySegment p, int n) {
        MemorySegment s = items(p, n);
        boolean[] r = new boolean[n];
        for (int i = 0; i < n; i++) {
            r[i] = s.get(ValueLayout.JAVA_BOOLEAN, i);
        }
        nanoql_h.nql_free(p);
        return r;
    }

    /**
     * A list of elements nanoql returned, each read from its C form, then freed.
     *
     * @param <T> the elements' type
     * @param p the elements
     * @param n their count
     * @param size a C element's bytes
     * @param read reads element i
     * @return the list
     */
    public static <T> List<T> takeList(MemorySegment p, int n, long size, ElementReader<T> read) {
        MemorySegment s = items(p, size * n);
        List<T> r = new ArrayList<>(n);
        for (int i = 0; i < n; i++) {
            r.add(read.read(s, i));
        }
        nanoql_h.nql_free(p);
        return r;
    }

    /** Reads one element from its C form. */
    @FunctionalInterface
    public interface ElementReader<T> {
        T read(MemorySegment s, int i);
    }

    public static List<Double> takeOptDoubles(MemorySegment p, MemorySegment has, int n) {
        double[] v = takeDoubles(p, n);
        boolean[] h = takeBools(has, n);
        List<Double> r = new ArrayList<>(n);
        for (int i = 0; i < n; i++) {
            r.add(h[i] ? v[i] : null);
        }
        return r;
    }

    public static List<Integer> takeOptInts(MemorySegment p, MemorySegment has, int n) {
        int[] v = takeInts(p, n);
        boolean[] h = takeBools(has, n);
        List<Integer> r = new ArrayList<>(n);
        for (int i = 0; i < n; i++) {
            r.add(h[i] ? v[i] : null);
        }
        return r;
    }

    public static double[][] takeMatrix(MemorySegment p, int rows, int columns) {
        double[] v = takeDoubles(p, rows * columns);
        double[][] m = new double[rows][columns];
        for (int i = 0; i < rows; i++) {
            System.arraycopy(v, i * columns, m[i], 0, columns);
        }
        return m;
    }

    public static List<double[]> takeJagged(MemorySegment p, MemorySegment lengths, int rows) {
        int[] l = takeInts(lengths, rows);
        int total = 0;
        for (int x : l) {
            total += x;
        }
        double[] v = takeDoubles(p, total);
        List<double[]> r = new ArrayList<>(rows);
        int at = 0;
        for (int x : l) {
            double[] row = new double[x];
            System.arraycopy(v, at, row, 0, x);
            r.add(row);
            at += x;
        }
        return r;
    }

    /**
     * A key-value collection nanoql returned, in its order.
     *
     * @param <K> the keys' type
     * @param <V> the values' type
     * @param keys the keys
     * @param values the values
     * @return the map
     */
    public static <K, V> Map<K, V> zip(List<K> keys, List<V> values) {
        Map<K, V> m = new LinkedHashMap<>();
        for (int i = 0; i < keys.size(); i++) {
            m.put(keys.get(i), values.get(i));
        }
        return m;
    }

    /**
     * An enum's constant by its value.
     *
     * @param <E> the enum
     * @param values the enum's constants
     * @param value the value
     * @param of the value of a constant
     * @return the constant
     */
    public static <E> E of(E[] values, int value, java.util.function.ToIntFunction<E> of) {
        for (E e : values) {
            if (of.applyAsInt(e) == value) {
                return e;
            }
        }
        throw new IllegalArgumentException("no constant has the value " + value);
    }

    /**
     * A new array of objects, for a reader.
     *
     * @param <T> the type
     * @param n the count
     * @param f a factory
     * @return the array
     */
    public static <T> T[] array(int n, IntFunction<T[]> f) {
        return f.apply(n);
    }

    /**
     * Releases a handle nanoql gave, unwrapped (a cleaner's action).
     *
     * @param h the handle
     */
    public static void release(MemorySegment h) {
        if (h.address() != 0) {
            nanoql_h.nql_release(h);
        }
    }

    /**
     * Retains a handle: a new handle to the same object, the caller's.
     *
     * @param h the handle
     * @return the new one
     */
    public static MemorySegment retain(MemorySegment h) {
        if (h.address() == 0) {
            return MemorySegment.NULL;
        }
        try (Arena a = Arena.ofConfined()) {
            MemorySegment r = a.allocate(ValueLayout.ADDRESS);
            check(nanoql_h.nql_retain(h, r));
            return r.get(ValueLayout.ADDRESS, 0);
        }
    }

    /**
     * The handle of an object that may be closed, refusing a closed one before the call crosses.
     *
     * @param o the object
     * @return its handle
     */
    public static MemorySegment self(NanoObject o) {
        return o.handle();
    }
}
