// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/** A refusal other than an argument's: QuantLib's checks, a refused handle (9, 10), and the rest of the codes. */
public class NanoQLException extends RuntimeException implements NanoQLError {
    private static final long serialVersionUID = 1L;

    private final int code;
    private final String param;
    private final String typeName;

    /**
     * A refusal.
     *
     * @param code its status code
     * @param message its message
     * @param param the parameter, or null
     * @param typeName the .NET exception's type, or null
     */
    public NanoQLException(int code, String message, String param, String typeName) {
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
