# MergeCatalogTool 使用文档

> 适用项目：`D:\UnityWork\zhou-master`  
> 工具类型：Unity Editor 工具  
> 主要文件：
> - `Assets/Editor/MergeCatalogTool.cs`
> - `Assets/Editor/MergeCatalogWindow.cs`
>
> 相关命令行脚本：
> - `Tools/merge_catalogs.ps1`
> - `Tools/generate_extra_catalog.ps1`

---

## 1. 工具简介

`MergeCatalogTool` 是用于合并两个 Unity Addressables 构建产物的 Editor 工具。

它的核心目标是：

- 让一个闭源分支（branch1）能够加载另一个开源分支（branch2）中的独有 Addressables 资源；
- 或者在 branch2 工程中，以“附加 catalog”的方式加载 branch1 的独有资源；
- 尽量避免两个分支之间“同名、同逻辑、但内容不同”的 AssetBundle 发生冲突。

工具当前支持两个方向：

1. **正向合并**
   - 以 branch1 为主；
   - 把 branch2 独有资源合并进 branch1 的 catalog；
   - 输出到 branch1 的构建目录。

2. **反向生成附加 catalog**
   - 以 branch2 为主；
   - 从 branch1 中筛出 branch2 没有的资源；
   - 生成一个独立的 `extra_catalog.json`；
   - branch2 运行时通过 `Addressables.LoadContentCatalogAsync()` 加载。

---

## 2. 打开工具

在 Unity 中打开 branch2 工程后，点击菜单：

```
Tools > Merge Catalog > Open Merge Window
```

会打开 `Catalog Merge Tool` 窗口。

窗口中的路径会保存到 `EditorPrefs`，下次打开时自动恢复。

---

## 3. 工具界面说明

窗口包含以下字段：

| 字段 | 说明 |
|---|---|
| `Branch1 Catalog (base)` | branch1 的 `catalog.json` |
| `Branch1 aa Bundle Root` | branch1 的 `aa` 目录，脚本会自动拼接 `StandaloneWindows64` |
| `Branch2 Catalog (to merge in)` | branch2 的 `catalog.json` |
| `Branch2 aa Bundle Root` | branch2 的 `aa` 目录，脚本会自动拼接 `StandaloneWindows64` |
| `Output Catalog (empty = overwrite B1)` | 合并后的 catalog 输出路径；留空则覆盖 Branch1 Catalog |
| `Copy B2 bundles into B1` | 是否把 branch2 独有 bundle 复制到 branch1 |

窗口包含四个按钮：

| 按钮 | 功能 |
|---|---|
| `Inspect Catalogs` | 只分析两个 catalog，不写文件 |
| `Merge + Copy Bundles` | 正向合并 catalog，并复制 branch2 独有 bundle |
| `Merge Catalog Only` | 正向合并 catalog，不复制 bundle |
| `Generate Branch1 Extra For Branch2` | 反向生成 branch1 附加 catalog，供 branch2 运行时加载 |

---

## 4. 正向合并：将 branch2 资源合并进 branch1

### 4.1 使用场景

- branch1 是闭源完整构建；
- branch2 是开源工程；
- 希望 branch1 能加载 branch2 的独有资源；
- branch1 原有资源必须保持可用。

### 4.2 选择路径示例

| 字段 | 示例 |
|---|---|
| Branch1 Catalog | `D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa\catalog.json` |
| Branch1 aa Bundle Root | `D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa` |
| Branch2 Catalog | `D:\UnityWork\Ark_N\ArknightN_Data\StreamingAssets\aa\catalog.json` |
| Branch2 aa Bundle Root | `D:\UnityWork\Ark_N\ArknightN_Data\StreamingAssets\aa` |
| Output Catalog | 留空，或与 Branch1 Catalog 相同 |

> 注意：Bundle Root 要填到 `aa` 这一层，不要填到 `StandaloneWindows64` 里面。

### 4.3 点击按钮

```
Merge + Copy Bundles
```

### 4.4 合并策略

正向合并遵循以下规则：

1. **保留 branch1 全部条目**
   - branch1 原有资源、address、bundle 都不删除；
   - branch1 原有逻辑优先。

2. **重复 address 保留 branch1**
   - 如果同一个 address 在 branch1 和 branch2 都存在；
   - 跳过 branch2 的该 address 条目；
   - branch1 继续使用自己的版本。

