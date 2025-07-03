# Azure Web Jobs Deployment Solution

## Problem
Web Jobs created manually in Azure Web App get deleted when deploying the .NET Core 6 Web API project.

## Root Cause
Azure deployment replaces the entire site content, including manually created Web Jobs that aren't part of the deployment package.

## Solution Overview
Include Web Jobs in your project deployment by:
1. Creating a Web Job as part of your project structure
2. Configuring proper deployment settings
3. Using Azure Functions as an alternative (recommended)

## Option 1: Include Web Job in Project Deployment

### Step 1: Create Web Job Structure in Your Project

Create the following folder structure in your .NET Core 6 Web API project:

```
YourWebApiProject/
├── Controllers/
├── App_Data/
│   └── jobs/
│       └── triggered/
│           └── RefreshDataJob/
│               ├── run.ps1
│               └── settings.job
├── wwwroot/
└── Program.cs
```

### Step 2: Create PowerShell Script

**File: `App_Data/jobs/triggered/RefreshDataJob/run.ps1`**

```powershell
# RefreshApplicationData Web Job
param([string]$baseUrl)

# Get the base URL from app settings or use parameter
if (-not $baseUrl) {
    $baseUrl = $env:WEBSITE_HOSTNAME
    if ($baseUrl) {
        $baseUrl = "https://$baseUrl"
    } else {
        $baseUrl = "https://your-webapp-name.azurewebsites.net"
    }
}

$apiEndpoint = "$baseUrl/api/RefreshApplicationData"

Write-Output "Starting RefreshApplicationData job at $(Get-Date)"
Write-Output "Calling endpoint: $apiEndpoint"

try {
    # Make the HTTP request to your API
    $response = Invoke-RestMethod -Uri $apiEndpoint -Method POST -TimeoutSec 300
    Write-Output "API call successful. Response: $response"
    
    # Log success
    Write-Output "RefreshApplicationData completed successfully at $(Get-Date)"
} catch {
    Write-Error "API call failed: $($_.Exception.Message)"
    Write-Output "RefreshApplicationData failed at $(Get-Date)"
    exit 1
}
```

### Step 3: Create Job Settings

**File: `App_Data/jobs/triggered/RefreshDataJob/settings.job`**

```json
{
  "schedule": "0 0 5 * * *",
  "timezone": "UTC"
}
```

**Note:** The schedule `0 0 5 * * *` is for 5:00 AM UTC, which equals 7:00 AM CET (Central European Time).

### Step 4: Update Project File

Add this to your `.csproj` file to ensure Web Job files are included in deployment:

```xml
<ItemGroup>
  <Content Include="App_Data\jobs\triggered\RefreshDataJob\run.ps1">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
  <Content Include="App_Data\jobs\triggered\RefreshDataJob\settings.job">
    <CopyToOutputDirectory>Always</CopyToOutputDirectory>
  </Content>
</ItemGroup>
```

### Step 5: Publish Profile Configuration

If using Visual Studio publish profiles, update your `.pubxml` file:

```xml
<PropertyGroup>
  <PublishUrl>your-publish-url</PublishUrl>
  <IncludeSetAclProviderOnDestination>False</IncludeSetAclProviderOnDestination>
  <EnableMSDeployAppOffline>true</EnableMSDeployAppOffline>
  <EnableMSDeployBackup>true</EnableMSDeployBackup>
  <DeployDefaultTarget>WebPublish</DeployDefaultTarget>
</PropertyGroup>

<ItemGroup>
  <MsDeploySkipRules Include="SkipWebJobsFolder">
    <ObjectName>dirPath</ObjectName>
    <AbsolutePath>App_Data\\jobs</AbsolutePath>
  </MsDeploySkipRules>
</ItemGroup>
```

## Option 2: Azure Functions (Recommended Alternative)

### Why Azure Functions?
- Better scalability and reliability
- Built-in scheduling with Timer Triggers
- No deployment conflicts
- Better monitoring and logging
- Consumption-based pricing

### Create Timer-Triggered Azure Function

**Function Code:**

```csharp
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;

public static class RefreshDataFunction
{
    private static readonly HttpClient httpClient = new HttpClient();

    [FunctionName("RefreshApplicationData")]
    public static async Task Run(
        [TimerTrigger("0 0 5 * * *", RunOnStartup = false)] TimerInfo myTimer,
        ILogger log)
    {
        log.LogInformation($"RefreshApplicationData function executed at: {DateTime.Now}");

        var baseUrl = Environment.GetEnvironmentVariable("WEB_API_BASE_URL") 
                     ?? "https://your-webapp-name.azurewebsites.net";
        
        var apiEndpoint = $"{baseUrl}/api/RefreshApplicationData";

        try
        {
            var response = await httpClient.PostAsync(apiEndpoint, null);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                log.LogInformation($"API call successful. Response: {content}");
            }
            else
            {
                log.LogError($"API call failed with status code: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            log.LogError($"Exception occurred: {ex.Message}");
            throw; // Re-throw to trigger retry policy if configured
        }
    }
}
```

## Implementation Steps

### For Web Job Approach:
1. Create the folder structure in your Web API project
2. Add the PowerShell script and settings files
3. Update your `.csproj` file
4. Deploy your application

### For Azure Functions Approach:
1. Create a new Azure Functions project
2. Add the timer-triggered function
3. Deploy the function app separately
4. Configure the `WEB_API_BASE_URL` app setting

## Verification

After deployment, verify the Web Job:
1. Go to Azure Portal → Your Web App → Development Tools → WebJobs
2. Check if "RefreshDataJob" appears in the list
3. Monitor the job execution logs

## Troubleshooting

### Common Issues:
1. **Time Zone**: Ensure schedule is in UTC time
2. **Permissions**: Web App must have permissions to call its own API
3. **Authentication**: If API requires authentication, add necessary headers to the script
4. **Timeout**: Increase timeout if API takes longer than expected

### Enhanced PowerShell Script with Authentication:

```powershell
# For APIs requiring authentication
$headers = @{
    "Authorization" = "Bearer $env:API_KEY"
    "Content-Type" = "application/json"
}

$response = Invoke-RestMethod -Uri $apiEndpoint -Method POST -Headers $headers -TimeoutSec 300
```

## Recommendation

For new implementations, I strongly recommend using **Azure Functions** as they provide better reliability, monitoring, and don't have deployment conflicts. Web Jobs are being phased out in favor of Azure Functions for scheduled tasks.