using namespace System.Net

# Input bindings are passed in via param block.
param($Request, $TriggerMetadata)
$subjects = $Request.Body
$roleId = "9b31e560-f630-4251-bf43-320ec9f94163" #readerRole Id
$Uri = "$($env:IDENTITY_ENDPOINT)?api-version=2019-08-01&resource=https%3A%2F%2Fgraph.microsoft.com%2F"
$header = @{}
$header.Add("X-IDENTITY-HEADER",$($env:IDENTITY_HEADER))
$response = Invoke-RestMethod -Uri $Uri -Method GET -Headers $header -ContentType "application/json"
$header = @{}
$header.Add("Authorization","Bearer $($response.access_token)")
foreach($subject in $subjects)
{
    $subscription = $subject.topic.split("/")[-1]
    $subject.subject.split("/")
    $subSelect = Select-AzSubscription $subscription
    try {
        $resource = Get-AzResource -ResourceId $subject.subject
        if($resource.Tags.ContainsKey("Role"))
    {
        $webApp = Get-AzWebApp -ResourceGroupName $resource.ResourceGroupName -Name $resource.Name
        if($webApp.Identity -ne $null)
        {
            $body = New-Object -typename psobject -Property @{
                principalId = $webApp.Identity.PrincipalId
                roleDefinitionId = $roleId
                directoryScopeId = "/"
            }
            #$responseValue = Invoke-RestMethod -Uri https://graph.microsoft.com/beta/roleManagement/directory/roleAssignments -ContentType "application/json" -Header $header -Method Post -body $(ConvertTo-Json -InputObject $body)
        }
    }
    else
    {
        $log = "No Tag"
        $log
    }
    }
    catch {
        $log = "Failed to get resource"
        $log
    }    
}

# Associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $responseValue.value
})
