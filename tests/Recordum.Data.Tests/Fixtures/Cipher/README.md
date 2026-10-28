# Cipher fixtures (wave D9)

Databases made with the SQLCipher 4.5.2 native bundle (`SQLitePCLRaw.bundle_e_sqlcipher` 2.1.11), before the swap to
SQLite3 Multiple Ciphers. They prove that a user's existing database opens unchanged after the upgrade.

- Key: raw 32 bytes `0x00..0x1F` (`CipherFixture.Key`), applied as `PRAGMA key = "x'...'"`.
- Content: real migrations (schema 2.19) plus `RecbakReferenceSeed`.
- `sqlcipher-4.5.2-v2.19.db`: clean, closed database.
- `sqlcipher-4.5.2-hot-journal.db` + `-journal`: a transaction left open (deleted completions, renamed a habit to `HALF`,
  created a filler table) whose pages spilled to the `.db`, copied while open: a crash mid-transaction.

**Never regenerate after the provider swap**: the output would be a fixture of the new bundle and prove nothing. Generator
(only against the old bundle): `RECORDUM_GENERATE_CIPHER_FIXTURE=<folder> dotnet test tests/Recordum.Data.Tests --filter "Category=FixtureGenerator&FullyQualifiedName~CipherFixtureGenerator"`.
