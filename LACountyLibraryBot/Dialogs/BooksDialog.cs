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
    public class BooksDialog : IDialog<object>
    {
        
        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var activity = await result;
            var patronId = string.Empty;

            if (context.UserData.ContainsKey("PatronID"))
                patronId = context.UserData.GetValue<string>("PatronID");

            if (string.IsNullOrEmpty(patronId))
            {
                var response = "Alright. I can help with that, but I'll need some information from you to get started.";
                var language = context.UserData.GetValue<string>("Language");
                if (language != "en" && !string.IsNullOrEmpty(language))
                {
                    var accessToken = await GetAuthenticationToken(context.UserData.GetValue<string>("TextTranslationAPIKey"));
                    var translation = await TranslateText(response, language, accessToken);
                    response = translation;
                }
                await context.PostAsync(response);
                await context.Forward(new PersonalInformationDialog(), ResumeAfterPersonalInformationDialog, activity, CancellationToken.None);
            }
            else
            {
                var books = GetCheckedOutBooksByPatron(patronId);
                var bookCollection = books.Result;
                //await context.PostAsync($"Books checked out: {books.Length}");
            }

            context.Wait(this.MessageReceivedAsync);
        }

        private async Task ResumeAfterPersonalInformationDialog(IDialogContext context, IAwaitable<object> result)
        {
            var resultFromPersonalInformation = await result;

            context.Wait(MessageReceivedAsync);
        }

        private async Task<bool> RenewBook(Book book)
        {
            var sConnection = @"";

            using (var conn = new SqlConnection(sConnection))
            {
                conn.Open();

                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "UPDATE PatronBookXSec SET DueBack = '' WHERE BookID = '' AND PatronID = ''";

                    //cmd.ExecuteNonQuery();
                }
                conn.Close();
            }

            return true;
        }

        private async Task<List<Book>> GetCheckedOutBooksByPatron(string patronId)
        {
            var books = new List<Book>();
            var sConnection = @"";

            using (var conn = new SqlConnection(sConnection))
            {
                conn.Open();

                using (var cmd = new SqlCommand())
                {
                    cmd.Connection = conn;
                    cmd.CommandType = CommandType.Text;
                    cmd.CommandText = "SELECT Book.ID, Book.Author, Book.Title, PBX.CheckedOutDate, PBX.DueBack, PBX.Fee FROM PatronBookXSec AS PBX INNER JOIN Book ON Book.ID = PBX.BookID WHERE PBX.PatronID = '" + patronId + "'";

                    using (var reader = cmd.ExecuteReader())
                    {
                        if (reader.HasRows)
                        {
                            while (reader.Read())
                            {
                                books.Add(new Book(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetDateTime(3), reader.GetDateTime(4), reader.GetDouble(5)));
                            }
                        }

                        reader.Close();
                    }
                }
                    conn.Close();
            }
                return books;
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