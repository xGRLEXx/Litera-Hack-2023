﻿using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Teams.AI;
using Microsoft.Teams.AI.AI;
using Microsoft.Teams.AI.AI.Moderator;
using Microsoft.Teams.AI.AI.Planner;
using Microsoft.Teams.AI.AI.Prompt;
using Microsoft.Teams.AI.State;
using AssistBot;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddHttpClient("WebClient", client => client.Timeout = TimeSpan.FromSeconds(600));
builder.Services.AddHttpContextAccessor();

// Prepare Configuration for ConfigurationBotFrameworkAuthentication
var config = builder.Configuration.Get<ConfigOptions>()!;
builder.Configuration["MicrosoftAppType"] = "MultiTenant";
builder.Configuration["MicrosoftAppId"] = config.BOT_ID;
builder.Configuration["MicrosoftAppPassword"] = config.BOT_PASSWORD;

// Create the Bot Framework Authentication to be used with the Bot Adapter.
builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

// Create the Cloud Adapter with error handling enabled.
// Note: some classes expect a BotAdapter and some expect a BotFrameworkHttpAdapter, so
// register the same adapter instance for all types.
builder.Services.AddSingleton<CloudAdapter, AdapterWithErrorHandler>();
builder.Services.AddSingleton<IBotFrameworkHttpAdapter>(sp => sp.GetService<CloudAdapter>()!);
builder.Services.AddSingleton<BotAdapter>(sp => sp.GetService<CloudAdapter>()!);

builder.Services.AddSingleton<IStorage, MemoryStorage>();

#region Use Azure OpenAI and Azure Content Safety
if (config.Azure == null
    || string.IsNullOrEmpty(config.Azure.OpenAIApiKey)
    || string.IsNullOrEmpty(config.Azure.OpenAIEndpoint)
    || string.IsNullOrEmpty(config.Azure.ContentSafetyApiKey)
    || string.IsNullOrEmpty(config.Azure.ContentSafetyEndpoint))
{
    throw new Exception("Missing Azure configuration.");
}

builder.Services.AddSingleton(_ => new AzureOpenAIPlannerOptions(config.Azure.OpenAIApiKey, "ai-customer-support-assistant-model", config.Azure.OpenAIEndpoint)
{
    LogRequests = true
});
builder.Services.AddSingleton(_ => new AzureContentSafetyModeratorOptions(config.Azure.ContentSafetyApiKey, config.Azure.ContentSafetyEndpoint, ModerationType.Both));

// Create the Application.
builder.Services.AddTransient<IBot>(sp =>
{
    ILoggerFactory loggerFactory = sp.GetService<ILoggerFactory>()!;

    IPromptManager<TurnState> promptManager = new PromptManager<TurnState>("./Prompts");
    IPlanner<TurnState> planner = new AzureOpenAIPlanner<TurnState>(sp.GetService<AzureOpenAIPlannerOptions>()!, loggerFactory);
    IModerator<TurnState> moderator = new AzureContentSafetyModerator<TurnState>(sp.GetService<AzureContentSafetyModeratorOptions>()!);

    ApplicationOptions<TurnState, TurnStateManager> applicationOptions = new()
    {
        AI = new AIOptions<TurnState>(planner, promptManager)
        {
            Moderator = moderator,
            Prompt = "Chat",
            History = new AIHistoryOptions()
            {
                AssistantHistoryType = AssistantHistoryType.Text
            }
        },
        Storage = sp.GetService<IStorage>(),
        LoggerFactory = loggerFactory
    };

    Application<TurnState, TurnStateManager> app = new(applicationOptions);

    // Register AI actions
    app.AI.ImportActions(new ActionHandlers());

    // Listen for user to say "/history".
    app.OnMessage("/history", ActivityHandlers.HistoryMessageHandler);

    return app;
});
#endregion

#region Use OpenAI
/**
if (config.OpenAI == null || string.IsNullOrEmpty(config.OpenAI.ApiKey))
{
    throw new Exception("Missing OpenAI configuration.");
}

builder.Services.AddSingleton(_ => new OpenAIPlannerOptions(config.OpenAI.ApiKey, "text-davinci-003")
{
    LogRequests = true
});
builder.Services.AddSingleton(_ => new OpenAIModeratorOptions(config.OpenAI.ApiKey, ModerationType.Both));

// Create the Application.
builder.Services.AddTransient<IBot>(sp =>
{
    ILoggerFactory loggerFactory = sp.GetService<ILoggerFactory>()!;

    IPromptManager<TurnState> promptManager = new PromptManager<TurnState>("./Prompts");
    IPlanner<TurnState> planner = new OpenAIPlanner<TurnState>(sp.GetService<OpenAIPlannerOptions>()!, loggerFactory);
    IModerator<TurnState> moderator = new OpenAIModerator<TurnState>(sp.GetService<OpenAIModeratorOptions>()!, loggerFactory);

    ApplicationOptions<TurnState, TurnStateManager> applicationOptions = new()
    {
        AI = new AIOptions<TurnState>(planner, promptManager)
        {
            Moderator = moderator,
            Prompt = "Chat",
            History = new AIHistoryOptions()
            {
                AssistantHistoryType = AssistantHistoryType.Text
            }
        },
        Storage = sp.GetService<IStorage>(),
        LoggerFactory = loggerFactory
    };

    Application<TurnState, TurnStateManager> app = new(applicationOptions);

    // Register AI actions
    app.AI.ImportActions(new ActionHandlers());

    // Listen for user to say "/history".
    app.OnMessage("/history", ActivityHandlers.HistoryMessageHandler);

    return app;
});
**/
#endregion

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseStaticFiles();
app.UseRouting();
app.MapControllers();

app.Run();
