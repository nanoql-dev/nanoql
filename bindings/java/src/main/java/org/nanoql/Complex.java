// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A complex number, as nanoql's crosses.
 *
 * @param re its real part
 * @param im its imaginary part
 */
public record Complex(double re, double im) {
}
