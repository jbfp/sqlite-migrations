using System;
using System.Collections.Generic;

namespace SqliteMigrations
{
    public sealed class LoggedMigrationFactory : IMigrationFactory
    {
        private readonly IMigrationFactory migrationFactory;

        public LoggedMigrationFactory(IMigrationFactory migrationFactory)
        {
            this.migrationFactory = migrationFactory;
        }

        public IEnumerable<Migration> BuildMigrations(int version)
        {
            Console.WriteLine($"Running migrations from version {version}.");

            foreach (var migration in migrationFactory.BuildMigrations(version))
            {
                Console.WriteLine(migration.Sql);

                yield return migration;
            }
        }
    }

    public sealed class MigrationFactory : IMigrationFactory
    {
        public IEnumerable<Migration> BuildMigrations(int version)
        {
            if (version < 1)
            {
                yield return new Migration(@"
                    CREATE TABLE person
                    ( id INTEGER NOT NULL PRIMARY KEY
                    , name TEXT NOT NULL
                    );
                ");
            }

            if (version < 2)
            {
                yield return new Migration(@"
                    CREATE TABLE new_person
                    ( id INTEGER NOT NULL PRIMARY KEY
                    , name TEXT NOT NULL
                    , created TEXT NOT NULL DEFAULT (date('now'))
                    );

                    INSERT INTO new_person
                    SELECT id, name, date('now') AS created
                      FROM person;

                    DROP TABLE person;

                    ALTER TABLE new_person
                    RENAME TO person;
                ");
            }

            if (version < 3)
            {
                yield return new Migration(@"
                    ALTER TABLE person
                    ADD COLUMN ssn TEXT NULL;
                ");
            }

            if (version < 4)
            {
                yield return new Migration(@"
                    CREATE UNIQUE INDEX ssn_uq_idx
                    ON person (ssn)
                    WHERE (ssn IS NOT NULL);
                ");
            }
        }
    }
}
