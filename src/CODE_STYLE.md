# 代码风格规定

**版本：1.1**  
**出版时间：2026年3月22日**

## 1. 功能分组注释块

类内为相同功能服务的属性与方法，使用以下格式分隔：

```
// ================================================================================
//                                  某某方法
// ================================================================================
```

## 2. 注释规范

### 类、方法、属性

尽可能为每个类、方法、属性添加注释。

### 函数内代码

**重要逻辑与局部变量**（包括for循环中的i）：注释放在前一行

```
// 注释
重要逻辑或局部变量
```

**其他情况**：注释写在语句后面

```
函数() // 注释
```

## 3. 命名规范

### C# 命名约定

- **公开字段**：`PascalCase`（如：`PublicField`）
- **私有字段**：`_lowerCamelCase`（如：`_privateField`）
- **公开静态字段**：`PascalCase`（如：`PublicStaticField`）
- **私有静态字段**：`lowerCamelCase`（如：`privateStaticField`）
- **总结**：私有的字段用 `_lowerCamelCase`，公开的字段用 `PascalCase`

- **公开静态只读字段**：`ALL_UPPER`（如：`PUBLIC_STATIC_READONLY_FIELD`）
- **私有静态只读字段**：`ALL_UPPER`（如：`PRIVATE_STATIC_READONLY_FIELD`）
- **总结**：静态只读字段用 `ALL_UPPER`

- **公开属性**：`PascalCase`（如：`PublicProperty`）
- **私有属性**：`PascalCase`（如：`PrivateProperty`）
- **公开静态非只读属性**：`PascalCase`（如：`PublicStaticProperty`）
- **私有静态非只读属性**：`PascalCase`（如：`PrivateStaticProperty`）
- **总结**：通常情况下的属性用 `PascalCase`


- **事件**：`PascalCase`（如：`EventName`）
- **方法**：`PascalCase`（如：`MethodName`）
- 
- **方法参数**：`lowerCamelCase`（如：`methodParameter`）
- **局部变量**：`lowerCamelCase`（如：`localVariable`）
- 
- **常量**：`ALL_UPPER`（如：`MAX_VALUE`）
- **特定规范文件名**：`ALL_UPPER`（如：`PROJECT_STANDARD.md`）

### 方法名建议
- **返回 bool 类型的方法**：应使用 `IsSomething` 或 `HasSomething` 等形式（如：`IsValid()`、`HasPermission()`）
- **对于类访问器的方法**：鼓励使用 `get`/`set` 等谓语词以助于理解。

### 其他编程语言与GDScript 交互命名约定

在与GDScript交互的包装类中，关于方法变量的命名采用 GDScript 的规范，即 `snake_case`（如：`gd_script_variable`）。

## 4. 空行规范

用空行明晰结构与逻辑，提高代码可读性。

### 相近功能(私有字段与公开属性)

- 相近功能与其对应的相近功能之间用**一个空行**隔开
- 相近功能作为一个整体，与其他代码用**两个空行**隔开
- 相近功能如私有字段与公开属性、方法重载等，也应遵循此规范。

**示例**：

```csharp

// 其他代码...


/// <summary>
/// 实例的唯一标识符
/// </summary>
private int _id;

/// <summary>
/// 获取实例的唯一标识符
/// </summary>
public int Id => _id;


/// <summary>
/// 所属世界实例
/// </summary>
private World _ownerWorld;

/// <summary>
/// 获取所属世界实例
/// </summary>
public World OwnerWorld => _ownerWorld;


// 其他代码...
```

## 5. 花括号规范

在本项目中，所有控制语句（如 if、for、while、switch 等）和方法、类的花括号必须采用 Allman 风格，即左花括号 { 单独成行，并与对应的关键字对齐。

**✅正确示例**：

```csharp
if (condition)
{
    // 代码
}

// 其他代码...
```

此风格符合 Microsoft 官方 C# 编码约定，有助于提升代码可读性、维护性及团队协作一致性。禁止将左花括号与条件或声明写在同一行。格式应通过
.editorconfig 和 IDE 自动化工具统一管理。

**❌错误示例**：

```csharp
if (condition){//应当放在新行❌❌❌
    // 代码
}

// 其他代码...
```

微软官方说法出处:

https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions?spm=5176.28103460.0.0.38f97d83YB5mYP#brace-placement

