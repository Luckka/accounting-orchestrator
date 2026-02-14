using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Accounting.Common.Interfaces;
using Accounting.Infrastructure.Repositories;
using Accounting.Common.Services;
using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.S3;

[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Accounting.ProcessorLambda;

public class Function
{
    private readonly IAccountingService _service;
    private readonly IAccountingRepository _repo;
    private readonly IAmazonS3 _s3;

    public Function() : this(new AccountingService(),
                            new DynamoAccountingRepository(new Amazon.DynamoDBv2.AmazonDynamoDBClient(), Environment.GetEnvironmentVariable("DDB_TABLE") ?? "AccountingEntries"),
                            new AmazonS3Client())
    {
    }

    public Function(IAccountingService service, IAccountingRepository repo, IAmazonS3 s3)
    {
        _service = service;
        _repo = repo;
        _s3 = s3;
    }

    public async Task FunctionHandler(SQSEvent evnt)
    {
        foreach (var record in evnt.Records)
        {
            var body = record.Body ?? "";
            string payload;
            if (body.TrimStart().StartsWith("{") && body.Contains("\"s3Bucket\""))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(body);
                var bucket = doc.RootElement.GetProperty("s3Bucket").GetString();
                var key = doc.RootElement.GetProperty("s3Key").GetString();
                if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(key)) continue;
                var resp = await _s3.GetObjectAsync(bucket, key);
                using var sr = new StreamReader(resp.ResponseStream);
                payload = await sr.ReadToEndAsync();
            }
            else
            {
                payload = body;
            }

            var batchId = Guid.NewGuid().ToString();
            var entries = _service.ExplodePayload(payload, batchId).ToList();
            if (entries.Any())
            {
                await _repo.SaveEntriesAsync(entries);
            }
        }
    }
}

