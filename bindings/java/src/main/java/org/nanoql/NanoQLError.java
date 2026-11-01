// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/** A refusal from nanoql (docs/bindings.md, "Errors"): its status code, the parameter as .NET names it, its .NET type. */
public interface NanoQLError {
    /**
     * The status code.
     *
     * @return the code
     */
    int code();

    /**
     * The parameter as .NET names it.
     *
     * @return the parameter, or null
     */
    String param();

    /**
     * The .NET exception's type.
     *
     * @return the type's full name, or null
     */
    String typeName();
}
