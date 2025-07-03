# Azure Web Job Persistence Solutions

## Problem
Your Azure Web Job (PowerShell script) gets deleted every time you deploy your .NET Core 6 Web API project to Azure Web App. This happens because web jobs are not included in the deployment package by default.

## Solutions

### Solution 1: Include Web Job in Project Deployment (Recommended)

Create the proper folder structure in your Web API project to include the web job in deployments:

#### Step 1: Create Web Job Structure
In your .NET Core 6 Web API project root, create the following folder structure:
```
YourWebAPIProject/
├── App_Data/
│   └── jobs/
│       └── triggered/
│           └── RefreshDataJob/
│               ├── run.ps1
│               └── settings.job
```

#### Step 2: PowerShell Script (run.ps1)
```powershell
# run.ps1
param()

try {
    Write-Output "Starting RefreshApplicationData job at $(Get-Date)"
    
    # Get the base URL from environment variables or app settings
    $baseUrl = $env:WEBSITE_HOSTNAME
    if ([string]::IsNullOrEmpty($baseUrl)) {
        $baseUrl = "your-webapp-name.azurewebsites.net"
    }
    
    $apiUrl = "https://$baseUrl/api/RefreshApplicationData"
    
    Write-Output "Calling API: $apiUrl"
    
    # Make the API call
    $response = Invoke-RestMethod -Uri $apiUrl -Method POST -TimeoutSec 300
    
    Write-Output "API call completed successfully at $(Get-Date)"
    Write-Output "Response: $response"
    
    exit 0
}
catch {
    Write-Error "Error occurred: $($_.Exception.Message)"
    Write-Error "Stack trace: $($_.Exception.StackTrace)"
    exit 1
}
```

#### Step 3: Job Settings (settings.job)
```json
{
    "schedule": "0 0 6 * * *",
    "timezone": "Central European Standard Time"
}
```

#### Step 4: Update .csproj File
Add this to your Web API project's `.csproj` file:
```xml
<ItemGroup>
  <Content Include="App_Data\jobs\triggered\RefreshDataJob\**">
    <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Solution 2: Azure Functions with Timer Trigger (Modern Approach)

Create a separate Azure Function App with a timer trigger:

#### Function Code (C#)
```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Net.Http;
using System.Threading.Tasks;

public class RefreshDataFunction
{
    private readonly ILogger _logger;
    private readonly HttpClient _httpClient;
    private readonly string _webApiBaseUrl;

    public RefreshDataFunction(ILoggerFactory loggerFactory, HttpClient httpClient)
    {
        _logger = loggerFactory.CreateLogger<RefreshDataFunction>();
        _httpClient = httpClient;
        _webApiBaseUrl = Environment.GetEnvironmentVariable("WEB_API_BASE_URL") ?? 
                        "https://your-webapp-name.azurewebsites.net";
    }

    [Function("RefreshApplicationData")]
    public async Task Run([TimerTrigger("0 0 6 * * *", 
                          UseMonitor = false)] TimerInfo myTimer)
    {
        _logger.LogInformation($"RefreshApplicationData function executed at: {DateTime.Now}");

        try
        {
            var apiUrl = $"{_webApiBaseUrl}/api/RefreshApplicationData";
            var response = await _httpClient.PostAsync(apiUrl, null);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogInformation($"API call successful. Response: {content}");
            }
            else
            {
                _logger.LogError($"API call failed with status: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling RefreshApplicationData API");
        }
    }
}
```

### Solution 3: Azure Logic Apps (No-Code Solution)

1. Create an Azure Logic App
2. Add a **Recurrence** trigger:
   - Frequency: Day
   - Interval: 1
   - Time zone: Central European Standard Time
   - At these hours: 6 (for 7am CET considering daylight saving)

3. Add an **HTTP** action:
   - Method: POST
   - URI: `https://your-webapp-name.azurewebsites.net/api/RefreshApplicationData`

### Solution 4: GitHub Actions / Azure DevOps (CI/CD Approach)

#### GitHub Actions Workflow
```yaml
name: Daily API Refresh
on:
  schedule:
    - cron: '0 6 * * *'  # 6 AM UTC (7 AM CET in winter, 6 AM CET in summer)
  workflow_dispatch:  # Allow manual trigger

jobs:
  refresh-data:
    runs-on: ubuntu-latest
    steps:
      - name: Call RefreshApplicationData API
        run: |
          curl -X POST https://your-webapp-name.azurewebsites.net/api/RefreshApplicationData \
               -H "Content-Type: application/json" \
               -w "HTTP Status: %{http_code}\n"
```

### Solution 5: ARM Template for Web Job Deployment

Create an ARM template that deploys the web job as part of your infrastructure:

```json
{
    "$schema": "https://schema.management.azure.com/schemas/2019-04-01/deploymentTemplate.json#",
    "contentVersion": "1.0.0.0",
    "parameters": {
        "webAppName": {
            "type": "string"
        }
    },
    "resources": [
        {
            "type": "Microsoft.Web/sites/deployments",
            "apiVersion": "2020-12-01",
            "name": "[concat(parameters('webAppName'), '/RefreshDataJob')]",
            "properties": {
                "active": true,
                "deployer": "VSTS",
                "author": "Auto Deploy",
                "message": "Auto-deployed web job"
            }
        }
    ]
}
```

## Recommendations

1. **For immediate fix**: Use **Solution 1** (Include Web Job in Project) as it requires minimal changes to your existing setup.

2. **For modern architecture**: Consider **Solution 2** (Azure Functions) as it provides better scalability, monitoring, and separation of concerns.

3. **For non-developers**: **Solution 3** (Logic Apps) offers a visual, no-code approach.

## Implementation Steps for Solution 1

1. Create the folder structure in your Web API project
2. Add the PowerShell script and settings.job file
3. Update your .csproj file
4. Deploy your Web API project
5. Verify the web job appears in Azure Portal under Web Jobs

## Monitoring and Troubleshooting

- Check web job logs in Azure Portal: App Service → WebJobs → Your Job → Logs
- Monitor execution history and output
- Set up Application Insights for better monitoring
- Consider adding retry logic and error notifications

## Time Zone Considerations

CET (Central European Time) has daylight saving time changes:
- **Winter (CET)**: UTC+1 → Schedule at 6 AM UTC for 7 AM CET
- **Summer (CEST)**: UTC+2 → Schedule at 5 AM UTC for 7 AM CEST

The cron expression `0 0 6 * * *` in the settings.job with timezone "Central European Standard Time" handles this automatically.