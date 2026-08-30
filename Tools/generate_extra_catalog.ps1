param(
    [string]$MainCatalog = 'D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Windows\catalog.json',
    [string]$MainBundleRoot = 'D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Windows',
    [string]$ExtraSourceCatalog = 'D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa\catalog.json',
    [string]$ExtraSourceBundleRoot = 'D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa',
    [int]$SourceEntryCount = 8348,
    [string]$OutputExtraCatalog = 'D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Windows\extra_catalog.json',
    [string]$OutputBundleRoot = 'D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Windows',
    [string]$OutputAddressList = 'D:\UnityWork\zhou-master\Tools\branch1_extra_addresses.txt'
)

$ErrorActionPreference = 'Stop'
function Get-BundleKey($internalId) {
    $idx = $internalId.IndexOf('StandaloneWindows64\')
    if ($idx -lt 0) { return $internalId }
    return $internalId.Substring($idx + 'StandaloneWindows64\'.Length)
}

function Read-Int32([byte[]]$data, [int]$offset) {
    return [BitConverter]::ToInt32($data, $offset)
}

function Read-Object([byte[]]$data, [int]$offset, [ref]$next) {
    $type = $data[$offset]
    $start = $offset
    $offset++
    switch ($type) {
        0 {
            $len = [BitConverter]::ToInt32($data, $offset)
            $offset += 4
            $value = [System.Text.Encoding]::ASCII.GetString($data, $offset, $len)
            $offset += $len
        }
        1 {
            $len = [BitConverter]::ToInt32($data, $offset)
            $offset += 4
            $value = [System.Text.Encoding]::Unicode.GetString($data, $offset, $len)
            $offset += $len
        }
        2 {
            $value = [BitConverter]::ToUInt16($data, $offset)
            $offset += 2
        }
        3 {
            $value = [BitConverter]::ToUInt32($data, $offset)
            $offset += 4
        }
        4 {
            $value = [BitConverter]::ToInt32($data, $offset)
            $offset += 4
        }
        5 {
            $len = $data[$offset]
            $offset++
            $value = [System.Text.Encoding]::ASCII.GetString($data, $offset, $len)
            $offset += $len
        }
        6 {
            $len = $data[$offset]
            $offset++
            $value = [System.Text.Encoding]::ASCII.GetString($data, $offset, $len)
            $offset += $len
        }
        7 {
            $asmLen = $data[$offset]; $offset++
            $asm = [System.Text.Encoding]::ASCII.GetString($data, $offset, $asmLen); $offset += $asmLen
            $clsLen = $data[$offset]; $offset++
            $cls = [System.Text.Encoding]::ASCII.GetString($data, $offset, $clsLen); $offset += $clsLen
            $jsonLen = [BitConverter]::ToInt32($data, $offset); $offset += 4
            $json = [System.Text.Encoding]::Unicode.GetString($data, $offset, $jsonLen); $offset += $jsonLen
            $value = [PSCustomObject]@{ Type = 'JsonObject'; Assembly = $asm; Class = $cls; Json = $json }
        }
        default {
            throw "Unknown object type $type at offset $start"
        }
    }
    $next.Value = $offset
    return $value
}

function Get-ObjectLength([byte[]]$data, [int]$offset) {
    $next = 0
    $null = Read-Object $data $offset ([ref]$next)
    return $next - $offset
}

function Write-ObjectBytes($obj) {
    $list = New-Object System.Collections.Generic.List[byte]
    if ($obj -is [string]) {
        $str = [string]$obj
        $utf16 = [System.Text.Encoding]::Unicode.GetBytes($str)
        $ascii = [System.Text.Encoding]::ASCII.GetBytes($str)
        $utf16Decoded = [System.Text.Encoding]::Unicode.GetString($utf16)
        $asciiDecoded = [System.Text.Encoding]::ASCII.GetString($ascii)
        if ($utf16Decoded -ceq $asciiDecoded) {
            $list.Add([byte]0)
            $list.AddRange([BitConverter]::GetBytes([int]$ascii.Length))
            $list.AddRange([byte[]]$ascii)
        } else {
            $list.Add([byte]1)
            $list.AddRange([BitConverter]::GetBytes([int]$utf16.Length))
            $list.AddRange([byte[]]$utf16)
        }
    } elseif ($obj -is [uint32]) {
        $list.Add([byte]3)
        $list.AddRange([BitConverter]::GetBytes([uint32]$obj))
    } elseif ($obj -is [uint16]) {
        $list.Add([byte]2)
        $list.AddRange([BitConverter]::GetBytes([uint16]$obj))
    } elseif ($obj -is [int]) {
        $list.Add([byte]4)
        $list.AddRange([BitConverter]::GetBytes([int]$obj))
    } else {
        throw "Unsupported key object type: $($obj.GetType().FullName)"
    }
    return $list.ToArray()
}

function Get-KeyIdentity($key) {
    if ($key -is [string]) { return 'S:' + $key }
    if ($key -is [int]) { return 'I:' + $key.ToString() }
    if ($key -is [uint32]) { return 'U32:' + $key.ToString() }
    if ($key -is [uint16]) { return 'U16:' + $key.ToString() }
    if ($key -is [PSCustomObject] -and $key.Type -eq 'JsonObject') { return 'J:' + $key.Assembly + '|' + $key.Class + '|' + $key.Json }
    return 'O:' + $key.GetType().FullName + ':' + $key.ToString()
}

function Decode-Catalog($path) {
    $json = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8) | ConvertFrom-Json
    $keyData = [Convert]::FromBase64String($json.m_KeyDataString)
    $bucketData = [Convert]::FromBase64String($json.m_BucketDataString)
    $entryData = [Convert]::FromBase64String($json.m_EntryDataString)
    $extraData = [Convert]::FromBase64String($json.m_ExtraDataString)

    $keyCount = Read-Int32 $keyData 0
    $keys = New-Object object[] $keyCount
    $offset = 4
    for ($i=0; $i -lt $keyCount; $i++) {
        $next = 0
        $keys[$i] = Read-Object $keyData $offset ([ref]$next)
        $offset = $next
    }

    $bucketCount = Read-Int32 $bucketData 0
    $buckets = New-Object object[] $bucketCount
    $bo = 4
    for ($i=0; $i -lt $bucketCount; $i++) {
        $dataOffset = Read-Int32 $bucketData $bo; $bo += 4
        $entryCount = Read-Int32 $bucketData $bo; $bo += 4
        $entries = New-Object int[] $entryCount
        for ($j=0; $j -lt $entryCount; $j++) {
            $entries[$j] = Read-Int32 $bucketData $bo; $bo += 4
        }
        $buckets[$i] = [PSCustomObject]@{ DataOffset = $dataOffset; Entries = $entries }
    }

    $entryCount = Read-Int32 $entryData 0
    $entries = New-Object object[] $entryCount
    $entryKeys = New-Object 'System.Collections.Generic.List[int][]' $entryCount
    for ($i=0; $i -lt $entryCount; $i++) {
        $entryKeys[$i] = New-Object 'System.Collections.Generic.List[int]'
    }
    $eo = 4
    for ($i=0; $i -lt $entryCount; $i++) {
        $internalIdIndex = Read-Int32 $entryData $eo; $eo += 4
        $providerIndex = Read-Int32 $entryData $eo; $eo += 4
        $dependencyKeyIndex = Read-Int32 $entryData $eo; $eo += 4
        $depHash = Read-Int32 $entryData $eo; $eo += 4
        $dataIndex = Read-Int32 $entryData $eo; $eo += 4
        $primaryKeyIndex = Read-Int32 $entryData $eo; $eo += 4
        $resourceTypeIndex = Read-Int32 $entryData $eo; $eo += 4
        $entries[$i] = [PSCustomObject]@{
            InternalIdIndex = $internalIdIndex
            ProviderIndex = $providerIndex
            DependencyKeyIndex = $dependencyKeyIndex
            DepHash = $depHash
            DataIndex = $dataIndex
            PrimaryKeyIndex = $primaryKeyIndex
            ResourceTypeIndex = $resourceTypeIndex
        }
    }
    for ($ki=0; $ki -lt $bucketCount; $ki++) {
        foreach ($ei in $buckets[$ki].Entries) {
            $entryKeys[$ei].Add($ki)
        }
    }

    return [PSCustomObject]@{
        Path = $path
        KeyCount = $keyCount
        Keys = $keys
        Buckets = $buckets
        EntryCount = $entryCount
        Entries = $entries
        EntryKeys = $entryKeys
        InternalIds = @($json.m_InternalIds)
        ProviderIds = @($json.m_ProviderIds)
        ResourceTypes = @($json.m_resourceTypes)
        ExtraData = $extraData
        Raw = $json
    }
}

function Add-UniqueString($list, $map, [string]$value) {
    if ($map.ContainsKey($value)) { return $map[$value] }
    $idx = $list.Count
    $list.Add($value)
    $map[$value] = $idx
    return $idx
}

function Add-UniqueKey($list, $map, $key) {
    $id = Get-KeyIdentity $key
    if ($map.ContainsKey($id)) { return $map[$id] }
    $idx = $list.Count
    $list.Add($key)
    $map[$id] = $idx
    return $idx
}

function Add-UniqueType($list, $map, $typeObj) {
    $asm = [string]$typeObj.m_AssemblyName
    $cls = [string]$typeObj.m_ClassName
    $id = $asm + '|' + $cls
    if ($map.ContainsKey($id)) { return $map[$id] }
    $idx = $list.Count
    $list.Add([PSCustomObject]@{ m_AssemblyName = $asm; m_ClassName = $cls })
    $map[$id] = $idx
    return $idx
}

function Get-BundleSuffixSet($cat) {
    $set = New-Object 'System.Collections.Generic.HashSet[string]'
    foreach ($id in $cat.InternalIds) {
        if ($id -like '*.bundle') {
            $idx = $id.IndexOf('StandaloneWindows64\')
            if ($idx -ge 0) {
                $suffix = $id.Substring($idx + 'StandaloneWindows64\'.Length).Replace('/','\')
                $null = $set.Add($suffix)
            }
        }
    }
    return $set
}

function Get-BundleLogicalName($internalId) {
    $idx = $internalId.IndexOf('StandaloneWindows64\')
    if ($idx -lt 0) { return $internalId }
    $suffix = $internalId.Substring($idx + 'StandaloneWindows64\'.Length).Replace('/','\')
    $name = [System.IO.Path]::GetFileNameWithoutExtension($suffix)
    if ($name -match '^(.*)_[0-9a-fA-F]{32}$') {
        return $Matches[1]
    }
    return $name
}


function Add-Entry($cat, $srcEntryIndex, $mergedEntries, $mergedExtra, $providerMap, $internalMap, $typeMap, $keyMap, $mergedProviders, $mergedInternalIds, $mergedTypes, $mergedKeys, $keyRemap) {
    $e = $cat.Entries[$srcEntryIndex]
    $provider = $cat.ProviderIds[$e.ProviderIndex]
    $internalId = $cat.InternalIds[$e.InternalIdIndex]
    $typeObj = $cat.ResourceTypes[$e.ResourceTypeIndex]

    $newProvider = Add-UniqueString $mergedProviders $providerMap $provider
    $newInternal = Add-UniqueString $mergedInternalIds $internalMap $internalId
    $newType = Add-UniqueType $mergedTypes $typeMap $typeObj
    $newPrimary = Add-UniqueKey $mergedKeys $keyMap $cat.Keys[$e.PrimaryKeyIndex]

    $newDep = -1
    if ($e.DependencyKeyIndex -ge 0) {
        $depKey = $cat.Keys[$e.DependencyKeyIndex]
        if ($null -ne $keyRemap -and $keyRemap.ContainsKey((Get-KeyIdentity $depKey))) {
            $depKey = $keyRemap[(Get-KeyIdentity $depKey)]
        }
        $newDep = Add-UniqueKey $mergedKeys $keyMap $depKey
    }

    $newData = -1
    if ($e.DataIndex -ge 0 -and $e.DataIndex -lt $cat.ExtraData.Length) {
        $len = Get-ObjectLength $cat.ExtraData $e.DataIndex
        $newData = $mergedExtra.Count
        for ($k=0; $k -lt $len; $k++) {
            $mergedExtra.Add($cat.ExtraData[$e.DataIndex + $k])
        }
    }

    $newEntryIndex = $mergedEntries.Count
    $mergedEntries.Add([PSCustomObject]@{
        InternalIdIndex = $newInternal
        ProviderIndex = $newProvider
        DependencyKeyIndex = $newDep
        DepHash = $e.DepHash
        DataIndex = $newData
        PrimaryKeyIndex = $newPrimary
        ResourceTypeIndex = $newType
    })

    # Ensure all keys associated with this entry are added to mergedKeys.
    foreach ($srcKeyIndex in $cat.EntryKeys[$srcEntryIndex]) {
    }

    return $newEntryIndex
}

$mainCat = Decode-Catalog $MainCatalog
$srcCat = Decode-Catalog $ExtraSourceCatalog

if ($SourceEntryCount -le 0 -or $SourceEntryCount -gt $srcCat.EntryCount) {
    $SourceEntryCount = $srcCat.EntryCount
}

Write-Host "[Extra] main entries=$($mainCat.EntryCount) source entries=$($srcCat.EntryCount) usingSourceEntries=$SourceEntryCount"

$abp = 'UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider'

# Main (branch2) address set
$mainAddresses = New-Object 'System.Collections.Generic.HashSet[string]'
for ($i=0; $i -lt $mainCat.EntryCount; $i++) {
    $e = $mainCat.Entries[$i]
    if ($mainCat.ProviderIds[$e.ProviderIndex] -eq $abp) { continue }
    $pk = $mainCat.Keys[$e.PrimaryKeyIndex]
    if ($pk -is [string]) { $null = $mainAddresses.Add($pk) }
}

# Main bundle internal ids and logical map
$mainBundleInternalIds = New-Object 'System.Collections.Generic.HashSet[string]'
$mainBundleLogical = @{}
for ($i=0; $i -lt $mainCat.EntryCount; $i++) {
    $e = $mainCat.Entries[$i]
    if ($mainCat.ProviderIds[$e.ProviderIndex] -ne $abp) { continue }
    $internalId = $mainCat.InternalIds[$e.InternalIdIndex]
    $null = $mainBundleInternalIds.Add($internalId)
    $logical = Get-BundleLogicalName $internalId
    if (-not $mainBundleLogical.ContainsKey($logical)) {
        $mainBundleLogical[$logical] = New-Object 'System.Collections.Generic.List[string]'
    }
    $mainBundleLogical[$logical].Add((Get-BundleKey $internalId))
}

# Source (branch1) bundle logical map, only selected entries
$srcBundleLogical = @{}
for ($i=0; $i -lt $SourceEntryCount; $i++) {
    $e = $srcCat.Entries[$i]
    if ($srcCat.ProviderIds[$e.ProviderIndex] -ne $abp) { continue }
    $internalId = $srcCat.InternalIds[$e.InternalIdIndex]
    $logical = Get-BundleLogicalName $internalId
    if (-not $srcBundleLogical.ContainsKey($logical)) {
        $srcBundleLogical[$logical] = New-Object 'System.Collections.Generic.List[string]'
    }
    $srcBundleLogical[$logical].Add((Get-BundleKey $internalId))
}

# Build remap: source overlapping bundle key -> main bundle key
$keyRemap = @{}
foreach ($logical in $srcBundleLogical.Keys) {
    if (-not $mainBundleLogical.ContainsKey($logical)) { continue }
    $mainKey = $mainBundleLogical[$logical][0]
    foreach ($srcKey in $srcBundleLogical[$logical]) {
        if ($srcKey -cne $mainKey) {
            $keyRemap[(Get-KeyIdentity $srcKey)] = $mainKey
        }
    }
}
Write-Host "[Extra] source overlapping bundle keys to remap: $($keyRemap.Count)"

# Merged collections for extra catalog
$mergedProviders = New-Object System.Collections.Generic.List[string]
$mergedInternalIds = New-Object System.Collections.Generic.List[string]
$mergedTypes = New-Object System.Collections.Generic.List[object]
$mergedKeys = New-Object System.Collections.Generic.List[object]
$providerMap = @{}
$internalMap = @{}
$typeMap = @{}
$keyMap = @{}
$mergedEntries = New-Object System.Collections.Generic.List[object]
$mergedExtra = New-Object System.Collections.Generic.List[byte]

$srcMergedIndex = New-Object int[] $srcCat.EntryCount
for ($i=0; $i -lt $srcCat.EntryCount; $i++) { $srcMergedIndex[$i] = -1 }

$skippedAddress = 0
$skippedBundle = 0
$added = 0
$addressList = New-Object System.Collections.Generic.List[string]

for ($i=0; $i -lt $SourceEntryCount; $i++) {
    $e = $srcCat.Entries[$i]
    $provider = $srcCat.ProviderIds[$e.ProviderIndex]
    $isBundleEntry = ($provider -eq $abp)

    if ($isBundleEntry) {
        $internalId = $srcCat.InternalIds[$e.InternalIdIndex]
        $bundleKey = Get-BundleKey $internalId
        if ($mainBundleInternalIds.Contains($internalId) -or $keyRemap.ContainsKey((Get-KeyIdentity $bundleKey))) {
            $skippedBundle++
            continue
        }
    } else {
        $pk = $srcCat.Keys[$e.PrimaryKeyIndex]
        if ($pk -is [string] -and $mainAddresses.Contains($pk)) {
            $skippedAddress++
            continue
        }
        if ($pk -is [string]) {
            $addressList.Add($pk)
        }
    }

    $mi = Add-Entry $srcCat $i $mergedEntries $mergedExtra $providerMap $internalMap $typeMap $keyMap $mergedProviders $mergedInternalIds $mergedTypes $mergedKeys $keyRemap
    $srcMergedIndex[$i] = $mi
    $added++
}

Write-Host "[Extra] added=$added skippedAddress=$skippedAddress skippedBundle=$skippedBundle"

# Build key -> entries preserving source bucket order, with remap applied
$keyToEntries = @{}
for ($ki=0; $ki -lt $srcCat.KeyCount; $ki++) {
    $list = New-Object 'System.Collections.Generic.List[int]'
    foreach ($ei in $srcCat.Buckets[$ki].Entries) {
        if ($ei -ge $SourceEntryCount) { continue }
        $mi = $srcMergedIndex[$ei]
        if ($mi -ge 0) { $list.Add($mi) }
    }
    if ($list.Count -gt 0) {
        $key = $srcCat.Keys[$ki]
        $keyId = Get-KeyIdentity $key
        if ($keyRemap.ContainsKey($keyId)) {
            $key = $keyRemap[$keyId]
        }
        $mki = Add-UniqueKey $mergedKeys $keyMap $key
        if (-not $keyToEntries.ContainsKey($mki)) {
            $keyToEntries[$mki] = New-Object 'System.Collections.Generic.List[int]'
        }
        foreach ($mi in $list) {
            $keyToEntries[$mki].Add($mi)
        }
    }
}

Write-Host "[Extra] merged providers=$($mergedProviders.Count) internalIds=$($mergedInternalIds.Count) keys=$($mergedKeys.Count) entries=$($mergedEntries.Count) extraBytes=$($mergedExtra.Count)"

# Serialize extra catalog
$keyDataList = New-Object System.Collections.Generic.List[byte]
$keyDataList.AddRange([BitConverter]::GetBytes([int]$mergedKeys.Count))
$keyOffsets = New-Object int[] $mergedKeys.Count
for ($i=0; $i -lt $mergedKeys.Count; $i++) {
    $keyOffsets[$i] = $keyDataList.Count
    $bytes = Write-ObjectBytes $mergedKeys[$i]
    $keyDataList.AddRange([byte[]]$bytes)
}
$keyData = $keyDataList.ToArray()

$bucketDataList = New-Object System.Collections.Generic.List[byte]
$bucketDataList.AddRange([BitConverter]::GetBytes([int]$mergedKeys.Count))
for ($i=0; $i -lt $mergedKeys.Count; $i++) {
    $bucketDataList.AddRange([BitConverter]::GetBytes([int]$keyOffsets[$i]))
    $entryList = $keyToEntries[$i]
    $bucketDataList.AddRange([BitConverter]::GetBytes([int]$entryList.Count))
    foreach ($ei in $entryList) {
        $bucketDataList.AddRange([BitConverter]::GetBytes([int]$ei))
    }
}
$bucketData = $bucketDataList.ToArray()

$entryDataList = New-Object System.Collections.Generic.List[byte]
$entryDataList.AddRange([BitConverter]::GetBytes([int]$mergedEntries.Count))
foreach ($e in $mergedEntries) {
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.InternalIdIndex))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.ProviderIndex))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.DependencyKeyIndex))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.DepHash))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.DataIndex))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.PrimaryKeyIndex))
    $entryDataList.AddRange([BitConverter]::GetBytes([int]$e.ResourceTypeIndex))
}
$entryData = $entryDataList.ToArray()
$extraData = $mergedExtra.ToArray()