3. **bundle 逻辑冲突时重定向**
   - 比较两个分支 bundle 的“逻辑名称”；
   - 逻辑名称会去掉末尾的 32 位 hash；
   - 例如：
     - branch1：`common_assets_all_8e94...bundle`
     - branch2：`common_assets_all_d75...bundle`
   - 两者逻辑名称相同，属于同一逻辑 bundle；
   - 合并时跳过 branch2 的这个 bundle；
   - 把 branch2 独有资源对它的依赖，重定向到 branch1 对应 bundle。

4. **只复制 branch2 逻辑不同名的 bundle**
   - branch2 中与 branch1 逻辑同名/冲突的 bundle 不复制；
   - 只复制 branch2 真正新增的 bundle；
   - 避免 branch1 目录中出现无用或冲突文件。

### 4.5 输出结果

- 合并后的 `catalog.json` 写入 Output Catalog；
- branch2 独有 bundle 复制到 `Branch1 aa Bundle Root/StandaloneWindows64/`；
- 控制台输出统计：

```
[Merge] B1 entries=...
[Merge] B2 entries=...
[Merge] merged catalog written: ...
[Merge] addedB2=... skippedAddress=... skippedDuplicateBundle=...
[Merge] bundle copy done: copied=... skippedSame=... conflictsDifferent=... missing=...
```

---

## 5. 反向生成：为 branch2 生成 branch1 附加 catalog

### 5.1 使用场景

- branch2 有完整工程，可控；
- branch1 是闭源构建；
- 希望 branch2 在编辑器或打包后加载 branch1 的独有资源；
- 不想修改 branch1 的构建目录。

### 5.2 选择路径示例

| 字段 | 示例 |
|---|---|
| Branch1 Catalog | branch1 的原始 `catalog.json` |
| Branch1 aa Bundle Root | branch1 的 `aa` 目录 |
| Branch2 Catalog | branch2 的 `catalog.json` |
| Branch2 aa Bundle Root | branch2 的 `aa` 目录 |

> 反向生成时，建议 Branch1 Catalog 使用**原始 branch1 catalog**。  
> 如果只有已经合并过的 branch1 catalog，PowerShell 脚本可以按前 N 条 B1 原始条目生成，但 Unity 工具默认推荐使用原始 catalog。

### 5.3 点击按钮

```
Generate Branch1 Extra For Branch2
```

### 5.4 生成策略

1. **以 branch2 为主**
   - branch2 原有 address、bundle 全部保留；
   - branch2 逻辑优先。

2. **跳过 branch1 中与 branch2 重复的 address**
   - branch2 已有该 address 时，branch1 的该条目不加入。

3. **跳过 branch1 中与 branch2 逻辑同名的 bundle**
   - 这些 bundle 不复制；
   - branch1 独有资源对它们的依赖，改为指向 branch2 对应 bundle。

4. **只保留 branch1 真正独有的资源**
   - branch2 没有的 address；
   - branch2 没有的逻辑 bundle。

### 5.5 输出结果

默认输出：

```
<Branch2 aa Bundle Root>/extra_catalog.json
```

独有 bundle 复制到：

```
<Branch2 aa Bundle Root>/StandaloneWindows64/
```

address 清单输出到：

```
<Tools>/branch1_extra_addresses.txt
```

控制台输出：

```
[Extra] main entries=...
[Extra] added=... skippedAddress=... skippedBundle=...
[Extra] extra catalog written: ...
[Extra] Wrote address list: ...
[Extra] bundle copy done: copied=... skippedSame=... missing=...
```

---

## 6. 运行时加载附加 catalog

### 6.1 在 `Init.cs` 中加载

branch2 的启动脚本 `Assets/Scripts/Init.cs` 已经集成了附加 catalog 加载流程：

```csharp
// 2.3. 加载附加 catalog（branch1 独有资源）
landing?.SetProgress(0.04f, "正在加载附加资源...");
await LoadExtraCatalogAsync();
await Task.Yield();
```

加载方法：

```csharp
private async Task LoadExtraCatalogAsync()
{
    var extraCatalogPath = UnityEngine.AddressableAssets.Addressables.RuntimePath + "/extra_catalog.json";
    try
    {
        await UnityEngine.AddressableAssets.Addressables.LoadContentCatalogAsync(extraCatalogPath, true).Task;
        Debug.Log("[Init] Extra catalog loaded: " + extraCatalogPath);
    }
    catch (Exception e)
    {
        Debug.LogError("[Init] Failed to load extra catalog: " + e);
    }
}
```

