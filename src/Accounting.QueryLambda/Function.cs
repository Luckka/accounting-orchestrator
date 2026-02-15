using System;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Collections.Generic;
using Accounting.Common.Models;
using Accounting.Common.Interfaces;
using Accounting.Infrastructure.Repositories;
using Amazon.DynamoDBv2;
using Amazon.Lambda.Core;
using Amazon.Lambda.APIGatewayEvents;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Accounting.QueryLambda;

public class Function
{
    private readonly IAccountingRepository _repo;

    public Function() : this(new DynamoAccountingRepository(new AmazonDynamoDBClient(), Environment.GetEnvironmentVariable("DDB_TABLE") ?? "AccountingEntries"))
    {
    }

    // for tests / DI
    public Function(IAccountingRepository repo)
    {
        _repo = repo;
    }

    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request, ILambdaContext context)
    {
        try
        {
            if (request.PathParameters == null || !request.PathParameters.TryGetValue("batchId", out var batchId) || string.IsNullOrEmpty(batchId))
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 400,
                    Body = JsonSerializer.Serialize(new { message = "batchId is required" })
                };
            }

            var entries = (await _repo.QueryEntriesByBatchAsync(batchId)).ToList();
            if (entries == null || !entries.Any())
            {
                return new APIGatewayProxyResponse
                {
                    StatusCode = 404,
                    Body = JsonSerializer.Serialize(new { message = "Batch not found" })
                };
            }

            var payload = new
            {
                batchId = batchId,
                entries = entries.Select(e => new
                {
                    account = e.Account,
                    amount = e.Amount,
                    description = e.Description,
                    date = e.Date.ToString("yyyy-MM-dd")
                })
            };

            return new APIGatewayProxyResponse
            {
                StatusCode = 200,
                Body = JsonSerializer.Serialize(payload),
                Headers = new Dictionary<string, string> { ["Content-Type"] = "application/json" }
            };
        }
        catch (Exception ex)
        {
            context.Logger.LogError($"Error querying batch: {ex}");
            return new APIGatewayProxyResponse
            {
                StatusCode = 500,
                Body = JsonSerializer.Serialize(new { message = "Internal server error" })
            };
        }
    }
}

