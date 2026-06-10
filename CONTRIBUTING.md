# Contributing Guidelines

We follow the [Conventional Commits](https://www.conventionalcommits.org/) specification. This helps us generate automated changelogs and maintain a clean project history.

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