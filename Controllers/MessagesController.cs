namespace CityOfWisdomBot.Controllers
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Threading.Tasks;
    using System.Web.Http;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json.Linq;
    using Services;
    using Newtonsoft.Json;
    using System.Web.Configuration;
    using Microsoft.Bot.Builder.Dialogs;
    using CityOfWisdomBot.Domains;
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
                await HandleSystemMessage(activity);
                var resultTest = Request.CreateResponse(HttpStatusCode.OK);
                return resultTest;
            }
            var result = Request.CreateResponse(HttpStatusCode.OK);
            return result;
        }
        /// <summary>
        /// The bot is obviously stateless. When we get the message from D365, we need to parse the data to find out which citizen should receive what message on which channel.
        /// </summary>
        /// <param name="activity"></param>
        private  void CommunicateMessageToUser(Activity activity)
        {
            //Parse the message.
            var message = JsonConvert.DeserializeObject<QueueMessage>(((JObject)activity.Value).GetValue("Message").ToString());

            //Conversation ID is generally the key in order to reply to the same conversation in the channel.
            if(!string.IsNullOrEmpty(message.ConversationID))
            {
                var recipient = new ChannelAccount(message.RecipientID, message.RecipientName);
                var from = new ChannelAccount(message.FromID, message.FromName);

                //These couple of lines are important to estabilish the trust that we are authorized to send the message in this channel.
                var connector = new ConnectorClient(new Uri(message.ServiceURL), new MicrosoftAppCredentials(ApiKey, ApiEndpoint));
                MicrosoftAppCredentials.TrustServiceUrl(message.ServiceURL);

                var alertMessage = Activity.CreateMessageActivity();
                if (!string.IsNullOrEmpty(message.ConversationID) && !string.IsNullOrEmpty(message.ChannelID))
                {
                    alertMessage.ChannelId = message.ChannelID;
                }
                else
                {
                   message.ConversationID = (connector.Conversations.CreateDirectConversationAsync(from, recipient)).Id.ToString();
                }

                //I know I am flipping the "recipient" and "from" objects. The reason is that when we originally received the message from the citizen, 
                //the citizen related attributes is present in the from object and bot related attributes are present in the recipient object and it makes sense as,
                //the citizen sends the message and the bot receives it. But, now the table has turned. The Bot sends the message and the citizen receives it,
                // which is why i flip it.
                alertMessage.Recipient = from;
                alertMessage.From = recipient;
                alertMessage.Conversation = new ConversationAccount(id: message.ConversationID);
                alertMessage.Text = message.Alert;
                alertMessage.Locale = "en-Us";

                //Once the connection is established to the channel and the conversation, the bot can reply with the text.
                connector.Conversations.SendToConversationAsync((Activity)alertMessage);
            }
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
                case ActivityTypes.Event:
                    {
                        if (activity.ServiceUrl == "https://directline.botframework.com/")
                        {
                            CommunicateMessageToUser(activity);
                        }
                    }
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
