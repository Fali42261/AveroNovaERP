# Local database

The application creates `AveroNovaLocal.db` automatically during startup through
`LocalDatabaseInitializer.InitializeAsync()`. The schema is defined by
`LocalAppDbContext` and `LocalEntities`; a generated user database is intentionally
not committed to source control.

- Windows: the database is stored inside the app's `FileSystem.AppDataDirectory`.
- Android: the database is stored inside the app-private data directory.

Every installation therefore gets its own database, and Android/Windows user and
company data is not shared accidentally through the repository.
