using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;
using System.Xml.Linq;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.FormFlow;
using Microsoft.Bot.Builder.Luis;
using Microsoft.Bot.Builder.Luis.Models;
using Microsoft.Bot.Connector;
using LACountyLibraryBot.Models;

namespace LACountyLibraryBot.Dialogs
{
    [Serializable]
    public class PersonalInformationDialog : IDialog<object>
    {
        private int questionNumber;
        private List<string> questions = new List<string>();

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<object> result)
        {
            var activity = await result as Activity;

            questions.Add("What is your date of birth?");
            questions.Add("What is your email address?");

            questionNumber = 0;

            PromptDialog.Text(context, OnQuestionAnswered, questions[questionNumber]);
        }

        private async Task<string> TranslateText(string inputText, string language, string accessToken)
        {
            string url = "http://api.microsofttranslator.us/v2/Http.svc/Translate";
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
            string endpoint = "https://virginia.api.cognitive.microsoft.us/sts/v1.0/issueToken";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("Ocp-Apim-Subscription-Key", key);
                var response = await client.PostAsync(endpoint, null);
                var token = await response.Content.ReadAsStringAsync();
                return token;
            }
        }

        private async Task OnQuestionAnswered(IDialogContext context, IAwaitable<string> argument)
        {
            var response = await argument;
            /*
             *  var response = "What is your date of birth?";
            var retry = "Sorry, I don't understand.";
            var language = context.UserData.GetValue<string>("Language");
            var birthdate = string.Empty;
            var emailaddress = string.Empty;

            if (context.UserData.ContainsKey("Birthdate"))
                birthdate = context.UserData.GetValue<string>("Birthdate");

            if (context.UserData.ContainsKey("EmailAddress"))
                emailaddress = context.UserData.GetValue<string>("EmailAddress");
             */
            var feedback = "OK, thanks.";
            var language = context.UserData.GetValue<string>("Language");

            switch(questionNumber)
            {
                case 0:
                    context.UserData.SetValue<string>("Birthdate", response);
                    break;
                case 1:
                    context.UserData.SetValue<string>("EmailAddress", response);
                    break;
                default:
                    break;
            }

            if (language != "en" && !string.IsNullOrEmpty(language))
            {
                var accessToken = await GetAuthenticationToken(context.UserData.GetValue<string>("TextTranslationAPIKey"));
                var translation = await TranslateText(feedback, language, accessToken);
                feedback = translation;
            }

            questionNumber++;

            if(questionNumber < questions.Count)
            {
                await context.PostAsync(feedback);
                PromptDialog.Text(context, OnQuestionAnswered, questions[questionNumber]);
            }
            else
            {
                await context.PostAsync($"I have your birthdate as {context.UserData.GetValue<string>("Birthdate")} and your email address as {context.UserData.GetValue<string>("EmailAddress")}.");
                context.Done(true);
            }
        }

        private async Task<Patron> GetPatron(DateTime birthdate, string emailaddress)
        {
            var patron = new Patron();
            var sConnection = @"";

            using (var conn = new SqlConnection(sConnection))
            {
                conn.Open();

                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT * FROM Patron WHERE BirthDate = '" + birthdate.ToShortDateString() + "' AND EmailAddress = '" + emailaddress + "'";

                    using (var reader = cmd.ExecuteReader())
                    {
                        if(reader.HasRows)
                        {
                            while(reader.Read())
                            {
                                patron.ID = reader.GetString(0);
                                patron.FirstName = reader.GetString(4);
                                patron.LastName = reader.GetString(5);
                                patron.EmailAddress = emailaddress;
                                patron.BirthDate = birthdate;
                                patron.PhoneNumber = reader.GetString(3);
                                patron.StreetAddress = reader.GetString(7);
                                patron.City = reader.GetString(8);
                                patron.State = reader.GetString(9);
                                patron.ZipCode = reader.GetString(10);
                            }
                        }

                        reader.Close();
                    }
                }
                    conn.Close();
            }
                return patron;
        }

        }
}