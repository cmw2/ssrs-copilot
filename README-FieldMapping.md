# Azure Search Field Mapping Configuration

This document explains how to configure the SSRS Copilot to work with different Azure Search index schemas by using configurable field mappings.

## Overview

The SSRS Copilot uses Azure Search to find and retrieve report documentation. Different Azure Search indexes may have different field names for the same logical data. To handle this flexibility, the application uses a configurable field mapping system.

## Configuration Structure

Field mappings are configured in the `AzureSearch:FieldMapping` section of your `appsettings.json` file:

```json
{
  "AzureSearch": {
    "Endpoint": "https://your-search-service.search.windows.net",
    "ApiKey": "your-search-api-key",
    "IndexName": "your-index-name",
    "SemanticConfigurationName": "your-semantic-configuration",
    "VectorSearchEnabled": true,
    "EmbeddingDeploymentName": "text-embedding-3-large",
    "FieldMapping": {
      "IdField": "id",
      "TitleField": "title", 
      "ContentField": "content",
      "UrlField": "url",
      "FilePathField": "filepath",
      "MetadataField": "meta_json_string",
      "VectorField": "contentVector",
      "ParentIdField": "parent_id"
    }
  }
}
```

## Field Mapping Properties

### Required Fields

- **IdField**: The unique identifier field in your search index (used as the document key)
- **TitleField**: The field containing document titles
- **ContentField**: The field containing the main text content of documents

### Optional Fields

- **UrlField**: The field containing document URLs (set to `null` if not available)
- **FilePathField**: The field containing file paths (set to `null` if not available)  
- **MetadataField**: The field containing additional metadata as JSON (set to `null` if not available)
- **VectorField**: The field containing vector embeddings for vector search (set to `null` if not available)
- **ParentIdField**: The field linking chunks to parent documents (set to `null` if not available)

## Example Configurations

### Legacy Schema Configuration

```json
"FieldMapping": {
  "IdField": "id",
  "TitleField": "title",
  "ContentField": "content", 
  "UrlField": "url",
  "FilePathField": "filepath",
  "MetadataField": "meta_json_string",
  "VectorField": "contentVector",
  "ParentIdField": "parent_id"
}
```

### New Schema Configuration (chunk-based)

```json
"FieldMapping": {
  "IdField": "chunk_id",
  "TitleField": "title",
  "ContentField": "chunk",
  "UrlField": null,
  "FilePathField": null, 
  "MetadataField": null,
  "VectorField": "text_vector",
  "ParentIdField": "parent_id"
}
```

## Using Different Configurations

### Method 1: Environment-Specific Configuration Files

Create separate configuration files for different environments:

- `appsettings.json` - Base configuration
- `appsettings.Development.json` - Development overrides
- `appsettings.Production.json` - Production overrides
- `appsettings.NewIndex.json` - Configuration for new index schema

To use a specific configuration, set the `ASPNETCORE_ENVIRONMENT` environment variable:

```bash
# Use new index configuration
set ASPNETCORE_ENVIRONMENT=NewIndex
```

### Method 2: Configuration Profiles

You can also use different configuration sections and switch between them programmatically or via environment variables.

## Switching Between Index Schemas

To switch from the old schema to the new schema:

1. **Update your configuration** to use the new field mappings:
   ```json
   "FieldMapping": {
     "IdField": "chunk_id",
     "TitleField": "title", 
     "ContentField": "chunk",
     "VectorField": "text_vector",
     "ParentIdField": "parent_id",
     "UrlField": null,
     "FilePathField": null,
     "MetadataField": null
   }
   ```

2. **Update the index name** and semantic configuration:
   ```json
   "IndexName": "ssrs-reports",
   "SemanticConfigurationName": "ssrs-reports-semantic-configuration"
   ```

3. **Update embedding configuration** if needed:
   ```json
   "EmbeddingDeploymentName": "text-embedding-ada-002"
   ```

4. **Restart the application** for changes to take effect.

## Validation

The application will validate field mapping configuration at startup and throw an exception if required fields are missing or invalid. Check the application logs for any configuration errors.

## Troubleshooting

### Common Issues

1. **Missing required fields**: Ensure `IdField`, `TitleField`, and `ContentField` are properly configured
2. **Incorrect field names**: Verify field names match exactly with your Azure Search index schema
3. **Vector search not working**: Check that `VectorField` points to the correct vector field in your index
4. **No search results**: Verify that the semantic configuration name matches your index configuration

### Debugging

Enable detailed logging to see which fields are being queried:

```json
"Logging": {
  "LogLevel": {
    "SSRSCopilot.Agent.Services": "Debug"
  }
}
```

This will show the field names being used in search queries and help identify configuration issues.