### 6.2 编辑器测试

1. 确保 branch2 已经构建过 Addressables；
2. 打开 Addressables Groups 窗口；
3. 将 `Play Mode Script` 切换为：

```
Use Existing Build (Packed Play Mode)
```

4. 运行游戏；
5. 观察 Console：

```
[Init] Extra catalog loaded: .../extra_catalog.json
```

### 6.3 打包注意事项

编辑器下 `Addressables.RuntimePath` 指向：

```
Library/com.unity.addressables/aa/Windows
```

打包后指向：

```
Application.streamingAssetsPath/aa
```

因此打包时需要把：

- `extra_catalog.json`
- branch1 独有 bundle

放入最终包的 `StreamingAssets/aa/` 下。

---

## 7. 核心实现逻辑

### 7.1 Address 粒度冲突处理

- 正向合并：相同 address 保留 branch1；
- 反向生成：相同 address 保留 branch2。

### 7.2 Bundle 逻辑名称匹配

工具会去掉 bundle 文件名末尾的 32 位 hash，得到逻辑名称。

例如：

```
common_assets_all_d75a2e0e34767d575681cfd249a4aad2.bundle
```

逻辑名为：

```
common_assets_all
```

两个分支中逻辑名称相同的 bundle，被视为同一逻辑 bundle。

### 7.3 依赖重定向

对于被跳过的逻辑冲突 bundle：

- 正向合并：把 branch2 资源的依赖改指向 branch1 对应 bundle；
- 反向生成：把 branch1 资源的依赖改指向 branch2 对应 bundle。

这样可以避免 Unity 报：

```
The AssetBundle '...' can't be loaded because another AssetBundle with the same files is already loaded.
```

### 7.4 只复制被采用的 bundle

工具只复制最终被加入 catalog 的独有 bundle。

- `Merge + Copy Bundles`：复制 branch2 独有 bundle；
- `Generate Branch1 Extra For Branch2`：复制 branch1 独有 bundle。

---

## 8. 常见问题

### 8.1 所有 bundle 都被跳过：`copied=0 skippedSame=...`

通常是因为 `Branch1 aa Bundle Root` 填成了 branch2 的 `aa` 目录。

例如错误填法：

```
D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Windows
```

结果：branch2 的 bundle 在 branch2 的 aa 目录里当然已经存在，所以全部被跳过。

正确填法：

```
D:\ArknightR\ArknightR0403\ArknightR\ArknightR_Data\StreamingAssets\aa
```

当前版本的窗口在点击 `Merge + Copy Bundles` 时，会自动把 `Branch1 aa Bundle Root` 设置为输出 catalog 所在目录，避免该问题。

### 8.2 Spine 模型随机变紫

可能原因：

- branch2 的 Spine 模型使用了 branch1 没有的 Shader；
- 合并时该 Shader 所在 bundle 被判定为逻辑同名 bundle，依赖被重定向到了 branch1；
- branch1 对应 bundle 中没有该 Shader；
- 某些情况下 Shader 已被其他途径加载，所以看起来正常；否则材质变紫。

典型例子：

```
Assets/Plugins/Spine/Runtime/spine-unity/Shaders/Spine-Skeleton-Tint
```

这个 Shader 在 branch1 原始 catalog 中不存在，但在 branch2 中存在。

解决方向：

- 在 branch2 工程中把这类 branch1 没有的 Shader/Material 单独拆到独立 Addressables Group；
- 重新构建 branch2 后重新生成合并 catalog；
- 让它们作为 branch2 独有 bundle 被复制，而不是被重定向。

### 8.3 加载 branch2 资源时卡住

可能原因：

- branch2 资源依赖了 branch2 的公共 bundle；
- 该 bundle 与 branch1 已加载的公共 bundle 冲突；
- Unity 报：

```
another AssetBundle with the same files is already loaded
```

解决方式：

- 使用当前工具的逻辑重定向功能；
- 重新生成合并 catalog；
- 确认 branch2 资源依赖已指向 branch1 对应 bundle。

### 8.4 编辑器工具报 CS0103

如果出现类似：

```
error CS0103: The name 'addedB2BundleInternalIds' does not exist in the current context
```

通常是分支合并后方法签名和内部变量不一致导致的。

当前 `CopyB2Bundles` 的签名是：

