using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;
using System.Text.Json;

// Assembly serializer
[assembly: Amazon.Lambda.Core.LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace Accounting.IngestLambda;

public class Function
{
    private readonly IAmazonSQS _sqs;
    private readonly string _queueUrl;

    public Function() : this(new AmazonSQSClient(), System.Environment.GetEnvironmentVariable("SQS_URL") ?? "")
    {
    }

    public Function(IAmazonSQS sqs, string queueUrl)
    {
        _sqs = sqs;
        _queueUrl = queueUrl;
    }

    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request)
    {
        var body = request.Body ?? "";
        if (string.IsNullOrWhiteSpace(body))
        {
            return new APIGatewayProxyResponse { StatusCode = 400, Body = "Empty body" };
        }
        // generate batch id and envelope the original payload
        var batchId = System.Guid.NewGuid().ToString();
        JsonElement payloadElement;
        try
        {
            using var doc = JsonDocument.Parse(body);
            payloadElement = doc.RootElement.Clone();
        }
        catch
        {
            // if body is not a JSON object, treat as raw string
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(body));
            payloadElement = doc.RootElement.Clone();
        }

        var envelope = new
        {
            batchId = batchId,
            payload = payloadElement
        };

        var messageBody = JsonSerializer.Serialize(envelope);

        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = messageBody
        });

        var responsePayload = new { batchId = batchId, status = "queued" };

        return new APIGatewayProxyResponse
        {
            StatusCode = 202,
            Body = JsonSerializer.Serialize(responsePayload),
            Headers = new System.Collections.Generic.Dictionary<string, string> { ["Content-Type"] = "application/json" }
        };
    }
}

