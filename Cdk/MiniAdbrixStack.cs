using System.Collections.Generic;
using Amazon.CDK;
using Amazon.CDK.AWS.EC2;
using Amazon.CDK.AWS.ECR;
using Amazon.CDK.AWS.Lambda;
using Amazon.CDK.AWS.Lambda.EventSources;
using Amazon.CDK.AWS.RDS;
using Amazon.CDK.AWS.SQS;
using Constructs;
using InstanceProps = Amazon.CDK.AWS.EC2.InstanceProps;

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

            // We need this security group to add an ingress rule and allow our EC2 to query the db
            var ec2SG = new SecurityGroup(this, "EC2 to RDS Connection", new SecurityGroupProps
            {
                Vpc = vpc,
                Description = "Ec2 security group"
            });

            var dbSG = new SecurityGroup(this, "DB SecurityGroup", new SecurityGroupProps
            {

                Vpc = vpc,
                Description = "RDS security group"
            });

            dbSG.AddIngressRule(lambdaSG, Port.Tcp(5432), "allow lambda connection");
            dbSG.AddIngressRule(ec2SG, Port.Tcp(5432), "allow EC2 connection");            
            ec2SG.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(80), "allow any http connection");
            ec2SG.AddIngressRule(Peer.AnyIpv4(), Port.Tcp(443), "allow any https connection");

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
                RetentionPeriod = Duration.Days(1),
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

            var eventsApiEC2 = new Instance_(this, "eventApiEC2", new InstanceProps
            {
                Vpc = vpc,
                VpcSubnets = new SubnetSelection { SubnetType = SubnetType.PUBLIC },
                SecurityGroup = ec2SG,
                InstanceType = InstanceType.Of(InstanceClass.T3, InstanceSize.MICRO),
                MachineImage = new AmazonLinuxImage(new AmazonLinuxImageProps
                {
                    Generation = AmazonLinuxGeneration.AMAZON_LINUX_2
                }),
            });

            eventsWorkerLambda.AddEventSource(new SqsEventSource(eventQueue, new SqsEventSourceProps
            {
                BatchSize = 10,
                ReportBatchItemFailures = true,
            }));

            eventDB.GrantConnect(eventsWorkerLambda);
            eventDB.GrantConnect(eventsApiEC2);
            eventDB.Secret.GrantRead(eventsWorkerLambda);
            eventDB.Secret.GrantRead(eventsApiEC2);
            eventQueue.GrantConsumeMessages(eventsWorkerLambda);
            eventQueue.GrantSendMessages(eventsApiEC2);
            eventDeadLetterQueue.GrantSendMessages(eventsWorkerLambda);

            /*
            //Fargate Cluster
            //api 자동 배포를 위해 추가
            */
            //var eventsApiImage = new DockerImageAsset(this, "EventsApiImage", new DockerImageAssetProps
            //{
            //    Directory = "../Events.API"
            //});

            //var cluster = new Cluster(this, "MiniAdbrixCluster", new ClusterProps
            //{
            //    Vpc = vpc,
            //});

            //new ApplicationLoadBalancedFargateService(this, "MiniAdbrixFargateService", new ApplicationLoadBalancedFargateServiceProps {
            //    Cluster = cluster,
            //    Cpu = 256,
            //    DesiredCount = 1,
            //    TaskImageOptions = new ApplicationLoadBalancedTaskImageOptions
            //    {
            //        Image = ContainerImage.FromDockerImageAsset(eventsApiImage),
            //        ContainerPort = 8080,
            //    },
            //    MemoryLimitMiB = 512,
            //    PublicLoadBalancer = true,
            //})


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
