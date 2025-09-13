namespace demo_M365_agent
{
    public class ConfigOptions
    {
        public OpenAIConfigOptions OpenAI { get; set; }
    }

    /// <summary>
    /// Options for Open AI
    /// </summary>
    public class OpenAIConfigOptions
    {
        public string ApiKey { get; set; }
        public string DefaultModel = "gpt-3.5-turbo";
    }
}