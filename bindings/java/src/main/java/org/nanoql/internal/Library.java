// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

package org.nanoql.internal;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.io.UncheckedIOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.DigestInputStream;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Arrays;
import java.util.HexFormat;
import java.util.Locale;

/**
 * Loads nanoql's native library once, before any call: the path {@code nanoql.library} names (a system property, or
 * {@link org.nanoql.NanoQL#useLibrary}), else the platform's from its natives jar on the class path. On Linux and macOS
 * the jar's library is copied into a private temporary folder and deleted with it once loaded, which those systems
 * allow, so nothing is left. Windows can't delete a library a process has loaded, so there it is copied once to the
 * user's temporary folder under a name its bytes' SHA-256 keys ({@code nanoql-<hash>/}) and reused by later runs, each
 * comparing the copy's whole SHA-256 with the jar's before loading it and writing a fresh copy where they differ. Not
 * part of nanoql's API.
 */
public final class Library {
    private static volatile String loaded;
    private static String requested = System.getProperty("nanoql.library");

    private Library() {
    }

    /** Loads the library if it isn't loaded. */
    public static void load() {
        if (loaded != null) {
            return;
        }
        synchronized (Library.class) {
            if (loaded != null) {
                return;
            }
            if (requested != null) {
                System.load(requested);
                loaded = requested;
                return;
            }
            Path file = extract();
            try {
                System.load(file.toString());
            } finally {
                if (!windows()) {
                    delete(file);
                }
            }
            loaded = file.toString();
        }
    }

    /**
     * Names the library to load in place of the platform's, before it is loaded.
     *
     * @param path the library's path
     * @throws IllegalStateException once a library is loaded
     */
    public static synchronized void use(String path) {
        if (loaded != null) {
            if (loaded.equals(path)) {
                return;
            }
            throw new IllegalStateException("nanoql's library is loaded from " + loaded + "; objects of another library are foreign to it");
        }
        requested = path;
    }

    /**
     * The loaded library's path, loading it first; on Linux and macOS, the natives jar's copy is deleted once loaded.
     *
     * @return the path
     */
    public static String path() {
        load();
        return loaded;
    }

    private static boolean windows() {
        return System.getProperty("os.name").toLowerCase(Locale.ROOT).contains("win");
    }

    private static Path extract() {
        String os = System.getProperty("os.name").toLowerCase(Locale.ROOT);
        String arch = System.getProperty("os.arch").toLowerCase(Locale.ROOT);
        String platform = (os.contains("win") ? "windows" : os.contains("mac") ? "macos" : "linux") + "-"
            + (arch.contains("aarch64") || arch.contains("arm64") ? "arm64" : "x64");
        String name = os.contains("win") ? "nanoql.dll" : os.contains("mac") ? "libnanoql.dylib" : "libnanoql.so";
        String resource = "/org/nanoql/natives/" + platform + "/" + name;
        try {
            if (!windows()) {
                // A folder of a random name only its owner can open (POSIX), the copy deleted once loaded.
                Path file = Files.createTempDirectory("nanoql").resolve(name);
                try (InputStream in = open(resource, platform)) {
                    Files.copy(in, file);
                }
                return file.toAbsolutePath();
            }
            byte[] hash = sha256(open(resource, platform));
            Path file = Path.of(System.getProperty("java.io.tmpdir"), "nanoql-" + HexFormat.of().formatHex(hash, 0, 8), name).toAbsolutePath();
            if (Files.isRegularFile(file) && Arrays.equals(sha256(Files.newInputStream(file)), hash)) {
                return file;
            }
            // Written whole beside it, then moved into place, so two runs starting together don't load a partial copy.
            Files.createDirectories(file.getParent());
            Path part = Files.createTempFile(file.getParent(), name, ".part");
            try (InputStream in = open(resource, platform)) {
                Files.copy(in, part, StandardCopyOption.REPLACE_EXISTING);
            }
            try {
                Files.move(part, file, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
                return file;
            } catch (IOException e) {
                // Another run moved its copy in first, or holds the one there loaded: that one if it is the jar's, else
                // this run's own copy, left beside it.
                if (Files.isRegularFile(file) && Arrays.equals(sha256(Files.newInputStream(file)), hash)) {
                    Files.deleteIfExists(part);
                    return file;
                }
                return part;
            }
        } catch (IOException e) {
            throw new UncheckedIOException(e);
        }
    }

    // A stream's SHA-256, the stream closed.
    private static byte[] sha256(InputStream stream) throws IOException {
        try (InputStream in = stream; DigestInputStream digest = new DigestInputStream(in, MessageDigest.getInstance("SHA-256"))) {
            digest.transferTo(OutputStream.nullOutputStream());
            return digest.getMessageDigest().digest();
        } catch (NoSuchAlgorithmException e) {
            throw new IllegalStateException(e);
        }
    }

    private static void delete(Path file) {
        try {
            Files.deleteIfExists(file);
            Files.deleteIfExists(file.getParent());
        } catch (IOException e) {
            // Left in the private folder; the system's temporary-folder cleaning removes it.
        }
    }

    private static InputStream open(String resource, String platform) {
        InputStream in = Library.class.getResourceAsStream(resource);
        if (in == null) {
            throw new UnsatisfiedLinkError("no " + resource + " on the class path: add org.nanoql:nanoql's natives-" + platform
                + " jar, or set nanoql.library to the library's path");
        }
        return in;
    }
}
