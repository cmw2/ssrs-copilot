# SSRS Copilot

A sample AI-powered assistant for SQL Server Reporting Services (SSRS) that helps users find and interact with reports using natural language queries.

## What it does

SSRS Copilot provides an intelligent interface to your SSRS environment by:

1. **Report Discovery**: Uses Azure AI Search to index and search through your SSRS reports
2. **Natural Language Queries**: Allows users to ask questions about reports in plain English
3. **Smart Recommendations**: Leverages AI to suggest relevant reports based on user intent
4. **Interactive Chat**: Provides a conversational interface for exploring your report catalog

## Sample Code

This repository contains sample code intended for demonstration purposes. It showcases how to integrate Azure Cognitive Services with SQL Server Reporting Services for intelligent report discovery and interaction. The code is provided as-is and may require modifications to fit specific use cases or production environments.

## Getting Started

### Prerequisites

- .NET 8.0 or later
- Visual Studio 2022 or VS Code
- Azure subscription with:
  - Azure AI Search service
  - Azure OpenAI service
- SQL Server Reporting Services (SSRS) instance

### 1. Clone the Repository

```bash
git clone <your-repo-url>
cd ssrs-copilot
```

### 2. Configure Application Settings

1. Update the configuration files with your Azure and SSRS settings:
   - `SSRSCopilot.Agent/appsettings.Development.json`
   - `SSRSCopilot.Web/appsettings.Development.json`

2. Configure the following settings:
   - Azure AI Search endpoint and API key
   - Azure OpenAI endpoint and API key
   - SSRS server URL and credentials

### 3. Build and Run

Using .NET Aspire (recommended):

```bash
dotnet run --project SSRSCopilot.AppHost
```

Or run individual services:

```bash
# Start the API service
dotnet run --project SSRSCopilot.Agent

# Start the web application
dotnet run --project SSRSCopilot.Web
```

### 4. Access the Application

- **Web Interface**: Navigate to the URL shown in the console (typically `https://localhost:7xxx`)
- **API Endpoints**: Access the REST API at the agent service URL

## Project Structure

- **SSRSCopilot.AppHost**: .NET Aspire orchestration host
- **SSRSCopilot.Agent**: Main API service with AI chat capabilities
- **SSRSCopilot.Web**: Web frontend application
- **SSRSCopilot.ServiceDefaults**: Shared service configurations

## Key Features

- **Chat Interface**: Ask questions about reports in natural language
- **Report Search**: Find reports by name, description, or content
- **Parameter Assistance**: Get help with report parameters
- **SSRS Integration**: Direct integration with your SSRS instance

## Disclaimer

This Sample Code is provided for the purpose of illustration only and is not intended to be used in a production environment. THIS SAMPLE CODE AND ANY RELATED INFORMATION ARE PROVIDED 'AS IS' WITHOUT WARRANTY OF ANY KIND, EITHER EXPRESSED OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE IMPLIED WARRANTIES OF MERCHANTABILITY AND/OR FITNESS FOR A PARTICULAR PURPOSE.
