using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Accounting.Common.Interfaces;
using Accounting.Infrastructure.Repositories;
using Accounting.Common.Services;
using System.Text.Json;
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
            string batchId = Guid.NewGuid().ToString();

            // Try parse envelope { batchId, payload } or envelope with s3 pointer
            if (body.TrimStart().StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("batchId", out var b))
                    {
                        batchId = b.GetString() ?? batchId;

                        if (root.TryGetProperty("payload", out var p))
                        {
                            // payload may be an object or string
                            payload = p.ValueKind == JsonValueKind.String ? p.GetString() ?? "" : p.GetRawText();
                        }
                        else if (root.TryGetProperty("s3Bucket", out var _))
                        {
                            var bucket = root.GetProperty("s3Bucket").GetString();
                            var key = root.GetProperty("s3Key").GetString();
                            if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(key)) continue;
                            var resp = await _s3.GetObjectAsync(bucket, key);
                            using var sr = new StreamReader(resp.ResponseStream);
                            payload = await sr.ReadToEndAsync();
                        }
                        else
                        {
                            // no payload field, treat original body as payload
                            payload = body;
                        }
                    }
                    else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("s3Bucket", out var _s3p))
                    {
                        // original S3 notification
                        var bucket = root.GetProperty("s3Bucket").GetString();
                        var key = root.GetProperty("s3Key").GetString();
                        if (string.IsNullOrEmpty(bucket) || string.IsNullOrEmpty(key)) continue;
                        var resp = await _s3.GetObjectAsync(bucket, key);
                        using var sr = new StreamReader(resp.ResponseStream);
                        payload = await sr.ReadToEndAsync();
                    }
                    else
                    {
                        payload = body;
                    }
                }
                catch
                {
                    payload = body;
                }
            }
            else
            {
                payload = body;
            }

            var entries = _service.ExplodePayload(payload, batchId).ToList();
            if (entries.Any())
            {
                await _repo.SaveEntriesAsync(entries);
            }
        }
    }
}

