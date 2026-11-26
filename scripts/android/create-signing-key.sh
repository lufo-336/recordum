#!/usr/bin/env bash
# Genera una volta il keystore di firma per il dogfooding Android di Recordum e salva la password nel portachiavi (secret-tool).
set -euo pipefail

command -v keytool >/dev/null 2>&1 || { echo "keytool non trovato nel PATH (serve un JDK installato)." >&2; exit 1; }
command -v secret-tool >/dev/null 2>&1 || { echo "secret-tool non trovato nel PATH (serve libsecret-tools)." >&2; exit 1; }

signing_dir="$HOME/.local/share/recordum-signing"
keystore="$signing_dir/recordum-dogfood.jks"

# Rifiuta sempre la rigenerazione: cambiare chiave impedisce gli aggiornamenti sul telefono senza perdita dei dati.
if [ -e "$keystore" ]; then
    echo "Interrotto: il keystore esiste già in $keystore." >&2
    echo "Perderlo o rifarlo rende impossibile aggiornare l'app sul telefono senza disinstallare e perdere i dati." >&2
    exit 1
fi

read -rs -p "Password del keystore (minimo 12 caratteri): " pass1
echo
if [ "${#pass1}" -lt 12 ]; then
    unset pass1
    echo "Interrotto: la password deve avere almeno 12 caratteri." >&2
    exit 1
fi

read -rs -p "Ripeti la password: " pass2
echo
if [ "$pass1" != "$pass2" ]; then
    unset pass1 pass2
    echo "Interrotto: le due password non coincidono." >&2
    exit 1
fi
unset pass2

mkdir -p "$signing_dir"
chmod 700 "$signing_dir"

# Prima la password nel portachiavi, poi il keystore: se il portachiavi non risponde non resta un keystore la cui
# password nessuno ha salvato (e che questo script rifiuterebbe poi di rifare). Se keytool fallisce, la voce si toglie.
printf '%s' "$pass1" | secret-tool store --label "Recordum signing" service recordum-signing

if ! RECORDUM_KEYSTORE_PASS="$pass1" keytool -genkeypair \
    -keystore "$keystore" \
    -alias recordum \
    -keyalg RSA \
    -keysize 4096 \
    -validity 10950 \
    -dname "CN=Recordum dogfood" \
    -storepass:env RECORDUM_KEYSTORE_PASS \
    -keypass:env RECORDUM_KEYSTORE_PASS; then
    unset pass1
    secret-tool clear service recordum-signing || true
    rm -f "$keystore"
    echo "Interrotto: keytool non ha creato il keystore; voce del portachiavi tolta." >&2
    exit 1
fi
chmod 600 "$keystore"
unset pass1

echo "Keystore creato in $keystore (0600) e password salvata nel portachiavi (service=recordum-signing)."
