using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.AI;

namespace wealthai
{
    [ApiController]
    [Route("[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IChatClient _chatClient;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
        private readonly ChatOptions _chatOptions;

        public ChatController(
            IChatClient chatClient,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            ChatOptions chatOptions
            )
        {
            _chatClient = chatClient;
            _embeddingGenerator = embeddingGenerator;
            _chatOptions = chatOptions;
        }

        [HttpPost("chat")]
        public async Task<ActionResult<IEnumerable<string>>> Chat(string userMessage)
        {
            var messages = new List<ChatMessage>
            {
                new(ChatRole.System, """
                Your name is Richie, you are friendly wealth management assistant! 
                I can help you with all your wealth related queries.
                Just let me know what you need and I'll do my best to help!
                """),
                new(ChatRole.User, userMessage)
            };

            var response = await _chatClient.CompleteAsync(messages, _chatOptions);
            // Console.WriteLine($"Chat response: {response.Message.Text}");
            return Ok(response.Message.Text);
        }
    }
}
