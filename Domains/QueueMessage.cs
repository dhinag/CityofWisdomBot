namespace CityOfWisdomBot.Domains
{
    public class QueueMessage
    {
        public string ChannelID { get; set; }
        public string FromID { get; set; }
        public string FromName { get; set; }
        public string RecipientID { get; set; }
        public string RecipientName { get; set; }
        public string ConversationID { get; set; }
        public string ServiceURL { get; set; }
        public string Alert { get; set; }
        public string MessageText { get; set; }
    }
}