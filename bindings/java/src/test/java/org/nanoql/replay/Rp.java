// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.replay;

import java.lang.foreign.MemorySegment;
import java.lang.invoke.MethodHandle;
import java.lang.invoke.MethodHandles;
import java.lang.invoke.MethodType;
import java.time.LocalDate;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.IdentityHashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Objects;
import java.util.Set;
import org.nanoql.Complex;
import org.nanoql.Distribution;
import org.nanoql.Draws;
import org.nanoql.NanoEnum;
import org.nanoql.NanoHandle;
import org.nanoql.NanoObject;
import org.nanoql.NanoQLError;
import org.nanoql.SequenceDraws;
import org.nanoql.Tuple2;
import org.nanoql.Tuple3;
import org.nanoql.internal.Native;
import org.nanoql.internal.Types;
import org.nanoql.time.Period;
import org.nanoql.time.TimeUnit;

/**
 * One replay (docs/bindings.md, "The vector suite through each binding"): the objects the trace names by id, readers
 * that make each argument as its Java member's parameter, and checks that compare each result with the recorded one:
 * doubles by their bits, NaN as NaN, objects by their ids and a new one by its class, collections element by element,
 * refusals by their codes. nanoql-bind's generated dispatch calls them, one handler per C function, each calling its
 * member through the layer. Every object a result brings is closed unless the trace names it again, so the run ends
 * with no live handle.
 */
final class Rp {
    boolean stop;
    String why; // why the layer can't make an argument, when it can't
    long line;
    long mismatches;
    long refusals;
    long shown = 20;
    // Off the reference platform, a double within this relative difference is a match, tallied with the widest (R19).
    static double tolerance;
    static long nearDoubles;
    static double widest;
    Jv afterValues;
    String fn = "";

    private final ArrayList<NanoObject> objs = new ArrayList<>();
    private boolean dates;
    private final Set<NanoObject> held = Collections.newSetFromMap(new IdentityHashMap<>());
    private final ArrayList<NanoObject> temps = new ArrayList<>();
    private static final ClassValue<Map<Integer, Object>> CONSTANTS = new ClassValue<>() {
        @Override
        protected Map<Integer, Object> computeValue(Class<?> cls) {
            Map<Integer, Object> m = new HashMap<>();
            for (Object c : cls.isEnum() ? cls.getEnumConstants() : new Object[0]) {
                m.putIfAbsent(((NanoEnum) c).value(), c);
            }
            return m;
        }
    };

    // A flags enum's of(int), which takes any combination.
    private static final ClassValue<MethodHandle> FLAGS = new ClassValue<>() {
        @Override
        protected MethodHandle computeValue(Class<?> cls) {
            try {
                return cls.isEnum() ? null : MethodHandles.publicLookup().findStatic(cls, "of", MethodType.methodType(cls, int.class));
            } catch (ReflectiveOperationException e) {
                throw new IllegalStateException(e);
            }
        }
    };

    void mismatch(String what, String want, String got) {
        if (mismatches++ < shown) {
            System.out.printf("MISMATCH line %d %s: %s: recorded %s, replayed %s%n", line, fn, what, want, got);
        }
    }

    // ---- arguments ----

    /** Argument i: an argument the trace couldn't write ({"x": ...}) stops the fact. */
    Jv arg(Jv a, int i) {
        Jv v = Jv.at(a, i);
        if (v != null && v.type == Jv.OBJECT) {
            stop = true;
        }
        return v;
    }

    double dbl(Jv a, int i) {
        return Jv.hexdouble(arg(a, i));
    }

    int i32(Jv a, int i) {
        return (int) Jv.num(arg(a, i));
    }

    long i64(Jv a, int i) {
        return Jv.num(arg(a, i));
    }

    boolean bool(Jv a, int i) {
        Jv v = arg(a, i);
        return v != null && v.type == Jv.BOOLEAN && v.bool;
    }

    Double optDbl(Jv a, int i) {
        Jv v = arg(a, i);
        return Jv.isNull(v) ? null : Jv.hexdouble(v);
    }

    Integer optI32(Jv a, int i) {
        Jv v = arg(a, i);
        return Jv.isNull(v) ? null : (int) Jv.num(v);
    }

