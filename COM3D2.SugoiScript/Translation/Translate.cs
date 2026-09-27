using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;


namespace COM3D2.ScriptTranslationTool
{
    internal class Translate
    {
        private static readonly HttpClient client = new HttpClient();
        private static bool isSugoiRunning = false;
        private static bool isLLMRunning = false;

        //Sugoi stuff
        private const string sugoiAdress = "http://127.0.0.1:14366/";

        //LLM stuff
        internal static string url = "http://127.0.0.1:1234/v1/chat/completions";
        internal static string apiKey = "api_key";
        internal static string modelName = "sugoi14b";
        internal static double temp = 0.5;
        internal static double repetition_penalty = 1.1;
        internal static double top_p = 0.9;
        internal static int max_tokens = -1;


        internal static ILine ToEnglish(ILine line)
        {
            line.MachineTranslation = TranslateAsyncSugoi(line.JapanesePrep).Result;

            return line;
        }

        internal static string ToEnglish(string text)
        {
            string TldLine = "";

            if (isSugoiRunning)
                TldLine = TranslateAsyncSugoi(text).Result;
            else if (isLLMRunning)
                TldLine = TranslateAsyncLLM(text).Result;

            return TldLine;
        }
        

        /// <summary>
        /// Translate  a line using Sugoi Translator
        /// </summary>
        /// <param name="str"></param>
        /// <returns></returns>
        private static async Task<string> TranslateAsyncSugoi(string str)
        {
            string json = $"{{\"content\":\"{str}\",\"message\":\"translate sentences\"}}";

            //string json = GetJson(str);

            var response = await client.PostAsync(
                sugoiAdress,
                new StringContent(json, Encoding.UTF8, "application/json"));

            string responseString = await response.Content.ReadAsStringAsync();

            string parsedString = Regex.Unescape(responseString).Trim('"');

            return parsedString;
        }

        private static async Task<string> TranslateAsyncLLM(string str)
        {

            if (!client.DefaultRequestHeaders.Contains("Authorization"))
            {
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
            }

            //create the request in OpenAI API format
            var chatRequest = new ChatRequest
            {
                model = modelName,
                messages = new List<Message>
                {
                    new Message { role = "user", content = str }
                },
                temperature = temp,
                max_tokens = -1,
                repetition_penalty = repetition_penalty,
                top_p = top_p,
                stream = false
            };

            var json = JsonConvert.SerializeObject(chatRequest);
            string result = "";
            try
            {
                //Send and Get the response
                var response = await client.PostAsync(
                    url,
                    new StringContent(json, Encoding.UTF8, "application/json"));

                var responseText = await response.Content.ReadAsStringAsync();

                //Extract the content, that is the translated line
                result = GetResponseContent(responseText);
            }
            catch (Exception) 
            {
                //eventual handling of the exception, but I'll likely just leave it as is.
            }

            return result;
        }

        private static string GetResponseContent(string responseText)
        {
            ChatResponse parsed = JsonConvert.DeserializeObject<ChatResponse>(responseText);
            string content = "";

            if (parsed?.choices != null && parsed.choices.Count > 0)
            {
                content = parsed.choices[0].message?.content;
            }

            return content;
        }

        internal static async Task<bool> CheckTranslatorState()
        {
            try
            {
                await TranslateAsyncSugoi("テスト");
                Tools.WriteLine("\nSugoi Translator is Ready", ConsoleColor.Green);
                isSugoiRunning = true;
            }
            catch (Exception)
            {
                //Console.WriteLine(e.Message);
                //Console.WriteLine(e.InnerException);
                isSugoiRunning = false;
            }

            if (!isSugoiRunning)
            {
                try
                {
                    //Adding a timeout, since the server may first have to load the model to answer.
                    var translationTask = TranslateAsyncLLM("こんにちは、世界！");
                    var timeoutTask = Task.Delay(10000);

                    var completedTask = await Task.WhenAny(translationTask, timeoutTask);

                    if (completedTask == translationTask)
                    {
                        var result = await translationTask;

                        if (!string.IsNullOrWhiteSpace(result))
                        {
                            Tools.WriteLine($"\nLarge language Model {modelName} is Ready", ConsoleColor.Green);
                            isLLMRunning = true;
                        }
                        else
                            isLLMRunning = false;
                    }
                    else
                    {
                        Tools.WriteLine("\nLLM server can be reached but does not answer.", ConsoleColor.Red);
                        isLLMRunning = false;
                    }

                }
                catch (Exception)
                {
                    //Console.WriteLine(e.Message);
                    //Console.WriteLine(e.InnerException);
                    isLLMRunning = false;
                }
            }

            if (!isSugoiRunning && !isLLMRunning)
            {
                Tools.WriteLine("\nTranslation servers are Offline, missing sentences won't be translated", ConsoleColor.Red);
            }

            return (isLLMRunning || isSugoiRunning);
        }



        public class Message
        {
            public string role { get; set; }
            public string content { get; set; }
        }

        //Request
        public class ChatRequest
        {
            public string model { get; set; }
            public List<Message> messages { get; set; }
            public double temperature { get; set; }
            public int max_tokens { get; set; }
            public double repetition_penalty { get; set; }
            public double top_p { get; set; }
            public bool stream { get; set; }
        }

        //Response
        public class ChatResponse
        {
            public List<Choice> choices { get; set; }
            public class Choice
            {
                public int index { get; set; }
                public object logprobs { get; set; }
                public string finish_reason { get; set; }
                public Message message { get; set; }
            }
        }
    }
}