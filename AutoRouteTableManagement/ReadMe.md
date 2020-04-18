# Route Table Management
### App Setting Varibles to set in your Function Configuration
- "appKey": "SECRET FOR APP"
- "clientId": "APP ID That has rights to the Azure Subscription"
- "location": "ANY LOCATION Ex: eastus"
- "subscriptionId": "AZURE SUB WHERE ROUTE TABLES ARE"
- "tenantId": "AAD TENATNID"

### Tag your Route Table with the AutoRoute Tag.  Set the value to a Comma dilimited string with Azure IP ServiceTags.
- Example: "AutoRoute": "Sql.EastUS,AzureMonitor.EastUS"
- Route Names will be in the following format: AutoRoute-{SERVICE TAG}-{CHANGE NUMBER}-{ENUMERATOR}

### If tag values are removed from the AutoRoute tags, the routes will be removed.