// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.replay;

import java.util.ArrayList;
import java.util.List;

/**
 * A JSON value of a trace line: a number keeps its text, a string its unescaped text, an array its items, an object its
 * keys and values in order.
 */
final class Jv {
    static final int NULL = 0;
    static final int BOOLEAN = 1;
    static final int NUMBER = 2;
    static final int STRING = 3;
    static final int ARRAY = 4;
    static final int OBJECT = 5;

    final int type;
    final String text;
    final boolean bool;
    final Jv[] items;
    final String[] keys;

    private Jv(int type, String text, boolean bool, Jv[] items, String[] keys) {
        this.type = type;
        this.text = text;
        this.bool = bool;
        this.items = items;
        this.keys = keys;
    }

    int count() {
        return items == null ? 0 : items.length;
    }

    /** Item i of an array, null past its end or for another value. */
    static Jv at(Jv v, int i) {
        return v != null && v.type == ARRAY && i < v.items.length ? v.items[i] : null;
    }

    /** An object's member, null where it has none. */
    static Jv member(Jv o, String key) {
        if (o == null || o.type != OBJECT) {
            return null;
        }
        for (int i = 0; i < o.keys.length; i++) {
            if (o.keys[i].equals(key)) {
                return o.items[i];
            }
        }
        return null;
    }

    static boolean isNull(Jv v) {
        return v == null || v.type == NULL;
    }

    /** A number's integer, 0 for anything else. */
    static long num(Jv v) {
        if (v == null || v.type != NUMBER) {
            return 0;
        }
        try {
            return Long.parseLong(v.text);
        } catch (NumberFormatException e) {
            return (long) Double.parseDouble(v.text);
        }
    }

    /** A double the trace writes as its bits in hexadecimal, 0 for anything else. */
    static double hexdouble(Jv v) {
        if (v == null || v.type != STRING) {
            return 0.0;
        }
        String s = v.text.length() > 16 ? v.text.substring(0, 16) : v.text;
        return Double.longBitsToDouble(Long.parseUnsignedLong(s, 16));
    }

    /** One line's value. */
    static Jv parse(String line) {
        return new Parser(line).value();
    }

    private static final class Parser {
        private final String s;
        private int p;

        Parser(String s) {
            this.s = s;
        }

        private void ws() {
            while (p < s.length()) {
                char c = s.charAt(p);
                if (c != ' ' && c != '\t' && c != '\r' && c != '\n') {
                    return;
                }
                p++;
            }
        }

        Jv value() {
            ws();
            if (p >= s.length()) {
                return new Jv(NULL, null, false, null, null);
            }
            char c = s.charAt(p);
            switch (c) {
                case '{': {
                    p++;
                    List<String> keys = new ArrayList<>();
                    List<Jv> items = new ArrayList<>();
                    ws();
                    if (p < s.length() && s.charAt(p) == '}') {
                        p++;
                    } else {
                        for (;;) {
                            keys.add(value().text);
                            ws();
                            if (p < s.length() && s.charAt(p) == ':') {
                                p++;
                            }
                            items.add(value());
                            ws();
                            if (p < s.length() && s.charAt(p) == ',') {
                                p++;
                                continue;
                            }
                            if (p < s.length() && s.charAt(p) == '}') {
                                p++;
                            }
                            break;
                        }
                    }
                    return new Jv(OBJECT, null, false, items.toArray(new Jv[0]), keys.toArray(new String[0]));
                }
                case '[': {
                    p++;
                    List<Jv> items = new ArrayList<>();
                    ws();
                    if (p < s.length() && s.charAt(p) == ']') {
                        p++;
                    } else {
                        for (;;) {
                            items.add(value());
                            ws();
                            if (p < s.length() && s.charAt(p) == ',') {
                                p++;
                                continue;
                            }
                            if (p < s.length() && s.charAt(p) == ']') {
                                p++;
                            }
                            break;
                        }
                    }
                    return new Jv(ARRAY, null, false, items.toArray(new Jv[0]), null);
                }
                case '"':
                    return new Jv(STRING, string(), false, null, null);
                case 'n':
                    p += 4;
                    return new Jv(NULL, null, false, null, null);
                case 't':
                    p += 4;
                    return new Jv(BOOLEAN, null, true, null, null);
                case 'f':
                    p += 5;
                    return new Jv(BOOLEAN, null, false, null, null);
                default: {
                    int q = p;
                    while (p < s.length() && "-+.eE0123456789".indexOf(s.charAt(p)) >= 0) {
                        p++;
                    }
                    return new Jv(NUMBER, s.substring(q, p), false, null, null);
                }
            }
        }

        private String string() {
            p++;
            int q = p;
            while (p < s.length() && s.charAt(p) != '"' && s.charAt(p) != '\\') {
                p++;
            }
            if (p < s.length() && s.charAt(p) == '"') {
                return s.substring(q, p++);
            }
            StringBuilder b = new StringBuilder().append(s, q, p);
            while (p < s.length() && s.charAt(p) != '"') {
                char c = s.charAt(p++);
                if (c != '\\') {
                    b.append(c);
                    continue;
                }
                char e = s.charAt(p++);
                switch (e) {
                    case 'n' -> b.append('\n');
                    case 't' -> b.append('\t');
                    case 'r' -> b.append('\r');
                    case 'b' -> b.append('\b');
                    case 'f' -> b.append('\f');
                    case 'u' -> {
                        b.append((char) Integer.parseInt(s.substring(p, p + 4), 16));
                        p += 4;
                    }
                    default -> b.append(e);
                }
            }
            p++;
            return b.toString();
        }
    }
}