    Long optI64(Jv a, int i) {
        Jv v = arg(a, i);
        return Jv.isNull(v) ? null : Jv.num(v);
    }

    Boolean optBool(Jv a, int i) {
        Jv v = arg(a, i);
        return Jv.isNull(v) ? null : v.bool;
    }

    String str(Jv a, int i) {
        return text(arg(a, i));
    }

    LocalDate date(Jv a, int i) {
        return Native.toDate((int) Jv.num(arg(a, i)));
    }

    Period period(Jv a, int i) {
        return period(arg(a, i));
    }

    Complex complex(Jv a, int i) {
        return complex(arg(a, i));
    }

    /**
     * An enum's constant, a flags enum's any combination; a value no constant has stops the fact, Java's enums holding
     * their constants only (a refusal's test of an undefined value).
     */
    <E extends NanoEnum> E enm(Jv a, int i, Class<E> cls) {
        return constant(arg(a, i), cls);
    }

    /** An object the replay made, as the parameter's class; one it didn't make stops the fact. */
    <T extends NanoHandle> T obj(Jv a, int i, Class<T> cls) {
        return object(arg(a, i), cls);
    }

    /**
     * An instance member's receiver. Null, which C passes as a NULL handle (a preset's lookup in the default convention
     * set), is an object of the class holding no handle, as C++'s T(nullptr).
     */
    <T extends NanoHandle> T self(Jv a, Class<T> cls) {
        Jv v = arg(a, 0);
        if (!Jv.isNull(v)) {
            return object(v, cls);
        }
        T o = Types.adopt(cls, MemorySegment.NULL);
        temps.add((NanoObject) o);
        return o;
    }

    /** A matrix, which the trace writes as {"r", "c", "v"}. */
    double[][] matrix(Jv a, int i) {
        Jv v = Jv.at(a, i);
        if (v == null || v.type != Jv.OBJECT) {
            return null;
        }
        Jv values = Jv.member(v, "v");
        if (values == null) {
            stop = true;
            return null;
        }
        int rows = (int) Jv.num(Jv.member(v, "r")), columns = (int) Jv.num(Jv.member(v, "c"));
        if (rows == 0 && columns != 0) {
            stop = true; // rows of none keep no columns
            why = "a 0-by-n matrix, which a double[][] doesn't hold";
            return null;
        }
        double[][] m = new double[rows][columns];
        for (int k = 0; k < rows * columns; k++) {
            m[k / columns][k % columns] = Jv.hexdouble(Jv.at(values, k));
        }
        return m;
    }

    double[] doubles(Jv a, int i) {
        return doubles(arg(a, i));
    }

