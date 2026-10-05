# Key Remapping Service

A small web service that stores and serves key remappings for the **Apex Pro Gen 3** keyboard.
Keys are identified by their USB HID Usage Code.

## Quick start

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download). Nothing else to install: SQLite is embedded
and every package used is free.

```bash
dotnet run --project KeyRemappingService   # http://localhost:5259
dotnet test
```

The SQLite file `keymaps.db` is created on first start and survives restarts. The connection string is in
`appsettings.json` (`ConnectionStrings:KeyMappings`).

## API

**A full Swagger documentation is generated at http://localhost:5259/swagger/index.html on start.**

Keyboard names are case-insensitive and trimmed. Encode spaces in the URL (`%20`).

### `PATCH /keyboards/{name}/mappings`: assign mappings

```json
{ "mappings": [ { "source": 57, "target": 224 }, { "source": 4, "target": 29 } ] }
```

Codes are decimal HID codes (57 = `0x39` Caps Lock, 224 = `0xE0` Left Ctrl). Both fields are required.

- Mappings are **merged** with the existing configuration; keys not mentioned are left untouched.
- Mapping a key to itself (`"source": 4, "target": 4`) **resets** it to its default behaviour.
- **All or nothing**: if one mapping is invalid, nothing is saved.

| Status | Meaning |
|---|---|
| `204` | Applied |
| `400` | Unknown key code, duplicate source, empty list, malformed JSON, missing field |
| `404` | Unknown keyboard |
| `415` | `Content-Type` is not JSON |

Validation errors are returned together: `{ "errors": [ "Target key 0xFF does not exist on Apex Pro Gen 3" ] }`.
Malformed JSON and wrong `Content-Type` are rejected by the framework before reaching the service.

### `GET /keyboards/{name}/mappings`: read all mappings

Returns **every** key of the keyboard (`200`), remapped or not; an unmapped key points to itself.
Each key has its code and its name for easier reading. `404` if the keyboard is unknown.

```json
[
  { "source": { "code": 4, "name": "A" }, "target": { "code": 29, "name": "Z" } },
  { "source": { "code": 5, "name": "B" }, "target": { "code": 5,  "name": "B" } }
]
```

### Example

```bash
# Remap Caps Lock to Left Ctrl, and A to Z
curl -i -X PATCH "http://localhost:5259/keyboards/Apex%20Pro%20Gen%203/mappings" \
  -H "Content-Type: application/json" \
  -d '{"mappings":[{"source":57,"target":224},{"source":4,"target":29}]}'

# Read everything back
curl -i "http://localhost:5259/keyboards/Apex%20Pro%20Gen%203/mappings"

# Reset A to its default
curl -i -X PATCH "http://localhost:5259/keyboards/Apex%20Pro%20Gen%203/mappings" \
  -H "Content-Type: application/json" \
  -d '{"mappings":[{"source":4,"target":4}]}'
```

## Project structure

```
KeyRemappingService.sln
KeyRemappingService/                     Web project (Minimal API)
├── Program.cs                           Wiring and startup
├── appsettings.json                     Connection string, log levels
├── Endpoints/                           Routes, DTOs, Result -> HTTP status
├── Services/KeyMappingService.cs        Validation and business rules
├── KeyboardCatalog/                     IKeyboardCatalog + loader of the JSON keyboard files
├── Keyboards/apex-pro-gen3.json         The 92 keys of the keyboard (code + name)
├── Data/                                IKeyMappingRepository, EF Core implementation, DbContext, entity
├── Domain/                              KeyMapping, KeyboardDefinition
└── Utils/                               Shared helpers
KeyRemappingService.Tests/               xUnit tests
```

```
HTTP -> Endpoints -> KeyMappingService -> IKeyboardCatalog      -> JSON files (read only)
                                       -> IKeyMappingRepository -> SQLite (EF Core)
```

## Design decisions

- **EntityFramework driven Repository.** I decided to use an ORM for the database integrations to simplify data access, improve code maintainability, and ensure type safety.
- **Two sources of truth.** JSON files describe what *exists* (keyboards and keys); SQLite stores what the user
  *chose*. Only remapped keys are stored, and `GET` rebuilds the full list from the catalog.
- **Merge with `PATCH`.** The test does not say if a new request replaces or completes the configuration.
  I chose to merge. Replacing would be a `PUT` plus a "delete then insert" in the repository.
- **Codes in, codes and names out.** A code is an unambiguous identifier for input; names make the response readable.
- **Hex in the keyboard files, decimal in the API.** Hex strings (`"0x04"`) in JSON match the HID specification;
  they are converted once at load time to ease stockage and readings.
- **Business errors vs. exceptions.** Expected errors go through a `Result` mapped to `400`/`404` by the endpoints.
  The service does not know about HTTP.
- **Fail fast.** Keyboard files are validated at startup (duplicate or out-of-range codes, invalid hex); an invalid
  file stops the application.
- **Interfaces at the boundaries** (catalog, repository) keep the service testable with fakes.

## Tests

Service rules (validation, merge, reset, all-or-nothing), repository (persistence across instances, isolation per
keyboard, upsert), and catalog loading (valid and invalid files). Each test uses its own temporary SQLite file.
The HTTP layer (malformed JSON, status codes) was checked by hand with `curl` and an API Client (`Bruno`), not automated.

Tests are automatically run on push via Github Actions.

## Limitations

- **Storage failures** (locked or unreachable database) are not handled specifically: the exception propagates
  and ASP.NET Core answers `500`. Writes are transactional, so no half-applied request is possible.
- **One keyboard** is defined; the code is generic but was not exercised with a second one.
- **No cleanup** if a key is removed from a keyboard file while the database still references it.
- The database is created with `EnsureCreated` (no migrations), and there is no authentication.

## Possible improvements

- Map storage failures to `503`.
- HTTP integration tests with `WebApplicationFactory`.
- A `PUT` endpoint to replace a whole configuration; several keyboards and per-user profiles.
- A desktop companion app that reads the configuration from this API and applies it to the connected keyboard (out of scope here).