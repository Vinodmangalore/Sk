﻿// Copyright (c) Microsoft. All rights reserved.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.Planning.Handlebars;
using Microsoft.SemanticKernel.Plugins.OpenApi;
using Plugins.DictionaryPlugin;
using RepoUtils;
using xRetry;
using Xunit;
using Xunit.Abstractions;

namespace Examples;

// This example shows how to use the Handlebars sequential planner.
public class Example65_HandlebarsPlanner : BaseTest
{
    private static int s_sampleIndex;

    private const string CourseraPluginName = "CourseraPlugin";

    private void WriteSampleHeading(string name)
    {
        WriteLine($"======== [Handlebars Planner] Sample {s_sampleIndex++} - Create and Execute Plan with: {name} ========");
    }
    private async Task RunSampleAsync(string goal, bool shouldPrintPrompt = false, params string[] pluginDirectoryNames)
    {
        string apiKey = TestConfiguration.AzureOpenAI.ApiKey;
        string chatDeploymentName = TestConfiguration.AzureOpenAI.ChatDeploymentName;
        string chatModelId = TestConfiguration.AzureOpenAI.ChatModelId;
        string endpoint = TestConfiguration.AzureOpenAI.Endpoint;

        if (apiKey == null || chatDeploymentName == null || chatModelId == null || endpoint == null)
        {
            WriteLine("Azure endpoint, apiKey, deploymentName, or modelId not found. Skipping example.");
            return;
        }

        var kernel = Kernel.CreateBuilder()
            .AddAzureOpenAIChatCompletion(
                deploymentName: chatDeploymentName,
                endpoint: endpoint,
                serviceId: "AzureOpenAIChat",
                apiKey: apiKey,
                modelId: chatModelId)
            .Build();

        if (pluginDirectoryNames.Length > 0)
        {
            if (pluginDirectoryNames[0] == StringParamsDictionaryPlugin.PluginName)
            {
                kernel.ImportPluginFromType<StringParamsDictionaryPlugin>(StringParamsDictionaryPlugin.PluginName);
            }
            else if (pluginDirectoryNames[0] == ComplexParamsDictionaryPlugin.PluginName)
            {
                kernel.ImportPluginFromType<ComplexParamsDictionaryPlugin>(ComplexParamsDictionaryPlugin.PluginName);
            }
            else if (pluginDirectoryNames[0] == CourseraPluginName)
            {
                await kernel.ImportPluginFromOpenApiAsync(
                    CourseraPluginName,
                    new Uri("https://www.coursera.org/api/rest/v1/search/openapi.yaml")
                );
            }
            else
            {
                string folder = RepoFiles.SamplePluginsPath();

                foreach (var pluginDirectoryName in pluginDirectoryNames)
                {
                    kernel.ImportPluginFromPromptDirectory(Path.Combine(folder, pluginDirectoryName));
                }
            }
        }

        // Use gpt-4 or newer models if you want to test with loops. 
        // Older models like gpt-35-turbo are less recommended. They do handle loops but are more prone to syntax errors.
        var allowLoopsInPlan = chatDeploymentName.Contains("gpt-4", StringComparison.OrdinalIgnoreCase);
        var planner = new HandlebarsPlanner(
            new HandlebarsPlannerOptions()
            {
                // Change this if you want to test with loops regardless of model selection.
                AllowLoops = allowLoopsInPlan
            });

        WriteLine($"Goal: {goal}");

        // Create the plan
        var plan = await planner.CreatePlanAsync(kernel, goal);

        // Print the prompt template
        if (shouldPrintPrompt && plan.Prompt is not null)
        {
            WriteLine($"\nPrompt template:\n{plan.Prompt}");
        }

        WriteLine($"\nOriginal plan:\n{plan}");

        // Execute the plan
        var result = await plan.InvokeAsync(kernel);
        WriteLine($"\nResult:\n{result}\n");
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(false)]
    public async Task PlanNotPossibleSampleAsync(bool shouldPrintPrompt = false)
    {
        WriteSampleHeading("Plan Not Possible");

        try
        {
            // Load additional plugins to enable planner but not enough for the given goal.
            await RunSampleAsync("Send Mary an email with the list of meetings I have scheduled today.", shouldPrintPrompt, "SummarizePlugin");
        }
        catch (KernelException ex) when (
            ex.Message.Contains(nameof(HandlebarsPlannerErrorCodes.InsufficientFunctionsForGoal), StringComparison.CurrentCultureIgnoreCase)
            || ex.Message.Contains(nameof(HandlebarsPlannerErrorCodes.HallucinatedHelpers), StringComparison.CurrentCultureIgnoreCase)
            || ex.Message.Contains(nameof(HandlebarsPlannerErrorCodes.InvalidTemplate), StringComparison.CurrentCultureIgnoreCase))
        {
            /*
                Unable to create plan for goal with available functions.
                Goal: Email me a list of meetings I have scheduled today.
                Available Functions: SummarizePlugin-Notegen, SummarizePlugin-Summarize, SummarizePlugin-MakeAbstractReadable, SummarizePlugin-Topics
                Planner output:
                I'm sorry, but it seems that the provided helpers do not include any helper to fetch or filter meetings scheduled for today. 
                Therefore, I cannot create a Handlebars template to achieve the specified goal with the available helpers. 
                Additional helpers may be required.
            */
            WriteLine($"\n{ex.Message}\n");
        }
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(true)]

