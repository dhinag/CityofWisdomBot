namespace CityOfWisdomBot.Dialog
{
    using Microsoft.Bot.Builder.Dialogs;
    using System;
    using System.Threading.Tasks;
    using Microsoft.Bot.Connector;
    using Newtonsoft.Json.Linq;
    using CityOfWisdomBot.Constants;

    [Serializable]
    public class LocationDialog : IDialog<string[]>
    {
        public async Task StartAsync(IDialogContext context)
        {
            await context.PostAsync(BotConstants.SHARE_LOCATION_REPLY);
            context.Wait(this.MessageReceivedAsync);
        }

        private async Task MessageReceivedAsync(IDialogContext context, IAwaitable<IMessageActivity> result)
        {
            var activity = await result as Activity;

            dynamic data = JObject.Parse(activity.ChannelData.ToString());

            //This is how we extract the geo coordinates data from the Messenger's payload.
            string longitude = data.message.attachments[0].payload.coordinates["long"].ToString();
            string latitude = data.message.attachments[0].payload.coordinates["lat"].ToString();

            //We do not want to user to wait until we create the service request in D365.
            //So, we just say "Thanks" do the service request creation later.
            await context.PostAsync(BotConstants.THANKS_FOR_LOCATION_REPLY);

            if(!string.IsNullOrEmpty(longitude) && !string.IsNullOrEmpty(latitude))
            {
                string[] geoCoordinates = new string[] { latitude, longitude };
                context.Done(geoCoordinates);
            }
        }
    }
}