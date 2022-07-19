using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.ECS;
using Amazon.CDK.AWS.IAM;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.Logs;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SQS;
using Constructs;

namespace Cdk
{

    public class MiniAdbrixStack : Stack
    {
        internal MiniAdbrixStack(Construct scope, string id, IStackProps props = null) : base(scope, id, props)
        {
            //VPC
            var vpc = new Vpc(this, "miniadbrix_vpc", new VpcProps
            {
                MaxAzs = 2,
                SubnetConfiguration = new ISubnetConfiguration[]
                {
                    new SubnetConfiguration
                    {
                        CidrMask = 24,
                        SubnetType = SubnetType.PUBLIC,
                        Name = "MiniAdbrixMyPublicSubnet"
                    },
                    new SubnetConfiguration
                    {
                        CidrMask = 24,
                        SubnetType = SubnetType.PRIVATE_WITH_NAT,
                        Name = "MiniAdbrixPrivateSubnet"
                    }
                }
            });

            // We need this security group to add an ingress rule and allow our lambda to query the db
            var lambdaSG = new SecurityGroup(this, "Lambda to RDS Connection", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "lambda security group"
            });

            // We need this security group to add an ingress rule and allow our fargate to query the db
            var fargateSG = new SecurityGroup(this, "fargate security group", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "fargate security group",
                AllowAllOutbound = true,
            });

            var dbSG = new SecurityGroup(this, "DB SecurityGroup", new SecurityGroupProps
            {

                Vpc = vpc,
                Description = "RDS security group"
            });

