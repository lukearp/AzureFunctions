using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace AutoRouteTable.Function
{
    public static class AutoRouteTableManagement
    {
        [FunctionName("AutoRouteTableManagement")]
        public static void Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log)
        {
            string apiVersion = GetEnvironmentVariable("apiVersion").Split(": ")[1];
            string subscriptionId = GetEnvironmentVariable("subscriptionId").Split(": ")[1];
            string location = GetEnvironmentVariable("location").Split(": ")[1];
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
            List<JObject> serviceTags;
            string requestUri = "/subscriptions/" + subscriptionId + "/providers/Microsoft.Network/locations/" + location + "/serviceTags?api-version=" + apiVersion;
            try {
                serviceTags = JObject.Parse(WebCalls.Get(requestUri)).Property("values").Value.ToObject<List<JObject>>();
            }
            catch (Exception e){
                serviceTags = null;
                Console.WriteLine("Failed to get ServiceTags: " + e.Message);
            }
            if(serviceTags != null)
            {
                JArray routeTables = RouteTables.GetRouteTables(subscriptionId);
                foreach (JToken token in routeTables)
                {
                    JObject RouteTable = JObject.Parse(token.ToString());
                    JObject RtProperties = JObject.Parse(RouteTable.Property("properties").Value.ToString());
                    List<JObject> routes = RtProperties.Property("routes").Value.ToObject<List<JObject>>();
                    if (RouteTable.ContainsKey("tags"))
                    {
                        JObject RtTags = JObject.Parse(RouteTable.Property("tags").Value.ToString());
                        routes.RemoveAll(x => x.Property("name").Value.ToString().Contains("AutoRoute-"));
                        if (RtTags.ContainsKey("AutoRoute"))
                        {
                            string[] requiredServiceTags = RtTags.Property("AutoRoute").Value.ToString().Split(",");
                            JObject tagRoutesProperties;
                            foreach (string Tag in requiredServiceTags)
                            {
                                try
                                {
                                    tagRoutesProperties = JObject.Parse(serviceTags.Find(x => x.Property("name").Value.ToString() == Tag).Property("properties").Value.ToString());
                                }
                                catch
                                {
                                    tagRoutesProperties = null;
                                }
                                if (tagRoutesProperties != null)
                                {
                                    int count = 0;
                                    string routeName = "AutoRoute-" + Tag + "-" + tagRoutesProperties.Property("changeNumber").Value.ToString() + "-" + count.ToString();
                                    routes.RemoveAll(x => x.Property("name").Value.ToString().Contains(Tag + "-"));
                                    foreach (string prefix in tagRoutesProperties.Property("addressPrefixes").Value.ToObject<List<string>>())
                                    {
                                        routes.Add(JObject.Parse("{\"name\": \"" + routeName + "\",\"properties\": {\"addressPrefix\": \"" + prefix + "\",\"nextHopType\": \"Internet\"}}"));
                                        count++;
                                        routeName = "AutoRoute-" + Tag + "-" + tagRoutesProperties.Property("changeNumber").Value.ToString() + "-" + count.ToString();
                                    }                                    
                                }
                                else
                                {
                                    //No Service Tag Found
                                }
                            }
                        }
                    }
                    string routeTxt = JsonConvert.SerializeObject(routes);
                    RtProperties.Remove("routes");
                    RtProperties.Add("routes", JArray.Parse(routeTxt));
                    RouteTable.Remove("properties");
                    RouteTable.Add("properties", RtProperties);
                    string RtUpdateUri = RouteTable.Property("id").Value.ToString() + "?api-version=" + apiVersion;
                    string testResult = WebCalls.Put(RtUpdateUri, RouteTable.ToString());
                }
            }            
        }

        public static string GetEnvironmentVariable(string name)
        {
            return name + ": " +
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    public class ApiUser
    {
        private static string clientId = AutoRouteTableManagement.GetEnvironmentVariable("clientId").Split(": ")[1];
        private static string appKey = AutoRouteTableManagement.GetEnvironmentVariable("appKey").Split(": ")[1];
        private static string aadInstance = "https://login.microsoftonline.com/";
        private static string tenantId = AutoRouteTableManagement.GetEnvironmentVariable("tenantId").Split(": ")[1];
        private static string apiUrl = "https://management.azure.com/";
        private static AuthenticationContext authContext;
        private static AuthenticationResult token;
        private static ClientCredential cred;
        private static DateTime timeStamp;

        static ApiUser()
        {
            cred = new ClientCredential(clientId, appKey);
            authContext = new AuthenticationContext(aadInstance + tenantId);
            token = authContext.AcquireTokenAsync(apiUrl, cred).Result;
            timeStamp = DateTime.Now;
        }

        public static void RefreshToken()
        {
            if (timeStamp.AddMinutes(5) < DateTime.Now)
            {
                timeStamp = DateTime.Now;
                cred = new ClientCredential(clientId, appKey);
                authContext = new AuthenticationContext(aadInstance + tenantId);
                token = authContext.AcquireTokenAsync(apiUrl, cred).Result;
                AzHttpClient.httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", token.AccessToken);
            }
        }

        public static string GetAADToken()
        {
            return token.AccessToken;
        }
    }

    class AzHttpClient
    {
        public static HttpClient httpClient;
        private static string graphUrl = "https://management.azure.com";

        static AzHttpClient()
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(graphUrl);
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("bearer", ApiUser.GetAADToken());
        }
    }

    public class RouteTables
    {
        public RouteTables()
        {
            //Constructor
        }

        public static JArray GetRouteTables(string subscriptionId)
        {
            string requestUri = "/subscriptions/" + subscriptionId + "/providers/Microsoft.Network/routeTables?api-version=2019-12-01";
            JArray routeTables = new JArray();
            try
            {

                JObject result = JObject.Parse(WebCalls.Get(requestUri));
                if (result.Property("@odata.nextLink") != null)
                {
                    routeTables.Add(JArray.Parse(result.Property("value").Value.ToString()));
                    while (result.Property("@odata.nextLink") != null)
                    {
                        result = JObject.Parse(WebCalls.Get(result.Property("@odata.nextLink").Value.ToString()));
                        routeTables.Merge(JArray.Parse(result.Property("value").Value.ToString()));
                    }
                }
                else
                {
                    routeTables = JArray.Parse(result.Property("value").Value.ToString());
                }
            }
            catch (Exception e)
            {

            }
            return routeTables;
        }
    }

    public class WebCalls
    {
        public static string Post(string requestUri, string body)
        {
            StringContent post = new StringContent(body);
            post.Headers.ContentType.MediaType = "application/json";
            HttpResponseMessage response;
            try
            {
                response = AzHttpClient.httpClient.PostAsync(requestUri, post).Result;
                if(response.StatusCode == System.Net.HttpStatusCode.Unauthorized )
                {
                    ApiUser.RefreshToken();
                    response = AzHttpClient.httpClient.PostAsync(requestUri, post).Result;
                }
            }
            catch (Exception e)
            {
                response = new HttpResponseMessage();
            }
            return response.Content.ReadAsStringAsync().Result;
        }

        public static string Put(string requestUri, string body)
        {
            StringContent post = new StringContent(body);
            post.Headers.ContentType.MediaType = "application/json";
            HttpResponseMessage response;
            try
            {
                response = AzHttpClient.httpClient.PutAsync(requestUri, post).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ApiUser.RefreshToken();
                    response = AzHttpClient.httpClient.PutAsync(requestUri, post).Result;
                }
            }
            catch (Exception e)
            {
                response = new HttpResponseMessage();
            }
            return response.Content.ReadAsStringAsync().Result;
        }

        public static string Get(string requestUri)
        {
            HttpResponseMessage response;
            try
            {
                response = AzHttpClient.httpClient.GetAsync(requestUri).Result;
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
                {
                    ApiUser.RefreshToken();
                    response = AzHttpClient.httpClient.GetAsync(requestUri).Result;
                }
            }
            catch (Exception e)
            {
                response = new HttpResponseMessage();
            }
            return response.Content.ReadAsStringAsync().Result;
        }

        public static string Patch(string requestUri, string body)
        {
            var method = new HttpMethod("PATCH");
            StringContent patch = new StringContent(body);
            patch.Headers.ContentType.MediaType = "application/json";
            var request = new HttpRequestMessage(method, requestUri);
            request.Content = patch;
            HttpResponseMessage response;
            try
            {
                response = AzHttpClient.httpClient.SendAsync(request).Result;
            }
            catch (Exception e)
            {
                response = new HttpResponseMessage();
            }
            return response.Content.ReadAsStringAsync().Result;
        }
    }
}
