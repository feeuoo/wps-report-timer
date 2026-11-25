param(
    [string]$TargetRoot = (Join-Path $env:APPDATA 'kingsoft\wps\jsaddons')
)

$ErrorActionPreference = 'Stop'
$addonName = 'WpsReportTimer'
$version = '1.0.1'
$folderName = $addonName + '_' + $version
$source = Join-Path $PSScriptRoot $folderName
$target = Join-Path $TargetRoot $folderName
$publishPath = Join-Path $TargetRoot 'publish.xml'

if (-not (Test-Path (Join-Path $source 'WpsReportTimer.exe'))) {
    throw '安装包不完整：缺少 WpsReportTimer.exe。'
}
if (-not (Test-Path (Join-Path $source 'ribbon.xml'))) {
    throw '安装包不完整：缺少 ribbon.xml。'
}

New-Item -ItemType Directory -Force -Path $TargetRoot | Out-Null
New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item -Path (Join-Path $source '*') -Destination $target -Recurse -Force
$legacyTarget = Join-Path $TargetRoot 'WpsReportTimer_1.0.0'
if (Test-Path $legacyTarget) {
    Remove-Item -Path $legacyTarget -Recurse -Force
}

if (Test-Path $publishPath) {
    Copy-Item $publishPath ($publishPath + '.before-WpsReportTimer.xml') -Force
    [xml]$publish = Get-Content -Path $publishPath -Raw -Encoding UTF8
    if ($publish.DocumentElement.Name -ne 'jsplugins') {
        throw '现有 publish.xml 根节点不是 jsplugins，已停止安装。原文件未修改。'
    }
} else {
    [xml]$publish = '<?xml version="1.0" encoding="UTF-8"?><jsplugins></jsplugins>'
}

$existing = $publish.SelectNodes("/jsplugins/*[@name='$addonName']")
foreach ($node in @($existing)) {
    if ($node -ne $null) {
        $node.ParentNode.RemoveChild($node) | Out-Null
    }
}

$entry = $publish.CreateElement('jsplugin')
$entry.SetAttribute('name', $addonName)
$entry.SetAttribute('type', 'wpp')
$entry.SetAttribute('url', $folderName + '/')
$entry.SetAttribute('version', $version)
$entry.SetAttribute('enable', 'enable_dev')
$publish.DocumentElement.AppendChild($entry) | Out-Null

$settings = New-Object System.Xml.XmlWriterSettings
$settings.Encoding = New-Object System.Text.UTF8Encoding($false)
$settings.Indent = $true
$writer = [System.Xml.XmlWriter]::Create($publishPath, $settings)
$publish.Save($writer)
$writer.Close()

if (-not (Test-Path (Join-Path $target 'WpsReportTimer.exe'))) {
    throw '安装后校验失败：计时器程序不存在。'
}
[xml]$check = Get-Content -Path $publishPath -Raw -Encoding UTF8
if ($check.SelectSingleNode("/jsplugins/jsplugin[@name='$addonName' and @type='wpp']") -eq $null) {
    throw '安装后校验失败：加载项注册信息不存在。'
}

Write-Output '安装成功。'
Write-Output ('加载项目录：' + $target)
Write-Output '请完全退出并重新打开 WPS 演示，在顶部“工具”选项卡中点击“启动报告计时器”。'
