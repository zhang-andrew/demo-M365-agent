﻿using demoM365Agent.Bot.Plugins;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Agents;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using System.Text;
using System.Text.Json.Nodes;

namespace demoM365Agent.Bot.Agents;

public class WeatherForecastAgent
{
    private readonly Kernel _kernel;
    private readonly ChatCompletionAgent _agent;

    private const string AgentName = "WeatherForecastAgent";
    private const string AgentInstructions = """
        You are a friendly assistant that helps people find a weather forecast for a given time and place.
        You may ask follow up questions until you have enough information to answer the customers question,
        but once you have a forecast forecast, make sure to format it nicely using an adaptive card.
        You should use adaptive JSON format to display the information in a visually appealing way and include a button for more details that points at https://www.msn.com/en-us/weather/forecast/in-{location}
        You should use adaptive cards version 1.5 or later.

        Respond in JSON format with the following JSON schema:
        
        {
            "contentType": "'Text' or 'AdaptiveCard' only",
            "content": "{The content of the response, may be plain text, or JSON based adaptive card}"
        }
        """;

    /// <summary>
    /// Initializes a new instance of the <see cref="WeatherForecastAgent"/> class.
    /// </summary>
    /// <param name="kernel">An instance of <see cref="Kernel"/> for interacting with an LLM.</param>
    public WeatherForecastAgent(Kernel kernel, IServiceProvider service)
    {
        _kernel = kernel;

        // Define the agent
        _agent =
            new()
            {
                Instructions = AgentInstructions,
                Name = AgentName,
                Kernel = _kernel,
                Arguments = new KernelArguments(new OpenAIPromptExecutionSettings()
                {
                    FunctionChoiceBehavior = FunctionChoiceBehavior.Auto(),
                    ResponseFormat = "json_object"
                }),
            };

        // Give the agent some tools to work with
        _agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<DateTimePlugin>(serviceProvider: service));
        _agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<WeatherForecastPlugin>(serviceProvider: service));
        _agent.Kernel.Plugins.Add(KernelPluginFactory.CreateFromType<AdaptiveCardPlugin>(serviceProvider: service));
    }

    /// <summary>
    /// Invokes the agent with the given input and returns the response.
    /// </summary>
    /// <param name="input">A message to process.</param>
    /// <returns>An instance of <see cref="WeatherForecastAgentResponse"/></returns>
    public async Task<WeatherForecastAgentResponse> InvokeAgentAsync(string input, ChatHistory chatHistory)
    {
        ArgumentNullException.ThrowIfNull(chatHistory);
        AgentThread thread = new ChatHistoryAgentThread();
        ChatMessageContent message = new(AuthorRole.User, input);
        chatHistory.Add(message);

        StringBuilder sb = new();
        // 
        await foreach (ChatMessageContent response in this._agent.InvokeAsync(chatHistory, thread: thread))
        {
            chatHistory.Add(response);
            sb.Append(response.Content);
        }

        // Make sure the response is in the correct format and retry if necessary
        try
        {
            string resultContent = sb.ToString();
            var jsonNode = JsonNode.Parse(resultContent);
            WeatherForecastAgentResponse result = new WeatherForecastAgentResponse()
            {
                Content = jsonNode["content"].ToString(),
                ContentType = Enum.Parse<WeatherForecastAgentResponseContentType>(jsonNode["contentType"].ToString(), true)
            };
            return result;
        }
        catch (Exception je)
        {
            return await InvokeAgentAsync($"That response did not match the expected format. Please try again. Error: {je.Message}", chatHistory);
        }
    }
}
