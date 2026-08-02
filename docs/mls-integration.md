# SolarWin E2EE / MLS integration

Verified upstream revisions on 2026-08-02:

- DysonNetwork `542451248991f3f66a0ca64cfabf776de595a085`
- Solian `e3f53289583ae17e5242fa45595c50c1fc7863d7`
- Solian `openmls` package `2.0.0`, archive SHA-256
  `CCE5A94372F35040CEA7647732B6ED35EAB8EAA6309020445460AC5E7DC378D5`
- OpenMLS tag `openmls-v0.8.1`, RFC 9420, ciphersuite
  `MLS_128_DHKEMX25519_AES128GCM_SHA256_Ed25519`

## Implemented path

- Per-account SQLCipher OpenMLS database, with the 32-byte database key and
  Ed25519 signer protected by Windows DPAPI for the current user.
- Stable persisted device ID on every Padlock MLS request.
- KeyPackage inventory, bootstrap ownership, Welcome and Commit fanout,
  GroupInfo/ratchet-tree publication, per-device membership, ordered pending
  envelope processing and acknowledge-after-success.
- `chat.mls.v2` application ciphertext in the ordinary Messager send endpoint.
  Plaintext message content is never included in the HTTP body for MLS rooms.
- Incoming REST, sync and WebSocket messages are processed by OpenMLS before
  display and persistence. Failed or unavailable decryption never falls back
  to plaintext sending.
- Manual group reset/rebootstrap recovery. DysonNetwork does not permit an
  encrypted room to be switched back to plaintext, so the UI exposes a
  disabled close control instead of issuing a false PATCH.

## Platform and media boundary

The shipped native bridge is Windows x64. Other architectures fail closed and
must not enable MLS until an equivalent native binary is produced and tested.

Solian encrypts Drive attachments using its separate `file.aesgcm.v1` envelope.
SolarWin's current DysonFS C# upload/download abstraction has no corresponding
encrypted-media pipeline, so attachment upload, voice, stickers, placeholders
and redirects are intentionally blocked in MLS rooms. Text and attachment IDs
already present in a message remain MLS-authenticated; raw new media is never
silently uploaded as plaintext.