```csharp
static void CopyB2Bundles(
    string b1BundleRoot,
    string b2BundleRoot,
    HashSet<string> bundleInternalIds)
```

内部循环应使用：

```csharp
foreach (var internalId in bundleInternalIds)
```

如果再次出现类似错误，检查方法参数名与内部变量名是否一致。

---

## 9. 命令行脚本

如果不想通过 Unity 窗口执行，也可以使用 PowerShell 脚本。

### 9.1 正向合并

```powershell
& 'D:\UnityWork\zhou-master\Tools\merge_catalogs.ps1'
```

默认参数会读取 branch1 / branch2 的 catalog 和 bundle 目录，并写入 branch1。

### 9.2 反向生成附加 catalog

```powershell
& 'D:\UnityWork\zhou-master\Tools\generate_extra_catalog.ps1'
```

默认参数会：

- 读取 branch2 的 catalog 和 aa 目录；
- 读取 branch1 的 catalog 和 aa 目录；
- 生成 `extra_catalog.json`；
- 复制独有 bundle；
- 输出 address 清单。

---

## 10. 限制与注意事项

1. **只能处理 Addressables 资源**
   - 非 Addressables 的 `StreamingAssets` 数据不在本工具范围内；
   - 例如 `Data/`、`Map/`、`Spine/` 等需要另行处理。

2. **共享 bundle 内容差异可能丢失**
   - 逻辑同名 bundle 中，branch2 独有的 Shader / Material / Texture 等；
   - 如果被重定向到 branch1，可能会缺失；
   - 需要把这类资源拆到独立 bundle 再重新构建。

3. **脚本/类型依赖无法通过资源合并解决**
   - 如果 branch2 独有资源引用了 branch2 特有的 C# 脚本 / MonoBehaviour；
   - branch1 闭源构建中不存在这些类型；
   - 加载后可能出现 Missing Script 或运行异常。

4. **构建版本需一致**
   - Unity 版本、Addressables 版本、目标平台、压缩设置等应保持一致；
   - 否则 bundle 可能无法加载。

5. **Windows 路径建议使用绝对路径**
   - 工具窗口支持浏览选择；
   - 路径会保存到 `EditorPrefs`。

---

## 11. 推荐工作流

### 11.1 正向合并到 branch1

```
branch2 素材更新
  │
  ▼
重新构建 branch2 Addressables
  │
  ▼
打开 Merge Catalog Tool
  │
  ▼
选择 branch1 / branch2 的 catalog 与 aa 目录
  │
  ▼
点击 Merge + Copy Bundles
  │
  ▼
运行 branch1 测试
```

### 11.2 反向加载 branch1 附加资源

```
branch1 构建更新
  │
  ▼
准备 branch1 原始 catalog 和 aa 目录
  │
  ▼
打开 Merge Catalog Tool
  │
  ▼
点击 Generate Branch1 Extra For Branch2
  │
  ▼
在 branch2 中运行 Init 流程加载 extra_catalog.json
  │
  ▼
测试 branch1 独有 address
```

---

## 12. 相关文件清单

| 文件 | 说明 |
|---|---|
| `Assets/Editor/MergeCatalogTool.cs` | 工具核心逻辑 |
| `Assets/Editor/MergeCatalogWindow.cs` | Unity Editor 窗口 |
| `Assets/Scripts/Init.cs` | branch2 启动流程，加载附加 catalog |
| `Assets/Scripts/ExtraCatalogLoader.cs` | 可选的独立附加 catalog 调试加载组件 |
| `Tools/merge_catalogs.ps1` | 命令行正向合并脚本 |
| `Tools/generate_extra_catalog.ps1` | 命令行反向生成附加 catalog 脚本 |
| `Tools/branch1_extra_addresses.txt` | 反向生成时输出的独有 address 清单 |

---

## 13. Android 平台支持

当前 `MergeCatalogTool` 已经支持自动识别平台目录，不再写死 `StandaloneWindows64`。

支持：

- Windows：`StandaloneWindows64`
- Android：`Android`
- 其他平台：按 catalog 的 internalId 实际平台目录识别

平台目录会从 catalog 的 bundle internalId 自动推导，例如：

```
{UnityEngine.AddressableAssets.Addressables.RuntimePath}/Android/xxx.bundle
```

会识别为：

```
Android
```

```
{UnityEngine.AddressableAssets.Addressables.RuntimePath}\StandaloneWindows64\xxx.bundle
```

