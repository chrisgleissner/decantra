#!/usr/bin/env bash
set -euo pipefail

# Rotates the password of a PKCS12 keystore and updates .env.
#
# Reads the current password from .env (KEYSTORE_STORE_PASSWORD),
# generates a new ~30-char alphanumeric password, applies it to the
# keystore via keytool, and rewrites .env with the new password.
#
# Usage:
#   ./scripts/keystore-rotate-password.sh [keystore-path] [env-path]
#
# Defaults:
#   keystore-path = ./release.keystore
#   env-path      = ./.env

KEYSTORE_FILE="${1:-release.keystore}"
ENV_FILE="${2:-.env}"
PASSWORD_LEN=30

warn() { printf 'WARNING: %s\n' "$*" >&2; }
die()  { printf 'ERROR: %s\n' "$*" >&2; exit 1; }

# Prerequisites
command -v keytool >/dev/null 2>&1 || die "keytool not found. Install a JDK (e.g., OpenJDK)."
command -v openssl >/dev/null 2>&1 || die "openssl not found. Install OpenSSL."
[[ -f "$KEYSTORE_FILE" ]] || die "Keystore not found: $KEYSTORE_FILE"
[[ -f "$ENV_FILE" ]]      || die ".env not found: $ENV_FILE"

# Read current password from .env
OLD_PASSWORD="$(grep -E '^KEYSTORE_STORE_PASSWORD=' "$ENV_FILE" | head -1 | cut -d= -f2-)"
[[ -n "$OLD_PASSWORD" ]] || die "KEYSTORE_STORE_PASSWORD not found in $ENV_FILE"

# Verify current password works
keytool -list -keystore "$KEYSTORE_FILE" -storepass "$OLD_PASSWORD" >/dev/null 2>&1 \
  || die "Current password from $ENV_FILE does not unlock $KEYSTORE_FILE"

# Generate new high-entropy alphanumeric password
gen_password() {
  local pw=""
  while [[ "${#pw}" -lt "$PASSWORD_LEN" ]]; do
    pw+=$(openssl rand -base64 96 | tr -dc 'A-Za-z0-9' | head -c "$PASSWORD_LEN")
  done
  printf '%s' "${pw:0:$PASSWORD_LEN}"
}

NEW_PASSWORD="$(gen_password)"

# Rotate keystore store password
keytool -storepasswd \
  -keystore "$KEYSTORE_FILE" \
  -storepass "$OLD_PASSWORD" \
  -new "$NEW_PASSWORD" \
  || die "Failed to change keystore store password"

# Verify new password works
keytool -list -keystore "$KEYSTORE_FILE" -storepass "$NEW_PASSWORD" >/dev/null 2>&1 \
  || { warn "New password verification failed. Attempting rollback...";
       keytool -storepasswd -keystore "$KEYSTORE_FILE" -storepass "$NEW_PASSWORD" -new "$OLD_PASSWORD" 2>/dev/null;
       die "Rotation failed. Old password restored."; }

# Update .env (preserve all other variables, update both password fields)
umask 077
sed -i \
  -e "s|^KEYSTORE_STORE_PASSWORD=.*|KEYSTORE_STORE_PASSWORD=${NEW_PASSWORD}|" \
  -e "s|^KEYSTORE_KEY_PASSWORD=.*|KEYSTORE_KEY_PASSWORD=${NEW_PASSWORD}|" \
  "$ENV_FILE"

printf 'Keystore password rotated successfully.\n'
printf '  Keystore: %s\n' "$KEYSTORE_FILE"
printf '  Env file: %s\n' "$ENV_FILE"
