namespace CityOfWisdomBot.Dialog
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.RegularExpressions;
    using System.Threading.Tasks;
    using CityOfWisdomBot.Services;
    using Microsoft.Bot.Builder.Dialogs;
    using Microsoft.Bot.Connector;
    using Microsoft.ProjectOxford.Vision;
    using D365Operations;
    using CityOfWisdomBot.Domains;
    using CityOfWisdomBot.Constants;

#pragma warning disable 1998

    [Serializable]
    public class RootDialog : IDialog<object>
    {
        private string[] geoCoordinates;

        string message;
        private static Uri imageURI;
        private readonly ICaptionService captionService = new MicrosoftCognitiveCaptionService();

        public async Task StartAsync(IDialogContext context)
        {
            context.Wait(this.MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var activity = await result as Activity;

            try
            {
                var connector = new ConnectorClient(new Uri(activity.ServiceUrl));

                //Get the caption of the image uploaded by sending the image to Cognitive Services - Computer Vision API
                message = await this.GetCaptionAsync(activity, connector);

                var reply = string.Format(BotConstants.REQUEST_TYPE_ACK, message);

               await context.PostAsync(reply);
               context.Call(new LocationDialog(), this.LocationDialogResumeAfter);
            }
            catch (ArgumentException e)
            {
                message = BotConstants.IMAGE_UPLOAD_EXCEPTION;
                Trace.TraceError(e.ToString());
            }
            catch (Exception e)
            {
                message = BotConstants.SOMETHING_WENT_WRONG_ERROR;
                if (e is ClientException && (e as ClientException).Error.Message.ToLowerInvariant().Contains("access denied"))
                {
                    message += " " + AppConstants.ACCESS_DENIED;
                }

                Trace.TraceError(e.ToString());
            }
        }

        private static async Task<Stream> GetImageStream(ConnectorClient connector, Attachment imageAttachment)
        {
            using (var httpClient = new HttpClient())
            {
                // The Skype attachment URLs are secured by JwtToken,
                // you should set the JwtToken of your bot as the authorization header for the GET request your bot initiates to fetch the image.
                // https://github.com/Microsoft/BotBuilder/issues/662
                imageURI = new Uri(imageAttachment.ContentUrl);
                if (imageURI.Host.EndsWith("skype.com") && imageURI.Scheme == "https")
                {
                    httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", await GetTokenAsync(connector));
                    httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/octet-stream"));
                }

                return await httpClient.GetStreamAsync(imageURI);
            }
        }

        private static async Task<string> GetTokenAsync(ConnectorClient connector)
        {
            var credentials = connector.Credentials as MicrosoftAppCredentials;
            if (credentials != null)
            {
                return await credentials.GetTokenAsync();
            }
            return null;
        }

        private async Task<string> GetCaptionAsync(Activity activity, ConnectorClient connector)
        {
            var imageAttachment = activity.Attachments?.FirstOrDefault(a => a.ContentType.Contains("image"));
            if (imageAttachment != null)
            {
                using (var stream = await GetImageStream(connector, imageAttachment))
                {
                    return await this.captionService.GetCaptionAsync(stream);
                }
            }

            string url;
            if (TryParseAnchorTag(activity.Text, out url))
            {
                return await this.captionService.GetCaptionAsync(url);
            }

            if (Uri.IsWellFormedUriString(activity.Text, UriKind.Absolute))
            {
                return await this.captionService.GetCaptionAsync(activity.Text);
            }

            // If we reach here then the activity is neither an image attachment nor an image URL.
            throw new ArgumentException(BotConstants.INVALID_IMAGE_ATTACHMENT_EXCEPTION);
        }

        private static bool TryParseAnchorTag(string text, out string url)
        {
            var regex = new Regex("^<a href=\"(?<href>[^\"]*)\">[^<]*</a>$", RegexOptions.IgnoreCase);
            url = regex.Matches(text).OfType<Match>().Select(m => m.Groups["href"].Value).FirstOrDefault();
            return url != null;
        }

        private async Task LocationDialogResumeAfter(IDialogContext context, IAwaitable<string[]> result)
        {
            try
            {
                geoCoordinates = await result;
                string type = string.Empty;

                switch (message)
                {
                    case "graffiti":
                        type = AppConstants.GRAFFITI_SR_TYPE;
                        break;
                    case "pothole":
                        type = AppConstants.POTHOLE_SR_TYPE;
                        break;
                    default:
                        break;
                }

                ServiceRequest sr = new ServiceRequest
                {
                    Latitude = geoCoordinates[0],
                    Longitude = geoCoordinates[1],
                    ServiceRequestType = type
                };

                ChannelProfile profile = new ChannelProfile
                {
                    ChannelID = context.Activity.ChannelId,
                    ConversationID = context.Activity.Conversation.Id,
                    FromID = context.Activity.From.Id,
                    FromName = context.Activity.From.Name,
                    RecipientID = context.Activity.Recipient.Id,
                    RecipientName = context.Activity.Recipient.Name,
                    ProfileName = string.Format("{0}'s Profile", context.Activity.From.Name),
                    ServiceURL = context.Activity.ServiceUrl
                };

                //create a service request record in Dynamics 365
                D365WebApi.CreateServiceRequest(sr, profile);
            }
            catch (TooManyAttemptsException)
            {
                await context.PostAsync(AppConstants.TRY_AGAIN);
            }
        }
    }
}