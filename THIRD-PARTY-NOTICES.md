# Third-party notices

Recordum is MIT-licensed (`LICENSE`). It ships with the components below, under their own licenses. The NuGet packages
not listed here are resolved at build time and keep the license stated in their own package.

## SQLite3 Multiple Ciphers — `SQLite3MC.PCLRaw.bundle`, `.lib`, `.provider` 2.6.1

The encrypted SQLite engine (native libraries and their SQLitePCLRaw provider). Files are written in SQLCipher 4 format.
<https://utelle.github.io/SQLite3MultipleCiphers/>

MIT License — Copyright (c) 2019-2026 Ulrich Telle (library); Copyright (c) 2023-2026 Ulrich Telle, et al. (NuGet
packages). The native library also compiles in, under their own terms (from the library's `src/filelist.md`):

- `sha2.c` — BSD-3-Clause, reproduced below;
- `rijndael.*` — LGPL-3.0+ WITH WxWindows-exception-3.1 (the exception allows distributing the binary under our terms);
- the AEGIS amalgamation (`libaegis.c`) — MIT, Copyright (c) 2024-2026 Ulrich Telle;
- `chacha20poly1305.c`, `fastpbkdf2.*`, `md5.c`, `sha1.*`, `rekeyvacuum.c` and the SQLite core — public domain.

MIT license text:

> Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated
> documentation files (the "Software"), to deal in the Software without restriction, including without limitation the
> rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit
> persons to whom the Software is furnished to do so, subject to the following conditions:
>
> The above copyright notice and this permission notice shall be included in all copies or substantial portions of the
> Software.
>
> THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE
> WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR
> COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR
> OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.

### `sha2.c` (inside the SQLite3 Multiple Ciphers native library)

> Copyright (C) 2005, 2007 Olivier Gay <olivier.gay@a3.epfl.ch>
> All rights reserved.
>
> Redistribution and use in source and binary forms, with or without modification, are permitted provided that the
> following conditions are met:
> 1. Redistributions of source code must retain the above copyright notice, this list of conditions and the following
>    disclaimer.
> 2. Redistributions in binary form must reproduce the above copyright notice, this list of conditions and the following
>    disclaimer in the documentation and/or other materials provided with the distribution.
> 3. Neither the name of the project nor the names of its contributors may be used to endorse or promote products
>    derived from this software without specific prior written permission.
>
> THIS SOFTWARE IS PROVIDED BY THE PROJECT AND CONTRIBUTORS ``AS IS'' AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING,
> BUT NOT LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO
> EVENT SHALL THE PROJECT OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR
> CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE, DATA, OR
> PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
> LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS SOFTWARE, EVEN IF
> ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

## SQLite

Compiled into the SQLite3 Multiple Ciphers native libraries. Public domain. <https://sqlite.org/copyright.html>

## SQLitePCLRaw.core 3.0.2

Copyright 2014-2025 SourceGear, LLC. Licensed under the Apache License, Version 2.0
(<https://www.apache.org/licenses/LICENSE-2.0>).
