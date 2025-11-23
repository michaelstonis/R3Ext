# AOT and Reflection Issues Matrix

This document tracks all potential AOT (Ahead-of-Time) compilation, reflection, and expression evaluation issues found in R3Ext and R3.DynamicData projects.

## Status Legend
- ⏳ **Pending** - Not yet addressed
- 🔄 **In Progress** - Currently being worked on
- ✅ **Fixed** - Issue resolved and committed
- ⚠️ **Review** - Needs design review or decision
- 🔍 **Investigating** - Under investigation

---

## High Priority Issues

### 1. Expression.Compile() Usage
**Status:** ⏳ Pending  
**Severity:** Critical  
**AOT Impact:** Expression.Compile() creates dynamic methods that are incompatible with AOT

#### Locations:
1. **`R3.DynamicData/Cache/ObservableCacheEx.WhenValueChanged.cs`**
   - Line 31: `var getter = propertyAccessor.Compile();`
   - Line 131: `var getter = propertyAccessor.Compile();`
   - **Context:** Used in WhenValueChanged operators to create property getters from expressions
   
2. **`R3.DynamicData/Binding/BindingOptions.cs`**
   - Line 156: `propertyAccessor.Compile()(source)`
   - **Context:** Used in WhenPropertyChanged to compile property access expression

#### Solution Approach:
- Replace Expression.Compile() with source-generated property accessors
- Use CompiledExpressionMapper pattern with code generation
- Consider caching compiled delegates with fallback for unknown types

#### Affected APIs:
- `WhenValueChanged<TObject, TKey, TValue>()`
- `WhenPropertyChanged<TObject, TProperty>()`

---

### 2. Reflection-Based Type Discovery
**Status:** ⏳ Pending  
**Severity:** High  
**AOT Impact:** Runtime reflection on types/properties fails with trimming

#### Locations:
1. **`R3.DynamicData/Cache/ObservableCacheEx.WhenValueChanged.cs`**
   - Lines 233-260: `GetKeySelector<TObject, TKey>()` method
   - Uses `typeof(TObject).GetProperty()` and `GetProperties()`
   - Searches for "Id", "[Key]" attribute, or naming patterns
   - **Issue:** Property discovery fails with aggressive trimming

#### Solution Approach:
- Require explicit key selector parameter (breaking change)
- Use source generator to detect key properties at compile time
- Add [DynamicDataKey] attribute with source-gen support
- Provide overloads that accept explicit key selector

#### Affected APIs:
- `WhenValueChanged<TObject, TKey, TValue>()` - internal GetKeySelector()

---

### 3. BindingRegistry Runtime Type Dispatch
**Status:** ⏳ Pending  
**Severity:** High  
**AOT Impact:** GetType() and typeof() comparisons with dynamic dispatch

#### Locations:
1. **`R3Ext/BindingRegistry.cs`**
   - Lines 65, 74-75, 84, 93-94, 103, 110: Multiple `typeof()` calls
   - Lines 118-119, 139-140, 159: `GetType()` calls for runtime type resolution
   - **Context:** Registry matches runtime types to find appropriate binding factories

#### Solution Approach:
- Generate registration code at compile time using source generators
- Use generic constraints to resolve types at compile time
- Create typed lookup dictionaries instead of object-based dispatch
- Consider compile-time binding resolution

#### Affected APIs:
- `RegisterOneWay<TFrom, TTarget>()`
- `RegisterTwoWay<TFrom, TTarget>()`
- `RegisterWhenChanged<TObj>()`
- All TryCreate methods

---

### 4. Node Type Comparison
**Status:** ⏳ Pending  
**Severity:** Low  
**AOT Impact:** Minor - GetType() for equality check

#### Locations:
1. **`R3.DynamicData/Cache/Node.cs`**
   - Line 156: `if (obj.GetType() != GetType())`
   - **Context:** Equality comparison in Node<TObject, TKey>

