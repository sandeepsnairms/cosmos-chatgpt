﻿using System;
using System.Configuration;
using System.Xml.Linq;
using static System.Net.Mime.MediaTypeNames;


namespace CosmosDB_ChatGPT.Data
{
    public class ChatService 
    {

        private List<ChatSession> chatSessions= new List<ChatSession>();

        private readonly CosmosService cosmos;
        private readonly OpenAiService openAi;


        public ChatService(IConfiguration configuration)
        {

            cosmos = new CosmosService(configuration);
            openAi = new OpenAiService(configuration);

        }

        /**
        Data here maintained in chatSessions List object.
        Any call made here is reflected in this object and is saved to Cosmos.
        
        Calls Here
        1. Get list of Chat Sessions for left-hand nav (called when instance created).
        2. User clicks on Chat Session, go get messages for that chat session.
        3. User clicks + to create a new Chat Session.
        4. User Inputs a chat session from "New Chat" to something else.
        5. User deletes a chat session.
        6. User types a question (prompt) into web page and hits enter.
            6.1 Save prompt in chatSessions.Messages[] 
            6.2 Save response in chatSessions.Messages[]
        **/


        // Returns list of chat session ids and names for left-hand nav to bind to (display ChatSessionName and ChatSessionId as hidden)
        public async Task<List<ChatSession>> GetAllChatSessionsAsync()
        {
            chatSessions = await cosmos.GetChatSessionsListAsync();
           
            return chatSessions;
        }

        //Returns the chat messages to display on the main web page when the user selects a chat from the left-hand nav
        public async Task<List<ChatMessage>> GetChatSessionMessages(string chatSessionId)
        {
            List<ChatMessage> chatMessages = new List<ChatMessage>();

            int index = chatSessions.FindIndex(s => s.ChatSessionId == chatSessionId);
            
            if (chatSessions[index].Messages.Count == 0)
            { 
                //Messages are not cached, go read from database
                chatMessages = await cosmos.GetChatSessionMessagesAsync(chatSessionId);

                //cache results
                 chatSessions[index].Messages = chatMessages;

            }
            else
            {
                //load from cache
                chatMessages = chatSessions[index].Messages;
            }
            return chatMessages;

        }

        //User creates a new Chat Session in left-hand nav
        public async Task CreateNewChatSession()
        {
            ChatSession chatSession = new ChatSession();

            chatSessions.Add(chatSession);
            
            await cosmos.InsertChatSessionAsync(chatSession);
                       
        }

        //User Inputs a chat from "New Chat" to user defined
        public async Task RenameChatSessionAsync(string chatSessionId, string newChatSessionName)
        {
            
            int index = chatSessions.FindIndex(s => s.ChatSessionId == chatSessionId);

            chatSessions[index].ChatSessionName = newChatSessionName;

            await cosmos.UpdateChatSessionAsync(chatSessions[index]);

        }

        //User deletes a chat session from left-hand nav
        public async Task DeleteChatSessionAsync(string chatSessionId)
        {
            int index = chatSessions.FindIndex(s => s.ChatSessionId == chatSessionId);

            chatSessions.RemoveAt(index);

            await cosmos.DeleteChatSessionAsync(chatSessionId);


        }

        //User prompt to ask OpenAI a question
        public async Task<string> AskOpenAi(string chatSessionId, string prompt)
        {
            await AddPromptMessage(chatSessionId, prompt);

            string response = await openAi.PostAsync(prompt);

            await AddResponseMessage(chatSessionId, response);

            return response;

        }

        // Add human prompt to the chat session message list object and insert into Cosmos.
        private async Task AddPromptMessage(string chatSessionId, string text)
        {
            ChatMessage chatMessage = new ChatMessage(chatSessionId, "Human", text);

            int index = chatSessions.FindIndex(s => s.ChatSessionId == chatSessionId);

            chatSessions[index].AddMessage(chatMessage);

            await cosmos.InsertChatMessageAsync(chatMessage);

        }

        // Add OpenAI ressponse to the chat session message list object and insert into Cosmos.
        private async Task AddResponseMessage(string chatSessionId, string text)
        {
            ChatMessage chatMessage = new ChatMessage(chatSessionId, "OpenAI", text);

            int index = chatSessions.FindIndex(s => s.ChatSessionId == chatSessionId);

            chatSessions[index].AddMessage(chatMessage);

            await cosmos.InsertChatMessageAsync(chatMessage);

        }

    }
}
