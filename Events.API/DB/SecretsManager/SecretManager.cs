using System;
using Amazon;
using Amazon.SecretsManager;
using Amazon.SecretsManager.Model;

namespace Events.API.DB.SecretsManager;

public class SecretManager
{
    string secretName;

    public SecretManager(string secretName)
    {
        this.secretName = secretName;
    }

    public string Get()
    {
        //get db admindId and Password from SecretManager
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
            return "{\"username\" : \"miniadbrix\", \"password\" : \"6c.Umxf5vq-infc3_yOJEYV_btAHDo\"}";
        }

        return response.SecretString;
    }
}

