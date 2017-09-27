using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.Bot.Builder.Dialogs;
using Microsoft.Bot.Builder.Location;
using Microsoft.Bot.Connector;

namespace LACountyLibraryBot.Dialogs
{
    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private int questionNumber;
        private List<string> questions = new List<string>();

        private enum EventOptions
        {
            Bookmobile,
            Immigration,
            Passports,
            StoryTime
        }

        public Task StartAsync(IDialogContext context)
        {
            context.Wait(MessageReceivedAsync);

            return Task.CompletedTask;
        }

        public virtual async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            // This is the Text API Key
            const string key = "";

            var message = await result;
            
            // Translation testing //
            Task.Run(async () =>
            {
                string originalMessage = message.Text;

                var accessToken = await GetAuthenticationToken(key);
                var language = await DetectLanguage(message.Text, accessToken);
                var isoLanguage = CultureInfo.GetCultureInfoByIetfLanguageTag(language).DisplayName;

                context.UserData.SetValue<string>("Language", language);
                context.UserData.SetValue<string>("LanguageDisplayName", isoLanguage);
                context.UserData.SetValue<string>("TextTranslationAPIKey", key);

                if (language != "en")
                {
                    // translate the message to english before passing it along to the next dialog
                    var translation = await TranslateText(message.Text, "en", accessToken);
                    message.Text = translation;
                }

                if (message.Text.ToLower().Contains("renew"))
                {
                    PromptDialog.Confirm(context, ResumeAfterRenewPrompt, "Do you want to renew material you have checked out?");
                }
                else if(message.Text.ToLower().Contains("events"))
                {
                    
                }
                else if(message.Text.ToLower().Contains("nearest library"))
                {
                    // Bing API Key
                    var bingAPIKey = "";
                    var options = LocationOptions.UseNativeControl | LocationOptions.ReverseGeocode | LocationOptions.SkipFinalConfirmation;
                    var requiredFields = LocationRequiredFields.StreetAddress | LocationRequiredFields.Locality | LocationRequiredFields.Region | LocationRequiredFields.Country | LocationRequiredFields.PostalCode;
                    var prompt = "Which library location would you like to visit? Please tell me your address.";
                    var locationDialog = new LocationDialog(bingAPIKey, message.ChannelId, prompt, options, requiredFields);
                    context.Call(locationDialog, this.ResumeAfterLocationDialog);
                }
                else
                {
                    await context.Forward(new GeneralQnAMakerDialog(), ResumeAfterGeneralQnADialog, message, CancellationToken.None);
                }

                if (language != "en")
                    await context.PostAsync($"You said: {originalMessage} in {isoLanguage}, and I have translated that to English as {message.Text}");
            }).Wait();
        }

        private async Task ResumeAfterRenewPrompt(IDialogContext context, IAwaitable<bool> result)
        {
            if (await result)
            {
                // Get patron information and lookup books currently checked out
                //await context.Forward(new BooksDialog(), ResumeAfterBooksDialog, new Activity(), CancellationToken.None);
                var patronId = string.Empty;
                var question1 = "What is your date of birth?";
                var question2 = "What is your email address?";

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

                        translation = await TranslateText(question1, language, accessToken);
                        question1 = translation;

                        translation = await TranslateText(question2, language, accessToken);
                        question2 = translation;
                    }
                    await context.PostAsync(response);

                    questions.Add(question1);
                    questions.Add(question2);

                    questionNumber = 0;

                    PromptDialog.Text(context, OnQuestionAnswered, questions[questionNumber]);
                }
                else
                {
                    await context.PostAsync($"Patron ID {patronId}");
                }
            }
            else
            {
                // Forward the request to the QnA Maker dialog
                await context.Forward(new GeneralQnAMakerDialog(), ResumeAfterGeneralQnADialog, new Activity { Text = "renew" }, CancellationToken.None);
            }
        }

        private async Task ResumeAfterLocationDialog(IDialogContext context, IAwaitable<Place> result)
        {
            var place = await result;

            if(place != null)
            {
                var address = place.GetPostalAddress();
                var formattedAddress = string.Join(", ", new[] { address.StreetAddress, address.Locality, address.Region, address.PostalCode, address.Country }.Where(x => !string.IsNullOrEmpty(x)));
                await context.PostAsync("OK, I have found the following County Library locations near " + formattedAddress);
            }

            context.Done<string>(null);
        }

        private async Task ResumeAfterGeneralQnADialog(IDialogContext context, IAwaitable<object> result)
        {
            var resultFromQnA = await result;

            context.Wait(this.MessageReceivedAsync);
        }

        private async Task ResumeAfterBooksDialog(IDialogContext context, IAwaitable<object> result)
        {
            var resultFromBooks = await result;

            context.Wait(MessageReceivedAsync);
        }

        private async Task OnQuestionAnswered(IDialogContext context, IAwaitable<string> argument)
        {
            var response = await argument;
  
            var feedback = "OK, thanks.";
            var language = context.UserData.GetValue<string>("Language");

            switch (questionNumber)
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

            if (questionNumber < questions.Count)
            {
                await context.PostAsync(feedback);
                PromptDialog.Text(context, OnQuestionAnswered, questions[questionNumber]);
            }
            else
            {
                await context.PostAsync($"I have your birthdate as {context.UserData.GetValue<string>("Birthdate")} and your email address as {context.UserData.GetValue<string>("EmailAddress")}.");
                await context.PostAsync($"Hi Brant. Here are the materials you currently have checked out...");

                ThumbnailCard book = new ThumbnailCard();
                book.Title = "The Old Man and the Sea";
                book.Subtitle = "Earnest Hemmingway";
                book.Text = "Checked Out: 08/31/2017\nDue: 09/22/2017";
                book.Images.Add(new CardImage("https://colalibrarybot.blob.core.windows.net/images/the-old-man-and-the-sea.jpg"));
                book.Buttons.Add(new CardAction("openUrl","Renew", null,"http://www.colapublib.org/"));

                ThumbnailCard book2 = new ThumbnailCard();
                book2.Title = "Adventures of Huckleberry Finn";
                book2.Subtitle = "Mark Twain";
                book2.Text = "Checked Out: 08/31/2017\nDue: 09/22/2017";
                book2.Images.Add(new CardImage("https://colalibrarybot.blob.core.windows.net/images/022511.jpg"));
                book2.Buttons.Add(new CardAction("openUrl", "Renew", null, "http://www.colapublib.org/"));

                ThumbnailCard book3 = new ThumbnailCard();
                book3.Title = "Toilet Paper Origami";
                book3.Subtitle = "Linda Wright";
                book3.Text = "Checked Out: 07/05/2017\nDue: **08/02/2017**";
                book3.Images.Add(new CardImage("https://colalibrarybot.blob.core.windows.net/images/ToiletPaperOrigami_Cover.jpg"));
                book3.Buttons.Add(new CardAction("openUrl", "Pay Fine", null, "http://www.colapublib.org/"));

                var message = context.MakeMessage() as IMessageActivity;
                message.AttachmentLayout = AttachmentLayoutTypes.Carousel;
                message.Attachments = new List<Attachment>();
                message.Attachments.Add(book.ToAttachment());
                message.Attachments.Add(book2.ToAttachment());
                message.Attachments.Add(book3.ToAttachment());

                await context.PostAsync(message);

                context.Wait(MessageReceivedAsync);
            }
        }

        private async Task<string> DetectLanguage(string inputText, string accessToken)
        {
            string url = "http://api.microsofttranslator.com/v2/Http.svc/Detect";
            string query = $"?text={System.Net.WebUtility.UrlEncode(inputText)}";

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
                var response = await client.GetAsync(url + query);
                var result = await response.Content.ReadAsStringAsync();

                if (!response.IsSuccessStatusCode)
                {
                    return result;
                }

                var languageDetected = XElement.Parse(result).Value;
                return languageDetected;
            }
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

                if(!response.IsSuccessStatusCode)
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