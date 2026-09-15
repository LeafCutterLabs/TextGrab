# Bundled dependencies

TextGrab performs OCR locally. These assets are included in the portable release; the application does not download dependencies at runtime.

| Component | Version/source | License/notice |
| --- | --- | --- |
| Tesseract .NET wrapper and native binaries | NuGet Tesseract 5.2.0, https://github.com/charlesw/tesseract/tree/5.2.0 | tesseract-wrapper-LICENSE.txt, InteropDotNet-LICENSE.txt |
| Tesseract OCR | 5.2.0 | tesseract-LICENSE.txt |
| Leptonica | 1.82.0 | leptonica-LICENSE.txt |
| English and Spanish OCR models | tessdata_fast commit 87416418657359cb625c412a48b6e1d6d41c29bd | tessdata-fast-LICENSE.txt |
| libjpeg-turbo, statically included in Leptonica | 2.1.4 | libjpeg-turbo-LICENSE.md, libjpeg-turbo-README.ijg |
| libpng, statically included in Leptonica | 1.6.37 | libpng-LICENSE.txt |
| zlib, statically included in Leptonica | 1.2.13 | zlib-LICENSE.txt |
| libtiff, statically included in Leptonica | 4.4.0 | libtiff-LICENSE.txt |
| Microsoft Visual C++ release runtime | VC143 14.44.35112 x64 redistributable directory | VisualStudio2022-REDIST.html |
| .NET / Windows Desktop runtime | 10.0.12, self-contained publish | dotnet-*-LICENSE.txt, dotnet-runtime-THIRD-PARTY-NOTICES.txt |

This software is based in part on the work of the Independent JPEG Group.

English model source: https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/87416418657359cb625c412a48b6e1d6d41c29bd/eng.traineddata

English model SHA-256: `7D4322BD2A7749724879683FC3912CB542F19906C83BCC1A52132556427170B2`.

Spanish model source: https://raw.githubusercontent.com/tesseract-ocr/tessdata_fast/87416418657359cb625c412a48b6e1d6d41c29bd/spa.traineddata

Spanish model SHA-256: `6F2E04D02774A18F01BED44B1111F2CD7F3BA7AC9DC4373CD3F898A40EA6B464`.

The Microsoft runtime DLLs are unmodified release files copied from the installed Visual Studio Build Tools redistributable directory. App-local deployment puts these DLLs beside TextGrab.exe; it does not run a redistributable installer. See https://learn.microsoft.com/en-us/cpp/windows/choosing-a-deployment-method?view=msvc-170 and the included Microsoft distributable-files notice for applicable terms.

`assets.sha256` records the supplied OCR model and Microsoft runtime checksums. NuGet locks record the managed/native package contents used by the build. Development SDK downloads are not included in the release.