会识别为：

```
StandaloneWindows64
```

### 13.1 使用 branch1 Android 构建

从 branch1 的 APK 中解压出 Android Addressables 内容，通常为：

```
assets/aa/
├── catalog.json
├── settings.json
└── Android/
    └── ...branch1 Android bundle...
```

在工具窗口中填写：

| 字段 | Android 示例 |
|---|---|
| Branch1 Catalog | `D:\ArknightR\...\assets\aa\catalog.json` |
| Branch1 aa Bundle Root | `D:\ArknightR\...\assets\aa` |
| Branch2 Catalog | `D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Android\catalog.json` |
| Branch2 aa Bundle Root | `D:\UnityWork\zhou-master\Library\com.unity.addressables\aa\Android` |

点击：

```
Generate Branch1 Extra For Branch2
```

工具会自动：

- 识别平台目录为 `Android`；
- 输出 `extra_catalog.json` 到 Branch2 Catalog 同目录：
  ```
  Library/com.unity.addressables/aa/Android/extra_catalog.json
  ```
- 复制 branch1 独有 bundle 到：
  ```
  Library/com.unity.addressables/aa/Android/Android/
  ```

### 13.2 Android 编辑器测试

1. Unity 切换平台：

```
File > Build Settings > Android > Switch Platform
```

2. 重新 Build Addressables：

```
Window > Asset Management > Addressables > Groups
Build > New Build > Default Build Script
```

3. Addressables Play Mode 选择：

```
Use Existing Build (Packed Play Mode)
```

4. 运行游戏。

`Init.cs` 会加载：

```
Addressables.RuntimePath + "/extra_catalog.json"
```

Android 编辑器下对应：

```
Library/com.unity.addressables/aa/Android/extra_catalog.json
```

5. 测试 branch1 独有 address，例如：

```
Assets/Bundles/Effect/agoat2_skill_03_buff_01
```

如果不再出现 `InvalidKeyException`，说明附加 catalog 已正确加载。

### 13.3 Android 打包

打包后 `Addressables.RuntimePath` 为：

```
Application.streamingAssetsPath + "/aa"
```

需要确保 APK 内包含：

```
assets/aa/
├── catalog.json              ← branch2 主 catalog
├── settings.json
├── extra_catalog.json        ← branch1 附加 catalog
└── Android/
    ├── ...branch2 bundle...
    └── ...branch1 独有 bundle...
```

推荐在 Addressables Build 之后、玩家 Build 之前，把：

- `extra_catalog.json`
- branch1 独有 bundle

复制到：

```
Assets/StreamingAssets/aa/
Assets/StreamingAssets/aa/Android/
```

然后正常 Build APK。

### 13.4 Android 注意事项

1. **不能使用 Windows bundle**
   - branch1 的 Windows AssetBundle 不能给 Android 使用；
   - 必须使用 branch1 Android APK 中解压出来的 Android bundle。

2. **必须使用 branch1 Android 版本的 catalog**
   - Android catalog 的 internalId 平台目录为 `Android`；
   - 与 Windows catalog 不通用。

3. **Unity / Addressables 版本尽量一致**
   - 当前 branch1 Android catalog 的 Addressables 版本为 `1.19.15`；
   - 需要与 branch2 保持一致。

4. **Shader / 材质风险**
   - Android 上 Shader 变体、纹理压缩与 PC 差异更大；
   - branch1 独有 Spine / 特效资源可能出现紫色或效果异常；
   - 若出现，通常需要把相关 Shader / Material 拆到独立 bundle 重新构建。

5. **StreamingAssets 复制逻辑**
   - 如果 branch2 在 Android 上会把 StreamingAssets 复制到持久化路径；
   - 需要确认 `extra_catalog.json` 与独有 bundle 也被复制；
   - 并确认 Addressables 最终读取的是 StreamingAssets 还是持久化路径。

6. **构建清理问题**
   - 每次 branch2 重新 Build Addressables 后，`Library/com.unity.addressables/aa/Android/` 可能被清理；
   - 需要重新执行一次 `Generate Branch1 Extra For Branch2`。

> 说明：`Tools/merge_catalogs.ps1` 与 `Tools/generate_extra_catalog.ps1` 目前主要按 Windows 平台脚本维护。
> Android 平台合并建议优先使用 Unity 编辑器中的 `Tools > Merge Catalog > Open Merge Window`，该入口已支持平台自动识别。
