namespace CityOfWisdomBot.Controllers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.Bot.Connector;
    using Services;
    using System.Web.Configuration;
    using Microsoft.Bot.Builder.Dialogs;
    using CityOfWisdomBot.Constants;
    using CityOfWisdomBot.Dialog;

    [BotAuthentication]
    public class MessagesController : ApiController
    {
        private readonly ICaptionService captionService = new MicrosoftCognitiveCaptionService();
        private static Uri imageURI;
        private static readonly string ApiKey = WebConfigurationManager.AppSettings["MicrosoftAppId"];
        private static readonly string ApiEndpoint = WebConfigurationManager.AppSettings["MicrosoftAppPassword"];

        string message;

        /// <summary>
        /// POST: api/Messages
        /// Receive a message from a user and reply to it
        /// </summary>
        public async Task<HttpResponseMessage> Post([FromBody]Activity activity)
        {
            //When the user taps on "Get Started" button on Messenger, "requestWelcome" is sent by FB Messenger in the payload.
            //We use this in order to display welcome message to the user.
            if (activity.Text == "requestWelcome")
            {
                var connector = new ConnectorClient(new Uri(activity.ServiceUrl));

                var response = activity.CreateReply();
                response.Text = BotConstants.UPLOAD_IMAGE_MESSAGE;
                await connector.Conversations.ReplyToActivityAsync(response);
            }

            if (activity.Type == ActivityTypes.Message)
            {
                await Conversation.SendAsync(activity, () => new RootDialog());
            }
            else
            {
                HandleSystemMessage(activity);
            }
            var result = Request.CreateResponse(HttpStatusCode.OK);
            return result;
        }

        /// <summary>
        /// Handles the system activity. Provides us with an ability to handle different events so that we can action accordingly.
        /// </summary>
        /// <param name="activity">The activity.</param>
        /// <returns>Activity</returns>
        private async Task<Activity> HandleSystemMessage(Activity activity)
        {
            switch (activity.Type)
            {
                case ActivityTypes.DeleteUserData:
                    // Implement user deletion here
                    // If we handle user deletion, return a real message
                    break;
                case ActivityTypes.ConversationUpdate:
                    break;
                case ActivityTypes.ContactRelationUpdate:
                    // Handle add/remove from contact lists
                    break;
                case ActivityTypes.Typing:
                    // Handle knowing that the user is typing
                    break;
                case ActivityTypes.Ping:
                    break;
            }
            return null;
        }
    }
}