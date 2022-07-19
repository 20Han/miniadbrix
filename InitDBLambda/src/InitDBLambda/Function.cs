using Amazon.Lambda.Core;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;
using Newtonsoft.Json.Linq;

// Assembly attribute to enable the Lambda function's JSON input to be converted into a .NET class.
[assembly: LambdaSerializer(typeof(Amazon.Lambda.Serialization.SystemTextJson.DefaultLambdaJsonSerializer))]

namespace InitDBLambda;

public class Function
{
    
    /// <summary>
    /// A simple function that takes a string and does a ToUpper
    /// </summary>
    /// <param name="input"></param>
    /// <param name="context"></param>
    /// <returns></returns>
    public async Task FunctionHandler(string input, ILambdaContext context)
    {
        //get db admindId and Password from SecretManager
        string secretName = Environment.GetEnvironmentVariable("RDS_SECRET_NAME") ?? "";
        string dbEndpoint = Environment.GetEnvironmentVariable("RDS_ENDPOINT") ?? "";
        string dbName = Environment.GetEnvironmentVariable("RDS_DB_NAME") ?? "";
        string dbAdminId = "";
        string dbAdminPassword = "";

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
            return;
        }

        var secretValues = JObject.Parse(response.SecretString);

        if (secretValues != null)
        {
            dbAdminId = secretValues["username"].ToString();
            dbAdminPassword = secretValues["password"].ToString();
        }
        else
        {
            context.Logger.LogInformation($"empty secretstring for {dbAdminId}");
            return;
        }

        //make postgresql query
        var connectionString = $"Host={dbEndpoint};Username={dbAdminId};Password={dbAdminPassword};Database={dbName}";


    }
}