#### Solution Approach:
- Replace with pattern matching: `obj is not Node<TObject, TKey>`
- Use proper type checking with generic constraints

---

### 5. JSON Serialization (Source Generator Build Tools)
**Status:** ⏳ Pending  
**Severity:** Low  
**AOT Impact:** Affects build-time tools only, not runtime

#### Locations:
1. **`R3Ext.Bindings.SourceGenerator/UiBindingMetadata.cs`**
   - Line 25: `JsonSerializer.Deserialize<UiBindingMetadata>()`
   
2. **`R3Ext.Bindings.MauiTargets/GenerateUiBindingTargetsTask.cs`**
   - Line 80: `JsonSerializer.Serialize()`

#### Solution Approach:
- Add `[JsonSerializable]` attributes with source generation
- These are build-time tools, not runtime - lower priority
- Consider STJ source generator for metadata

---

## Medium Priority Issues

### 6. MemberExpression Reflection Access
**Status:** ⏳ Pending  
**Severity:** Medium  
**AOT Impact:** Direct member info access

#### Locations:
1. **`R3.DynamicData/Binding/BindingOptions.cs`**
   - Line 161: `memberExpression.Member is System.Reflection.PropertyInfo`
   - **Context:** Extracting property name from expression

2. **`R3.DynamicData/Cache/ObservableCacheEx.WhenValueChanged.cs`**
   - Line 220: `propertyAccessor.Body is MemberExpression memberExpr`

#### Solution Approach:
- Expression parsing is compile-time safe if not trimmed
- Ensure expression trees are preserved
- Add [DynamicDependency] attributes if needed

---

### 7. typeof() Usage for Logging/Diagnostics
**Status:** ⏳ Pending  
**Severity:** Low  
**AOT Impact:** Generally safe but review needed

#### Locations:
Multiple locations in BindingRegistry.cs and elsewhere for diagnostics

#### Solution Approach:
- These are generally safe as they don't create dynamic behavior
- Review for any dynamic type creation
- Mark as safe or replace with compile-time alternatives

---

## Project Configuration

### Current AOT Settings:
- **R3Ext.csproj**: `<IsTrimmable>true</IsTrimmable>`
- **R3.DynamicData.csproj**: No specific AOT settings

### Recommended Additions:
```xml
<PropertyGroup>
  <IsAotCompatible>true</IsAotCompatible>
  <PublishAot>true</PublishAot>
  <EnableTrimAnalyzer>true</EnableTrimAnalyzer>
  <EnableSingleFileAnalyzer>true</EnableSingleFileAnalyzer>
</PropertyGroup>
```

---

## Testing Strategy

### AOT Compatibility Testing:
1. Enable PublishAot on test projects
2. Create test app with NativeAOT compilation
3. Run full test suite under AOT
4. Validate trimming warnings

### Benchmarking:
- Compare performance before/after changes
- Measure source-generated vs reflection approaches
- Ensure no regressions

---

## Implementation Priority

### Phase 1: Critical Blockers
1. ✅ Document all issues (this file)
2. ⏳ Fix Expression.Compile() in WhenValueChanged
3. ⏳ Fix Expression.Compile() in BindingOptions
4. ⏳ Add explicit key selector overloads

### Phase 2: High Impact
5. ⏳ Refactor BindingRegistry for AOT
6. ⏳ Fix GetKeySelector reflection
7. ⏳ Add source generators where needed

### Phase 3: Polish
8. ⏳ Fix Node.GetType() comparison
9. ⏳ Add JSON source generation for build tools
10. ⏳ Enable AOT analyzers project-wide
11. ⏳ Full AOT testing and validation

---

## Notes

- Some changes may be breaking (explicit key selector requirement)
- Source generation adds compile-time dependency
- Consider versioning strategy for breaking changes
- Document migration path for users

---

**Last Updated:** 2025-11-23  
**Branch:** feature/aot-reflection-fixes
