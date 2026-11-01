// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A triple a member returns, its elements in order.
 *
 * @param <A> the first's type
 * @param <B> the second's type
 * @param <C> the third's type
 * @param item1 the first
 * @param item2 the second
 * @param item3 the third
 */
public record Tuple3<A, B, C>(A item1, B item2, C item3) {
}
