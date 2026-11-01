// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A pair a member returns, its elements in order.
 *
 * @param <A> the first's type
 * @param <B> the second's type
 * @param item1 the first
 * @param item2 the second
 */
public record Tuple2<A, B>(A item1, B item2) {
}
