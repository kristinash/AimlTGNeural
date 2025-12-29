using AIMLbot;
using System.Collections.Generic;

namespace AIMLTGBot
{
    public class AIMLService
    {
        readonly Bot bot;
        readonly Dictionary<long, User> users = new Dictionary<long, User>();

        public AIMLService()
        {
            bot = new Bot();
            bot.loadSettings();
            bot.isAcceptingUserInput = false;
            bot.loadAIMLFromFiles();
            bot.isAcceptingUserInput = true;
        }

        public string Talk(long userId, string userName, string phrase)
        {
            var result = "";
            User user;
            if (!users.ContainsKey(userId))
            {
                user = new User(userId.ToString(), bot);
                users.Add(userId, user);
                user.Predicates.addSetting("username", userName);
            }
            else
            {
                user = users[userId];
            }
            result += bot.Chat(new Request(phrase, user, bot)).Output;
            return result;
        }
    }
}

