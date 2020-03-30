using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon;
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Amazon.S3;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Titles.Api.Models;

namespace Titles.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TitlesController : ControllerBase
    {
        private static readonly RegionEndpoint Region = RegionEndpoint.USEast1;
        private static readonly AmazonDynamoDBClient DynamoDbClient = new AmazonDynamoDBClient(Region);
        private static readonly AmazonSQSClient SqsClient = new AmazonSQSClient(Region);
        private static readonly AmazonS3Client S3Client = new AmazonS3Client(Region);

        private static readonly JsonSerializerSettings JsonSerializerSettings = new JsonSerializerSettings
        {
            ContractResolver = new CamelCasePropertyNamesContractResolver(),
        };

        private const string TitlesTable = "dev-titles-table";
        private const string TitlesQueue = "https://sqs.us-east-1.amazonaws.com/714871639201/titles-synchronisation-queue";
        private const string S3Bucket = "titles-backup-bucket";
        
        [HttpPost]
        public async Task<IActionResult> CreateTitle([FromBody] Title newTitle)
        {
            return await PutTitle(newTitle);
        }

        [HttpPut]
        public async Task<IActionResult> PutTitle([FromBody] Title newTitle)
        {
            await PutTitleToDynamo(newTitle);
            await SqsClient.SendMessageAsync(TitlesQueue, JsonConvert.SerializeObject(new
            {
                EventType = "PUT",
                Payload = newTitle,
            }, JsonSerializerSettings));

            return Ok();
        }

        private static async Task PutTitleToDynamo(Title newTitle)
        {
            await DynamoDbClient.PutItemAsync(new PutItemRequest(TitlesTable, new Dictionary<string, AttributeValue>
            {
                {"isbn", new AttributeValue {S = newTitle.Isbn}},
                {"name", new AttributeValue {S = newTitle.Name}},
                {"description", new AttributeValue {S = newTitle.Description}},
            }));
        }

        [HttpDelete("{isbn}")]
        public async Task<IActionResult> DeleteTitle(string isbn)
        {
            await DynamoDbClient.DeleteItemAsync(TitlesTable, new Dictionary<string, AttributeValue>
            {
                {"isbn", new AttributeValue(isbn)}
            });

            await SqsClient.SendMessageAsync(TitlesQueue, JsonConvert.SerializeObject(new
            {
                EventType = "DELETE",
                Payload = new { isbn },
            }, JsonSerializerSettings));

            return Ok();
        }

        [HttpGet("{isbn}")]
        public async Task<IActionResult> Get(string isbn)
        {
            var response = await DynamoDbClient.GetItemAsync(TitlesTable, new Dictionary<string, AttributeValue>
            {
                { "isbn", new AttributeValue(isbn) }
            });

            if (response.IsItemSet)
            {
                return new JsonResult(new Title
                {
                    Isbn = isbn,
                    Description = response.Item["description"].S,
                    Name = response.Item["name"].S,
                });
            }

            return NotFound();
        }
        
        [HttpGet("fail-on-sqs-consume")]
        public async Task<IActionResult> FailOnConsume()
        {
            await SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest(TitlesQueue)
            {
                MaxNumberOfMessages = 1
            });
        
            return Ok();
        }

        [HttpGet("fail-on-s3-backup-bucket-access")]
        public async Task<IActionResult> FailOnBucketRead()
        {
            await S3Client.GetObjectAsync(S3Bucket, "555");
            return Ok();
        }
    }
}