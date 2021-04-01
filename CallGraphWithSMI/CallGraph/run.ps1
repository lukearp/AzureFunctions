using namespace System.Net

# Input bindings are passed in via param block.
param($Request, $TriggerMetadata)

$Uri = "$($env:IDENTITY_ENDPOINT)?api-version=2019-08-01&resource=https%3A%2F%2Fgraph.microsoft.com%2F"
$Uri
$header = @{}
$header.Add("X-IDENTITY-HEADER",$($env:IDENTITY_HEADER))
$header
$response = Invoke-RestMethod -Uri $Uri -Method GET -Headers $header -ContentType "application/json"
$header = @{}
$header.Add("Authorization","Bearer $($response.access_token)")

$responseValue = Invoke-RestMethod -Uri https://graph.microsoft.com/<API> -ContentType "application/json" -Header $header -Method Get

# Associate values to output bindings by calling 'Push-OutputBinding'.
Push-OutputBinding -Name Response -Value ([HttpResponseContext]@{
    StatusCode = [HttpStatusCode]::OK
    Body = $responseValue.value
})
