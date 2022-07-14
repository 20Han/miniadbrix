using Amazon.Lambda.Core;
using Amazon.Lambda.SQSEvents;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Npgsql;


// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace EventsWorker.Lambda;

public class Function
{
    List<SQSBatchResponse.BatchItemFailure> batchItemFailures = new();
    /// <summary>
    /// Default constructor. This constructor is used by Lambda to construct the instance. When invoked in a Lambda environment
    /// the AWS credentials will come from the IAM role associated with the function and the AWS region will be set to the
    /// region the Lambda function is executed in.
    /// </summary>
    public Function()
    {
    }


    /// <summary>
    /// This method is called for every Lambda invocation. This method takes in an SQS event object and can be used 
    /// to respond to SQS messages.
    /// </summary>
    /// <param name="evnt"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task<SQSBatchResponse> FunctionHandler(SQSEvent evnt, ILambdaContext context)
    {
        context.Logger.LogInformation($"lambda function start");
        //get db admindId and Password from SecretManager
        string secretName = Environment.GetEnvironmentVariable("RDS_SECRET_NAME") ?? "";
        string dbEndpoint = Environment.GetEnvironmentVariable("RDS_ENDPOINT") ?? "";
        string dbName = Environment.GetEnvironmentVariable("RDS_DB_NAME") ?? "";
        string eventsTableName = Environment.GetEnvironmentVariable("RDS_EVENTS_TABLE_NAME") ?? "";
        string parametersTableName = Environment.GetEnvironmentVariable("RDS_PARAMETERS_TABLE_NAME") ?? "";
        string dbAdminId = "";
        string dbAdminPassword = "";

        context.Logger.LogInformation($"lambda environment, \nsecretName:{secretName}, \ndbEndPoint:{dbEndpoint}, \ndbName:{dbName}, \neventTableName : {eventsTableName}");
        IAmazonSecretsManager secretClient = new AmazonSecretsManagerClient(Amazon.RegionEndpoint.APNortheast3);
        GetSecretValueRequest secretRequest = new GetSecretValueRequest();
        secretRequest.SecretId = secretName;
        secretRequest.VersionStage = "AWSCURRENT";
        GetSecretValueResponse response = null;

        try
        {
            response = secretClient.GetSecretValueAsync(secretRequest).Result;
        }
        catch (Exception e)
        {
            context.Logger.LogInformation($"error while GetSceretValueAsync, errorMessage = {e.Message}");
            batchItemFailures.Concat(evnt.Records.Select(_message => new SQSBatchResponse.BatchItemFailure { ItemIdentifier = _message.MessageId }).ToList());
            return new SQSBatchResponse(batchItemFailures);
        }

        context.Logger.LogInformation("get secretstring success!");
        var secretValues = JObject.Parse(response.SecretString);

        if (secretValues!= null && secretValues["username"] != null && secretValues["password"] != null)
        {
            dbAdminId = secretValues["username"].ToString();
            dbAdminPassword = secretValues["password"].ToString();
        }
        else
        {
            context.Logger.LogInformation($"empty secretstring for {dbAdminId}");
            batchItemFailures.Concat(evnt.Records.Select(_message => new SQSBatchResponse.BatchItemFailure { ItemIdentifier = _message.MessageId }).ToList());
            return new SQSBatchResponse(batchItemFailures);
        }

        var connectionString = $"Host={dbEndpoint};Username={dbAdminId};Password={dbAdminPassword};Database={dbName}";

        using (var connection = new NpgsqlConnection(connectionString))
        {
            connection.Open();

            foreach (var message in evnt.Records)
            {
                await ProcessMessageAsync(message, context, connection, eventsTableName, parametersTableName);
            }

            connection.Close();
        }

        return new SQSBatchResponse(batchItemFailures);
    }

    private async Task ProcessMessageAsync(SQSEvent.SQSMessage message, ILambdaContext context, NpgsqlConnection npgsqlConnection, string eventTableName, string parameterTableName)
    {
        Event? _event = null;

        try
        {
            _event = JsonConvert.DeserializeObject<Event>(message.Body);
        }
        catch
        {
            context.Logger.LogInformation($"error while parsing event, message : {message.Body}");
            batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
            return;
        }

        if(_event == null)
        {
            context.Logger.LogInformation($"cannot deserialize message to event, message: {message.Body}");
            batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
            return;
        }

        if (_event.Parameters != null)
        {
            string parameterDbSql = $"insert into \"{parameterTableName}\" (\"OrderId\", \"Currency\", \"Price\") values (:OrderId, :Currency, :Price) ON CONFLICT (\"OrderId\") DO NOTHING;";

            using (NpgsqlCommand cmd = new NpgsqlCommand(parameterDbSql, npgsqlConnection))
            {

                if (cmd == null)
                {
                    context.Logger.LogInformation($"can not make parameterDbSql NpgsqlCommand, cmd is null");
                    batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
                    return;
                }

                cmd.Parameters.AddWithValue("OrderId", _event.Parameters.OrderId);
                cmd.Parameters.AddWithValue("Currency", _event.Parameters.Currency);
                cmd.Parameters.AddWithValue("Price", _event.Parameters.Price);

                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (Exception ex)
                {
                    context.Logger.LogInformation($"error while parameterDbSql execute query. error message : {ex.Message}");
                    batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
                    return;
                }
            }
        }

        string eventDbSql = _event.Parameters == null ?
            $"insert into \"{eventTableName}\" (\"EventId\", \"UserId\", \"EventName\", \"ParametersOrderId\", \"CreateDate\") values (:EventId, :UserId, :EventName, :ParametersOrderId, :CreateDate) ON CONFLICT (\"EventId\") DO NOTHING;"
            : $"insert into \"{eventTableName}\" (\"EventId\", \"UserId\", \"EventName\", \"CreateDate\") values (:EventId, :UserId, :EventName, :CreateDate) ON CONFLICT (\"EventId\") DO NOTHING;";

        using (NpgsqlCommand cmd = new NpgsqlCommand(eventDbSql, npgsqlConnection))
        {

            if (cmd == null)
            {
                context.Logger.LogInformation($"can not make eventDbSql NpgsqlCommand, cmd is null");
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
                return;
            }

            cmd.Parameters.AddWithValue("EventId", _event.EventId);
            cmd.Parameters.AddWithValue("UserId", _event.UserId);
            cmd.Parameters.AddWithValue("EventName", _event.EventName);
            if(_event.Parameters != null)
                cmd.Parameters.AddWithValue("ParametersOrderId", _event.Parameters.OrderId);
            cmd.Parameters.AddWithValue("CreateDate", _event.CreateDate);

            try
            {
                await cmd.ExecuteNonQueryAsync();
            }
            catch (Exception ex)
            {
                context.Logger.LogInformation($"error while eventDbSql execute query. error message : {ex.Message}");
                batchItemFailures.Add(new SQSBatchResponse.BatchItemFailure { ItemIdentifier = message.MessageId });
                return;
            }
        }

        await Task.CompletedTask;
    }
}