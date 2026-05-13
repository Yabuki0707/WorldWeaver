# MapSystem 简介

本模块当前聚焦四个核心层级：`Tile`、`Chunk`、`Grid`、`Layer`。

## 1. 层级关系

- `Tile`：最小地图单位，命名来源于瓦片地图语义。
- `Chunk`：动态加载的基本单位，内部按数组存储 Tile 数据。
- `Grid`：以 Chunk 为单位的网格层，主要用于未来结构化扩展与更高层组织能力。
- `Layer`：地图运行容器，内部通过 `Manager` 管理 Chunk（以及相关网格/转换逻辑）。

简化关系可理解为：

```text
Layer
  ├─ ChunkManager  -> 管理 Chunk 生命周期与数据操作
  └─ GridManager   -> 管理以 Chunk 为单位的 Grid 组织

Chunk
  └─ Tile[]
```

## 2. 坐标系与转换

当前实现以“全局坐标 + 分层转换”为主，不再使用旧的 `GlobalTilePosition/LocalTilePosition` 类型，而改为转换器。

- 全局 Tile 坐标：`Vector2I`，用于跨 Chunk 访问。
- Chunk 坐标：`ChunkPosition`，用于定位具体区块。
- 局部 Tile 坐标：Chunk 内部坐标，最终会映射到一维 `tileIndex`。
- Grid 坐标：以 Chunk 为粒度的网格坐标，用于更粗粒度管理。

主要转换器：

- `GlobalTilePositionConverter`
- `LocalTilePositionConverter`
- `GlobalPositionSizeConverter` / `LocalPositionSizeConverter`
- `GlobalPositionSizeExpConverter` / `LocalPositionSizeExpConverter`

## 3. Shape 系统

Shape 系统在 MapSystem 中的意义是：为“网格元素范围”提供统一规定。

它不负责具体业务逻辑，只负责回答一个问题：某次操作覆盖了哪些离散网格点。  
基于这套范围表达，读写、设置、移除、恢复等流程可以使用同一套输入语义，从而避免各模块各自定义范围格式，降低接口歧义与转换成本。

简而言之：Shape 是网格元素操作范围的标准描述层。

---

这个 README 仅描述当前核心结构与职责边界，具体 API 请按各子模块文件注释与类型定义为准。
