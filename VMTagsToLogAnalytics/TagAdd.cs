using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Company.Function
{
    public static class TagAdd
    {
        [FunctionName("TagAdd")]
        public static void Run([TimerTrigger("0 0 * * * *")]TimerInfo myTimer, ILogger log)
        {
            JArray vms = VirtualMachines.GetVMTags();    
            log.LogInformation(vms.ToString());
            string success = LogAnalyticsHttpClient.Post(vms.ToString());
        }

        public static string GetEnvironmentVariable(string name)
        {
            return name + ": " +
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }
    }

    class LogAnalyticsHttpClient
    {
        public static HttpClient httpClient;
        private static string workspaceId = TagAdd.GetEnvironmentVariable("workspaceId").Split(": ")[1];
        private static string workspaceKey = TagAdd.GetEnvironmentVariable("workspaceKey").Split(": ")[1];
        private static string logAnalyticsApiVersion = TagAdd.GetEnvironmentVariable("logAnalyticsApiVersion").Split(": ")[1];
        private static string logType = TagAdd.GetEnvironmentVariable("logName").Split(": ")[1];
        private static string graphUrl = "https://" + workspaceId + ".ods.opinsights.azure.com/";
        private static HMACSHA256 hashForKey;

        static LogAnalyticsHttpClient()
        {
            httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri(graphUrl);
            httpClient.DefaultRequestHeaders.Clear();
            byte[] key = Convert.FromBase64String(workspaceKey);
            hashForKey = new HMACSHA256(key);
        }

        static void BuildHeader(string verb, int contentLength, string time)
        {
            string StringToSign = verb + "\n" + contentLength.ToString() + "\n" + "application/json" + "\nx-ms-date:" + time + "\n" + "/api/logs";
            var encoding = new System.Text.ASCIIEncoding();
            byte[] encodedBytes = encoding.GetBytes(StringToSign);
            byte[] hashByte = hashForKey.ComputeHash(encodedBytes);            
            string Signature = Convert.ToBase64String(hashByte);
            string auth = "SharedKey " + workspaceId + ":" + Signature;
            httpClient.DefaultRequestHeaders.Add("Accept","application/json");
            httpClient.DefaultRequestHeaders.Add("Log-Type",logType);
            httpClient.DefaultRequestHeaders.Add("Authorization",auth);
            httpClient.DefaultRequestHeaders.Add("x-ms-date",time);
            httpClient.DefaultRequestHeaders.Add("time-generated-field","");
        }

        static public string Post(string body)
        {
            string success = "SUCCESS";
            HttpContent post = new StringContent(body, System.Text.Encoding.UTF8); 
            post.Headers.ContentType = new MediaTypeHeaderValue("application/json"); 
            string uri = "/api/logs?api-version=" + logAnalyticsApiVersion;          
            string time = DateTime.UtcNow.ToString("R");
            var utf8Encoding = new System.Text.UTF8Encoding();
            int size = utf8Encoding.GetBytes(body).Length; //System.Text.Encoding.UTF8.GetBytes(body).Length;
            HttpResponseMessage response;           
            try
            {
                BuildHeader("POST",size,time);
                response = httpClient.PostAsync(uri,post).Result;
            }
            catch (Exception e)
            {
                success = "FAIL";
                response = new HttpResponseMessage();
            }
            return success;
        }
    }

    public class ApiUser
    {
        private static string clientId = TagAdd.GetEnvironmentVariable("clientId").Split(": ")[1];
        private static string appKey = TagAdd.GetEnvironmentVariable("appKey").Split(": ")[1];
        private static string aadInstance = "https://login.microsoftonline.com/";
        private static string tenantId = TagAdd.GetEnvironmentVariable("tenantId").Split(": ")[1];
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
                Uri request = new Uri(requestUri, UriKind.Relative);
                response = AzHttpClient.httpClient.GetAsync(request).Result;
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

    class VirtualMachines
    {
        static string apiVersion = TagAdd.GetEnvironmentVariable("vmApiVersion").Split(": ")[1];
        static string subscriptionId = TagAdd.GetEnvironmentVariable("subscriptionId").Split(": ")[1];
        static public JArray GetVMTags()
        {
            JArray virtualMachines = GetVMs(subscriptionId);
            JArray LogObject = new JArray();
            foreach(JToken virtualMachine in virtualMachines)
            {
                if(JObject.Parse(virtualMachine.ToString()).ContainsKey("tags"))
                {
                    foreach(JProperty tag in virtualMachine["tags"].ToObject<JObject>().Properties())
                    {
                        LogObject.Add(JObject.Parse("{\"Computer\": \""+ virtualMachine["name"].Value<string>() +"\",\"TagKey\":\"" + tag.Name + "\",\"TagValue\": \"" + tag.Value.ToString() + "\"}"));
                    }
                }
            }
            return LogObject;
        }

        static JArray GetVMs(string subscriptionId)
        {
            string uri = "/subscriptions/" + subscriptionId + "/providers/Microsoft.Compute/virtualMachines?api-version=" + apiVersion;
            JObject vms = new JObject();
            JArray vmsData = new JArray();
            try {
                vms = JObject.Parse(WebCalls.Get(uri));
                vmsData.Merge(vms["value"].ToObject<JArray>());
                if(vms.ContainsKey("nextLink"))
                {
                    HttpClient newClient = new HttpClient();
                    newClient.DefaultRequestHeaders.Authorization = AzHttpClient.httpClient.DefaultRequestHeaders.Authorization;
                    while(vms.ContainsKey("nextLink"))
                    {
                        vms = JObject.Parse(newClient.GetAsync(vms["nextLink"].ToString()).Result.ToString());
                        vmsData.Merge(vms["value"].ToObject<JArray>());
                    }
                }                
            }
            catch (Exception e) {
                
            }
            return vmsData;
        }
    }
}
