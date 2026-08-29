param(
    [string]$B1Catalog = 'D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa\catalog.json',
    [string]$B1BundleRoot = 'D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa',
    [string]$B2Catalog = 'D:\UnityWork\Ark_N\ArknightN_Data\StreamingAssets\aa\catalog.json',
    [string]$B2BundleRoot = 'D:\UnityWork\Ark_N\ArknightN_Data\StreamingAssets\aa',
    [string]$OutputCatalog = 'D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa\catalog.json',
    [switch]$CopyBundles = $true
)

$ErrorActionPreference = 'Stop'

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

$b1 = Decode-Catalog $B1Catalog
$b2 = Decode-Catalog $B2Catalog

Write-Host "[Merge] B1 keys=$($b1.KeyCount) entries=$($b1.EntryCount) internalIds=$($b1.InternalIds.Count)"
Write-Host "[Merge] B2 keys=$($b2.KeyCount) entries=$($b2.EntryCount) internalIds=$($b2.InternalIds.Count)"

$b1BundleSuffixes = Get-BundleSuffixSet $b1
$b2BundleSuffixes = Get-BundleSuffixSet $b2

$b1AddressSet = New-Object 'System.Collections.Generic.HashSet[string]'
$b1ProviderAssetBundle = 'UnityEngine.ResourceManagement.ResourceProviders.AssetBundleProvider'
for ($i=0; $i -lt $b1.EntryCount; $i++) {
    $e = $b1.Entries[$i]
    $provider = $b1.ProviderIds[$e.ProviderIndex]
    if ($provider -eq $b1ProviderAssetBundle) { continue }
    $pk = $b1.Keys[$e.PrimaryKeyIndex]
    if ($pk -is [string]) {
        $null = $b1AddressSet.Add($pk)
    }
}

$b1BundleInternalIds = New-Object 'System.Collections.Generic.HashSet[string]'
for ($i=0; $i -lt $b1.EntryCount; $i++) {
    $e = $b1.Entries[$i]
    $provider = $b1.ProviderIds[$e.ProviderIndex]
    if ($provider -eq $b1ProviderAssetBundle) {
        $null = $b1BundleInternalIds.Add($b1.InternalIds[$e.InternalIdIndex])
    }
}

Write-Host "[Merge] B1 address count=$($b1AddressSet.Count) B1 bundle internal ids=$($b1BundleInternalIds.Count)"

