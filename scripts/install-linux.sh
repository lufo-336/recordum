#!/usr/bin/env bash
# Installa (o aggiorna) Recordum per l'uso quotidiano su Linux: build Release di `main`, lanciatore e voce .desktop.
# I dati veri stanno solo nella build di main (docs/DOGFOODING.md): per questo si compila un ref, mai la copia di lavoro.
#
#   scripts/install-linux.sh                 # installa main in ~/.local
#   scripts/install-linux.sh --ref <ref>     # un altro ref (prove)
#   scripts/install-linux.sh --prefix <dir>  # un'altra radice al posto di ~/.local (prove)
#   scripts/install-linux.sh --yes           # salta la domanda sul backup
set -euo pipefail

ref=main
prefix="$HOME/.local"
assume_yes=0
while [ $# -gt 0 ]; do
    case "$1" in
        --ref) ref="$2"; shift 2 ;;
        --prefix) prefix="$2"; shift 2 ;;
        --yes) assume_yes=1; shift ;;
        *) echo "argomento sconosciuto: $1" >&2; exit 2 ;;
    esac
done

repo="$(git -C "$(dirname "$0")" rev-parse --show-toplevel)"
commit="$(git -C "$repo" rev-parse --verify "$ref^{commit}")"

dotnet_bin="$(command -v dotnet || true)"
[ -n "$dotnet_bin" ] || dotnet_bin="$HOME/.dotnet/dotnet"
[ -x "$dotnet_bin" ] || { echo "dotnet non trovato (né nel PATH né in ~/.dotnet)" >&2; exit 1; }
dotnet_bin="$(readlink -f "$dotnet_bin")"
dotnet_root="$(dirname "$dotnet_bin")"

app_home="$prefix/share/recordum"
app="$app_home/app"
cache="${XDG_CACHE_HOME:-$HOME/.cache}/recordum-build"
src="$cache/src"

# Un journal rimasto da un crash a metà scrittura lo annulla solo la build che l'ha scritto: SQLite3MC (D9) non riconosce
# quello del bundle SQLCipher di prima, lo scarta e lascia il database a metà (trappola misurata, docs/phase-4-issues.md).
# La build installata usa la cartella di default; RECORDUM_DATA_DIR, se c'è, è quella di sviluppo: si guardano tutte e due.
for dir in "${XDG_DATA_HOME:-$HOME/.local/share}/Recordum" ${RECORDUM_DATA_DIR:+"$RECORDUM_DATA_DIR"}; do
    if [ -s "$dir/recordum.db-journal" ]; then
        echo "Interrotto: c'è $dir/recordum.db-journal (Recordum è aperto, o si è chiuso a metà di una scrittura)." >&2
        echo "Aprire e chiudere la build che usa quella cartella, che lo annulla, poi rilanciare." >&2
        exit 1
    fi
done

# Regola del dogfooding: un backup manuale prima di ogni build nuova.
if [ -d "$app" ] && [ "$assume_yes" -eq 0 ]; then
    read -r -p "Hai fatto un backup manuale (Impostazioni → Dati e backup → Backup ora)? [s/N] " answer
    case "$answer" in s|S|si|sì|y|Y) ;; *) echo "Interrotto: prima il backup."; exit 1 ;; esac
fi

# Sorgente pulito del ref scelto, fuori dalla copia di lavoro.
rm -rf "$src"
mkdir -p "$src"
git -C "$repo" archive "$commit" | tar -x -C "$src"

rm -rf "$app.new"
DOTNET_ROOT="$dotnet_root" "$dotnet_bin" publish "$src/src/Recordum.Desktop/Recordum.Desktop.csproj" \
    -c Release -r linux-x64 --self-contained false -o "$app.new" -nologo -v quiet -clp:ErrorsOnly

cat > "$app.new/recordum" <<EOF
#!/bin/sh
export DOTNET_ROOT="$dotnet_root"
exec "$dotnet_bin" "$app/Recordum.Desktop.dll" "\$@"
EOF
chmod 755 "$app.new/recordum"
echo "$commit" > "$app.new/COMMIT"

# Scambio: la versione precedente resta in app.prev per tornare indietro con un mv.
rm -rf "$app.prev"
[ -d "$app" ] && mv "$app" "$app.prev"
mv "$app.new" "$app"
rm -rf "$src"

install -D -m 644 "$repo/scripts/linux/recordum.svg" "$prefix/share/icons/hicolor/scalable/apps/recordum.svg"
install -d "$prefix/share/applications"
cat > "$prefix/share/applications/recordum.desktop" <<EOF
[Desktop Entry]
Type=Application
Name=Recordum
Comment=Habits, diary and what they say about you — offline and encrypted
Comment[it]=Abitudini, diario e quello che dicono di te — offline e cifrato
Exec=$app/recordum
Icon=recordum
Terminal=false
Categories=Utility;
StartupWMClass=recordum
EOF
command -v update-desktop-database >/dev/null && update-desktop-database -q "$prefix/share/applications" || true

echo "Recordum $(git -C "$repo" rev-parse --short "$commit") installato in $app"
if [ -d "$app.prev" ]; then echo "Versione precedente in $app.prev (per tornare indietro: rm -rf app && mv app.prev app)"; fi
