#!/usr/bin/env bash
# One-time setup of the Mercado Pago sandbox for QR payments: creates a store and a POS (cash register)
# for the test seller, as required by the Orders API (config.qr.external_pos_id = POS external_id).
#
# Usage: scripts/setup-mercadopago-sandbox.sh      (reads MERCADOPAGO_ACCESS_TOKEN from the env or .env)
# Idempotent: if the POS already exists, it only prints its external_id.
# Docs: https://www.mercadopago.com.br/developers/en/docs/qr-code/create-store-and-pos
set -euo pipefail

cd "$(dirname "${BASH_SOURCE[0]}")/.."

if [ -z "${MERCADOPAGO_ACCESS_TOKEN:-}" ] && [ -f .env ]; then
  MERCADOPAGO_ACCESS_TOKEN="$(grep -E '^MERCADOPAGO_ACCESS_TOKEN=' .env | cut -d= -f2-)"
fi
: "${MERCADOPAGO_ACCESS_TOKEN:?Set MERCADOPAGO_ACCESS_TOKEN (env or .env) with the TEST access token}"

API=https://api.mercadopago.com
STORE_EXTERNAL_ID=TORQUEOSSTORE001
POS_EXTERNAL_ID=TORQUEOSPOS001

mp() { # mp <method> <path> [json] — never prints the token
  local method="$1" path="$2" body="${3:-}"
  local args=(-sS -X "$method" "$API$path" -H "Authorization: Bearer $MERCADOPAGO_ACCESS_TOKEN" -H 'Content-Type: application/json')
  [ "$method" = POST ] && args+=(-H "X-Idempotency-Key: torqueos-setup-$(date +%s)-$RANDOM")
  [ -n "$body" ] && args+=(-d "$body")
  curl "${args[@]}"
}

USER_ID="$(mp GET /users/me | jq -r '.id // empty')"
[ -n "$USER_ID" ] || { echo "Could not read /users/me — check the access token." >&2; exit 1; }
echo "Test seller user_id: $USER_ID"

EXISTING_POS="$(mp GET "/pos?external_id=$POS_EXTERNAL_ID" | jq -r '.results[0].external_id // empty')"
if [ -n "$EXISTING_POS" ]; then
  echo "POS already exists."
else
  STORE_ID="$(mp GET "/users/$USER_ID/stores/search?external_id=$STORE_EXTERNAL_ID" | jq -r '.results[0].id // empty')"
  if [ -z "$STORE_ID" ]; then
    STORE_ID="$(mp POST "/users/$USER_ID/stores" "$(jq -n --arg ext "$STORE_EXTERNAL_ID" '{
      name: "Torque-OS Oficina",
      external_id: $ext,
      business_hours: { monday: [{open: "08:00", close: "18:00"}], tuesday: [{open: "08:00", close: "18:00"}],
                        wednesday: [{open: "08:00", close: "18:00"}], thursday: [{open: "08:00", close: "18:00"}],
                        friday: [{open: "08:00", close: "18:00"}] },
      location: { street_number: "100", street_name: "Avenida Paulista", city_name: "São Paulo",
                  state_name: "São Paulo", latitude: -23.5614, longitude: -46.6559, reference: "Torque-OS sandbox" }
    }')" | tee /dev/stderr | jq -r '.id // empty')"
    echo >&2
    [ -n "$STORE_ID" ] || { echo "Store creation failed (response above)." >&2; exit 1; }
  fi
  echo "Store id: $STORE_ID"

  mp POST /v2/pos "$(jq -n --arg store "$STORE_ID" --arg ext "$POS_EXTERNAL_ID" '{
    name: "Torque-OS Caixa 1", store_id: $store, external_id: $ext, config: { qr: { operating_mode: "pdv" } }
  }')" | tee /dev/stderr | jq -e '.external_id' >/dev/null || { echo "POS creation failed (response above)." >&2; exit 1; }
  echo >&2
fi

echo
echo "Put this in .env:  MERCADOPAGO_EXTERNAL_POS_ID=$POS_EXTERNAL_ID"
