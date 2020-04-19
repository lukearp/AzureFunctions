
# Scenario

> VMs are being deployed into an Azure virtual networking environment. I need a way to managed Inbound and Outbound Networking Security rules automatically once provisioned.

  

> I want this solution to work reguardless of deployment method (Portal or API).

  

# Proposed Solution

> Pre-Configure Network Security Group Inbound and Outbound rules referencing Application Security Groups instead of IPs. Monitor the creation of Network Interfaces and based on a Resource tag, add the IPConfiguration to the Application Security group.

  

> Enforce the TAGs existence through Azure Policy for all Network Interfaces deployed in the environment.

  

# Techinical Implementation

* Create required Application Security Groups.

* Create required Network Security Rules referencing the Application Security Groups.

* Create an EvenGrid Subscription to monitor the Write actions of Network Interfaces within a Subscription

* Have EventGrid Subscription trigger an Azure Function using a WebHook (Had issues with EventGrid Trigger type). The Function would then add the IPConfiguration to the Application Security Group based on a Tag Key-Value Pair.

  

# What is in this Repository

1. Azure Function that will search a Network Interface for a Tag Key called "ASG" and add the value as a Application Security Group within the Primary IPConfiguration.

2. The ASG Tag Value is the Name of a Application Security Group that is located in a known Resource Gorup.

3. A EventGrid Subscription Template.

  

## Function Setup

1. Add the following AppSettings to the Function Configuration:
> "apiVersion": "2020-03-01",
> "appKey": "APP KEY",
> "clientId": "APP ID",
> "subscriptionId": "AZURE SUBSCRIPTION THAT IS BEING MONITORED",
> "tenantId": "AAD TENANT",
> "applicationSecurityGroupResouceGroup": "RESOURCE GROUP WHERE ASG's EXIST"

  

2. Deploy Function App

  

## Event Grid Setup

1. Create a Azure Subscripton Event Grid Subscription. Set the included events to "Microsoft.Resources.ResourceWriteSuccess" and "Microsoft.Resources.ResourceActionSuccess".

2. Set WebHook to Function address.
>* WebHook will need to be verified. This can be done by visiting the verification link in the verification post body. This can be seen if you uncomment line 27 and watch the function logs while saving the Event Grid subscription.

3. Set Advanced Filter to 'subject String Contains "/providers/Microsoft.Network/networkInterfaces/"'

4. Create EventGrid Subscription

  

# Current Limitations

1. This initial release only supports adding one ASG to one IPConfiguration on a Network Interface. I plan on adding the ability to assign multiple ASGs to Multiple IPConfigurations on a single Network Interface.

2. This is only monitoring Network Interfaces. Later, I may add support for monitoring the Creation of VMs. This would allow this method to be used in situations where only the VM Object is being tagged and not all child objects of the VM. If deployed through the Azure Portal, tagging child items is the default.