    public Task RunCourseraSampleAsync(bool shouldPrintPrompt = false)
    {
        this.WriteSampleHeading("Coursera OpenAPI Plugin");
        return RunSampleAsync("Show me courses about Artificial Intelligence.", shouldPrintPrompt, CourseraPluginName);
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(false)]
    public Task RunDictionaryWithBasicTypesSampleAsync(bool shouldPrintPrompt = false)
    {
        this.WriteSampleHeading("Basic Types using Local Dictionary Plugin");
        return RunSampleAsync("Get a random word and its definition.", shouldPrintPrompt, StringParamsDictionaryPlugin.PluginName);
        /*
            Original plan:
            {{!-- Step 1: Get a random word --}}
            {{set "randomWord" (DictionaryPlugin-GetRandomWord)}}

            {{!-- Step 2: Get the definition of the random word --}}
            {{set "definition" (DictionaryPlugin-GetDefinition word=(get "randomWord"))}}

            {{!-- Step 3: Output the random word and its definition --}}
            {{json (array (get "randomWord") (get "definition"))}}

            Result:
            ["book","a set of printed or written pages bound together along one edge"]
        */
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(true)]
    public Task RunLocalDictionaryWithComplexTypesSampleAsync(bool shouldPrintPrompt = false)
    {
        this.WriteSampleHeading("Complex Types using Local Dictionary Plugin");
        return RunSampleAsync("Teach me two random words and their definition.", shouldPrintPrompt, ComplexParamsDictionaryPlugin.PluginName);
        /*
            Original Plan:
            {{!-- Step 1: Get two random dictionary entries --}}
            {{set "entry1" (DictionaryPlugin-GetRandomEntry)}}
            {{set "entry2" (DictionaryPlugin-GetRandomEntry)}}

            {{!-- Step 2: Extract words from the entries --}}
            {{set "word1" (DictionaryPlugin-GetWord entry=(get "entry1"))}}
            {{set "word2" (DictionaryPlugin-GetWord entry=(get "entry2"))}}

            {{!-- Step 3: Extract definitions for the words --}}
            {{set "definition1" (DictionaryPlugin-GetDefinition word=(get "word1"))}}
            {{set "definition2" (DictionaryPlugin-GetDefinition word=(get "word2"))}}

            {{!-- Step 4: Display the words and their definitions --}}
            Word 1: {{json (get "word1")}}
            Definition: {{json (get "definition1")}}

            Word 2: {{json (get "word2")}}
            Definition: {{json (get "definition2")}}

            Result:
            Word 1: apple
            Definition 1: a round fruit with red, green, or yellow skin and a white flesh

            Word 2: dog
            Definition 2: a domesticated animal with four legs, a tail, and a keen sense of smell that is often used for hunting or companionship
        */
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(false)]
    public Task RunPoetrySampleAsync(bool shouldPrintPrompt = false)
    {
        this.WriteSampleHeading("Multiple Plugins");
        return RunSampleAsync("Write a poem about John Doe, then translate it into Italian.", shouldPrintPrompt, "SummarizePlugin", "WriterPlugin");
        /*
            Original plan:
            {{!-- Step 1: Initialize the scenario for the poem --}}
            {{set "scenario" "John Doe, a mysterious and kind-hearted person"}}

            {{!-- Step 2: Generate a short poem about John Doe --}}
            {{set "poem" (WriterPlugin-ShortPoem input=(get "scenario"))}}

            {{!-- Step 3: Translate the poem into Italian --}}
            {{set "translatedPoem" (WriterPlugin-Translate input=(get "poem") language="Italian")}}

            {{!-- Step 4: Output the translated poem --}}
            {{json (get "translatedPoem")}}

            Result:
            C'era una volta un uomo di nome John Doe,
            La cui gentilezza si mostrava costantemente,
            Aiutava con un sorriso,
            E non si arrendeva mai,
            Al mistero che lo faceva brillare.
        */
    }

    [RetryTheory(typeof(HttpOperationException))]
    [InlineData(false)]
    public Task RunBookSampleAsync(bool shouldPrintPrompt = false)
    {
        this.WriteSampleHeading("Loops and Conditionals");
        return RunSampleAsync("Create a book with 3 chapters about a group of kids in a club called 'The Thinking Caps.'", shouldPrintPrompt, "WriterPlugin", "MiscPlugin");
        /*
            Original plan:
            {{!-- Step 1: Initialize the book title and chapter count --}}
            {{set "bookTitle" "The Thinking Caps"}}
            {{set "chapterCount" 3}}

            {{!-- Step 2: Generate the novel outline with the given chapter count --}}
            {{set "novelOutline" (WriterPlugin-NovelOutline input=(get "bookTitle") chapterCount=(get "chapterCount"))}}

            {{!-- Step 3: Loop through the chapters and generate the content for each chapter --}}
            {{#each (range 1 (get "chapterCount"))}}
                {{set "chapterIndex" this}}
                {{set "chapterSynopsis" (MiscPlugin-ElementAtIndex input=(get "novelOutline") index=(get "chapterIndex"))}}
                {{set "previousChapterSynopsis" (MiscPlugin-ElementAtIndex input=(get "novelOutline") index=(get "chapterIndex" - 1))}}
                
                {{!-- Step 4: Write the chapter content using the WriterPlugin-NovelChapter helper --}}
                {{set "chapterContent" (WriterPlugin-NovelChapter input=(get "chapterSynopsis") theme=(get "bookTitle") previousChapter=(get "previousChapterSynopsis") chapterIndex=(get "chapterIndex"))}}
                
                {{!-- Step 5: Output the chapter content --}}
                {{json (get "chapterContent")}}
            {{/each}}
        */
    }

    public Example65_HandlebarsPlanner(ITestOutputHelper output) : base(output)
    {
    }
}