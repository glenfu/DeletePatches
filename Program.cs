using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.IdentityModel.Clients.ActiveDirectory;
using Newtonsoft.Json.Linq;

namespace DeletePatches
{
    class Program
    {
        private static readonly HttpClient client = new HttpClient()
        {
            Timeout = Timeout.InfiniteTimeSpan
        };

        static async Task<string> AccessTokenGenerator(string clientId, string clientSecret, string resourceUrl, string tenantId)
        {
            string authority = $"https://login.microsoftonline.com/{tenantId}";
            ClientCredential credentials = new ClientCredential(clientId, clientSecret);
            var authContext = new AuthenticationContext(authority);
            var result = await authContext.AcquireTokenAsync(resourceUrl, credentials);
            return result.AccessToken;
        }

        static async Task<List<string>> GetSolutionPatches(
            Uri urlPrefix,
            string token,
            string parentSolutionId
            )
        {
            var patches = new List<string>();

            var patchesURL = new Uri(urlPrefix, $"solutions?$filter=_parentsolutionid_value%20eq%20%27{parentSolutionId}%27&$orderby=installedon desc&$select=uniquename,friendlyname,installedon,modifiedon");

            using (var request = new HttpRequestMessage(new HttpMethod("GET"), patchesURL))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                try
                {
                    using (var response = await client.SendAsync(request))
                    {
                        dynamic json = JValue.Parse(response.Content.ReadAsStringAsync().Result);
                        JArray values = (JArray)JValue.Parse(json.value.ToString());

                        foreach (JObject item in values)
                        {
                            string solutionid = item.GetValue("solutionid").ToString();
                            patches.Add(solutionid);
                        }
                    }
                }
                // Filter by InnerException.
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    // Handle timeout.
                    Console.WriteLine("Timed out: " + ex.Message);
                }
                catch (TaskCanceledException ex)
                {
                    // Handle cancellation.
                    Console.WriteLine("Canceled: " + ex.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                }
            }

            await Task.Delay(TimeSpan.FromSeconds(5));

            return patches;
        }

        static async Task DeletePatchAsync(
            Uri urlPrefix,
            string token,
            string solutionid
            )
        {
            var patch = new Uri(urlPrefix, $"solutions({solutionid})");

            using (var request = new HttpRequestMessage(new HttpMethod("DELETE"), patch))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

                try
                {
                    Console.WriteLine($"Deleting solution: {solutionid}");

                    var response = await client.SendAsync(request);					
                }
                // Filter by InnerException.
                catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
                {
                    // Handle timeout.
                    Console.WriteLine("Timed out: " + ex.Message);
                }
                catch (TaskCanceledException ex)
                {
                    // Handle cancellation.
                    Console.WriteLine("Canceled: " + ex.Message);
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e);
                }
            }
        }

        static void Main(string[] args)
        {
            using (StreamReader r = new StreamReader("DeletePatches.runtimeconfig.json"))
            {
                dynamic json = JValue.Parse(r.ReadToEnd());

                string url = json.runtimeOptions.configProperties.orgDetails.orgUrl.ToString();
                string clientId = json.runtimeOptions.configProperties.orgDetails.clientId.ToString();
                string clientSecret = json.runtimeOptions.configProperties.orgDetails.clientSecret.ToString();
                string tenantId = json.runtimeOptions.configProperties.orgDetails.tenantId.ToString();

                string parentSolutionId = json.runtimeOptions.configProperties.parentSolutionId.ToString();

                if (url != String.Empty && clientId != String.Empty && clientSecret != String.Empty && tenantId != String.Empty && parentSolutionId != String.Empty)
                {
                    string token = AccessTokenGenerator(
                        clientId,
                        clientSecret,
                        url,
                        tenantId).Result;

                    var uri = new Uri($"{url}/api/data/v9.0/");

                    var patches = GetSolutionPatches(
                        uri,
                        token,
                        parentSolutionId
                        ).GetAwaiter().GetResult();

                    ConsoleSpinner spin = new ConsoleSpinner();

                    if (patches.Count > 0)
                    {
                        Console.ForegroundColor = ConsoleColor.White;
                        Console.WriteLine($"No. of patches left: {patches.Count}");

                        DeletePatchAsync(
                            uri,
                            token,
                            patches.FirstOrDefault().ToString()
                            );
                    }

                    var currentPatchNo = patches.Count;
                    var index = currentPatchNo;

                    while (patches.Count >= 1)
                    {
                        token = AccessTokenGenerator(
                            clientId,
                            clientSecret,
                            url,
                            tenantId).Result;

                        if (index == currentPatchNo)
                        {
                            patches = GetSolutionPatches(
                                uri,
                                token,
                                parentSolutionId
                                ).GetAwaiter().GetResult();

                            currentPatchNo = patches.Count;
                        }

                        if (index != currentPatchNo)
                        {
                            Console.ForegroundColor = ConsoleColor.White;
                            Console.WriteLine($"No. of patches left: {patches.Count}");

                            index--;

                            if (index > 0)
                            {
                                DeletePatchAsync(
                                    uri,
                                    token,
                                    patches.FirstOrDefault().ToString()
                                    );
                            }
                        }

                        spin.Turn();
                    }
                }

                Console.WriteLine("Completed Successfully!!");
            }
        }
    }

    public class ConsoleSpinner
    {
        int counter;
        public ConsoleSpinner()
        {
            counter = 0;
        }
        public void Turn()
        {
            counter++;
            Console.ForegroundColor = ConsoleColor.Green;
            switch (counter % 4)
            {
                case 0: Console.Write("/"); break;
                case 1: Console.Write("-"); break;
                case 2: Console.Write("\\"); break;
                case 3: Console.Write("|"); break;
            }
            Console.SetCursorPosition(Console.CursorLeft - 1, Console.CursorTop);
        }
    }
}
