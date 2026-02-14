using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Accounting.Common.Interfaces;
using Accounting.Common.Models;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;

namespace Accounting.Infrastructure.Repositories;

public class DynamoAccountingRepository : IAccountingRepository
{
    private readonly IAmazonDynamoDB _client;
    private readonly string _tableName;

    public DynamoAccountingRepository(IAmazonDynamoDB client, string tableName)
    {
        _client = client;
        _tableName = tableName;
    }

    public async Task SaveEntryAsync(AccountingEntry entry)
    {
        var item = new Dictionary<string, AttributeValue>
        {
            ["EntryId"] = new AttributeValue { S = entry.EntryId },
            ["Account"] = new AttributeValue { S = entry.Account },
            ["Amount"] = new AttributeValue { N = entry.Amount.ToString() },
            ["Date"] = new AttributeValue { S = entry.Date.ToString("o") },
            ["Description"] = new AttributeValue { S = entry.Description },
            ["BatchId"] = new AttributeValue { S = entry.BatchId }
        };

        await _client.PutItemAsync(new PutItemRequest
        {
            TableName = _tableName,
            Item = item
        });
    }

    public async Task SaveEntriesAsync(IEnumerable<AccountingEntry> entries)
    {
        // Idempotency: check if batch already exists (requires a GSI on BatchId named "BatchId-index")
        var list = entries.ToList();
        if (!list.Any()) return;

        if (await BatchExistsAsync(list.First().BatchId))
        {
            // already processed
            return;
        }

        var writeRequests = list.Select(e => new WriteRequest
        {
            PutRequest = new PutRequest
            {
                Item = new Dictionary<string, AttributeValue>
                {
                    ["EntryId"] = new AttributeValue { S = e.EntryId },
                    ["Account"] = new AttributeValue { S = e.Account },
                    ["Amount"] = new AttributeValue { N = e.Amount.ToString() },
                    ["Date"] = new AttributeValue { S = e.Date.ToString("o") },
                    ["Description"] = new AttributeValue { S = e.Description },
                    ["BatchId"] = new AttributeValue { S = e.BatchId }
                }
            }
        }).ToList();

        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                [_tableName] = writeRequests
            }
        };

        await _client.BatchWriteItemAsync(request);
    }

    private async Task<bool> BatchExistsAsync(string batchId)
    {
        if (string.IsNullOrEmpty(batchId)) return false;

        // Query GSI BatchId-index
        var req = new QueryRequest
        {
            TableName = _tableName,
            IndexName = "BatchId-index",
            KeyConditionExpression = "BatchId = :v_batch",
            ExpressionAttributeValues = new Dictionary<string, AttributeValue>
            {
                [":v_batch"] = new AttributeValue { S = batchId }
            },
            Limit = 1
        };

        var resp = await _client.QueryAsync(req);
        return resp.Count > 0;
    }
}

