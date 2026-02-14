using System.Threading.Tasks;
using Amazon.Lambda.APIGatewayEvents;
using Amazon.Lambda.Core;
using Amazon.SQS;
using Amazon.SQS.Model;

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

        await _sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = body
        });

        return new APIGatewayProxyResponse { StatusCode = 200, Body = "Enqueued" };
    }
}

