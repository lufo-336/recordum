#!/usr/bin/env bash
# Compila in Release, verifica il manifest unito, firma con la chiave del dogfooding e aggiorna Recordum sul telefono via adb.
set -euo pipefail

repo="$(cd "$(dirname "$0")/../.." && pwd)"
keystore="$HOME/.local/share/recordum-signing/recordum-dogfood.jks"

command -v adb >/dev/null 2>&1 || { echo "adb non trovato nel PATH (Android SDK platform-tools)." >&2; exit 1; }
command -v secret-tool >/dev/null 2>&1 || { echo "secret-tool non trovato nel PATH (serve libsecret-tools)." >&2; exit 1; }

# Deve esserci un solo telefono collegato e autorizzato.
devices_output="$(adb devices)"
mapfile -t connected_devices < <(printf '%s\n' "$devices_output" | awk 'NR > 1 && $2 == "device" { print $1 }')
if [ "${#connected_devices[@]}" -eq 0 ]; then
    echo "Errore: nessun dispositivo Android autorizzato trovato da 'adb devices'." >&2
    printf '%s\n' "$devices_output" >&2
    exit 1
fi
if [ "${#connected_devices[@]}" -gt 1 ]; then
    echo "Errore: trovati più dispositivi Android (${#connected_devices[@]}). Scollegarne fino a lasciarne uno solo." >&2
    printf '%s\n' "$devices_output" >&2
    exit 1
fi
device_serial="${connected_devices[0]}"

# Regola del dogfooding: un backup manuale prima di ogni installazione sul telefono.
read -r -p "Hai fatto «Backup ora» sul telefono? [s/N] " answer
case "$answer" in
    s|S|si|sì) ;;
    *) echo "Interrotto: prima fai «Backup ora» sul telefono." >&2; exit 1 ;;
esac

if [ ! -f "$keystore" ]; then
    echo "Errore: keystore non trovato in $keystore." >&2
    echo "Se è la prima installazione, lancia scripts/android/create-signing-key.sh (altrimenti recupera il keystore originale)." >&2
    exit 1
fi

sign_pass="$(secret-tool lookup service recordum-signing || true)"
if [ -z "$sign_pass" ]; then
    echo "Errore: password di firma non trovata nel portachiavi (service=recordum-signing)." >&2
    echo "Lancia scripts/android/create-signing-key.sh per creare chiave e voce nel portachiavi." >&2
    exit 1
fi

export DOTNET_ROOT="$HOME/.dotnet" PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
command -v dotnet >/dev/null 2>&1 || { echo "dotnet non trovato né in \$HOME/.dotnet né nel PATH." >&2; exit 1; }

# --no-incremental: una build incrementale dopo una modifica del solo manifest ha prodotto un APK che muore all'avvio (UnsatisfiedLinkError su n_onCreate, sonda).
RECORDUM_SIGN_PASS="$sign_pass" dotnet publish "$repo/src/Recordum.Android/Recordum.Android.csproj" \
    -c Release --no-incremental \
    -p:AndroidKeyStore=true \
    -p:AndroidSigningKeyStore="$keystore" \
    -p:AndroidSigningKeyAlias=recordum \
    -p:AndroidSigningStorePass=env:RECORDUM_SIGN_PASS \
    -p:AndroidSigningKeyPass=env:RECORDUM_SIGN_PASS
unset sign_pass

bash "$repo/scripts/android/check-manifest.sh"

apk="$(find "$repo/src/Recordum.Android/bin/Release" -type f -path '*/publish/*' -name '*-Signed.apk' -printf '%T@ %p\n' | sort -rn | head -n 1 | cut -d' ' -f2- || true)"
if [ -z "$apk" ] || [ ! -f "$apk" ]; then
    echo "Errore: APK firmato (*-Signed.apk) non trovato in una cartella publish/ sotto src/Recordum.Android/bin/Release/ (una build senza publish è firmata con la chiave di debug)." >&2
    exit 1
fi

# Mai eseguire adb uninstall: cancellerebbe i dati locali sul telefono.
set +e
install_out="$(adb -s "$device_serial" install -r "$apk" 2>&1)"
install_rc=$?
set -e
printf '%s\n' "$install_out"

if [ "$install_rc" -ne 0 ]; then
    if printf '%s' "$install_out" | grep -Fq 'INSTALL_FAILED_UPDATE_INCOMPATIBLE'; then
        echo >&2
        echo "Errore: l'APK sul telefono è firmato con un'altra chiave (INSTALL_FAILED_UPDATE_INCOMPATIBLE)." >&2
        echo "Non disinstallare l'app: disinstallare cancellerebbe tutti i dati." >&2
        echo "Usa la chiave del dogfooding giusta ($keystore)." >&2
    fi
    exit "$install_rc"
fi

echo "Installato $apk su $device_serial."
echo "Per vedere la riga d'avvio: adb logcat -s Recordum"
