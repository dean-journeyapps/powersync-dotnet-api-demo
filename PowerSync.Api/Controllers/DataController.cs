using Microsoft.AspNetCore.Mvc;
using PowerSync.Domain.Interfaces;
using PowerSync.Domain.Records;

namespace PowerSync.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class DataController(
        IPersister persister,
        ILogger<DataController> logger) : ControllerBase
    {
        private readonly IPersister _persister = persister;
        private readonly ILogger<DataController> _logger = logger;

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] BatchRequest request)
        {
            if (request is null || request.Batch is null)
            {
                return BadRequest(new { message = "Invalid body provided" });
            }

            try
            {
                await _persister.UpdateBatchAsync(request.Batch);
                return Ok(new { message = "Batch completed" });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Request failed");
                return BadRequest(new { message = $"Request failed: {e.Message}" });
            }
        }

        [HttpPut]
        public async Task<IActionResult> Put([FromBody] BatchOperation batchOperation)
        {
            if (batchOperation?.Table is null || batchOperation.Data is null)
            {
                return BadRequest(new { message = "Invalid body provided" });
            }

            try
            {
                batchOperation.Op = PowerSync.Domain.Enums.OperationType.PUT;
                await _persister.UpdateBatchAsync([batchOperation]);
                return Ok(new { message = $"PUT completed for {batchOperation.Table} {batchOperation.Data["id"]}" });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PUT request failed");
                return BadRequest(new { message = $"Request failed: {e.Message}" });
            }
        }

        [HttpPut("checkpoint")]
        public async Task<IActionResult> CreateCheckpoint([FromBody] CheckpointRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "Invalid body provided" });
            }

            var userId = request.UserId ?? "UserID";
            var clientId = request.ClientId ?? "1";

            var checkpoint = await _persister.CreateCheckpointAsync(userId, clientId);
            return Ok(new { checkpoint });
        }

        [HttpPatch]
        public async Task<IActionResult> Patch([FromBody] BatchOperation batchOperation)
        {
            if (batchOperation?.Table == null || batchOperation.Data == null)
            {
                return BadRequest(new { message = "Invalid body provided" });
            }

            try
            {
                batchOperation.Op = PowerSync.Domain.Enums.OperationType.PATCH;
                await _persister.UpdateBatchAsync([batchOperation]);
                return Ok(new { message = $"PATCH completed for {batchOperation.Table}" });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "PATCH request failed");
                return BadRequest(new { message = $"Request failed: {e.Message}" });
            }
        }

        [HttpDelete]
        public async Task<IActionResult> Delete([FromBody] BatchOperation batchOperation)
        {
            if (batchOperation?.Table == null || batchOperation.Id == null)
            {
                return BadRequest(new { message = "Invalid body provided, expected table and data" });
            }

            try
            {
                batchOperation.Op = PowerSync.Domain.Enums.OperationType.DELETE;
                await _persister.UpdateBatchAsync([batchOperation]);
                return Ok(new { message = $"DELETE completed for {batchOperation.Table} {batchOperation.Id}" });
            }
            catch (Exception e)
            {
                _logger.LogError(e, "DELETE request failed");
                return BadRequest(new { message = $"Request failed: {e.Message}" });
            }
        }
    }
}