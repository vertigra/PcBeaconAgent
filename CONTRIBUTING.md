# Contributing Guidelines

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. This helps us generate automated changelogs and maintain a clean project history.
For coding conventions (naming, logging, DI, async, JSON), see the **[Coding Guidelines](docs/coding-guidelines.md)**.

## Commit Message Format

Each commit message must follow this structure:

`<type>(<scope>): <short summary>`

### Types
- `feat`: A new feature
- `fix`: A bug fix
- `docs`: Documentation only changes
- `refactor`: A code change that neither fixes a bug nor adds a feature
- `ci`: Changes to our CI configuration files and scripts
- `test`: Adding or correcting tests

### Scopes
- `client`: Changes related to the Android MAUI application.
- `server`: Changes related to the ASP.NET Core service.
- `core`: Changes to the shared library.

### Examples
- `feat(server): implement UDP discovery protocol`
- `fix(client): resolve socket exception on disconnect`
- `ci: update release workflow path filters`

## Versioning

This project follows [Semantic Versioning](https://semver.org/) with the
rules below. The version lives in the `Version` and `AssemblyVersion`
properties of the project's `.csproj` and is injected from the Git tag by
the CI pipeline (`server.v.X.Y.Z` / `client.v.X.Y.Z`).

### Bump rules

- **MAJOR (X.0.0)** — breaking changes that require the user to take
  action or that break compatibility between an old client and a new
  server (or vice versa). Examples: removing a field from a DTO,
  changing the meaning of an existing field, changing the default port,
  removing an API endpoint.
- **MINOR (0.X.0)** — new functionality that is backward-compatible.
  Examples: adding a new DTO field (the old client ignores it), adding
  a new API endpoint, a new user-visible feature in the Android app,
  a significant internal refactor (project split, new shared library)
  that does not break the wire protocol.
- **PATCH (0.0.X)** — backward-compatible bug fixes and security
  hardening. Examples: fixing the primary-display disable bug, adding
  a `lock` to a singleton, switching `Random.Shared` to a CSPRNG,
  fixing a crash, updating a dependency for a security patch.

### How to decide

When a change touches multiple categories, bump the **highest** affected
component. For example, a commit that both fixes a bug (PATCH) and adds
a new DTO field (MINOR) results in a MINOR bump.

### Server vs client

Server and client are versioned **independently** because they ship
separately. A server change that does not affect the client (e.g. a
`lock` inside `PairingService`) bumps only the server version. A client
change that does not affect the server (e.g. a UI refactor) bumps only
the client version. A wire-protocol change (e.g. a new DTO field) bumps
**both**, because both sides must understand the new contract.

### Worked example: 1.0.0 → 1.1.0

The Tier 1 polish pass shipped as `1.1.0` for both server and client.
It included:

- `fix`: primary-display disable, last-active guard, `lock` in
  `PairingService` — would be PATCH on their own.
- `security`: CSPRNG for the PIN — would be PATCH on its own.
- `feat`: `IsPrimary` field in `DisplayDeviceDto` + "★ Primary" badge
  in the client — a new DTO field is a wire-protocol change, so MINOR.
- `refactor`: project split (Contracts, Client.Core, Server.Core) —
  a significant internal change, MINOR.

The highest bump among these is MINOR, so the release is `1.1.0`, not
`1.0.1`.