# Security notes

## LiteDB 4 compatibility exception

Playnite 10 stores its library in LiteDB 4 format, so `GameCompare.Playnite` pins
LiteDB `4.1.4` for read compatibility. NuGet correctly reports advisory
`GHSA-3x49-g6rc-c284` / `CVE-2022-23535` for that version. The vulnerable path is
unsafe object-type materialization from attacker-controlled JSON via LiteDB's
mapper.

GameCompare deliberately limits this dependency to a narrow importer boundary:

- Playnite must be closed before import.
- Database files are copied to an app-owned, randomly named temporary snapshot.
- The source Playnite database is never opened for writing.
- Records are read only as raw `BsonDocument` values.
- Only explicitly named primitive fields (GUID, string, integer and date) are
  extracted; no POCO/object mapping and no JSON deserialization is used.
- Snapshot files are removed after each import.

Do not add `BsonMapper`, `JsonSerializer.Deserialize`, `ToObject`, or arbitrary
type materialization to the Playnite importer. Re-evaluate the compatibility
strategy when Playnite changes its database format or provides a supported export
API that contains the required metadata.
