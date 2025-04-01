using System.Text.Json;
using Npgsql;
using PowerSync.Domain.Enums;
using PowerSync.Domain.Interfaces;
using PowerSync.Domain.Records;

namespace PowerSync.Infrastructure.Persistence.Postgres
{
    public class PostgresPersister : IPersister
    {
        private readonly NpgsqlDataSource _dataSource;

        public PostgresPersister(string uri)
        {
            Console.WriteLine("Using Postgres Persister");

            try
            {
                // Check if the string is a URI format
                if (uri.StartsWith("postgres://") || uri.StartsWith("postgresql://"))
                {
                    // Manually parse the URI and build a connection string
                    var connString = ConvertUriToConnectionString(uri);
                    _dataSource = NpgsqlDataSource.Create(connString);
                }
                else
                {
                    // Assume it's already in the correct format
                    _dataSource = NpgsqlDataSource.Create(uri);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Connection string error: {ex.Message}");
                throw;
            }
        }

        private static string ConvertUriToConnectionString(string uri)
        {
            try
            {
                Uri pgUri = new(uri);

                // Extract components
                string server = pgUri.Host;
                int port = pgUri.Port > 0 ? pgUri.Port : 5432; // Default to 5432 if not specified
                string database = pgUri.AbsolutePath.TrimStart('/');

                // Parse userinfo (username:password)
                string username = string.Empty;
                string password = string.Empty;

                if (!string.IsNullOrEmpty(pgUri.UserInfo))
                {
                    string[] userInfoParts = pgUri.UserInfo.Split(':');
                    username = userInfoParts[0];
                    password = userInfoParts.Length > 1 ? userInfoParts[1] : string.Empty;
                }

                // Build connection string
                return $"Host={server};Port={port};Database={database};Username={username};Password={password};";
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to parse URI: {ex.Message}");
                throw new ArgumentException($"Invalid PostgreSQL URI format: {uri}", ex);
            }
        }

        public async Task UpdateBatchAsync(List<BatchOperation> batch)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            try
            {
                foreach (var op in batch)
                {
                    switch (op.Op)
                    {
                        case OperationType.PUT:
                            await HandlePutOperation(connection, op);
                            break;
                        case OperationType.PATCH:
                            await HandlePatchOperation(connection, op);
                            break;
                        case OperationType.DELETE:
                            await HandleDeleteOperation(connection, op);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(op.Op), $"Unknown operation type: {op.Op}");
                    }
                }
                await transaction.CommitAsync();
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"Error: {ex.Message}");
                throw;
            }
        }

        private static async Task HandlePutOperation(NpgsqlConnection connection, BatchOperation op)
        {
            if (string.IsNullOrWhiteSpace(op.Table) || string.IsNullOrWhiteSpace(op.Id))
                throw new ArgumentException("Table name and Id cannot be empty");

            if (op.Data is null || op.Data.Count == 0)
                throw new ArgumentException("Data is required for PUT operation");

            var dataDict = new Dictionary<string, object>(op.Data)
            {
                ["id"] = op.Id
            };

            var jsonData = JsonSerializer.Serialize(dataDict);
            var sql = $@"
                WITH data_row AS (
                    SELECT (json_populate_record(null::{op.Table}, @data::json)).*
                )
                INSERT INTO {op.Table} SELECT * FROM data_row
                ON CONFLICT(id) DO UPDATE SET {string.Join(", ", dataDict.Keys.Where(k => k != "id").Select(k => $"{k} = EXCLUDED.{k}"))}";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@data", jsonData);
            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task HandlePatchOperation(NpgsqlConnection connection, BatchOperation op)
        {
            if (string.IsNullOrWhiteSpace(op.Id))
                throw new ArgumentException("Id is required for PATCH operation");

            if (op.Data is null || op.Data.Count == 0)
                throw new ArgumentException("Data is required for PATCH operation");

            // Exclude 'id' from update columns
            var updateColumns = op.Data
                .Where(kvp => !kvp.Key.Equals("id", StringComparison.CurrentCultureIgnoreCase))
                .ToList();

            // Create update clauses dynamically
            var updateClauses = updateColumns
                .Select(kvp => $"{kvp.Key} = data_row.{kvp.Key}")
                .ToList();

            // If no updatable columns, throw an exception
            if (!updateClauses.Any())
                throw new ArgumentException("No updatable columns provided");

            // Prepare the data object with ID
            var dataWithId = new Dictionary<string, object>(op.Data);
            if (!dataWithId.ContainsKey("id"))
            {
                dataWithId["id"] = op.Id;
            }

            var statement = $@"
                WITH data_row AS (
                    SELECT (json_populate_record(null::{op.Table}, @data::json)).*
                )
                UPDATE {op.Table}
                SET {string.Join(", ", updateClauses)}
                FROM data_row
                WHERE {op.Table}.id = data_row.id";

            await using var cmd = new NpgsqlCommand(statement, connection);
            cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(dataWithId));

            await cmd.ExecuteNonQueryAsync();
        }

        private static async Task HandleDeleteOperation(NpgsqlConnection connection, BatchOperation op)
        {
            if (string.IsNullOrWhiteSpace(op.Id))
                throw new ArgumentException("Id is required for DELETE operation");

            var sql = $@"
                WITH data_row AS (
                    SELECT (json_populate_record(null::{op.Table}, @data::json)).*
                )
                DELETE FROM {op.Table}
                USING data_row
                WHERE {op.Table}.id = data_row.id";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@data", JsonSerializer.Serialize(new { id = op.Id }));
            await cmd.ExecuteNonQueryAsync();
        }

        public async Task<long> CreateCheckpointAsync(string userId, string clientId)
        {
            await using var connection = await _dataSource.OpenConnectionAsync();
            await using var cmd = new NpgsqlCommand(@"
            INSERT INTO checkpoints(user_id, client_id, checkpoint)
            VALUES (@userId, @clientId, '1')
            ON CONFLICT (user_id, client_id)
            DO UPDATE SET checkpoint = checkpoints.checkpoint + 1
            RETURNING checkpoint", connection);

            cmd.Parameters.AddWithValue("@userId", userId);
            cmd.Parameters.AddWithValue("@clientId", clientId);

            var result = await cmd.ExecuteScalarAsync();
            return Convert.ToInt64(result);
        }
    }
}