            dbSG.AddIngressRule(lambdaSG, Port.Tcp(5432), "allow lambda connection");
            dbSG.AddIngressRule(fargateSG, Port.Tcp(5432), "allow fargate connection");
            dbSG.AddIngressRule(Peer.Ipv4("106.241.27.82/32"), Port.Tcp(5432), "allow local connection");
            fargateSG.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(8080), "allow any 80 connection");
            fargateSG.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80), "allow any http connection");
            fargateSG.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "allow any https connection");
            fargateSG.AddIngressRule(Peer.Ipv4("106.241.27.82/32"), Port.Tcp(22), "allow any https connection");

            // PostgreSql DB Instance (delete protection turned off because pattern is for learning.)
            // re-enable delete protection for a real implementation
            var eventDB = new DatabaseInstance(this, "DBInstance", new DatabaseInstanceProps
            {
                Engine = DatabaseInstanceEngine.Postgres(new PostgresInstanceEngineProps {
                    Version = PostgresEngineVersion.VER_14_2
                }),
                InstanceType = InstanceType.Of(InstanceClass.T3, InstanceSize.MICRO),
                Vpc = vpc,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                RemovalPolicy = RemovalPolicy.DESTROY,
                DeletionProtection = false,
                SecurityGroups = new[] { dbSG },
                Credentials = Credentials.FromGeneratedSecret("miniadbrix"),
                MultiAz = false,
            });

            var lambdaTimeoutSeconds = 10;
            var eventsWorkerLambda = new Function(this, "eventsWorker", new FunctionProps
            {
                Runtime = Runtime.DOTNET_6,
                Handler = "EventsWorker.Lambda::EventsWorker.Lambda.Function::FunctionHandler",
                Code = Code.FromAsset("./EventsWorker.Lambda/src/EventsWorker.Lambda/bin/Release/net6.0/publish"),
                Vpc = vpc,
                SecurityGroups = new[] { lambdaSG },
                Environment = new Dictionary<string, string> {
                    { "RDS_ENDPOINT", eventDB.DbInstanceEndpointAddress },
                    { "RDS_SECRET_NAME", eventDB.Secret.SecretName },
                    { "RDS_DB_NAME", "Events"},
                    { "RDS_EVENTS_TABLE_NAME", "Events"},
                    { "RDS_PARAMETERS_TABLE_NAME", "Parameters"},
                },
                Timeout = Duration.Seconds(lambdaTimeoutSeconds),
            });

            Queue eventDeadLetterQueue = new(this, id + "deadLetterQueue", new QueueProps
            {
                QueueName = "EventDeadLetterQueue.fifo",
                DeliveryDelay = Duration.Millis(0),
                RetentionPeriod = Duration.Minutes(30),
                ContentBasedDeduplication = true,
                Fifo = true
            });

            Queue eventQueue = new(this, id + "eventsQueue", new QueueProps {
                QueueName = "EventsQueue.fifo",
                DeliveryDelay = Duration.Millis(0),
                VisibilityTimeout = Duration.Seconds(6 * lambdaTimeoutSeconds),
                ContentBasedDeduplication = true,
                Fifo = true,
                
                DeadLetterQueue = new DeadLetterQueue {
                    MaxReceiveCount = 3,
                    Queue = eventDeadLetterQueue,
                }
            });

            var ecrRepository = new Repository(this, id + "MiniAdbrixRepository", new RepositoryProps {
                RepositoryName = "mini_adbrix_repository",
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            var fargateRole = new Role(this, "fargaterRole", new RoleProps
            {
                AssumedBy = new ServicePrincipal("ecs-tasks.amazonaws.com"),
                Description = "fargate IAM Role"
            });

            eventsWorkerLambda.AddEventSource(new SqsEventSource(eventQueue, new SqsEventSourceProps
            {
                BatchSize = 10,
                ReportBatchItemFailures = true,
            }));

            eventDB.GrantConnect(eventsWorkerLambda);
            eventDB.GrantConnect(fargateRole);
            eventDB.Secret.GrantRead(eventsWorkerLambda);
            eventDB.Secret.GrantRead(fargateRole);
            eventQueue.GrantConsumeMessages(eventsWorkerLambda);
            eventQueue.GrantSendMessages(fargateRole);
            eventDeadLetterQueue.GrantSendMessages(eventsWorkerLambda);

            /*
            //Fargate Cluster
            //api 자동 배포를 위해 추가
            //첫 배포때는 주석 처리한 후 ECR Repository에 image 배포가 끝나면 다시 주석 해제후 배포
L            */
            var cluster = new Cluster(this, "MiniAdbrixCluster", new ClusterProps
            {
                Vpc = vpc,
            });

            var fargateTaskDefinition = new FargateTaskDefinition(this, "fargateTask", new FargateTaskDefinitionProps
            {
                MemoryLimitMiB = 512,
                Cpu = 256,
                TaskRole = fargateRole,
            });

            var fargateLogGroup = new LogGroup(this, "fargateServiceLogGroup", new LogGroupProps
            {
                LogGroupName = "/ecs/fargateEventApi",
                RemovalPolicy = RemovalPolicy.DESTROY
            });

            fargateTaskDefinition.AddContainer("EventApi", new ContainerDefinitionOptions
            {
                Image = ContainerImage.FromEcrRepository(ecrRepository, "latest"),
                PortMappings = new PortMapping[] {new PortMapping { ContainerPort = 80} },
                Logging = new AwsLogDriver(new AwsLogDriverProps {
                    LogGroup = fargateLogGroup,
                    StreamPrefix = "FargateEventApiService"
                }),
                Environment = new Dictionary<string, string> {
                    { "RDS_ENDPOINT", eventDB.DbInstanceEndpointAddress },
                    { "RDS_SECRET_NAME", eventDB.Secret.SecretName },
                    { "RDS_DB_NAME", "Events"},
                }
            });

            var fargateService = new FargateService(this, "MiniAdbrixFargateService", new FargateServiceProps
            {
                Cluster = cluster,
                DesiredCount = 1,
                TaskDefinition = fargateTaskDefinition,
                AssignPublicIp = true,
                SecurityGroups = new ISecurityGroup[] { fargateSG },
            });

            /*
             init DB
             현재는 database 생성 및 table setting을 직접 sql 쿼리로 넣어줘야 하는데 번거로움이 많음
             => initDBLambda와 CustomResource를 활용하면 DB 시작과 동시에 database 및 table 생성 작업 가능할 듯
             */
            //lambda that initialize database
            //    var initDBLambda = new Function(this, "initDBLambda", new FunctionProps
            //    {
            //        Runtime = Runtime.DOTNET_6,
            //        Handler = "InitDBLambda::InitDBLambda.Function::FunctionHandler",
            //        Code = Code.FromAsset("./InitDBLambda/src/InitDBLambda/bin/Release/net6.0/publish"),
            //        Vpc = vpc,
            //        VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
            //        SecurityGroups = new[] { lambdaSG },
            //        Environment = new Dictionary<string, string> {
            //            { "RDS_ENDPOINT", eventDB.DbInstanceEndpointAddress },
            //            { "RDS_SECRET_NAME", eventDB.Secret.SecretName },
            //            { "RDS_DB_NAME", "Events"}
            //        }
            //    });

            //    eventDB.GrantConnect(initDBLambda);
            //    eventDB.Secret.GrantRead(initDBLambda);

            //    var role = new Role(this, "initDBRole", new RoleProps
            //    {
            //        AssumedBy = new ServicePrincipal("lambda.amazonaws.com"),
            //    });
            //    role.AddManagedPolicy(ManagedPolicy.FromAwsManagedPolicyName("AWSLambda_FullAcess"));

            //    var provider = new Provider(this, "dbInitProvider", new ProviderProps
            //    {
            //        OnEventHandler = initDBLambda,
            //        LogRetention = Amazon.CDK.AWS.Logs.RetentionDays.ONE_DAY,
            //        Role = role,
            //    });

            //    var dbInit = new CustomResource(this, "databaseInit", new CustomResourceProps
            //    {
            //        ServiceToken = provider.ServiceToken,
            //        Properties = new Dictionary<string, object> {
            //            { "RDS_ENDPOINT", eventDB.DbInstanceEndpointAddress },
            //            { "RDS_SECRET_NAME", eventDB.Secret.SecretName },
            //            { "RDS_DB_NAME", "Events"},
            //        },
            //    });
            //    dbInit.Node.AddDependency(eventDB);
        }
    }
}
