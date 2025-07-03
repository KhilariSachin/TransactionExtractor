# Azure Functions End-to-End Implementation Guide

## Overview
This guide will help you create an Azure Functions app that calls your `RefreshApplicationData` API endpoint every day at 7:00 AM CET.

## Prerequisites
- Visual Studio 2022 or Visual Studio Code
- Azure Functions Core Tools
- .NET 6 SDK
- Azure subscription
- Your existing .NET Core 6 Web API deployed to Azure

## Step 1: Create Azure Functions Project

### Option A: Using Visual Studio 2022
1. Open Visual Studio 2022
2. Create a new project → "Azure Functions"
3. Project name: `RefreshDataScheduler`
4. Target Framework: `.NET 6.0`
5. Functions worker: `.NET 6.0`
6. Function template: `Timer trigger`
7. Schedule: `0 0 5 * * *` (5 AM UTC = 7 AM CET)

### Option B: Using Azure Functions Core Tools (Command Line)
```bash
# Install Azure Functions Core Tools (if not already installed)
npm install -g azure-functions-core-tools@4 --unsafe-perm true

# Create new function app
func init RefreshDataScheduler --dotnet
cd RefreshDataScheduler

# Add timer trigger function
func new --name RefreshApplicationData --template "Timer trigger"
```

### Option C: Using .NET CLI
```bash
# Create new console app
dotnet new console -n RefreshDataScheduler
cd RefreshDataScheduler

# Add Azure Functions packages
dotnet add package Microsoft.Azure.Functions.Worker
dotnet add package Microsoft.Azure.Functions.Worker.Sdk
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Timer
```

## Step 2: Project Structure and Files

### Project File (.csproj)
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net6.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="1.10.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="1.7.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.Timer" Version="4.1.0" />
    <PackageReference Include="Microsoft.ApplicationInsights.WorkerService" Version="2.21.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.ApplicationInsights" Version="1.0.0" />
  </ItemGroup>
  <ItemGroup>
    <None Update="host.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
    <None Update="local.settings.json">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>
</Project>
```

### Program.cs
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        services.AddApplicationInsightsTelemetryWorkerService();
        services.ConfigureFunctionsApplicationInsights();
        
        // Add HttpClient
        services.AddHttpClient();
    })
    .Build();

host.Run();
```

### RefreshApplicationData.cs (Main Function)
```csharp
using System.Net.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace RefreshDataScheduler
{
    public class RefreshApplicationData
    {
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        public RefreshApplicationData(ILoggerFactory loggerFactory, IHttpClientFactory httpClientFactory)
        {
            _logger = loggerFactory.CreateLogger<RefreshApplicationData>();
            _httpClient = httpClientFactory.CreateClient();
        }

        [Function("RefreshApplicationData")]
        public async Task Run([TimerTrigger("0 0 5 * * *")] TimerInfo myTimer)
        {
            _logger.LogInformation("RefreshApplicationData function started at: {time}", DateTime.UtcNow);

            try
            {
                // Get the base URL from environment variables
                var baseUrl = Environment.GetEnvironmentVariable("WEB_API_BASE_URL");
                
                if (string.IsNullOrEmpty(baseUrl))
                {
                    _logger.LogError("WEB_API_BASE_URL environment variable not set");
                    throw new InvalidOperationException("WEB_API_BASE_URL environment variable is required");
                }
                
                var apiEndpoint = $"{baseUrl.TrimEnd('/')}/api/RefreshApplicationData";
                _logger.LogInformation("Calling API endpoint: {endpoint}", apiEndpoint);

                // Configure request timeout (5 minutes)
                using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
                
                // Add authentication header if needed
                var apiKey = Environment.GetEnvironmentVariable("API_KEY");
                if (!string.IsNullOrEmpty(apiKey))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
                }

                // Make the API call
                var response = await _httpClient.PostAsync(apiEndpoint, null, cts.Token);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("API call successful. Status: {status}, Response: {response}", 
                        response.StatusCode, content);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("API call failed. Status: {status}, Error: {error}", 
                        response.StatusCode, errorContent);
                    throw new HttpRequestException($"API call failed with status {response.StatusCode}: {errorContent}");
                }
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError("Request timeout: {message}", ex.Message);
                throw;
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError("HTTP request exception: {message}", ex.Message);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError("Unexpected exception: {message}", ex.Message);
                throw;
            }
            finally
            {
                _logger.LogInformation("RefreshApplicationData function completed at: {time}", DateTime.UtcNow);
            }

            // Log next scheduled run
            if (myTimer.ScheduleStatus is not null)
            {
                _logger.LogInformation("Next timer schedule at: {time}", myTimer.ScheduleStatus.Next);
            }
        }
    }
}
```

