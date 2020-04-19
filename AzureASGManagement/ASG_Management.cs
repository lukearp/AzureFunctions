using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Az.NSG
{
    public static class ASG_Management
    {
        [FunctionName("ASG_Management")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            string success;
            //log.LogInformation(requestBody);
            try {
                JArray data = JArray.Parse(requestBody);
                foreach(JToken token in data)
                {
                    log.LogInformation("Setting Interface: " + token.SelectToken("subject").ToString());
                    NetworkInterfaces.SetASG(token.SelectToken("subject").ToString());
                }
                success = "SUCCESS";
            }
            catch(Exception e)
            {
                success = "FAIL";
            }
            log.LogInformation(success);
            return new JsonResult(success);
        }

        public static string GetEnvironmentVariable(string name)
        {
            return name + ": " +
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    public class ApiUser
    {
        private static string clientId = ASG_Management.GetEnvironmentVariable("clientId").Split(": ")[1];
        private static string appKey = ASG_Management.GetEnvironmentVariable("appKey").Split(": ")[1];
        private static string aadInstance = "https://login.microsoftonline.com/";
        private static string tenantId = ASG_Management.GetEnvironmentVariable("tenantId").Split(": ")[1];
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

    public class ApplicationSecurityGroups
    {
        static private string asgRg = ASG_Management.GetEnvironmentVariable("applicationSecurityGroupResouceGroup").Split(": ")[1];
        static private string asgSub = ASG_Management.GetEnvironmentVariable("subscriptionId").Split(": ")[1];

        static ApplicationSecurityGroups()
        {
            //Constructor
        }

        public static string GetASGId(string asgName)
        {
            string resourceId = "/subscriptions/" + asgSub + "/resourceGroups/" + asgRg + "/providers/Microsoft.Network/applicationSecurityGroups/" + asgName;
            return resourceId;
        }
    }

    public class NetworkInterfaces
    {
        public NetworkInterfaces()
        {
            //Constructor
        }

        public static void SetASG(string resourceId)
        {
            JObject networkInterface = GetNetworkInterface(resourceId);
            JArray ipConfigurations;
            string result = "";
            string uri;
            if(networkInterface.ContainsKey("tags"))
            {
                if(networkInterface.Property("tags").Value.ToObject<JObject>().ContainsKey("ASG"))
                {
                    string ASGArray = "[{\"id\": \"" + ApplicationSecurityGroups.GetASGId(networkInterface.Property("tags").Value.ToObject<JObject>().Property("ASG").Value.ToString()) +"\"}]";
                    ipConfigurations = networkInterface["properties"].Value<JArray>("ipConfigurations");
                    JProperty asgArray = new JProperty("applicationSecurityGroups", JArray.Parse(ASGArray));
                    string thisTest = networkInterface["properties"]["ipConfigurations"][0]["properties"].Last.ToString();
                    bool appSecurity = false;
                    try{
                        appSecurity = networkInterface["properties"]["ipConfigurations"][0]["properties"]["applicationSecurityGroups"].HasValues;
                    }
                    catch(Exception e)
                    {

                    }
                    if(appSecurity)
                    {
                        networkInterface["properties"]["ipConfigurations"][0]["properties"]["applicationSecurityGroups"] = JArray.Parse(ASGArray);
                    }
                    else
                    {
                        networkInterface["properties"]["ipConfigurations"][0]["properties"].Last.AddAfterSelf(asgArray);
                    }
                    uri = resourceId + "?api-version=" + ASG_Management.GetEnvironmentVariable("apiVersion").Split(": ")[1];
                    result = WebCalls.Put(uri, networkInterface.ToString());
                }
            }
            string test = result;
        }

        private static JObject GetNetworkInterface(string resourceId)
        {
            string requestUri = resourceId + "?api-version=" + ASG_Management.GetEnvironmentVariable("apiVersion").Split(": ")[1];
            JObject networkInterface;
            try
            {
                networkInterface = JObject.Parse(WebCalls.Get(requestUri));
                if(networkInterface.ContainsKey("error"))
                {
                    networkInterface = null;
                }
            }
            catch (Exception e)
            {
                networkInterface = null;
            }
            return networkInterface;
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
                if (response.StatusCode == System.Net.HttpStatusCode.Unauthorized)
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
