# Asset Usage Tracking System

A comprehensive C# .NET solution for tracking, analyzing, and sending telemetry assets for Sitecore publishing events. This system provides real-time monitoring of item publishing operations, detailed field-level change tracking of assets, and asynchronous telemetry transmission to Sitecore Content Hub.

## Table of Contents
- [Overview](#overview)
- [Features](#features)
- [Architecture](#architecture)
- [Installation](#installation)
- [Configuration](#configuration)
- [Sitecore Content Hub OAuth Configuration](#sitecore-content-hub-oauth-configuration)
- [Custom Property Configuration](#custom-property-configuration)
- [Troubleshooting](#troubleshooting)
  
## Overview

This system integrates with Sitecore 10.4+ to provide comprehensive tracking and telemetry for publishing operations. It captures detailed information about items being published, including field-level changes, revision tracking, and context information, then transmits this data asynchronously to configured external telemetry services.

### Key Capabilities
- Real-time publishing event tracking
- Field-level change detection (Rich Text, Image, and custom fields)
- Revision ID tracking and comparison
- Asynchronous telemetry transmission
- Thread-safe concurrent processing
- Detailed diagnostic logging
- Configurable external service integration

## Features

### Publishing Event Tracking
- **Item Processing Events**: Captures items before they are processed for publishing
- **Item Processed Events**: Captures items after they have been published
- **Context Tracking**: Maintains publishing context including source/target databases, languages, and timestamps
- **Concurrent Processing**: Uses thread-safe collections to handle multiple simultaneous publishing operations

### Field Change Analysis
- **Rich Text Field Tracking**: Detects changes in Rich Text fields, including HTML content analysis
- **Image Field Tracking**: Monitors image field modifications and media library references
- **Custom Field Support**: Extensible architecture for tracking additional field types
- **Revision Comparison**: Compares source and target item revisions to identify actual changes

### Telemetry & Logging
- **Asynchronous Telemetry**: Non-blocking telemetry transmission to external services
- **Detailed Logging**: Comprehensive Sitecore.Diagnostics.Log integration with Info, Warn, and Error levels
- **Field Analysis Logs**: Detailed logging of field types, values, and change detection
- **Performance Metrics**: Tracks processing times and queue sizes

### External Service Integration
- **HTTP Client Integration**: Configurable HTTP client for external API communication
- **JSON Serialization**: Automatic serialization of telemetry data to JSON format
- **Timeout Configuration**: Configurable timeout settings for external service calls
- **Error Handling**: Robust exception handling with detailed error logging

## Architecture

### Component Overview

1. **Event Handlers**
   - ItemProcessedEventHandler - Handles post-publishing events
   - ItemProcessingEventHandler - Handles pre-publishing events

2. **Services**
   - TelemetryService - Manages external telemetry API communication
   - Singleton pattern with thread-safe initialization

3. **Analyzers**
   - ItemUpdatedAnalyzer - Analyzes field changes and revision differences

4. **Models**
   - ItemPublishedEvent - Telemetry event data model
   - PublishingContext - Publishing operation context
   - FieldUpdateInfo - Field-level change information

5. **Collections**
   - ConcurrentQueue<T> - Thread-safe queues for processing and updated items
   - ConcurrentDictionary<T> - Thread-safe context tracking

### Data Flow

1. Publishing operation starts
2. ItemProcessingEventHandler captures pre-processing context
3. Sitecore processes item
4. ItemProcessedEventHandler captures post-processing data
5. System analyzes field changes and revisions
6. Telemetry data is queued for transmission
7. Asynchronous HTTP POST sends data to external service
8. Logs record operation results

## Installation

### Prerequisites
- Sitecore 10.4 or later
- .NET Framework 4.8 or later
- Visual Studio 2019 or later
- Sitecore Publishing Service (optional, for enhanced publishing features)

### Package Manager Settings

Before you get started, configure Package Manager settings -> Package Sources:

| Name | Source |
|------|--------|
| Sitecore | https://sitecore.myget.org/F/sc-packages/api/v3/index.json#myget.org |
| nuget.org | https://api.nuget.org/v3/index.json |

### Packages to Install

- Sitecore.Kernel
- Sitecore.Mvc
- Sitecore.Analytics

### In Your Solution

References -> Everything from Sitecore.* right-click -> Properties -> Copy Local = False (helps to avoid overwriting existing local files)

### Steps

**Step 1: Build Project**

Build your project in Visual Studio

**Step 2: Copy DLL**

In `{PROJECT_PATH}\bin\Debug` place the `IO.Sitecore.publishing.dll` (Application Extension, not the Configuration Source File)

**Step 3: Deploy DLL to Sitecore**

Place the DLL you copied in step 2 in `{SITECORE_ROOT}\bin`

**Step 4: Create Configuration Folder**

In `{SITECORE_ROOT}\App_Config\Include` you need to create folder `zzz.IO`

**Step 5: Add Configuration File**

In the folder place the configuration file (thus in `{SITECORE_ROOT}\App_Config\Include\zzz.IO`)

Use the provided `iO.Publishing.Events.config` file

**Step 6: Restart IIS**

Restart IIS to apply changes

**Verification:**

When you now do a Site publish / Item Publish, you can find the JSON logs in:

    {SITECORE_ROOT}\App_Data\logs

with the name `log.[yyyy/mm/dd].[numbers]`

## Configuration

### Asset Usage Service Configuration

Configure the external API endpoint for the Asset Usage Service by creating a configuration file in your Sitecore instance.

**File Location:**

    {SITECORE_ROOT}\App_Config\Include\AssetUsageService.config

**Configuration Content:**

    <?xml version="1.0" encoding="utf-8"?>
    <configuration xmlns:patch="http://www.sitecore.net/xmlconfig/">
      <sitecore>
        <settings>
          <!-- Azure Function endpoint for Asset Usage Service -->
          <setting name="AssetUsageService.ApiEndpoint" value="http://localhost:7183/api/SitecorePublishAPI" />
        </settings>
      </sitecore>
    </configuration>

**Configuration Settings:**

- **AssetUsageService.ApiEndpoint**: The URL endpoint of your external telemetry/asset tracking service
  - For local development: `http://localhost:7183/api/SitecorePublishAPI`
  - For production: Update to your Azure Function or external API endpoint

**Note:** Make sure to update the endpoint URL to match your environment (development, staging, production).

## Sitecore Content Hub OAuth Configuration

This section describes the process to setup OAuth in Sitecore Content Hub for the external asset tracking service.

### STEP 1: CREATE USER WITH MINIMUM REQUIRED PERMISSIONS

#### 1.1 Create New User

1. Log in to your Sitecore Content Hub
2. Go to **Manage** (⚙️ settings icon) **> Users**
3. Click on **User**
4. Click + User button to add a new user
5. Fill in the following information:
   - **Username**: e.g., "asset-service-user"
6. Click on **Save**
7. Click on Edit profile button and fill in:
   - **Email**: a valid email address
8. Verify the email address by going to the previously filled in email address inbox
9. After verifying click reset password (called: click here) link

#### 1.2 Create User Group

1. Go to **Manage > Users > User groups**
2. Click on + **Usergroup**
3. Fill in the following information:
   - **Name**: e.g, "Asset Editors Service"
   - Modules: "Media"
4. Click on the User field + button
5. Search and add the newly created user
6. Click on **Save**

#### 1.4 Configure User Group Policy

1. Go to the User Group overview page
2. Click **Policies** (⚙️ settings icon) on the User Group you created
3. Click on **New rule**
4. Configure the policy as follows:

**Entity Definition:**
- Select: **M.Asset**

**Permissions:**
Check the following permissions:
- ✅ Read (to retrieve assets)
- ✅ Create (to add new assets)
- ✅ Update (to edit existing assets)
- ✅ AddVersion (to upload new versions of assets)
- ✅ ReadPublicLinks (to retrieve public links)
- ⚠️ Delete (only if deletion is required)

**Conditions (optional):**
If you want to restrict permissions to specific assets:
- Click on **Add condition**
- Define criteria based on metadata (e.g., only assets in certain folders)

5. Click on **Save**

#### 1.5 Add User to Everyone Group

1. Go back to **Manage > Users**
2. Open the newly created user
3. Go to the **User groups** tab
4. Verify that the user is a member of:
   - ✅ **Everyone** (this often happens automatically)
   - ✅ **Asset Editors Service** (just added)
5. Click on **Save**

#### 1.6 Test Permissions with Impersonation

1. Stay in the user details
2. Click on **Impersonate** in the top right
3. You are now logged in as this user
4. Verify that you can:
   - ✅ See assets
   - ✅ Edit assets
   - ✅ Add new assets
   - ❌ DO NOT have access to other modules (Settings, Manage, etc.)
5. Click on **Stop impersonating** in the top right to return to your own account

### STEP 2: CREATE OAUTH CLIENT

#### 2.1 Create New OAuth Client

1. Go to **Manage > OAuth clients**
2. Click on **OAuth client** to add a new client
3. Fill in the following information:

**Name:**

    Asset Service Client

**Client ID:**

    asset-service-client

**Client Secret:**

    generate a strong password

⚠️ **IMPORTANT**: Copy and save the Client Secret immediately! You won't be able to see it later.

**Redirect URL:**

    https://localhost/

⚠️ **Note**: This value is required but not used in Client Credentials flow. You can enter a dummy HTTPS URL here.

**Type:**
- Select: **Client Credentials**

**User:**
- Search and select the newly created user: "asset-service-user"

4. Click on **Save**

## Custom Property Configuration

### Add a Custom Property to Sitecore Content Hub (M.Asset)

This guide shows how to add a new property on M.Asset, place it in a member group, and set read/write permissions.

#### Step 1: Open Schema

1. Sign in with a superuser or a user with schema permissions
2. Go to **Manage (⚙️)**
3. Open **Schema**

#### Step 2: Find the M.Asset entity definition

1. In Schema, use search and type `M.Asset`
2. Select **M.Asset** from the results

#### Step 3: Create a Member group

Create a new group:

1. Click **New group**
2. Fill in **Name** (e.g., `UsageTracking`)
3. Click **Save**

#### Step 4: Add a new Property member

1. Inside the chosen member group, click **New member**
2. In the **New member** dialog, next to **Property**, click **Select**
3. Choose the **Data type JSON**
4. Click **Next**, then configure the property:
   - **Name**: (e.g., `UsageTracking`)
   - **Allow Updates:** Selected
   - **Secured:** Selected
5. Click **Save**

#### Step 5: Grant read access for Everyone

Goal: Every user can see the field, but not edit it.

1. Go to **Manage (⚙️) › Users**
2. Open the **User groups** tab
3. Find **Everyone**, then click **Policies (⚙️)**
4. Open the **Member security** tab
5. Under **Definitions**, select **M.Asset**
6. Under **Member groups**, select the group you used (e.g., `UsageTracking`)
7. Under **Members**, find your property (e.g., `UsageTracking`) and check only **Read**
8. Click **Save**

#### Step 6: Grant write access to a specific group and service user

Goal: Only editors and the designated service user can modify the field.

1. Go to **Manage (⚙️) › Users › User groups**
2. Go to the usergroup created in the OAuth Client Setup (Asset Editors Service)
3. Click **Policies (⚙️)**
4. Open the **Member security** tab
5. Under **Definitions**, select **M.Asset**
6. Under **Member groups**, pick the group that contains the property (e.g., `UsageTracking`)
7. Under **Members**, find your property (e.g., `UsageTracking`) and check **Read** and **Write**
8. Click **Save**

#### Result

- Users in **Everyone** can see the field but cannot edit it
- Members of the designated editor group can both view and edit the field

## Troubleshooting

### Common Issues

1. **Telemetry Not Being Sent**
   - Verify AssetUsageService.ApiEndpoint is configured correctly
   - Check the API endpoint is accessible from your Sitecore instance
   - Review Sitecore logs for HTTP request errors
   - Ensure network connectivity to telemetry service

2. **Missing Field Changes**
   - Confirm field types are supported (Rich Text, Image)
   - Check revision IDs are different between source and target
   - Review field analysis logs for detailed information

3. **Configuration Not Loading**
   - Verify AssetUsageService.config is in the correct location
   - Check XML syntax is valid
   - Restart IIS after configuration changes
   - Review Sitecore logs for configuration errors

4. **OAuth Authentication Failures**
   - Verify OAuth client credentials are correct
   - Check that the service user has proper permissions
   - Ensure the OAuth client type is set to Client Credentials
   - Review Content Hub logs for authentication errors

### Debug Logging

Check Sitecore logs located at:

    {SITECORE_ROOT}\App_Data\logs

Look for entries related to:
- ItemProcessedEventHandler
- ItemProcessingEventHandler
- AssetUsageService
- HTTP request failures