### host.json
```json
{
  "version": "2.0",
  "logging": {
    "applicationInsights": {
      "samplingSettings": {
        "isEnabled": true,
        "excludedTypes": "Request"
      }
    }
  },
  "functionTimeout": "00:10:00",
  "retry": {
    "strategy": "exponentialBackoff",
    "maxRetryCount": 3,
    "minimumInterval": "00:00:05",
    "maximumInterval": "00:00:30"
  }
}
```

### local.settings.json (for local development)
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "WEB_API_BASE_URL": "https://your-webapp-name.azurewebsites.net",
    "API_KEY": "your-api-key-if-needed"
  }
}
```

## Step 3: Azure Resource Creation

### Using Azure Portal
1. **Create Resource Group** (if not exists)
   - Go to Azure Portal
   - Create Resource Group: `rg-refresh-scheduler`

2. **Create Function App**
   - Resource Group: `rg-refresh-scheduler`
   - Function App name: `func-refresh-scheduler-[unique-suffix]`
   - Runtime stack: `.NET`
   - Version: `6 (LTS)`
   - Region: Same as your Web API
   - Hosting: Consumption (Serverless)

3. **Create Application Insights** (optional but recommended)
   - Will be created automatically with Function App
   - Or create separately for better control

### Using Azure CLI
```bash
# Login to Azure
az login

# Create resource group
az group create --name rg-refresh-scheduler --location eastus

# Create storage account (required for functions)
az storage account create \
  --name strefreshscheduler123 \
  --resource-group rg-refresh-scheduler \
  --location eastus \
  --sku Standard_LRS

# Create function app
az functionapp create \
  --resource-group rg-refresh-scheduler \
  --consumption-plan-location eastus \
  --runtime dotnet-isolated \
  --runtime-version 6 \
  --functions-version 4 \
  --name func-refresh-scheduler-123 \
  --storage-account strefreshscheduler123 \
  --app-insights func-refresh-scheduler-123
```

## Step 4: Deployment

### Option A: Visual Studio Deployment
1. Right-click project → "Publish"
2. Target: "Azure"
3. Specific target: "Azure Function App (Windows)" or "Azure Function App (Linux)"
4. Select your Function App
5. Click "Publish"

### Option B: Azure Functions Core Tools
```bash
# Build the project
dotnet build

# Deploy to Azure
func azure functionapp publish func-refresh-scheduler-123
```

### Option C: GitHub Actions (CI/CD)
Create `.github/workflows/deploy-function.yml`:

```yaml
name: Deploy Azure Function

on:
  push:
    branches: [ main ]
    paths: [ 'RefreshDataScheduler/**' ]

env:
  AZURE_FUNCTIONAPP_NAME: 'func-refresh-scheduler-123'
  AZURE_FUNCTIONAPP_PACKAGE_PATH: './RefreshDataScheduler'
  DOTNET_VERSION: '6.0.x'

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest
    steps:
    - name: 'Checkout GitHub Action'
      uses: actions/checkout@v3

    - name: 'Setup DotNet Environment'
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: ${{ env.DOTNET_VERSION }}

    - name: 'Resolve Project Dependencies'
      shell: bash
      run: |
        pushd './${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}'
        dotnet build --configuration Release --output ./output
        popd

    - name: 'Run Azure Functions Action'
      uses: Azure/functions-action@v1
      with:
        app-name: ${{ env.AZURE_FUNCTIONAPP_NAME }}
        package: '${{ env.AZURE_FUNCTIONAPP_PACKAGE_PATH }}/output'
        publish-profile: ${{ secrets.AZURE_FUNCTIONAPP_PUBLISH_PROFILE }}