    int[] ints(Jv a, int i) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        int[] x = new int[v.count()];
        for (int k = 0; k < x.length; k++) {
            x[k] = (int) Jv.num(v.items[k]);
        }
        return x;
    }

    long[] longs(Jv a, int i) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        long[] x = new long[v.count()];
        for (int k = 0; k < x.length; k++) {
            x[k] = Jv.num(v.items[k]);
        }
        return x;
    }

    boolean[] bools(Jv a, int i) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        boolean[] x = new boolean[v.count()];
        for (int k = 0; k < x.length; k++) {
            x[k] = v.items[k].type == Jv.BOOLEAN && v.items[k].bool;
        }
        return x;
    }

    /** A list, each element of its kind (the surface's), objects and enums of their class. */
    @SuppressWarnings({"rawtypes", "unchecked"})
    List list(Jv a, int i, String kind, Class<?> cls) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        List x = new ArrayList(v.count());
        for (Jv e : v.items) {
            x.add(element(e, kind, cls));
        }
        return x;
    }

    /** A tuple argument, which the trace writes as its elements in order (B2R3): a Tuple2 or a Tuple3. */
    Object tuple(Jv a, int i, String[] kinds, Class<?>[] classes) {
        return tupleOf(arg(a, i), kinds, classes);
    }

    /** A collection of tuples, each written as its elements in order (B2R3). */
    @SuppressWarnings({"rawtypes", "unchecked"})
    List tuples(Jv a, int i, String[] kinds, Class<?>[] classes) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        List x = new ArrayList(v.count());
        for (Jv e : v.items) {
            x.add(tupleOf(e, kinds, classes));
        }
        return x;
    }

    private Object tupleOf(Jv v, String[] kinds, Class<?>[] classes) {
        Object a = element(Jv.at(v, 0), kinds[0], classes[0]);
        Object b = element(Jv.at(v, 1), kinds[1], classes[1]);
        return kinds.length == 2 ? new Tuple2<>(a, b) : new Tuple3<>(a, b, element(Jv.at(v, 2), kinds[2], classes[2]));
    }

    List<double[]> jagged(Jv a, int i) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        List<double[]> x = new ArrayList<>(v.count());
        for (Jv row : v.items) {
            x.add(doubles(row));
        }
        return x;
    }

    /** A key-value collection, which the trace writes as its pairs in order. */
    @SuppressWarnings({"rawtypes", "unchecked"})
    Map map(Jv a, int i, String keyKind, Class<?> keyClass, String valueKind, Class<?> valueClass) {
        Jv v = arg(a, i);
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        Map x = new LinkedHashMap();
        for (Jv pair : v.items) {
            x.put(element(Jv.at(pair, 0), keyKind, keyClass), element(Jv.at(pair, 1), valueKind, valueClass));
        }
        return x;
    }

    private Object element(Jv e, String kind, Class<?> cls) {
        return switch (kind) {
            case "double" -> Jv.hexdouble(e);
            case "int32" -> (int) Jv.num(e);
            case "int64" -> Jv.num(e);
            case "bool" -> e != null && e.type == Jv.BOOLEAN && e.bool;
            case "double?" -> Jv.isNull(e) ? null : Jv.hexdouble(e);
            case "int32?" -> Jv.isNull(e) ? null : (Object) (int) Jv.num(e);
            case "string" -> text(e);
            case "date", "date?" -> Native.toDate((int) Jv.num(e));
            case "period" -> period(e);
            case "complex" -> complex(e);
            case "enum" -> cls == Integer.class ? (Object) (int) Jv.num(e) : constant(e, cls.asSubclass(NanoEnum.class));
            case "handle", "boxed", "boxed?", "snapshot" -> object(e, cls.asSubclass(NanoHandle.class));
            default -> throw new IllegalStateException("no reader for the element kind " + kind);
        };
    }

    private static String text(Jv v) {
        return v != null && v.type == Jv.STRING ? v.text : null;
    }

    private Period period(Jv v) {
        if (Jv.isNull(v)) {
            return null;
        }
        TimeUnit u = constant(Jv.at(v, 1), TimeUnit.class);
        return u == null ? null : new Period((int) Jv.num(Jv.at(v, 0)), u);
    }

    private static Complex complex(Jv v) {
        return Jv.isNull(v) ? null : new Complex(Jv.hexdouble(Jv.at(v, 0)), Jv.hexdouble(Jv.at(v, 1)));
    }

    private static double[] doubles(Jv v) {
        if (Jv.isNull(v) || v.type != Jv.ARRAY) {
            return null;
        }
        double[] x = new double[v.count()];
        for (int k = 0; k < x.length; k++) {
            x[k] = Jv.hexdouble(v.items[k]);
        }
        return x;
    }

    private <E> E constant(Jv v, Class<E> cls) {
        if (Jv.isNull(v)) {
            return null;
        }
        MethodHandle flags = FLAGS.get(cls);
        if (flags != null) {
            try {
                return cls.cast(flags.invoke((int) Jv.num(v)));
            } catch (Throwable e) {
                throw new IllegalStateException(e);
            }
        }
        Object c = CONSTANTS.get(cls).get((int) Jv.num(v));
        if (c == null) {
            stop = true;
            why = "an enum value Java's enum doesn't hold";
        }
        return cls.cast(c);
    }

    private <T extends NanoHandle> T object(Jv v, Class<T> cls) {
        if (Jv.isNull(v)) {
            return null;
        }
        long id = v.type == Jv.NUMBER ? Jv.num(v) : 0;
        NanoObject o = id > 0 && id < objs.size() ? objs.get((int) id) : null;
        if (o == null) {
            stop = true; // an object made by a call the replay didn't make
            return null;
        }
        if (cls.isInstance(o)) {
            return cls.cast(o);
        }
        T c = o.as(cls);
        temps.add((NanoObject) c);
        return c;
    }

    /** Closes the objects a call's arguments took as another class. */
    void done() {
        for (NanoObject o : temps) {
            o.close();
        }
        temps.clear();
    }

    // ---- the call ----

    /** The call succeeded: true when C#'s did too and its result is to be checked. */
    boolean ok(int recorded) {
        return st(0, recorded, null);
    }

    /** The call threw: a refusal is checked against the recorded status, anything else is a mismatch. */
    void fail(int recorded, RuntimeException e) {
        if (e instanceof NanoQLError q) {
            st(q.code(), recorded, e.getMessage());
        } else {
            mismatch("call", "status " + recorded, "an exception: " + e);
        }
    }

    private boolean st(int got, int recorded, String message) {
        if (got == recorded) {
            if (got != 0) {
                refusals++;
            }
            return got == 0;
        }
        mismatch("status", "status " + recorded, "status " + got + (got != 0 ? " (" + message + ")" : ""));
        return false;
    }

    /** A span's or an in-out argument's values after the call: the recorded "w" list k. */
    void after(int k, double[] v) {
        Jv w = Jv.at(afterValues, k);
        if (v == null ? w != null && w.count() != 0 : w == null || w.count() != v.length) {
            mismatch("span after the call", "its values", "another count");
            return;
        }
        for (int j = 0; v != null && j < v.length; j++) {
            check(w.items[j], v[j]);
        }
    }

    void after(int k, int[] v) {
        Jv w = Jv.at(afterValues, k);
        if (v == null ? w != null && w.count() != 0 : w == null || w.count() != v.length) {
            mismatch("span after the call", "its values", "another count");
            return;
        }
        for (int j = 0; v != null && j < v.length; j++) {
            check(w.items[j], v[j]);
        }
    }

    void after(int k, double[][] m) {
        after(k, m == null ? null : flat(m));
    }

    // ---- results ----

    void check(Jv res, double v) {
        double want = Jv.hexdouble(res);
        if (!same(want, v)) {
            mismatch("double", hex(want), hex(v));
        }
    }

    void check(Jv res, int v) {
        if (Jv.num(res) != v) {
            mismatch("integer", Long.toString(Jv.num(res)), Integer.toString(v));
        }
    }

    void check(Jv res, long v) {
        if (Jv.num(res) != v) {
            mismatch("integer", Long.toString(Jv.num(res)), Long.toString(v));
        }
    }

    void check(Jv res, boolean v) {
        boolean want = res != null && res.type == Jv.BOOLEAN && res.bool;
        if (want != v) {
            mismatch("bool", Boolean.toString(want), Boolean.toString(v));
        }
    }

    /** A result whose kind holds a date: a date the layer gives as null is the trace's serial 0, no date. */
    void checkDates(Jv res, Object v) {
        dates = true;
        try {
            check(res, v);
        } finally {
            dates = false;
        }
    }

    void check(Jv res, Object v) {
        if (v == null) {
            if (!Jv.isNull(res) && !(dates && res.type == Jv.NUMBER && Jv.num(res) == 0)) {
                mismatch("value", "a value", "null");
            }
        } else if (v instanceof Double d) {
            nonNull(res);
            check(res, d.doubleValue());
        } else if (v instanceof Integer x) {
            nonNull(res);
            check(res, x.intValue());
        } else if (v instanceof Long x) {
            nonNull(res);
            check(res, x.longValue());
        } else if (v instanceof Boolean b) {
            nonNull(res);
            check(res, b.booleanValue());
        } else if (v instanceof String s) {
            if (!Objects.equals(text(res), s)) {
                mismatch("string", String.valueOf(text(res)), s);
            }
        } else if (v instanceof LocalDate d) {
            if (Jv.num(res) != Native.date(d)) {
                mismatch("date", Long.toString(Jv.num(res)), Integer.toString(Native.date(d)));
            }
        } else if (v instanceof Period p) {
            if (Jv.num(Jv.at(res, 0)) != p.length() || Jv.num(Jv.at(res, 1)) != p.units().value()) {
                mismatch("period", String.valueOf(Jv.num(Jv.at(res, 0))) + "/" + Jv.num(Jv.at(res, 1)), p.length() + "/" + p.units().value());
            }
        } else if (v instanceof Complex c) {
            if (!same(Jv.hexdouble(Jv.at(res, 0)), c.re()) || !same(Jv.hexdouble(Jv.at(res, 1)), c.im())) {
                mismatch("complex", "", "");
            }
        } else if (v instanceof NanoEnum e) {
            if (Jv.num(res) != e.value()) {
                mismatch("enum", Long.toString(Jv.num(res)), Integer.toString(e.value()));
            }
        } else if (v instanceof NanoObject o) {
            checkObject(res, o);
        } else if (v instanceof double[] x) {
            if (count(res, x.length)) {
                for (int k = 0; k < x.length; k++) {
                    check(res.items[k], x[k]);
                }
            }
        } else if (v instanceof int[] x) {
            if (count(res, x.length)) {
                for (int k = 0; k < x.length; k++) {
                    check(res.items[k], x[k]);
                }
            }
        } else if (v instanceof long[] x) {
            if (count(res, x.length)) {
                for (int k = 0; k < x.length; k++) {
                    check(res.items[k], x[k]);
                }
            }
        } else if (v instanceof boolean[] x) {
            if (count(res, x.length)) {
                for (int k = 0; k < x.length; k++) {
                    check(res.items[k], x[k]);
                }
            }
        } else if (v instanceof double[][] m) {
            // A matrix: its shape, then its values by rows; a null one comes back empty. Rows of none keep no columns.
            if (res == null || res.type != Jv.OBJECT) {
                if (m.length != 0) {
                    mismatch("matrix", "null", "a matrix");
                }
            } else if (Jv.num(Jv.member(res, "r")) != m.length || (m.length > 0 && Jv.num(Jv.member(res, "c")) != m[0].length)) {
                mismatch("matrix", "its shape", "another shape");
            } else {
                check(Jv.member(res, "v"), flat(m));
            }
        } else if (v instanceof List<?> x) {
            if (count(res, x.size())) {
                for (int k = 0; k < x.size(); k++) {
                    check(res.items[k], x.get(k));
                }
            }
        } else if (v instanceof Map<?, ?> x) {
            if (count(res, x.size())) {
                int k = 0;
                for (Map.Entry<?, ?> e : x.entrySet()) {
                    check(Jv.at(res.items[k], 0), e.getKey());
                    check(Jv.at(res.items[k], 1), e.getValue());
                    k++;
                }
            }
        } else if (v instanceof Tuple2<?, ?> t) {
            check(Jv.at(res, 0), t.item1());
            check(Jv.at(res, 1), t.item2());
        } else if (v instanceof Tuple3<?, ?, ?> t) {
            check(Jv.at(res, 0), t.item1());
            check(Jv.at(res, 1), t.item2());
            check(Jv.at(res, 2), t.item3());
        } else if (v instanceof Distribution d) {
            double[][] parts = {d.x(), d.dx(), d.density(), d.cumulative(), d.excess(), d.average()};
            for (int k = 0; k < parts.length; k++) {
                check(Jv.at(res, k), parts[k]);
            }
        } else if (v instanceof Draws<?> d) {
            // A generator's draws: values, weights, the next snapshot.
            check(Jv.at(res, 0), d.values());
            check(Jv.at(res, 1), d.weights());
            check(Jv.at(res, 2), d.next());
        } else if (v instanceof SequenceDraws<?> d) {
            // A sequence generator's draws: values by rows, weights, the dimension, the next snapshot.
            double[][] p = d.points();
            check(Jv.at(res, 0), flat(p));
            check(Jv.at(res, 1), d.weights());
            check(Jv.at(res, 2), p.length == 0 ? (int) Jv.num(Jv.at(res, 2)) : p[0].length);
            check(Jv.at(res, 3), d.next());
        } else {
            mismatch("value", "a checked type", v.getClass().getName());
        }
    }

    private void nonNull(Jv res) {
        if (Jv.isNull(res)) {
            mismatch("value", "null", "a value");
        }
    }

    // A collection's count; a null one comes back empty.
    private boolean count(Jv res, int n) {
        if (res == null || res.type != Jv.ARRAY) {
            if (n != 0) {
                mismatch("array", "null", "an array");
            }
            return false;
        }
        if (res.count() != n) {
            mismatch("array", res.count() + " elements", n + " elements");
            return false;
        }
        return true;
    }

    /** A result object: a new one registered under its id (its class checked), a known one compared by equality. */
    void checkObject(Jv res, NanoObject o) {
        Jv n = Jv.member(res, "n");
        Jv s = Jv.member(res, "s");
        if (Jv.isNull(res)) {
            mismatch("object", "null", "an object");
            return;
        }
        int id = (int) Jv.num(n != null ? n : s);
        while (objs.size() <= id) {
            objs.add(null);
        }
        if (n != null || objs.get(id) == null) {
            // A new object's class: the .NET type's full name C#'s had.
            Jv y = Jv.member(res, "y");
            if (n != null && y != null && y.type == Jv.STRING) {
                String got = o.typeName();
                if (!typeKey(y.text).equals(typeKey(got))) {
                    mismatch("object type", y.text, got);
                }
            }
            NanoObject old = objs.set(id, o);
            if (old != null && old != o) {
                held.remove(old);
                old.close();
            }
            held.add(o);
            return;
        }
        if (!objs.get(id).equals(o)) {
            mismatch("object", "the same object", "another");
        }
    }

    /** Closes every object a result brought that the replay didn't register. */
    void release(Object v) {
        if (v instanceof NanoObject o) {
            if (!held.contains(o)) {
                o.close();
            }
        } else if (v instanceof Iterable<?> x) {
            for (Object e : x) {
                release(e);
            }
        } else if (v instanceof Map<?, ?> x) {
            for (Map.Entry<?, ?> e : x.entrySet()) {
                release(e.getKey());
                release(e.getValue());
            }
        } else if (v instanceof Tuple2<?, ?> t) {
            release(t.item1());
            release(t.item2());
        } else if (v instanceof Tuple3<?, ?, ?> t) {
            release(t.item1());
            release(t.item2());
            release(t.item3());
        } else if (v instanceof Draws<?> d) {
            release(d.next());
        } else if (v instanceof SequenceDraws<?> d) {
            release(d.next());
        }
    }

    /** Closes every registered object: the run's end. */
    void closeAll() {
        for (NanoObject o : objs) {
            if (o != null) {
                o.close();
            }
        }
        objs.clear();
        held.clear();
    }

    private static double[] flat(double[][] m) {
        int columns = m.length == 0 ? 0 : m[0].length;
        double[] x = new double[m.length * columns];
        for (int i = 0; i < m.length; i++) {
            System.arraycopy(m[i], 0, x, i * columns, columns);
        }
        return x;
    }

    private static boolean same(double a, double b) {
        if ((Double.isNaN(a) && Double.isNaN(b)) || Double.doubleToRawLongBits(a) == Double.doubleToRawLongBits(b)) {
            return true;
        }
        double d = Math.abs(a - b);
        double m = Math.max(Math.abs(a), Math.abs(b));
        if (tolerance > 0 && Double.isFinite(a) && Double.isFinite(b) && d <= tolerance * m) {
            nearDoubles++;
            if (m > 0) {
                widest = Math.max(widest, d / m);
            }
            return true;
        }
        return false;
    }

    private static String hex(double d) {
        return String.format("%016x (%.17g)", Double.doubleToRawLongBits(d), d);
    }

    /** A type's name without each file-local class's compiler prefix (<File>F<hash>__, the hash of its source file's path). */
    static String typeKey(String name) {
        StringBuilder out = new StringBuilder(name.length());
        for (int i = 0; i < name.length(); i++) {
            if (name.charAt(i) == '<') {
                int q = name.indexOf('>', i);
                if (q >= 0 && q + 1 < name.length() && name.charAt(q + 1) == 'F') {
                    int e = q + 2;
                    while (e < name.length() && Character.digit(name.charAt(e), 16) >= 0) {
                        e++;
                    }
                    if (e > q + 2 && name.startsWith("__", e)) {
                        i = e + 1;
                        continue;
                    }
                }
            }
            out.append(name.charAt(i));
        }
        return out.toString();
    }
}
