using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqliteMigrations
{
    public static class Program
    {
        public static async Task Main(string[] args)
        {
            var databaseFilePath = Path.Combine("data.db");
            var databaseFile = new FileInfo(databaseFilePath);

            databaseFile.Delete();

            var database = new SqliteDatabase(databaseFile);
            var migrationFactory = new LoggedMigrationFactory(new MigrationFactory());
            await database.MigrateAsync(migrationFactory, CancellationToken.None).ConfigureAwait(false);

            Console.ReadLine();
        }
    }
}
