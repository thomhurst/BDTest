Param(
    [Parameter(Mandatory=$true)]
    [string]$FilePath,

    [Parameter(Mandatory=$true)]
    [string]$Find,

    [Parameter(Mandatory=$true)]
    [string]$Replace
)

(Get-Content $FilePath).Replace($Find, $Replace) | Set-Content $FilePath