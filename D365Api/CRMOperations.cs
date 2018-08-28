using CityOfWisdomBot.Constants;
using CityOfWisdomBot.Domains;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using System.Web.Configuration;

namespace CityOfWisdomBot.D365Operations
{
    public class D365WebApi
    {
        private static readonly string serviceUrl = WebConfigurationManager.AppSettings["ServiceUrl"];
        private static readonly string clientId = WebConfigurationManager.AppSettings["ClientId"];
        private static readonly string redirectUrl = WebConfigurationManager.AppSettings["RedirectUrl"];
        private static HttpMessageHandler messageHandler;
        private static Version webAPIVersion = new Version(9, 0);

        public static void GetConnection()
        {
            //One message handler for OAuth authentication, and the other for Windows integrated 
            // authentication.  (Assumes that HTTPS protocol only used for CRM Online.)
            if (serviceUrl.StartsWith("https://"))
            {
                messageHandler = new OAuthMessageHandler(serviceUrl, clientId, redirectUrl,
                         new HttpClientHandler());
            }
        }
       
        /// <summary>
        /// Create the service request in D365.
        /// </summary>
        /// <param name="sr"></param>
        /// <param name="profile"></param>
        public static async void CreateServiceRequest(ServiceRequest sr, ChannelProfile profile)
        {
            GetConnection();
            try
            {
                //Create an HTTP client to send a request message to the CRM Web service.
                using (HttpClient httpClient = new HttpClient(messageHandler))
                {
                    //Specify the Web API address of the service and the period of time each request 
                    // has to execute.
                    httpClient.BaseAddress = new Uri(serviceUrl);
                    httpClient.Timeout = new TimeSpan(0, 2, 0);  //2 minutes

                    httpClient.DefaultRequestHeaders.Add("OData-MaxVersion", "4.0");
                    httpClient.DefaultRequestHeaders.Add("OData-Version", "4.0");
                    httpClient.DefaultRequestHeaders.Accept.Add(
                        new MediaTypeWithQualityHeaderValue(AppConstants.JSON_CONTENT_TYPE));

                    //Find if there is a bot channel profile for the particular conversation id
                    HttpRequestMessage getBotChannelProfileRequest =
                        new HttpRequestMessage(HttpMethod.Get, getVersionedWebAPIPath() + "bot_botchannelprofiles(bot_conversationid='" + profile.ConversationID + "')?$select=bot_botchannelprofileid");

                    var getBotChannelProfileResponse = await httpClient.SendAsync(getBotChannelProfileRequest);

                    var createBotChannelProfileResponse = new HttpResponseMessage();
                    String channelProfileURI;

                    //If it doesn't already exist, create a bot channel profile record.
                    if (!getBotChannelProfileResponse.IsSuccessStatusCode)
                    {
                        //Create a Bot Channel Profile
                        var botChannelProfile = ConstructChannelProfileObject(profile);
                        HttpRequestMessage botChannelProfileCreateRequest =
                            new HttpRequestMessage(HttpMethod.Post, getVersionedWebAPIPath() + "bot_botchannelprofiles");

                        botChannelProfileCreateRequest.Content = new StringContent(botChannelProfile.ToString(),
                            Encoding.UTF8, AppConstants.JSON_CONTENT_TYPE);

                        createBotChannelProfileResponse = await httpClient.SendAsync(botChannelProfileCreateRequest);
                        channelProfileURI = createBotChannelProfileResponse.Headers.GetValues("OData-EntityId").FirstOrDefault();
                    }
                    //If the record already exist, then obtain the channel profile id.
                    else
                    {
                        var retrievedData = JsonConvert.DeserializeObject<JObject>(
                         await getBotChannelProfileResponse.Content.ReadAsStringAsync());

                        var channelProfileId = retrievedData.GetValue("bot_botchannelprofileid").ToString();
                        channelProfileURI = httpClient.BaseAddress + getVersionedWebAPIPath() + "bot_botchannelprofiles(" + channelProfileId + ")";
                    }

                    var srObject = ConstructServiceRequestObject(sr, channelProfileURI);
                    var createSRRequest =
                        new HttpRequestMessage(HttpMethod.Post, getVersionedWebAPIPath() + "bot_servicerequests");

                    createSRRequest.Content = new StringContent(srObject.ToString(),
                        Encoding.UTF8, AppConstants.JSON_CONTENT_TYPE);
                    var srResponse = await httpClient.SendAsync(createSRRequest);
                }
            }
            catch (Exception ex)
            {
                DisplayException(ex);
                throw;
            }
        }

        private static JObject ConstructServiceRequestObject(ServiceRequest sr, string channelProfileURI)
        {
            JObject srObject = new JObject();
            srObject.Add("bot_name", AppConstants.SERVICE_REQUEST_TITLE);
            srObject.Add("bot_servicerequesttype", sr.ServiceRequestType);
            srObject.Add("bot_servicerequestlatitude", sr.Latitude);
            srObject.Add("bot_servicerequestlongitude", sr.Longitude);
            srObject.Add("bot_BotChannelProfileId@odata.bind", channelProfileURI);

            return srObject;
        }

        private static JObject ConstructChannelProfileObject(ChannelProfile profile)
        {
            JObject botChannelProfile = new JObject();
            botChannelProfile.Add("bot_channelid", profile.ChannelID);
            botChannelProfile.Add("bot_conversationid", profile.ConversationID);
            botChannelProfile.Add("bot_fromid", profile.FromID);
            botChannelProfile.Add("bot_fromname", profile.FromName);
            botChannelProfile.Add("bot_recipientid", profile.RecipientID);
            botChannelProfile.Add("bot_recipientname", profile.RecipientName);
            botChannelProfile.Add("bot_serviceurl", profile.ServiceURL);
            botChannelProfile.Add("bot_name", profile.ProfileName);

            return botChannelProfile;
        }

        private static string getVersionedWebAPIPath()
        {
            return string.Format("api/data/v{0}/", webAPIVersion.ToString(2));
        }

        /// <summary> Displays exception information to the console. </summary>
        /// <param name="ex">The exception to output</param>
        private static void DisplayException(Exception ex)
        {
            Console.WriteLine("The application terminated with an error.");
            Console.WriteLine(ex.Message);
            while (ex.InnerException != null)
            {
                Console.WriteLine("\t* {0}", ex.InnerException.Message);
                ex = ex.InnerException;
            }
        }
    }

    /// <summary>
    ///Custom HTTP message handler that uses OAuth authentication thru ADAL.
    /// </summary>
    class OAuthMessageHandler : DelegatingHandler
    {
        public AuthenticationHeaderValue authHeader;
        private static readonly string authEndPoint = WebConfigurationManager.AppSettings["AuthEndpoint"];
        private static readonly string key = WebConfigurationManager.AppSettings["Key"];

        public OAuthMessageHandler(string serviceUrl, string clientId, string redirectUrl,
                HttpMessageHandler innerHandler)
            : base(innerHandler)
        {
            AuthenticationContext authContext = new AuthenticationContext(authEndPoint, false);
            ClientCredential clientCred = new ClientCredential(clientId, key);

            //Note that an Azure AD access token has finite lifetime, default expiration is 60 minutes.
            AuthenticationResult authResult = authContext.AcquireToken(serviceUrl, clientCred);

            authHeader = new AuthenticationHeaderValue("Bearer", authResult.AccessToken);
        }

        protected override Task<HttpResponseMessage> SendAsync(
                 HttpRequestMessage request, System.Threading.CancellationToken cancellationToken)
        {
            request.Headers.Authorization = authHeader;
            return base.SendAsync(request, cancellationToken);
        }
    }
}


