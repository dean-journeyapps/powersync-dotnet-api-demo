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
            _dataSource = NpgsqlDataSource.Create(uri);
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

        private async Task HandlePutOperation(NpgsqlConnection connection, BatchOperation op)
        {
            if (string.IsNullOrWhiteSpace(op.Table) || string.IsNullOrWhiteSpace(op.Id))
                throw new ArgumentException("Table name and Id cannot be empty");

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

        private async Task HandlePatchOperation(NpgsqlConnection connection, BatchOperation op)
        {
            if (string.IsNullOrWhiteSpace(op.Id))
                throw new ArgumentException("Id is required for PATCH operation");

            var jsonData = JsonSerializer.Serialize(op.Data);
            var sql = $@"
                WITH data_row AS (
                    SELECT (json_populate_record(null::{op.Table}, @data::json)).*
                )
                UPDATE {op.Table}
                SET {string.Join(", ", op.Data.Keys.Select(k => $"{k} = data_row.{k}"))}
                FROM data_row
                WHERE {op.Table}.id = data_row.id";

            await using var cmd = new NpgsqlCommand(sql, connection);
            cmd.Parameters.AddWithValue("@data", jsonData);
            await cmd.ExecuteNonQueryAsync();
        }

        private async Task HandleDeleteOperation(NpgsqlConnection connection, BatchOperation op)
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