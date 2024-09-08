using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
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
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
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
        HttpClient httpClient = new();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        HttpResponseMessage response =
            await httpClient.GetAsync(
                $"https://graph.microsoft.com/v1.0/me/mailFolders/junkemail/messages?$select=body,from, subject");

        if (response.IsSuccessStatusCode)
        {
            string content = await response.Content.ReadAsStringAsync();
            JsonDocument jsonDocument = JsonDocument.Parse(content);
            JsonElement.ArrayEnumerator messages = jsonDocument.RootElement.GetProperty("value").EnumerateArray();

            foreach (JsonElement message in messages)
            {
                string messageId = message.GetProperty("id").GetString();
                DeleteBySubject(httpClient, message, messageId);
                DeleteByFrom(httpClient, message, messageId);
                DeleteByBody(httpClient, message, messageId);
                await Task.Delay(300);
            }
        }
    }


    private static async Task DeleteBySubject(HttpClient httpClient, JsonElement message, string messageId)
    {
        string currentFolderPath = Directory.GetCurrentDirectory();
        string subject = message.GetProperty("subject").GetString();
        string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamSubject.txt");

        foreach (string keyword in lines)
            if (subject.ToLower().Contains(keyword.ToLower()))
            {
                await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                return;
            }
    }

    private static async Task DeleteByFrom(HttpClient httpClient, JsonElement message, string messageId)
    {
        string from;
        string currentFolderPath = Directory.GetCurrentDirectory();
        if (message.TryGetProperty("from", out JsonElement fromElement))
        {
            if (fromElement.TryGetProperty("emailAddress", out JsonElement emailAddressElement) &&
                emailAddressElement.TryGetProperty("name", out JsonElement nameElement))
            {
                from = nameElement.GetString();
            }
            else
            {
                from = fromElement.GetString();
            }
            
        }
        else
        {
            return;
        }
        //string from = message.GetProperty("from").GetString();
        string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamFrom.txt");

        foreach (string keyword in lines)
            if (from.ToLower().Contains(keyword.ToLower()))
            {
                await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                return;
            }
    }

    private static async Task DeleteByBody(HttpClient httpClient, JsonElement message, string messageId)
    {
        string currentFolderPath = Directory.GetCurrentDirectory();
        string body = message.GetProperty("body").GetString();
        string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamBody.txt");

        JsonElement bodyElement = message.GetProperty("body");
        string content = bodyElement.GetProperty("content").GetString();
        string noTags = Regex.Replace(RemoveHtmlTags(content), @"\s+", string.Empty);

        // body likely has only an image. Delete the message
        if (string.IsNullOrEmpty(noTags))
            await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
        else
            foreach (string keyword in lines)
                if (body.ToLower().Contains(keyword.ToLower()))
                {
                    await httpClient.DeleteAsync($"https://graph.microsoft.com/v1.0/me/messages/{messageId}");
                    return;
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
            if (System.IO.File.Exists(CacheFilePath))
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
                System.IO.File.WriteAllBytes(CacheFilePath, encryptedData);
            }
    }
}