# HotmailJunkMailDeleter
Deletes messages from Hotmail's Junk Email folder using keywords

#### 
##### Why was it built:
I created it because I was noticing the same few spam messages appearing very often in my Hotmail Junk Email folder and after months of seeing them, I decided I didn't want to see them anymore. 
It's unfortunate that the custom rules that users can create in Hotmail do not get applied to the Junk folder. So you're stuck with deleting them manually or waiting 10 days for Hotmail to delete them for you. Fewer messages in the junk folder means less time spent scanning the folder for valid messages. The more keywords to scan against and the higher frequency the app is running, the fewer spam you'll have in the junk email folder.

I have this app running once every hour on my home computer through the task scheduler. My junk folder rarely has more than 5 messages. Hotmail still sometimes puts valid messages in the junk folder.
Since this app is .NET core based, it might run on Linux and MacOS. I use Windows only.


#### How to use:

* The first version of this app used IMAP and it seems Hotmail disabled this type of access.
* Create an app registration in Azure Portal using the Hotmail account this app is going to use.
* Add a redirect url = http://localhost:8888. Starting in the Overview left menu.
* Copy the ClientId of the registered app and paste in the appsettings.json file.
* The app uses 3 text files which contain the keywords to delete messages against:
    * SpamBody.txt. If a message's body contains any of the words in the file, message gets deleted. Also if a message contains no text, it gets deleted. That's a sign it's spam.
    * SpamSubject.txt. If a message's subject contains any of the words in the file, message gets deleted.
    * SpamFrom.txt. If a message's from field contains any of the words in the file, message gets deleted.
* Review the files and remove any word that you think is too broad and valid messages might get deleted.
    
Download the release zip file and unzip it in a folder. 
You can use Windows' Task Scheduler to have it run automatically for you. As often as you want. Make sure when you specify the HotmailJunkMailDeleter.exe file to specify also the 'Start in' folder and select the folder where HotmailJunkMailDeleter.exe resides in.  

#### Roadmap:  
The app is basically complete for my needs.   
Open an issue if you have a question, an issue/bug or an enhancement request.  
  
