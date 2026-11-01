// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A generator's draws: its values, their weights, and the next snapshot, from which the stream continues.
 *
 * @param <G> the generator
 * @param values the values
 * @param weights the weights
 * @param next the next snapshot
 */
public record Draws<G>(double[] values, double[] weights, G next) {
}
