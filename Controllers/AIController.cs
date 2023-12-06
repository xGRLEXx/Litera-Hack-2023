using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.Net;
using System.Text;

namespace CustomerSupportAsistantWeb.Controllers
{
    public class Choice
    {
        public string text { get; set; }
    }

    public class Root
    {
        public List<Choice> choices { get; set; }
    }


    public class AIController : Controller
    {
        private readonly IConfiguration _configuration;
        private readonly int Max_tokens = 1024;
        private static readonly HttpClient client = new HttpClient();

        public AIController(IConfiguration configuration)
        {
            _configuration = configuration;        
        }

        [HttpGet]
        public async Task<IActionResult> PerformTraining(string question, string answer)
        {
            ViewBag.Answer = "";
            if (question == null || answer == null)
            {
                return View();
            }
            var example = $"Question: {question}\nAnswer: {answer}";
            var examples = new List<string> { example };

            client.DefaultRequestHeaders.Clear();
            string apiKey = _configuration["OpenAI:ApiKey"];
            client.DefaultRequestHeaders.Add("api-key", $"{apiKey}");

            var prompt = string.Join("\n", examples);

            var content = new StringContent(
                JsonConvert.SerializeObject(new { prompt = prompt, max_tokens = Max_tokens }),
                Encoding.UTF8,
                "application/json");

            string endpoint = _configuration["OpenAI:Endpoint"];
            var response = await client.PostAsync(endpoint, content);

            var result = await response.Content.ReadAsStringAsync();

            if (result != null)
            {
                ViewBag.Answer = "Added pair";
            }
            // Output the answer
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AskQuestion(string question)
        {
            if (!string.IsNullOrEmpty(question))
            {
                client.DefaultRequestHeaders.Clear();
                string apiKey = _configuration["OpenAI:ApiKey"];
                client.DefaultRequestHeaders.Add("api-key", $"{apiKey}");


                var prompt = $"[Professional tone] Question: {question}\nAnswer:";

                var content = new StringContent(
                    JsonConvert.SerializeObject(new { prompt = prompt, max_tokens = Max_tokens }),
                    Encoding.UTF8,
                    "application/json");

                string endpoint = _configuration["OpenAI:Endpoint"];

                var response = await client.PostAsync(endpoint, content);

                var result = await response.Content.ReadAsStringAsync();

                // Deserialize the result
                dynamic obj = JsonConvert.DeserializeObject(result);

                // Get the answer text
                string answer = obj.choices[0].text.Value;

                // Format the answer as needed
                string formattedAnswer = answer.Trim(); // Removes any leading/trailing whitespace
                
                // Remove any text after the first square bracket (left only Proffesional tone answer)
                int index = formattedAnswer.IndexOf('[');
                if (index > 0)
                {
                    formattedAnswer = formattedAnswer.Substring(0, index);
                }
                index = formattedAnswer.IndexOf('<');
                if (index > 0)
                {
                    formattedAnswer = formattedAnswer.Substring(0, index);
                }
                index = formattedAnswer.IndexOf('`');
                if (index > 0)
                {
                    formattedAnswer = formattedAnswer.Substring(0, index);
                }

                ViewBag.Answer = formattedAnswer;
                ViewBag.Question = question;
            }
            return View();
        }

        public IActionResult Index()
        {
            return View();
        }

        public IActionResult PerformTraining()
        {
            return View();
        }

        public IActionResult AskQuestion()
        {
            return View();
        }
    }
}
