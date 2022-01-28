# HotmailJunkMailDeleter
Delete junk messages from Hotmail's Junk Email folder using keywords


  
#### What does the app do:
This is a little .NET 6.0 console app that deletes messages in Hotmail's Junk Email folder based on keywords in the from field or subject line or in the message body.   
#### 
##### Why was it built:
I created it because I was noticing the same few spam messages appearing very often in my Hotmail Junk Email folder and after months of seeing them, I decided I didn't want to see them anymore. 
It's unfortunate that the custom rules that users can create in Hotmail do not get applied to the Junk folder. So you're stuck with deleting them manually or waiting 10 days for Hotmail to delete them for you. Fewer messages in the junk folder means less time spent scanning the folder for valid messages. The more keywords to scan against and the higher frequency the app is running, the fewer spam you'll have in the junk email folder.

#### How to use:

Place your Hotmail username and password in the appsettings.json file.

Messages to be deleted that contain a keyword or phrase in the subject line are placed in the SpamSubject.txt file. One keyword/phrase per line.  
Messages to be deleted that contain a keyword or phrase in the From field (sender) are placed in the SpamFrom.txt file. One keyword/phrase per line.
Messages to be deleted that contain a keyword or phrase in the message body are placed in the SpamBody.txt file. One keyword/phrase per line.

Compile the application or unzip the release into a folder on your computer.
You can use Windows' Task Scheduler to have it run automatically for you. As often as you want. Make sure when you specify the HotmailJunkMailDeleter.exe file to specify also the 'Start in' folder and select the folder where HotmailJunkMailDeleter.exe resides in.  

#### Special thanks:  
Special thanks to the creators and maintainers of MailKit.  
  
#### Roadmap:  
The app is basically complete for my needs.   
Open an issue if you have a question, an issue/bug or an enhancement request.  
  
