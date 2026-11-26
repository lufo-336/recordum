#!/usr/bin/env bash
# Verifica il manifest Android unito: vieta INTERNET e CAMERA, impone android:allowBackup="false".
#
#   scripts/android/check-manifest.sh              # cerca il manifest unito più recente sotto obj/Release
#   scripts/android/check-manifest.sh <percorso>   # controlla il file indicato
set -euo pipefail

if [ $# -gt 1 ]; then
    echo "Uso: $0 [percorso-AndroidManifest.xml]" >&2
    exit 2
fi

repo="$(cd "$(dirname "$0")/../.." && pwd)"

if [ $# -eq 1 ]; then
    manifest="$1"
else
    release_obj="$repo/src/Recordum.Android/obj/Release"
    manifest=""
    # Solo il manifest UNITO (android/manifest/): quello in obj/.../android-arm64/ e' precedente all'unione delle
    # dipendenze e non vedrebbe un permesso portato da un AAR (review Argon del Task 4).
    if [ -d "$release_obj" ]; then
        manifest="$(find "$release_obj" -type f -path '*/android/manifest/AndroidManifest.xml' -printf '%T@ %p\n' | sort -rn | head -n 1 | cut -d' ' -f2- || true)"
    fi
fi

if [ -z "$manifest" ] || [ ! -f "$manifest" ]; then
    echo "Errore: manifest Android unito non trovato (eseguire prima la build Release di Recordum.Android)." >&2
    exit 1
fi

failed=0
if grep -Fq 'android.permission.INTERNET' "$manifest"; then
    echo "Errore: il manifest unito ($manifest) contiene android.permission.INTERNET." >&2
    failed=1
fi
if grep -Fq 'android.permission.CAMERA' "$manifest"; then
    echo "Errore: il manifest unito ($manifest) contiene android.permission.CAMERA." >&2
    failed=1
fi
if ! grep -Fq 'android:allowBackup="false"' "$manifest"; then
    echo "Errore: il manifest unito ($manifest) non contiene android:allowBackup=\"false\"." >&2
    failed=1
fi

if [ "$failed" -ne 0 ]; then
    exit 1
fi

echo "manifest ok: $manifest"
