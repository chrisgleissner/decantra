#!/usr/bin/env bash
set -euo pipefail

# Generates:
# - ./release.keystore
# - ./.env  (with KEYSTORE_STORE_PASSWORD, KEYSTORE_KEY_PASSWORD, KEYSTORE_KEY_ALIAS, KEYSTORE_STORE_FILE)
#
# Safeguards:
# - abort if .env or release.keystore already exist
#
# Password:
# - ~30 chars
# - charset: a-zA-Z0-9
#
# Usage:
#   ./generate-keystore-and-env.sh [alias]
# Example:
#   ./generate-keystore-and-env.sh decantra

ENV_FILE=".env"
KEYSTORE_FILE="release.keystore"
ALIAS="${1:-decantra}"
PASSWORD_LEN=30

warn() { printf 'WARNING: %s\n' "$*" >&2; }
die() { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# Safeguards
if [[ -e "$ENV_FILE" ]]; then
  warn "Found existing $ENV_FILE in current directory. Aborting to avoid overwriting."
  exit 1
fi
if [[ -e "$KEYSTORE_FILE" ]]; then
  warn "Found existing $KEYSTORE_FILE in current directory. Aborting to avoid overwriting."
  exit 1
fi

# Dependencies
command -v keytool >/dev/null 2>&1 || die "keytool not found. Install a JDK (e.g., OpenJDK) so keytool is available."
command -v openssl >/dev/null 2>&1 || die "openssl not found. Install OpenSSL."

# Generate high-entropy alnum password.
# We generate more bytes than needed, filter to [A-Za-z0-9], then cut to length.
gen_password() {
  local pw=""
  while [[ "${#pw}" -lt "$PASSWORD_LEN" ]]; do
    # 64 bytes per iteration, filter, append
    pw+=$(openssl rand -base64 96 | tr -dc 'A-Za-z0-9' | head -c "$PASSWORD_LEN")
  done
  printf '%s' "${pw:0:$PASSWORD_LEN}"
}

STORE_PASSWORD="$(gen_password)"
KEY_PASSWORD="${STORE_PASSWORD}"

# Create keystore (non-interactive). Distinguished name is arbitrary but required.
# RSA 4096 and long validity suitable for release signing.
keytool -genkeypair \
  -v \
  -keystore "$KEYSTORE_FILE" \
  -storepass "$STORE_PASSWORD" \
  -alias "$ALIAS" \
  -keypass "$KEY_PASSWORD" \
  -keyalg RSA \
  -keysize 4096 \
  -validity 10000 \
  -dname "CN=${ALIAS}, OU=Mobile, O=Decantra, L=London, S=London, C=GB" \
  >/dev/null

# Write .env with strict permissions
umask 077
cat > "$ENV_FILE" <<EOF
KEYSTORE_STORE_PASSWORD=${STORE_PASSWORD}
KEYSTORE_KEY_PASSWORD=${KEY_PASSWORD}
KEYSTORE_KEY_ALIAS=${ALIAS}
KEYSTORE_STORE_FILE=${KEYSTORE_FILE}
EOF

printf 'Created %s and %s in %s\n' "$ENV_FILE" "$KEYSTORE_FILE" "$(pwd)"
printf 'Keystore alias: %s\n' "$ALIAS"
printf 'NOTE: Store the .env contents securely. Do NOT commit .env or release.keystore.\n'

