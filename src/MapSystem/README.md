# 地图系统设计文档

## 概述

地图系统采用分层架构设计，从微观到宏观依次为：Tile → Chunk → Grid → Layer → World。该系统支持无限地图、动态加载、多层级交互等特性。

## 坐标系统设计原则

地图系统中的各Position类型遵循**单向转换**原则：每个Position类型只提供从自身转换到其他Position类型的实例方法（`To*`
方法），不提供从其他Position类型构建自身的静态工厂方法。

**设计理由**：

- 转换逻辑集中在源类型，职责清晰
- 避免重复代码
- 符合"Tell, Don't Ask"原则

**示例**：

- `GlobalTilePosition.ToChunkPosition()` ✓ 从自己转换到ChunkPosition
- `GlobalTilePosition.ToLocalTilePosition()` ✓ 从自己转换到局部坐标
- `LocalTilePosition.ToGlobal()` ✓ 从自己转换到GlobalTilePosition
- `ChunkPosition.FromGlobalTilePosition(...)` ✗ 违反原则（应删除）

## Position类型详解

### 坐标系层级关系

```
Vector2/Vector2I (像素坐标)
        ↓ ToGlobalTilePosition
GlobalTilePosition (全局Tile坐标)
        ↓ ToChunkPosition / ToLocalTilePosition
ChunkPosition + LocalTilePosition (区块坐标 + 局部Tile坐标)
```

### Position类型说明

#### 1. GlobalTilePosition（全局Tile位置）

**定义**：Tile在世界中的全局坐标，唯一标识一个Tile。

**特性**：

- 世界范围内唯一
- 可直接转换为ChunkPosition和LocalTilePosition
- 支持位运算高效转换

**核心方法**：

- `ToChunkPosition(layer)` → 获取所属区块坐标
- `ToLocalTilePosition(layer)` → 获取区块内局部坐标
- `ToKey()` → 生成long类型字典键

#### 2. LocalTilePosition（局部Tile位置）

**定义**：Tile在区块内的局部坐标，仅在所属区块内有效。

**特性**：

- 范围：[0, ChunkSize-1]
- 需配合ChunkPosition才能确定全局位置
- 用于区块内部Tile索引

**核心方法**：

- `ToGlobalTilePosition(layer, chunkPosition)` → 转换为全局坐标
- `ToTileIndex(layer)` → 获取Tile在一维数组中的索引
- `IsValid(layer)` → 校验坐标是否在区块范围内

#### 3. ChunkPosition（区块位置）

**定义**：区块在世界中的坐标位置。

**特性**：

- 标识区块在世界中的位置
- 用于区块管理器的索引

**核心方法**：

- `ToGlobalTilePosition(layer, localTilePosition)` → 根据局部坐标还原全局坐标
- `GetOriginGlobalTilePosition(layer)` → 获取区块左上角原点的全局Tile坐标

### 位运算优化

系统要求Tile大小和区块大小均为2的幂，以支持高效位运算：

| 参数              | 说明               | 示例                     |
|-----------------|------------------|------------------------|
| `tileSizeExp`   | Tile大小指数 (2^Exp) | TileSize=16 → Exp=4    |
| `chunkSizeExp`  | 区块大小指数 (2^Exp)   | ChunkSize=16 → Exp=4   |
| `chunkSizeMask` | 区块大小掩码 (2^Exp-1) | ChunkSize=16 → Mask=15 |

**转换公式**：

- 全局→区块：`chunkCoord = globalCoord >> chunkSizeExp`
- 全局→局部：`localCoord = globalCoord & chunkSizeMask`
- 局部→全局：`globalCoord = (chunkCoord << chunkSizeExp) + localCoord`

## 层级结构

### 1. Tile（瓦片）

**定义**：地图的最基本单位，类似于《我的世界》、《泰拉瑞亚》等沙盒游戏中的方块。

**特性**：

- 2D平面游戏的基本构成元素
- 像素风格渲染
- 最小的地图单元

---

### 2. Chunk（区块）

**定义**：矩形区域，内部包含多个Tile，是游戏动态加载的基本单位。

**规格**：

- 1 Chunk = W*H Tile（W>0且H>0）
- 矩形结构

**内容**：

- Tile信息
- Entity（实体）信息
- 理论上可内含无限个实体

**动态加载机制**：

