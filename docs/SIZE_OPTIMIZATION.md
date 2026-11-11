# catTransform v0.1.1 size optimization

The previous v0.1.0 installer was 165,792,578 bytes (158.1 MiB); the published folder was about 435.3 MiB. The new build targets a smaller download while keeping conversion, WinUI, bundled EPUBCheck, offline Java, font and licenses.

## Changes

- Build the Java runtime with `jlink` from the SHA-256-pinned Eclipse Temurin 21.0.13+9 JDK. Include the Java modules used by EPUBCheck and its dependencies, plus charset, locale, TLS and XML support. Preserve `legal/` and `NOTICE` in the installed runtime.
- Do not precompress the Java image. Inno Setup's solid LZMA2 compression handles the uncompressed modules more efficiently. A `jlink --compress=2` candidate made the installer larger, so that option was rejected.
- Use the supported `lzma2/ultra64` installer compression level. It takes longer to build and uses a larger decompression dictionary than the previous `lzma2/fast` setting.
- Keep the Windows App SDK and .NET self-contained. Narrowing the WinUI NuGet reference saved only about 2 MiB and did not remove AI binaries from the self-contained framework package, so that experiment was reverted.

## Measured candidate

| Measure | v0.1.0 | v0.1.1 candidate |
| --- | ---: | ---: |
| Installer | 165,792,578 bytes / 158.1 MiB | about 129 MB / 123.2 MiB |
| Published folder | 435.3 MiB | 399.5 MiB |
| Java runtime | about 145 MiB | 109.0 MiB |

Installer download reduction is about 22%; installed folder reduction is about 8%. The compressed artifact is checked again after the versioned release build.

## Verification and boundaries

The reduced Temurin runtime reports Java 21.0.13, retains module legal files, and validated a real EPUB 3.3 fixture with zero EPUBCheck errors. The application batch UI test converted TXT, JSON and EPUB through the bundled Java runtime, then checked output and history. The 70 automated tests and the application batch UI conversion passed after the v0.1.1 version rebuild. A clean Windows 11 installation and all real-world EPUB variants remain to be validated. The reduced Java image is specific to current EPUBCheck 5.3.0; update the module list and rerun integration tests when EPUBCheck changes.

Official references: [jlink](https://docs.oracle.com/en/java/javase/21/docs/specs/man/jlink.html); [Inno Setup compression](https://jrsoftware.org/ishelp/topic_setup_compression.htm).
