# Photo fixtures

No binary files are committed. `PhotoFixtures.cs` (this folder) builds them at test time:

- JPEG/PNG are encoded by SkiaSharp (which writes no EXIF).
- `WithExif` inserts an APP1 `Exif` segment right after the SOI marker: a little-endian TIFF with an IFD0 holding
  Orientation (0x0112) and a pointer to a GPS IFD with GPSLatitudeRef/GPSLatitude. SkiaSharp reading the orientation
  (output taller than wide) is the proof the segment is valid; `exiftool` is not needed.
- HEIC is simulated with a `ftypheic` box header, "text renamed .jpg" is plain text, "truncated" is half a JPEG,
  and the over-50 MB file is a sparse file created in the test's own temp directory.
