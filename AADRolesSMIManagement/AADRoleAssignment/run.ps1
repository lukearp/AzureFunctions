using namespace System.Net

# Input bindings are passed in via param block.
param($Request, $TriggerMetadata)
$subjects = $Request.Body
$roleId = "88d8e3e3-8f55-4a1e-953a-9b9898b8876b" #readerRole template id Id
$Uri = "$($env:IDENTITY_ENDPOINT)?api-version=2019-08-01&resource=https%3A%2F%2Fgraph.microsoft.com%2F"
$header = @{}
$header.Add("X-IDENTITY-HEADER", $($env:IDENTITY_HEADER))
$response = Invoke-RestMethod -Uri $Uri -Method GET -Headers $header -ContentType "application/json"
$header = @{}
$header.Add("Authorization", "Bearer $($response.access_token)")
foreach ($subject in $subjects) {
    $subscription = $subject.topic.split("/")[-1]
    $subject.subject.split("/")
    $subSelect = Select-AzSubscription $subscription
    try {
        $resource = Get-AzResource -ResourceId $subject.subject
        if ($resource.Tags.Role -eq "Reader") {
            $webApp = Get-AzWebApp -ResourceGroupName $resource.ResourceGroupName -Name $resource.Name
            if ($webApp.Identity -ne $null) {
                $body = @'
{{
  "principalId": "{0}",
  "roleDefinitionId": "{1}",
  "directoryScopeId": "/"
}}
'@
                $jbody = $body -f $webApp.Identity.PrincipalId, $roleId
                $jbody
                $uri = "https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments?`$filter=principalId+eq+'{0}'+&+roleDefinitionId+eq+'{1}'" -f $webApp.Identity.PrincipalId,$roleId
                $uri
                $getValue = Invoke-RestMethod -Uri $uri -Method Get -Header $header -ContentType "application/json"
                if($getValue.value.Count -eq 0)
                {
                    $uri = "https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments"
                    $uri
                    $responseValue = Invoke-RestMethod -Uri $uri -ContentType "application/json" -Header $header -Method Post -body $jbody
                }
            }
        }
        else {
            $webApp = Get-AzWebApp -ResourceGroupName $resource.ResourceGroupName -Name $resource.Name
            $uri = "https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments?`$filter=principalId+eq+'{0}'+&+roleDefinitionId+eq+'{1}'" -f $webApp.Identity.PrincipalId,$roleId
            $uri
            $getValue = Invoke-RestMethod -Uri $uri -Method Get -Header $header -ContentType "application/json"
            if($getValue.value.Count -ne 0)
            {
                $responseValue = Invoke-RestMethod -Uri $("https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments/" + $getValue.value[0].id) -ContentType "application/json" -Header $header -Method Delete
            }
            $log = "No Tag"
            $log
        }
    }
    catch {
        $_.Exception.Message
    }    
}

# Associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
        StatusCode = [HttpStatusCode]::OK
        Body       = $responseValue.value
    })