$out = [PSCustomObject]@{
    m_LocatorId = 'Branch1ExtraContentCatalog'
    m_InstanceProviderData = $mainCat.Raw.m_InstanceProviderData
    m_SceneProviderData = $mainCat.Raw.m_SceneProviderData
    m_ResourceProviderData = $mainCat.Raw.m_ResourceProviderData
    m_ProviderIds = $mergedProviders.ToArray()
    m_InternalIds = $mergedInternalIds.ToArray()
    m_KeyDataString = [Convert]::ToBase64String($keyData)
    m_BucketDataString = [Convert]::ToBase64String($bucketData)
    m_EntryDataString = [Convert]::ToBase64String($entryData)
    m_ExtraDataString = [Convert]::ToBase64String($extraData)
    m_resourceTypes = $mergedTypes.ToArray()
    m_InternalIdPrefixes = @()
}

$json = ConvertTo-Json -InputObject $out -Depth 20 -Compress
$tmp = $OutputExtraCatalog + '.tmp'
[System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
if (Test-Path $OutputExtraCatalog) { Remove-Item $OutputExtraCatalog -Force }
Move-Item -Path $tmp -Destination $OutputExtraCatalog -Force
Write-Host "[Extra] Wrote $OutputExtraCatalog ($((Get-Item $OutputExtraCatalog).Length) bytes)"

# Write address list
$addressList | Sort-Object -Unique | Set-Content -Path $OutputAddressList -Encoding UTF8
Write-Host "[Extra] Wrote address list: $OutputAddressList ($((@($addressList | Sort-Object -Unique)).Count) addresses)"

# Copy unique source bundles
$srcBase = Join-Path $ExtraSourceBundleRoot 'StandaloneWindows64'
$dstBase = Join-Path $OutputBundleRoot 'StandaloneWindows64'
$copied = 0
$skippedSame = 0
$conflict = 0
for ($i=0; $i -lt $SourceEntryCount; $i++) {
    if ($srcMergedIndex[$i] -lt 0) { continue }
    $e = $srcCat.Entries[$i]
    if ($srcCat.ProviderIds[$e.ProviderIndex] -ne $abp) { continue }
    $internalId = $srcCat.InternalIds[$e.InternalIdIndex]
    $idx = $internalId.IndexOf('StandaloneWindows64\')
    if ($idx -lt 0) { continue }
    $suffix = $internalId.Substring($idx + 'StandaloneWindows64\'.Length).Replace('/','\')
    $srcFile = Join-Path $srcBase $suffix
    $dstFile = Join-Path $dstBase $suffix
    if (-not (Test-Path $srcFile)) {
        Write-Warning "[Extra] missing source bundle: $suffix"
        continue
    }
    $dstDir = Split-Path $dstFile -Parent
    if (-not (Test-Path $dstDir)) { New-Item -ItemType Directory -Force -Path $dstDir | Out-Null }
    if (Test-Path $dstFile) {
        $h1 = (Get-FileHash $dstFile -Algorithm MD5).Hash
        $h2 = (Get-FileHash $srcFile -Algorithm MD5).Hash
        if ($h1 -eq $h2) { $skippedSame++; continue }
        Write-Warning "[Extra] conflict different content: $suffix"
        $conflict++
        continue
    }
    Copy-Item -Path $srcFile -Destination $dstFile -Force
    $copied++
}
Write-Host "[Extra] bundle copy done: copied=$copied skippedSame=$skippedSame conflictsDifferent=$conflict"