```

## Step 5: Azure Configuration

### Configure Application Settings
In Azure Portal → Function App → Configuration → Application settings:

| Setting Name | Value | Description |
|-------------|-------|-------------|
| `WEB_API_BASE_URL` | `https://your-webapp-name.azurewebsites.net` | Your Web API URL |
| `API_KEY` | `your-api-key` | If authentication is required |
| `WEBSITE_TIME_ZONE` | `Central Europe Standard Time` | For better logging |

### Configure Function App Settings
```bash
# Set application settings using Azure CLI
az functionapp config appsettings set \
  --name func-refresh-scheduler-123 \
  --resource-group rg-refresh-scheduler \
  --settings \
  "WEB_API_BASE_URL=https://your-webapp-name.azurewebsites.net" \
  "WEBSITE_TIME_ZONE=Central Europe Standard Time"
```

## Step 6: Testing and Monitoring

### Test Locally
```bash
# Start the function locally
func start

# Test the timer trigger manually
# The function will run according to its schedule
```

### Test in Azure
1. Go to Azure Portal → Function App → Functions → RefreshApplicationData
2. Click "Test/Run"
3. Click "Run" to manually trigger
4. Check logs for execution details

### Monitor Execution
1. **Application Insights**
   - Go to Function App → Application Insights
   - View logs, performance, and failures

2. **Function Logs**
   - Go to Function App → Functions → RefreshApplicationData → Monitor
   - View execution history and logs

3. **Log Stream**
   - Go to Function App → Log stream
   - See real-time logs

### Set Up Alerts
```bash
# Create alert rule for function failures
az monitor metrics alert create \
  --name "RefreshData Function Failures" \
  --resource-group rg-refresh-scheduler \
  --scopes "/subscriptions/{subscription-id}/resourceGroups/rg-refresh-scheduler/providers/Microsoft.Web/sites/func-refresh-scheduler-123" \
  --condition "count 'FunctionExecutionCount' > 0" \
  --description "Alert when function fails"
```

## Step 7: Schedule Verification

The function is scheduled with `"0 0 5 * * *"` which means:
- **5:00 AM UTC** = **7:00 AM CET** (during standard time)
- **5:00 AM UTC** = **6:00 AM CET** (during daylight saving time)

### Alternative: Use CET Timezone
If you want to maintain 7:00 AM CET regardless of daylight saving:

```csharp
[Function("RefreshApplicationData")]
public async Task Run([TimerTrigger("0 0 7 * * *", TimeZone = "Central Europe Standard Time")] TimerInfo myTimer)
```

## Troubleshooting

### Common Issues

1. **Function Not Triggering**
   - Check timer expression syntax
   - Verify Function App is running
   - Check Application Insights logs

2. **API Call Failures**
   - Verify `WEB_API_BASE_URL` setting
   - Check if Web API requires authentication
   - Test API endpoint manually

3. **Timeout Issues**
   - Increase timeout in `host.json`
   - Check Web API performance
   - Add retry logic

### Debug Locally
```bash
# Enable detailed logging
export AzureWebJobsScriptRoot=/path/to/function
export AzureFunctionsJobHost__Logging__Console__IsEnabled=true

# Run with debug logging
func start --verbose
```

## Cost Estimation

### Consumption Plan (Recommended)
- **Free Tier**: 1M executions + 400,000 GB-s
- **Your Usage**: ~30 executions/month (once daily)
- **Expected Cost**: $0 (within free tier)

### Premium Plan (if needed)
- For guaranteed cold start performance
- Cost: ~$150-300/month

## Summary

This implementation provides:
- ✅ Scheduled execution at 7:00 AM CET daily
- ✅ Robust error handling and logging
- ✅ Application Insights monitoring
- ✅ Retry mechanism for failures
- ✅ Separate from Web API deployment
- ✅ Cost-effective serverless solution
- ✅ Easy maintenance and updates

The Azure Function will reliably call your `RefreshApplicationData` API endpoint every day without interfering with your Web API deployments.