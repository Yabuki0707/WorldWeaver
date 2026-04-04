# ChunkData 形状接口改动说明

本次调整将 `ChunkData` 的 shape 读写流程统一为显式的 `fast / safe` 两套实现：

- `Get(PixelShape)`：
  - 返回 `TileValueShape`
  - 返回结果始终携带 `TileRunIds`
- `Set(TileRegion)`：
  - 返回 `TileValueShape`
  - 返回结果携带操作完成后的最终 `TileRunIds`
- `Set(TileValueShape)`：
  - 返回 `TileValueShape`
  - 输入必须携带与点序一致的 `TileRunIds`
- `Remove(PixelShape)` / `Delete(PixelShape)`：
  - 返回 `TileValueShape`
  - 返回结果只描述命中的全局坐标点，`TileRunIds` 固定为 `null`
- `Restore(PixelShape)`：
  - 返回 `TileValueShape`
  - 返回结果携带操作完成后的最终 `TileRunIds`

补充约定：

- `ChunkData` 构造时不再注入独立回调，而是直接持有 `Chunk`，并通过 `Chunk` 转发 Tile 变化通知。
- 原子操作不再校验坐标或索引合法性，调用方必须先保证参数有效。
- 包装层根据 `shape.BoundingBox` 是否完整落在当前 Chunk 内，自动选择快路径或安全路径。
