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
  - Azure Storage Account
  - Azure AI Search service
  - Azure OpenAI service
- SQL Server Reporting Services (SSRS) instance

### Deploy Azure OpenAI Models

The application requires specific AI models to be deployed in your Azure OpenAI service:

1. **Deploy GPT-4o Model**: For chat completion and natural language processing
2. **Deploy text-embedding-ada-002 Model**: For document vectorization and semantic search

Make note of your deployment names as you'll need them for configuration.

### Setting Up Document Indexing

Before running the application, you'll need to set up your AI Search index with your SSRS reports:

1. **Create Azure Storage Account**: Set up a storage account to hold your report documents
2. **Upload Documents**: Upload your SSRS report user guides to the storage account
3. **Use AI Search Import and Vectorize Data Wizard**: Configure the wizard to:
   - Connect to your storage account
   - Index and vectorize your documents (requires the text-embedding model deployed above)
   - Create the search index that the application will query

### 1. Clone the Repository

```bash
git clone https://github.com/cmw2/ssrs-copilot
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

3. Configuring Field Mappings

   The application uses Azure AI Search field mappings to properly index and retrieve document content. You may need to adjust these mappings based on your index field names.

   For detailed information on configuring field mappings, see: [README-FieldMapping.md](README-FieldMapping.md)

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
