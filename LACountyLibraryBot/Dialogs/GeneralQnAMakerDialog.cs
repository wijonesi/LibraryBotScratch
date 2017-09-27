using System;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml.Linq;

using Microsoft.Bot.Builder.Dialogs;
using QnAMakerDialog;

namespace LACountyLibraryBot.Dialogs
{
    [Serializable]
    // subscriptionKey , knowledgebaseId
    [QnAMakerService("", "")]
    public class GeneralQnAMakerDialog : QnAMakerDialog<object>
    {
        public override async Task NoMatchHandler(IDialogContext context, string originalQueryText)
        {
            var response = $"Sorry, I could not find an answer for '{originalQueryText}'.";
            var language = context.UserData.GetValue<string>("Language");
            if (language != "en")
            {
                var accessToken = await GetAuthenticationToken(context.UserData.GetValue<string>("TextTranslationAPIKey"));
                var translation = await TranslateText(response, language, accessToken);
                response = translation;
            }
            await context.PostAsync(response);
            context.Done(false);
        }

        public override async Task DefaultMatchHandler(IDialogContext context, string originalQueryText, QnAMakerResult result)
        {
            var response = result.Answer;
            var language = context.UserData.GetValue<string>("Language");
            if (language != "en")
            {
                var accessToken = await GetAuthenticationToken(context.UserData.GetValue<string>("TextTranslationAPIKey"));
                var translation = await TranslateText(response, language, accessToken);
                response = translation;
            }
            await context.PostAsync(response);
            context.Done(true);
        }

        private async Task<string> TranslateText(string inputText, string language, string accessToken)
        {
            string url = "http://api.microsofttranslator.com/v2/Http.svc/Translate";
            string query = $"?text={System.Net.WebUtility.UrlEncode(inputText)}&to={language}&contentType=text/plain";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url + query);
                var result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return result;
                }

                var translatedText = XElement.Parse(result).Value;
                return translatedText;
            }
        }

        private async Task<string> GetAuthenticationToken(string key)
        {
            string endpoint = "https://api.cognitive.microsoft.com/sts/v1.0/issueToken";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                var response = await client.PostAsync(endpoint, null);
                var token = await response.Content.ReadAsStringAsync();
                return token;
            }
        }
    }
}