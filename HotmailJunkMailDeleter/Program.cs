using System.Net;

using Microsoft.Extensions.Configuration;

namespace HotmailJunkMailDeleter
{
    class Program
    {
        private static IConfiguration configuration;


        public Program()
        {
        }

        static void Main(string[] args)
        {
            Start();
        }

        private static void Start()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            configuration = builder.Build();

            
            
            string userName = configuration["Hotmail:UserName"];
            string password = configuration["Hotmail:Password"];

            Hotmail hotmail = new Hotmail(userName, password);
            hotmail.Start();

          
        }

        

        

        
    }
}