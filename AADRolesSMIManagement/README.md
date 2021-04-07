# What does this do?
> An Azure function that grants SMI of azure resources Azure Active Directory Roles

# How to use?
> Deploy an Azure Function with an SMI, and grant the SMI Application permissions for MIcrosoft Graph RoleManagement.ReadWrite.Directory

> I'm using an HTTP Trigger, but I've coded it to accept a WebHook from an Azure Event Grid subscription.  You would creat an EventGrid subscription for Successful Writes and set a filter to what subjects you want to trigger on.  In my example, I'm just triggering on Microsoft.Web/sites

> Add an Azure Resource Tag called Role and give it the value Reader and the function will make the SMI of the Azure WebApp an AAD Directory Reader.