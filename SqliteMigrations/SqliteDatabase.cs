using Dapper;
using Microsoft.Data.Sqlite;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace SqliteMigrations
{
    public sealed class Migration
    {
        public Migration(string sql, object parameter = null, CommandType? commandType = null, int? commandTimeout = null)
        {
            Sql = sql ?? throw new ArgumentNullException(nameof(sql));
            Parameter = parameter;
            CommandType = commandType;
            CommandTimeout = commandTimeout;
        }

        public string Sql { get; }
        public object Parameter { get; }
        public CommandType? CommandType { get; }
        public int? CommandTimeout { get; }
    }

    public interface IMigrationFactory
    {
        IEnumerable<Migration> BuildMigrations(int version);
    }

    public sealed class SqliteDatabase
    {
        private readonly Func<CancellationToken, Task<SqliteConnection>> connectionFactory;

        public SqliteDatabase(FileInfo file)
        {
            if (file == null)
            {
                throw new ArgumentNullException(nameof(file));
            }

            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = file.FullName,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ConnectionString;

            connectionFactory = async token =>
            {
                var connection = new SqliteConnection(connectionString);
                await connection.OpenAsync(token).ConfigureAwait(false);
                return connection;
            };
        }

        public async Task MigrateAsync(IMigrationFactory migrationFactory, CancellationToken cancellationToken)
        {
            if (migrationFactory == null)
            {
                throw new ArgumentNullException(nameof(migrationFactory));
            }

            cancellationToken.ThrowIfCancellationRequested();

            using (var connection = await connectionFactory(cancellationToken))
            using (var transaction = connection.BeginTransaction(IsolationLevel.Serializable))
            {
                var version = await connection.QuerySingleAsync<int>(
                    "PRAGMA user_version;",
                    transaction: transaction
                ).ConfigureAwait(false);

                var migrations = migrationFactory.BuildMigrations(version);

                foreach (var migration in migrations)
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    await connection.ExecuteAsync(
                        sql: migration.Sql,
                        param: migration.Parameter,
                        commandType: migration.CommandType,
                        commandTimeout: migration.CommandTimeout,
                        transaction: transaction
                    ).ConfigureAwait(false);

                    // Bump version; Dapper does not support parameters with PRAGMAs.
                    // SQL injection is not an issue here, since we use integers for version.
                    await connection.ExecuteAsync(
                        $"PRAGMA user_version = {++version};",
                        transaction: transaction
                    ).ConfigureAwait(false);
                }

                cancellationToken.ThrowIfCancellationRequested();
                transaction.Commit();
            }
        }
    }
}
