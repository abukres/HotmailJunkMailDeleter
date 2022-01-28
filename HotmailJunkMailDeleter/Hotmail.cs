using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;

namespace HotmailJunkMailDeleter
{
    public class Hotmail
    {
        private string _userName;
        private string _password;
        private string _host = "imap-mail.outlook.com";
        private int _portNumber = 993;

        public Hotmail(string userName, string password)
        {
            _userName = userName;
            _password = password;
        }


        public void Start()
        {
            using (var client = new ImapClient())
            {
                using (var cancel = new CancellationTokenSource())
                {
                    client.Connect(_host, _portNumber, true, cancel.Token);
                    client.AuthenticationMechanisms.Remove("XOAUTH");
                    client.Authenticate(_userName, _password, cancel.Token);

                    IMailFolder? junkFolder = null;
                    foreach (var folder in client.GetFolders(client.PersonalNamespaces[0]))
                    {
                        if (folder.Name == "Junk")
                        {
                            junkFolder = folder;
                            junkFolder.Open(FolderAccess.ReadWrite, cancel.Token);

                            // if the only way to get a uid is by running a query, then the process of deleting is not very efficient
                            // it would be better if the uid is a property of the message but maybe it's an IMAP limitation
                            // Hotmail is giving BAD response with using SearchQuery.HasKeywords

                            DeleteByBody(junkFolder);
                            DeleteByFrom(junkFolder);
                            DeleteBySubject(junkFolder);

                            junkFolder.Expunge();
                        }
                    }

                    client.Disconnect(true, cancel.Token);
                }
            }
        }

        private void DeleteBySubject(IMailFolder junkFolder)
        {
            var currentFolderPath = Directory.GetCurrentDirectory();

            string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamSubject.txt");

            foreach (var keyword in lines)
            {
                var query = SearchQuery.SubjectContains(keyword);
                var uids = junkFolder.Search(query);
                foreach (var uid in uids)
                {
                    junkFolder.AddFlags(uid, MessageFlags.Deleted, true);
                }
            }
        }

        private void DeleteByFrom(IMailFolder junkFolder)
        {
            var currentFolderPath = Directory.GetCurrentDirectory();

            string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamFrom.txt");

            foreach (var keyword in lines)
            {
                var query = SearchQuery.FromContains(keyword);
                var uids = junkFolder.Search(query);
                foreach (var uid in uids)
                {
                    junkFolder.AddFlags(uid, MessageFlags.Deleted, true);
                }
            }
        }

        private void DeleteByBody(IMailFolder junkFolder)
        {
            // if you want to search by multiple keywords, Mailkit supports binary searches using OR and AND. Read the docs.
            var currentFolderPath = Directory.GetCurrentDirectory();

            string[] lines = File.ReadAllLines($@"{currentFolderPath}\SpamBody.txt");

            foreach (var keyword in lines)
            {
                var query = SearchQuery.BodyContains(keyword);
                var uids = junkFolder.Search(query);
                foreach (var uid in uids)
                {
                    junkFolder.AddFlags(uid, MessageFlags.Deleted, true);
                }
            }
        }
    }
}