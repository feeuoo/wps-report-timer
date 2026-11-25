param(
    [string]$TargetRoot = (Join-Path $env:APPDATA 'kingsoft\wps\jsaddons')
)

$ErrorActionPreference = 'Stop'
$addonName = 'WpsReportTimer'
$folderNames = @('WpsReportTimer_1.0.0', 'WpsReportTimer_1.0.1')
$publishPath = Join-Path $TargetRoot 'publish.xml'

if (Test-Path $publishPath) {
    Copy-Item $publishPath ($publishPath + '.before-WpsReportTimer-uninstall.xml') -Force
    [xml]$publish = Get-Content -Path $publishPath -Raw -Encoding UTF8
    $existing = $publish.SelectNodes("/jsplugins/*[@name='$addonName']")
    foreach ($node in @($existing)) {
        if ($node -ne $null) {
            $node.ParentNode.RemoveChild($node) | Out-Null
        }
    }
    $settings = New-Object System.Xml.XmlWriterSettings
    $settings.Encoding = New-Object System.Text.UTF8Encoding($false)
    $settings.Indent = $true
    $writer = [System.Xml.XmlWriter]::Create($publishPath, $settings)
    $publish.Save($writer)
    $writer.Close()
}

foreach ($folderName in $folderNames) {
    $target = Join-Path $TargetRoot $folderName
    if (Test-Path $target) {
        Remove-Item -Path $target -Recurse -Force
    }
}
Write-Output 'WPS 报告计时器加载项已卸载。请重启 WPS。'