# Build logical bundle maps for remapping B2 overlapping bundles to B1 equivalents.
$b1BundleLogical = @{}
for ($i=0; $i -lt $b1.EntryCount; $i++) {
    $e = $b1.Entries[$i]
    if ($b1.ProviderIds[$e.ProviderIndex] -ne $b1ProviderAssetBundle) { continue }
    $internalId = $b1.InternalIds[$e.InternalIdIndex]
    $idx = $internalId.IndexOf('StandaloneWindows64\')
    if ($idx -lt 0) { continue }
    $key = $internalId.Substring($idx + 'StandaloneWindows64\'.Length)
    $logical = Get-BundleLogicalName $internalId
    if (-not $b1BundleLogical.ContainsKey($logical)) {
        $b1BundleLogical[$logical] = New-Object 'System.Collections.Generic.List[string]'
    }
    $b1BundleLogical[$logical].Add($key)
}

$b2BundleLogical = @{}
for ($i=0; $i -lt $b2.EntryCount; $i++) {
    $e = $b2.Entries[$i]
    if ($b2.ProviderIds[$e.ProviderIndex] -ne $b1ProviderAssetBundle) { continue }
    $internalId = $b2.InternalIds[$e.InternalIdIndex]
    $idx = $internalId.IndexOf('StandaloneWindows64\')
    if ($idx -lt 0) { continue }
    $key = $internalId.Substring($idx + 'StandaloneWindows64\'.Length)
    $logical = Get-BundleLogicalName $internalId
    if (-not $b2BundleLogical.ContainsKey($logical)) {
        $b2BundleLogical[$logical] = New-Object 'System.Collections.Generic.List[string]'
    }
    $b2BundleLogical[$logical].Add($key)
}

$keyRemap = @{}
foreach ($logical in $b2BundleLogical.Keys) {
    if (-not $b1BundleLogical.ContainsKey($logical)) { continue }
    $b1Key = $b1BundleLogical[$logical][0]
    foreach ($b2Key in $b2BundleLogical[$logical]) {
        if ($b2Key -cne $b1Key) {
            $keyRemap[(Get-KeyIdentity $b2Key)] = $b1Key
        }
    }
}
Write-Host "[Merge] B2 overlapping bundle keys to remap: $($keyRemap.Count)"


$mergedProviders = New-Object System.Collections.Generic.List[string]
$mergedInternalIds = New-Object System.Collections.Generic.List[string]
$mergedTypes = New-Object System.Collections.Generic.List[object]
$mergedKeys = New-Object System.Collections.Generic.List[object]

$providerMap = @{}
$internalMap = @{}
$typeMap = @{}
$keyMap = @{}
$keyToEntries = @{}
$mergedEntries = New-Object System.Collections.Generic.List[object]
$mergedExtra = New-Object System.Collections.Generic.List[byte]

$b1Added = 0
for ($i=0; $i -lt $b1.EntryCount; $i++) {
    $null = Add-Entry $b1 $i $mergedEntries $mergedExtra $providerMap $internalMap $typeMap $keyMap $mergedProviders $mergedInternalIds $mergedTypes $mergedKeys $null
    $b1Added++
}

$b2MergedIndex = New-Object int[] $b2.EntryCount
for ($i=0; $i -lt $b2.EntryCount; $i++) {
    $b2MergedIndex[$i] = -1
}

$b2SkippedAddress = 0
$b2SkippedBundle = 0
$b2Added = 0
for ($i=0; $i -lt $b2.EntryCount; $i++) {
    $e = $b2.Entries[$i]
    $provider = $b2.ProviderIds[$e.ProviderIndex]
    $isBundleEntry = ($provider -eq $b1ProviderAssetBundle)

    if ($isBundleEntry) {
        $internalId = $b2.InternalIds[$e.InternalIdIndex]
        $idx = $internalId.IndexOf('StandaloneWindows64\')
        $b2BundleKey = if ($idx -ge 0) { $internalId.Substring($idx + 'StandaloneWindows64\'.Length) } else { $internalId }
        if ($b1BundleInternalIds.Contains($internalId)) {
            $b2SkippedBundle++
            continue
        }
        if ($keyRemap.ContainsKey((Get-KeyIdentity $b2BundleKey))) {
            $b2SkippedBundle++
            continue
        }
    } else {
        $pk = $b2.Keys[$e.PrimaryKeyIndex]
        if ($pk -is [string] -and $b1AddressSet.Contains($pk)) {
            $b2SkippedAddress++
            continue
        }
    }

    $mi = Add-Entry $b2 $i $mergedEntries $mergedExtra $providerMap $internalMap $typeMap $keyMap $mergedProviders $mergedInternalIds $mergedTypes $mergedKeys $keyRemap
    $b2MergedIndex[$i] = $mi
    $b2Added++
}

# 閹稿甯慨?bucket 妞ゅ搫绨柌宥呯紦 key -> entries 閺勭姴鐨犻敍灞芥晼闁插繋绻氶幐?B1 閸樼喐婀佹い鍝勭碍
$keyToEntries = @{}
for ($ki=0; $ki -lt $b1.KeyCount; $ki++) {
    $mki = Add-UniqueKey $mergedKeys $keyMap $b1.Keys[$ki]
    if (-not $keyToEntries.ContainsKey($mki)) {
        $keyToEntries[$mki] = New-Object 'System.Collections.Generic.List[int]'
    }
    foreach ($ei in $b1.Buckets[$ki].Entries) {
        $keyToEntries[$mki].Add($ei)
    }
}
for ($ki=0; $ki -lt $b2.KeyCount; $ki++) {
    $list = New-Object 'System.Collections.Generic.List[int]'
    foreach ($ei in $b2.Buckets[$ki].Entries) {
        $mi = $b2MergedIndex[$ei]
        if ($mi -ge 0) {
            $list.Add($mi)
        }
    }
    if ($list.Count -gt 0) {
        $mki = Add-UniqueKey $mergedKeys $keyMap $b2.Keys[$ki]
        if (-not $keyToEntries.ContainsKey($mki)) {
            $keyToEntries[$mki] = New-Object 'System.Collections.Generic.List[int]'
        }
        foreach ($mi in $list) {
            $keyToEntries[$mki].Add($mi)
        }
    }
}

Write-Host "[Merge] B1 added=$b1Added B2 added=$b2Added B2 skippedAddress=$b2SkippedAddress B2 skippedDuplicateBundle=$b2SkippedBundle"
Write-Host "[Merge] merged providers=$($mergedProviders.Count) internalIds=$($mergedInternalIds.Count) keys=$($mergedKeys.Count) entries=$($mergedEntries.Count) extraBytes=$($mergedExtra.Count)"

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
    m_LocatorId = $b1.Raw.m_LocatorId
    m_InstanceProviderData = $b1.Raw.m_InstanceProviderData
    m_SceneProviderData = $b1.Raw.m_SceneProviderData
    m_ResourceProviderData = $b1.Raw.m_ResourceProviderData
    m_ProviderIds = $mergedProviders.ToArray()
    m_InternalIds = $mergedInternalIds.ToArray()
    m_KeyDataString = [Convert]::ToBase64String($keyData)
    m_BucketDataString = [Convert]::ToBase64String($bucketData)
    m_EntryDataString = [Convert]::ToBase64String($entryData)
    m_ExtraDataString = [Convert]::ToBase64String($extraData)
    m_resourceTypes = $mergedTypes.ToArray()
    m_InternalIdPrefixes = @($b1.Raw.m_InternalIdPrefixes)
}

$json = ConvertTo-Json -InputObject $out -Depth 20 -Compress

$tmp = $OutputCatalog + '.tmp'
[System.IO.File]::WriteAllText($tmp, $json, (New-Object System.Text.UTF8Encoding($false)))
if (Test-Path $OutputCatalog) {
    Remove-Item $OutputCatalog -Force
}
Move-Item -Path $tmp -Destination $OutputCatalog -Force
Write-Host "[Merge] Wrote $OutputCatalog ($((Get-Item $OutputCatalog).Length) bytes)"

if ($CopyBundles) {
$destBase = Join-Path $B1BundleRoot 'StandaloneWindows64'
$srcBase = Join-Path $B2BundleRoot 'StandaloneWindows64'
$copyCount = 0
$skipCount = 0
$errorCount = 0
$allSrc = Get-ChildItem $srcBase -Recurse -File -Filter *.bundle
foreach ($srcFile in $allSrc) {
    $rel = $srcFile.FullName.Substring($srcBase.Length + 1)
    $destFile = Join-Path $destBase $rel
    $destDir = Split-Path $destFile -Parent
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    }
    if (Test-Path $destFile) {
        $h1 = (Get-FileHash $destFile -Algorithm MD5).Hash
        $h2 = (Get-FileHash $srcFile.FullName -Algorithm MD5).Hash
        if ($h1 -eq $h2) {
            $skipCount++
            continue
        } else {
            Write-Warning "[Merge] Conflicting bundle with different content: $rel"
            $errorCount++
            continue
        }
    }
    Copy-Item -Path $srcFile.FullName -Destination $destFile -Force
    $copyCount++
}
Write-Host "[Merge] Bundle copy done: copied=$copyCount skippedSame=$skipCount conflictsDifferent=$errorCount"

}

