using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Identity.Client;

namespace HotmailJunkMailDeleter;

internal class Program
{
    private static readonly string CacheFilePath = "msal_cache.bin";
    private static readonly object FileLock = new();
    private static IPublicClientApplication app;
    private static readonly string[] Scopes = new string[] { "User.Read", "Mail.Read", "Mail.ReadWrite" };


    private static async Task Main(string[] args)
    {
        AuthenticationResult result;


        try
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", false, true)
                .Build();
            string clientId = configuration["ClientId"];

            app = PublicClientApplicationBuilder.Create(clientId)
                .WithAuthority(AzureCloudInstance.AzurePublic, "consumers")
                .WithRedirectUri("http://localhost:8888")
                .Build();

            //cache the token so we don't get the browser prompt for every run. It will be shown the first time only.
            app.UserTokenCache.SetBeforeAccess(BeforeAccessNotification);
            app.UserTokenCache.SetAfterAccess(AfterAccessNotification);


            try
            {
                IEnumerable<IAccount>? accounts = await app.GetAccountsAsync();
                result = await app.AcquireTokenSilent(Scopes, accounts.FirstOrDefault()).ExecuteAsync();
            }
            catch (MsalUiRequiredException)
            {
                try
                {
                    result = await app.AcquireTokenInteractive(Scopes).ExecuteAsync();
                }
                catch (MsalException msalEx)
                {
                    Console.WriteLine($"Error acquiring token: {msalEx.Message}");
                    return;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return;
            }

            Console.WriteLine("Access token acquired successfully.");

            await FilterAndDeleteJunkEmails(result.AccessToken);
        }
        catch (Exception ex)
        {
            string message = ex.Message;
            string? innerMessage = ex.InnerException?.Message;
            throw;
        }
    }

    private static async Task FilterAndDeleteJunkEmails(string accessToken)
    {
        bool isDeleted = false;
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        string messageId = null;

        HttpResponseMessage response =
            await httpClient.GetAsync(
                $"https://graph.microsoft.com/v1.0/me/mailFolders/junkemail/messages?$select=body,from, subject&$top=100");


        if (response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            JsonDocument jsonDocument = JsonDocument.Parse(content);
            JsonElement root = jsonDocument.RootElement;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("value", out JsonElement valueElement))
            {
                if (valueElement.ValueKind == JsonValueKind.Array)
                {
                    JsonElement.ArrayEnumerator arrayEnumerator = valueElement.EnumerateArray();
                    foreach (JsonElement message in arrayEnumerator)
                    {
                        if (message.TryGetProperty("id", out JsonElement idElement))
                        {
                            messageId = idElement.GetString();
                        }

                        isDeleted = await DeleteBySubject(httpClient, message, messageId);
                        if (!isDeleted)
                            isDeleted = await DeleteByFrom(httpClient, message, messageId);
                        if (!isDeleted)
                            await DeleteByBody(httpClient, message, messageId);
                        await Task.Delay(300);
                    }
                }
                else
                {
                    Console.WriteLine("The 'value' property is not of type array.");
                }
            }
            else
            {
                Console.WriteLine("The 'value' property is not found or is not of type object.");
            }
        }
    }


    private static async Task<bool> DeleteBySubject(HttpClient httpClient, JsonElement message, string messageId)
    {
        string currentFolderPath = Directory.GetCurrentDirectory();
        string subject = message.GetProperty("subject").GetString();
        string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamSubject.txt");

        foreach (string keyword in lines)
            if (subject.ToLower().Contains(keyword.ToLower()))
            {
                await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                return true;
            }

        return false;
    }

    private static async Task<bool> DeleteByFrom(HttpClient httpClient, JsonElement message, string messageId)
    {
        string from;
        string currentFolderPath = Directory.GetCurrentDirectory();
        if (message.TryGetProperty("from", out JsonElement fromElement))
        {
            if (fromElement.TryGetProperty("emailAddress", out JsonElement emailAddressElement) &&
                emailAddressElement.TryGetProperty("name", out JsonElement nameElement))
                from = nameElement.GetString();
            else
                from = fromElement.GetString();
        }
        else
        {
            return false;
        }

        string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamFrom.txt");

        foreach (string keyword in lines)
            if (from.ToLower().Contains(keyword.ToLower()))
            {
                await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                return true;
            }

        return false;
    }

    private static async Task<bool> DeleteByBody(HttpClient httpClient, JsonElement message, string messageId)
    {
        string body;

        try
        {
            string currentFolderPath = Directory.GetCurrentDirectory();

            if (message.TryGetProperty("body", out JsonElement bodyElement))
                body = bodyElement.GetProperty("content").GetString();
            else
                body = message.GetProperty("body").GetString();


            string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamBody.txt");
            string noTags = Regex.Replace(RemoveHtmlTags(body), @"\s+", string.Empty);

            // body likely has only an image. Delete the message
            if (string.IsNullOrEmpty(noTags))
            {
                await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                return true;
            }
            else
            {
                foreach (string keyword in lines)
                    if (body.ToLower().Contains(keyword.ToLower()))
                    {
                        await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                        return true;
                    }
            }

            return false;
        }
        catch (Exception ex)
        {
            string? innerMessage = ex.InnerException?.Message;

            throw;
        }
    }

    private static string RemoveHtmlTags(string input)
    {
        return Regex.Replace(input, "<.*?>", string.Empty);
    }

    private static void BeforeAccessNotification(TokenCacheNotificationArgs args)
    {
        lock (FileLock)
        {
            if (File.Exists(CacheFilePath))
            {
                byte[] encryptedData = System.IO.File.ReadAllBytes(CacheFilePath);
                byte[]? data = ProtectedData.Unprotect(encryptedData, null, DataProtectionScope.CurrentUser);
                args.TokenCache.DeserializeMsalV3(data);
            }
        }
    }

    private static void AfterAccessNotification(TokenCacheNotificationArgs args)
    {
        if (args.HasStateChanged)
            lock (FileLock)
            {
                byte[]? data = args.TokenCache.SerializeMsalV3();
                byte[]? encryptedData = ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(CacheFilePath, encryptedData);
            }
    }
}