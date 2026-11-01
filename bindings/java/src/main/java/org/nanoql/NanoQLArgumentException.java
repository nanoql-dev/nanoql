// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/** An argument refused (codes 2, 3 and 4): an argument, a missing one, one out of its range. */
public class NanoQLArgumentException extends IllegalArgumentException implements NanoQLError {
    private static final long serialVersionUID = 1L;

    private final int code;
    private final String param;
    private final String typeName;

    /**
     * A refused argument.
     *
     * @param code its status code
     * @param message its message
     * @param param the parameter, or null
     * @param typeName the .NET exception's type, or null
     */
    public NanoQLArgumentException(int code, String message, String param, String typeName) {
        super(message);
        this.code = code;
        this.param = param;
        this.typeName = typeName;
    }

    @Override
    public int code() {
        return code;
    }

    @Override
    public String param() {
        return param;
    }

    @Override
    public String typeName() {
        return typeName;
    }
}
