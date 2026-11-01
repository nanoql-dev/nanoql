// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql;

/**
 * A sequence generator's draws: its points by rows, their weights, and the next snapshot.
 *
 * @param <G> the generator
 * @param points the points, a row each of the generator's dimension
 * @param weights the weights
 * @param next the next snapshot
 */
public record SequenceDraws<G>(double[][] points, double[] weights, G next) {
}
