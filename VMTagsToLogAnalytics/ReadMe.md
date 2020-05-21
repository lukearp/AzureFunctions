# Add VM Tags to Log Analytics Workspace
### App Setting Varibles to set in your Function Configuration
```
  {
    "vmApiVersion": "2019-12-01", {AZURE RESOURCE MANAGER VM API VERSION}
    "logAnalyticsApiVersion": "2016-04-01", {LOG ANALYTICS DATA COLLECTOR API VERSION}
    "workspaceId": "LOG ANALYTICS WORKSPACE ID",
    "workspaceKey": "{LOG ANALYTICS WORKSPACE KEY}",
    "subscriptionId": "{SUBSCRIPTION ID}",
    "clientId": "{APPLICATION ID}",
    "appKey": "{APP SECRET}",
    "tenantId": "{AAD Directory ID}",
    "logName": "{NAME OF CUSTOM LOG}"
  } 
```
### What does this do?
- Get all VMS in Azure Subscription and create a row in a Log Analytics Table.  This will allow you to do queries and alerts based on Tag Values in Log Analytics.
- Logs can be found in your Log Analytics Workspace -> CustomLogs -> logName_CL
- Time Trigger Function, runs every hour by default