- 区块是游戏动态加载的基本单位
- 区块加载的申请单位是实体Entity
- 只有特定实体Entity具备申请区块加载的能力
- 申请能力有范围限制：
    - 加载范围（哪些区块）
    - 加载等级（加载深度）

**持久化**：

- 区块被卸载后，存储Tile信息和Entity信息

---

### 3. Grid（网格）

**定义**：矩形区域，内部包含多个Chunk，负责存储结构和体系的生成蓝图。

**规格**：

- 1 Grid = W*H Chunk（W>0且H>0）
- 矩形结构

**内容**：

- 结构（Structure）：如神庙、村庄等固定建筑群
- 体系（System）：如羊群、鸟群、居民点等复杂群体

**加载机制**：

- 网格在某个区块被请求加载时才创建/加载
- 内部的Chunk不要求加载具体内容
- 区块加载时根据网格内的结构与体系生成对应的具体内容

**生成蓝图**：

- 网格存储的"结构和体系"是一种生成蓝图
- 当区块加载时，根据蓝图生成具体内容

**持久化**：

- 网格的结构和体系信息需要持久化

---

### 4. Layer（层级）

**定义**：无限层级的地图层，基本单位是Chunk。

**特性**：

- 层级数量无限
- 每个层级只能绑定一个World
- 不同层级之间可以交互和关联

**层级间交互**：

- 游戏交互层面：传送门设计
- 代码架构层面：World维护通道注册表

**实体绑定规则**：

- 一个实体在同一时刻只能绑定一个World和一个Layer

---

### 5. World（世界）

**定义**：最高层级的地图容器，统筹管理所有Layer。

**特性**：

- 可绑定任意个Layer
- 统筹所有Layer的运作

**职责**：

- 管理Layer的创建、销毁
- 维护层级间通道注册表
- 处理层级间传送请求

**通道注册表**：

- 记录层级间的连接规则
- 验证传送请求的合法性
- 处理传送后的坐标映射

---

## 层级间交互系统

### 传送门机制

**游戏交互层面**：

- 传送门作为游戏内实体存在
- 玩家与传送门交互触发传送

**代码架构层面**：

- 传送门收集信息：
    - 目标层级ID
    - 目标坐标
    - 传送实体
- 传送门将信息提交给World
- World根据通道注册表判断是否允许传送
- World处理传送后的坐标等细节

### 通道注册表

**数据结构参考**：

```
通道注册表 = {
    源层级ID: {
        目标层级ID: {
            坐标映射规则,
            传送条件
        }
    }
}
```

**功能**：

- 定义层级间的合法连接
- 提供坐标映射规则
- 验证传送权限

---

## 动态加载流程

```
1. 具备加载能力的Entity请求加载区块
2. 系统检查该区块所属的Grid是否已加载
3. 若Grid未加载，创建/加载Grid（读取持久化数据或生成新蓝图）
4. 加载Chunk
5. 根据Grid的结构和体系蓝图生成具体内容
6. Chunk进入活跃状态
```

## 卸载流程

```
1. Chunk不再被任何Entity需要
2. 保存Tile信息和Entity信息
3. 卸载Chunk
4. 若Grid内所有Chunk都已卸载，保存Grid的结构和体系信息
5. 卸载Grid
```

## 实体区块加载能力

**能力持有者**：

- 只有特定Entity具备此能力（如玩家、关键NPC）

**能力参数**：

- 加载范围：定义需要加载的区块范围
- 加载等级：定义区块的加载深度
  参考：（非实际设计）
    - Level 0：仅加载于内存
    - Level 1：加载于游戏（完全激活）

---

## 数据流向图

```
World
  └── Layer (无限个)
        └── Grid (按需加载)
              └── Chunk (动态加载)
                    └── Tile + Entity
```

## 持久化需求

| 层级    | 持久化内容           | 时机         |
|-------|-----------------|------------|
| Chunk | Tile信息、Entity信息 | 卸载时        |
| Grid  | 结构和体系蓝图         | 所有Chunk卸载时 |
| World | 通道注册表           | 变更时        |

---

## 待实现功能

- [ ] Chunk动态加载系统
- [ ] Grid蓝图生成系统
- [ ] Layer管理系统
- [ ] World统筹系统
- [ ] 通道注册表
- [ ] 传送门实体
- [ ] 实体区块加载能力组件
- [ ] 持久化系统对接
