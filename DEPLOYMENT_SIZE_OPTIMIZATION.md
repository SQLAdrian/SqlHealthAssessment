# Deployment Size Optimization Guide

## Current Deployment Size Analysis

### Typical .NET 8 WPF Blazor Deployment: ~150-200 MB
- .NET Runtime: ~80-100 MB
- Application DLLs: ~20-30 MB
- Dependencies (Blazor, Serilog, etc.): ~30-40 MB
- Native libraries: ~20-30 MB

---

## Optimization Strategies

### 1. Self-Contained vs Framework-Dependent (CRITICAL)

**Framework-Dependent (Recommended for Enterprise)**
```xml
<PropertyGroup>
  <SelfContained>false</SelfContained>
  <PublishSingleFile>false</PublishSingleFile>
</PropertyGroup>
```
- **Size**: ~30-50 MB (requires .NET 8 Runtime installed)
- **Pros**: Smallest size, shared runtime, easier updates
- **Cons**: Requires .NET 8 Desktop Runtime on target machines

**Self-Contained (Current)**
```xml
<PropertyGroup>
  <SelfContained>true</SelfContained>
</PropertyGroup>
```
- **Size**: ~150-200 MB (includes .NET Runtime)
- **Pros**: No runtime dependency
- **Cons**: Large deployment size

**Recommendation**: Use Framework-Dependent for enterprise deployment with .NET 8 Runtime prerequisite.

---

### 2. Enable ReadyToRun Compilation

```xml
<PropertyGroup>
  <PublishReadyToRun>true</PublishReadyToRun>
  <PublishReadyToRunShowWarnings>true</PublishReadyToRunShowWarnings>
</PropertyGroup>
```
- **Impact**: Faster startup, slightly larger size (+5-10%)
- **Trade-off**: Worth it for enterprise deployment

---

### 3. Trim Unused Code (Use with Caution)

```xml
<PropertyGroup>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>partial</TrimMode>
</PropertyGroup>
```
- **Impact**: Can reduce size by 20-30%
- **Risk**: May break reflection-based code (Blazor, DI)
- **Recommendation**: Test thoroughly before enabling

---

### 4. Single File Deployment

```xml
<PropertyGroup>
  <PublishSingleFile>true</PublishSingleFile>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <IncludeAllContentForSelfExtract>true</IncludeAllContentForSelfExtract>
</PropertyGroup>
```
- **Impact**: Single EXE file, easier distribution
- **Size**: Same or slightly larger
- **Pros**: Simpler deployment, no DLL sprawl

---

### 5. Compression

```xml
<PropertyGroup>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
</PropertyGroup>
```
- **Impact**: 10-20% size reduction for single file
- **Trade-off**: Slightly slower startup (decompression)

---

### 6. Remove Unnecessary Dependencies

**Check for unused NuGet packages:**
```bash
dotnet list package --include-transitive
```

**Common culprits:**
- Multiple JSON serializers (use System.Text.Json only)
- Unused logging providers
- Development-only packages

---

### 7. Optimize Assets

**CSS/JS Minification:**
- Minify app.css (currently ~50KB, can reduce to ~30KB)
- Remove unused CSS rules
- Combine multiple CSS files

**Remove Debug Symbols:**
```xml
<PropertyGroup>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```
- **Impact**: ~5-10 MB reduction

---

### 8. Runtime Identifier Optimization

**Use specific RID instead of portable:**
```xml
<PropertyGroup>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
</PropertyGroup>
```
- **Impact**: Removes unused platform code
- **Reduction**: ~10-15%

---

## Recommended Configuration for Enterprise

### Option A: Framework-Dependent (Smallest - 30-50 MB)
```xml
<PropertyGroup>
  <SelfContained>false</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

**Prerequisites:**
- .NET 8 Desktop Runtime (install via GPO/SCCM)
- Download: https://dotnet.microsoft.com/download/dotnet/8.0

---

### Option B: Self-Contained Optimized (100-120 MB)
```xml
<PropertyGroup>
  <SelfContained>true</SelfContained>
  <PublishSingleFile>true</PublishSingleFile>
  <PublishReadyToRun>true</PublishReadyToRun>
  <RuntimeIdentifier>win-x64</RuntimeIdentifier>
  <IncludeNativeLibrariesForSelfExtract>true</IncludeNativeLibrariesForSelfExtract>
  <EnableCompressionInSingleFile>true</EnableCompressionInSingleFile>
  <DebugType>none</DebugType>
  <DebugSymbols>false</DebugSymbols>
</PropertyGroup>
```

---

## Build Commands

### Framework-Dependent Release Build
```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

### Self-Contained Release Build
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

---

## Size Comparison

| Configuration | Size | Startup | Prerequisites |
|--------------|------|---------|---------------|
| Self-Contained (Current) | ~180 MB | Fast | None |
| Self-Contained + Optimized | ~120 MB | Fast | None |
| Self-Contained + Single File | ~110 MB | Medium | None |
| Framework-Dependent | ~40 MB | Fastest | .NET 8 Runtime |
| Framework-Dependent + Single File | ~35 MB | Fastest | .NET 8 Runtime |

---

## Additional Optimizations

### 1. Lazy Load Assemblies
- Load Serilog only when logging is needed
- Defer heavy dependencies until first use

### 2. Remove Unused Cultures
```xml
<PropertyGroup>
  <InvariantGlobalization>true</InvariantGlobalization>
</PropertyGroup>
```
- **Impact**: ~10 MB reduction
- **Trade-off**: English-only application

### 3. Use Native AOT (Future)
- .NET 8+ supports Native AOT for WPF (experimental)
- **Potential**: 50-70 MB self-contained
- **Status**: Not production-ready for WPF Blazor

---

## Deployment Package Structure

### Recommended Structure
```
SqlHealthAssessment/
├── SqlHealthAssessment.exe (35-120 MB depending on config)
├── appsettings.json (5 KB)
├── appsettings.Production.json (2 KB)
├── dashboard-config.json (10 KB)
└── README.txt (1 KB)
```

### Optional Files (can be generated on first run)
- logs/ (created automatically)
- audit-logs/ (created automatically)
- SqlHealthAssessment-cache.db (created on first query)

---

## MSI Installer Optimization

### Compress Installation Files
- Use CAB compression in WiX
- **Impact**: 30-40% smaller MSI

### Separate Runtime Installer
- Create prerequisite check for .NET 8 Runtime
- Download runtime only if missing
- **Impact**: MSI size ~40 MB instead of ~180 MB

---

## Monitoring Deployment Size

### Check Published Size
```bash
dotnet publish -c Release -r win-x64
dir bin\Release\net8.0-windows\win-x64\publish /s
```

### Analyze Assembly Sizes
```bash
dotnet-ilverify bin\Release\net8.0-windows\win-x64\publish\*.dll
```

---

## Recommendations Summary

**For Enterprise Internal Deployment:**
1. Use Framework-Dependent deployment (~40 MB)
2. Deploy .NET 8 Runtime via GPO/SCCM
3. Enable ReadyToRun compilation
4. Use single file deployment
5. Remove debug symbols

**For External/Standalone Distribution:**
1. Use Self-Contained deployment (~120 MB optimized)
2. Enable single file + compression
3. Remove debug symbols
4. Consider MSI with CAB compression

**Expected Final Sizes:**
- Framework-Dependent: 35-40 MB
- Self-Contained Optimized: 100-120 MB
- Current (unoptimized): 180-200 MB

**Savings: 40-60% reduction possible**
