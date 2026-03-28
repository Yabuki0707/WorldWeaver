using System;
using System.IO;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using Newtonsoft.Json;
using System.Threading;
using WorldWeaver.MapSystem.GridSystem;
using WorldWeaver.MapSystem.LayerSystem;

namespace WorldWeaver.MapSystem.ChunkSystem
{
    /// <summary>
    /// 区块持久化器，负责区块数据的读写。
    /// <para>提供阻塞（带冷却）和非阻塞（带结果缓存与并发限制）两种方案。</para>
    /// </summary>
    public static class ChunkPersistence
    {
        /*******************************
                  公共配置
        ********************************/


        /// <summary>
        /// 最大并发任务数（非阻塞方案）
        /// <para>限制同时进行的保存/加载操作数量，防止资源耗尽</para>
        /// </summary>
        const int MAX_CONCURRENT_TASKS = 12;

        /// <summary>
        /// 主线程阻塞式保存/加载冷却时间（毫秒）
        /// <para>防止频繁操作导致性能问题</para>
        /// </summary>
        const ulong BLOCKING_COOLDOWN_MS = 700; // 0.70秒

        /// <summary>
        /// 非阻塞方案结果缓存过期时间（毫秒）
        /// <para>控制缓存数据的有效期，防止过期数据被错误使用</para>
        /// </summary>
        const ulong DEFAULT_RESULT_EXPIRATION_MS = 20000; // 20秒

        /*******************************
                  阻塞方案 (Blocking)
        ********************************/
        
        // 全局冷却时间戳 (Godot Time.GetTicksMsec)
        private static ulong _nextAllowedBlockingTime = 0;

        // 冷却期间的请求计数
        private static int _blockingRequestCount = 0;

        /// <summary>
        /// 冷却期间最大阻塞式请求数
        /// <para>使得频繁请求的主线程能够得到正确反馈</para>
        /// </summary>
        private const int MAX_BLOCKING_REQUESTS_IN_COOLDOWN = 128;

        /// <summary>
        /// 阻塞式保存（带冷却限制）
        /// </summary>
        /// <returns>true: 保存成功; false: 冷却中或失败; null: 请求过多或参数错误</returns>
        public static bool? SaveBlocking(Chunk chunk, string saveDir)
        {
            // 检查参数是否为 null
            if (chunk == null)
            {
                GD.PushError("[ChunkPersistence] SaveBlocking: chunk 参数为 null - 无法提供有效的UID、坐标和数据");
                return null;
            }
            
            // 检查参数是否为 Empty
            if (chunk == Chunk.Empty)
            {
                GD.PushError("[ChunkPersistence] SaveBlocking: chunk 参数为 Empty - 无法提供有效的UID、坐标和数据");
                return null;
            }
            
            // 目前的时间戳
            ulong currentTime = Time.GetTicksMsec();
            // 若在冷却期间则返回对应的失败操作
            if (currentTime < _nextAllowedBlockingTime)
            {
                // 检查是否超过最大请求数
                if (_blockingRequestCount == MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
                    GD.PushError($"[ChunkPersistence] 阻塞式读写操作在冷却期间申请了超出{MAX_BLOCKING_REQUESTS_IN_COOLDOWN}次的阈值，之后会返回null表示阻塞，该次阈值操作的时间为 {currentTime}，操作的区块为 {chunk.Uid}");
                else if (_blockingRequestCount > MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
                {
                    return null;
                }
                _blockingRequestCount++;
                return false; // 冷却中
            }

            // 重置计数
            _blockingRequestCount = 0;
            
            // 拼接目录路径: saveDir/Grid_UID/Chunk_UID.json
            string path = GetChunkFilePath(chunk, saveDir);
            try
            {
                // 无数据视为成功保存,取消冷却时间的操作
                if (chunk.Data == null)
                    return true; 
                // 序列化与写入
                string json = JsonConvert.SerializeObject(chunk.Data);
                // 写入文件
                File.WriteAllText(path, json);
                // 更新冷却时间
                _nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
                return true;
            }
            catch (Exception e)
            {
                // 更新冷却时间
                _nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
                GD.PushError($"[ChunkPersistence] 保存区块{chunk.Uid}的数据到 {path} 路径失败: {e.Message}");
                return false;
            }
        }

        /// <summary>
        /// 阻塞式加载（带冷却限制）
        /// </summary>
        /// <returns>true: 加载成功或文件不存在(data可能为null); false: 冷却中或失败; null: 请求过多或参数错误</returns>
        public static bool? LoadBlocking(Chunk chunk, string saveDir, out ChunkData data)
        {
            data = null;
            // 检查参数是否为 null
            if (chunk == null)
            {
                GD.PushError("[ChunkPersistence] LoadBlocking: chunk 参数为 null - 无法提供有效的UID和坐标");
                return null;
            }
            
            // 检查参数是否为 Empty
            if (chunk == Chunk.Empty)
            {
                GD.PushError("[ChunkPersistence] LoadBlocking: chunk 参数为 Empty - 无法提供有效的UID和坐标");
                return null;
            }
            
            ulong currentTime = Time.GetTicksMsec();
            if (currentTime < _nextAllowedBlockingTime)
            {
                // 检查是否超过最大请求数
                if (_blockingRequestCount == MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
                    GD.PushError($"[ChunkPersistence] 阻塞式读写操作在冷却期间申请了超出{MAX_BLOCKING_REQUESTS_IN_COOLDOWN}次的阈值，之后会返回null表示阻塞，该次阈值操作的时间为 {currentTime}，操作的区块为 {chunk.Uid}");
                if (_blockingRequestCount > MAX_BLOCKING_REQUESTS_IN_COOLDOWN)
                {
                    return null;
                }
                _blockingRequestCount++;
                return false; // 冷却中
            }
            // 重置计数
            _blockingRequestCount = 0;
            // 拼接目录路径: saveDir/Grid_UID/Chunk_UID.json
            string path = GetChunkFilePath(chunk, saveDir);
            try
            {
                if (File.Exists(path))
                {
                    string json = File.ReadAllText(path);
                    data = JsonConvert.DeserializeObject<ChunkData>(json);
                }
                // 文件不存在视为成功加载,不增加冷却时间
                else
                    return true;
                // 更新冷却时间
                _nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
                return true;
            }
            catch (Exception e)
            {
                // 更新冷却时间
                _nextAllowedBlockingTime = currentTime + BLOCKING_COOLDOWN_MS;
                GD.PushError($"[ChunkPersistence] 从 {path} 路径加载区块{chunk.Uid}的数据失败: {e.Message}");
                return false;
            }
        }

        /*******************************
                  非阻塞方案 (Async)
        ********************************/

        /// <summary>
        /// 异步操作类型
        /// </summary>
        public enum PersistenceOperationType
        {
            Load = 0,
            Save = 1
        }

        // 结果表条目
        /// <summary>
        /// 构造函数：创建结果表条目
        /// </summary>
        private class ResultEntry(object result, ulong timestamp, PersistenceOperationType type)
        {
            // 结果数据或成功标志
            public object Result = result; // ChunkData 或 bool (success)

            // 时间戳
            public ulong Timestamp = timestamp;
            
            // 操作类型
            public PersistenceOperationType OperationType = type;

            public override string ToString()
            {
                return $"ResultEntry(Type: {OperationType}, Result: {Result}, Timestamp: {Timestamp})";
            }
        }

        // 任务表: Key(Uid_OpType) -> Task
        private static readonly ConcurrentDictionary<string, Task> _activeTasks = new();
        
        // 结果表: Key(Uid_OpType) -> ResultEntry
        private static readonly ConcurrentDictionary<string, ResultEntry> _resultTable = new();

        private static string GetResultKey(string uid, PersistenceOperationType type)
        {
            return $"{uid}_{type}";
        }


        // 信号量限制并发数 (虽然任务表限制了创建，但Semaphore可确保执行层面的并发控制)
        // 只能存在12个任务要通过 _activeTasks.Count 控制创建
        private static readonly SemaphoreSlim _concurrencySemaphore = new(MAX_CONCURRENT_TASKS);

        /// <summary>
        /// 获取区块存档路径
        /// </summary>
        public static string GetChunkFilePath(Chunk chunk, string saveDir)
        {
            // 1. 计算 Grid 位置
            MapLayer layer = chunk.OwnerManager.OwnerLayer;
            // 此时 MapLayer.GridSize 已经是 Vector2I 类型
            MapGridPosition gridPos = chunk.CPosition.ToGridPosition(layer);
            string gridUid = gridPos.ToString();

            // 2. 拼接目录路径: saveDir/Grid_UID
            string gridDir = $"{saveDir}/{gridUid}";

            // 3. 确保目录存在
            if (!DirAccess.DirExistsAbsolute(gridDir))
            {
                DirAccess.MakeDirRecursiveAbsolute(gridDir);
            }

            // 4. 返回完整文件路径: saveDir/Grid_UID/chunk_{Uid}.json
            return $"{gridDir}/chunk_{chunk.Uid}.json";
        }

        /// <summary>
        /// 尝试获取异步加载结果或启动任务
        /// </summary>
        /// <param name="chunk">目标区块</param>
        /// <param name="saveDir">存档目录</param>
        /// <param name="data">输出数据（仅当返回true时有效）</param>
        /// <returns>true: 已获取结果; false: 任务进行中或刚启动; null: 参数错误</returns>
        public static bool? TryLoadAsync(Chunk chunk, string saveDir, out ChunkData data)
        {
            data = null;
            // 检查参数是否为 null
            if (chunk == null)
            {
                GD.PushError("[ChunkPersistence] TryLoadAsync: chunk 参数为 null - 无法提供有效的UID和坐标");
                return null;
            }
            
            // 检查参数是否为 Empty
            if (chunk == Chunk.Empty)
            {
                GD.PushError("[ChunkPersistence] TryLoadAsync: chunk 参数为 Empty - 无法提供有效的UID和坐标");
                return null;
            }
            
            string uid = chunk.Uid;
            string resultKey = GetResultKey(uid, PersistenceOperationType.Load);

            // 结果表中存在则移除并返回数据与成功
            if (_resultTable.TryRemove(resultKey, out ResultEntry entry) == true)
            {
                if (entry.OperationType != PersistenceOperationType.Load)
                {
                    GD.PushError($"[ChunkPersistence] key为加载操作的结果项,内部却并非加载操作，虽该次操作失败但错误结果已被移除，结果项: {resultKey}");
                    return false;
                }
                data = entry.Result as ChunkData;
                return true;
            }

            // 存在该区块的任务在运行则返回临时失败
            if (_activeTasks.ContainsKey(uid))
            {
                return false;
            }

            // 若并发达到最大限制
            if (_activeTasks.Count >= MAX_CONCURRENT_TASKS)
            {
                return false; // 稍后再试
            }

            // 无限制则创建新任务
            CreateLoadTask(uid, GetChunkFilePath(chunk, saveDir));
            return false;
        }

        /// <summary>
        /// 创建异步加载任务
        /// </summary>
        /// <param name="uid">区块唯一标识符</param>
        /// <param name="path">加载路径</param>
        private static void CreateLoadTask(string uid, string path)
        {
            string resultKey = GetResultKey(uid, PersistenceOperationType.Load);
            // 启动一个后台任务，在线程池中异步执行文件加载逻辑
            Task task = Task.Run(async () =>
            {
                // 等待信号量许可，确保并发任务数不超过系统限制（作为 TryLoadAsync 中 Count 检查的双重保险）
                await _concurrencySemaphore.WaitAsync();
                try
                {
                    // 初始化结果数据为 null（若文件不存在或加载失败，则保持为 null）
                    ChunkData resultData = null;
                    
                    // 仅当文件存在时才尝试读取和解析
                    if (File.Exists(path))
                    {
                        string json;
                        // 异步读取整个文件内容为字符串
                        using (StreamReader reader = new(path))
                        {
                            json = await reader.ReadToEndAsync();
                        }
                        // 将 JSON 字符串反序列化为 ChunkData 对象
                        resultData = JsonConvert.DeserializeObject<ChunkData>(json);
                    }
                    
                    // 构造结果条目（包含数据和时间戳），存入结果表供 TryLoadAsync 消费
                    ResultEntry entry = new(resultData, Time.GetTicksMsec(), PersistenceOperationType.Load);
                    _resultTable[resultKey] = entry;
                }
                catch (Exception e)
                {
                    // 记录加载错误日志（例如文件损坏、格式错误等）
                    GD.PushError($"[ChunkPersistence] 加载区块({uid})失败: {e.Message}");
                    
                    // 即使发生异常，也向结果表写入一个 null 结果，
                    // 避免 TryLoadAsync 因无结果而反复重试同一失败任务（防止死循环）
                    ResultEntry entry = new(null, Time.GetTicksMsec(), PersistenceOperationType.Load);
                    _resultTable[resultKey] = entry;
                }
                finally
                {
                    // 释放信号量许可，允许其他等待的任务继续执行
                    _concurrencySemaphore.Release();
                    // 从活跃任务表中移除当前任务，标记该 uid 的加载流程已完成
                    _activeTasks.TryRemove(uid, out _);
                }
            });

            // 立即将新创建的任务注册到活跃任务字典中，
            // 确保后续对同一 uid 的 TryLoadAsync 调用能识别任务已在运行
            _activeTasks[uid] = task;
        }


        /// <summary>
        /// 尝试获取异步保存结果或启动任务
        /// </summary>
        /// <returns>true: 保存完成; false: 任务进行中或刚启动; null: 参数错误</returns>
        public static bool? TrySaveAsync(Chunk chunk, string saveDir)
        {
            // 检查参数是否为 null
            if (chunk == null)
            {
                GD.PushError("[ChunkPersistence] TrySaveAsync: chunk 参数为 null - 无法提供有效的UID、坐标和数据");
                return null;
            }
            
            // 检查参数是否为 Empty
            if (chunk == Chunk.Empty)
            {
                GD.PushError("[ChunkPersistence] TrySaveAsync: chunk 参数为 Empty - 无法提供有效的UID、坐标和数据");
                return null;
            }
            
            string uid = chunk.Uid;
            string resultKey = GetResultKey(uid, PersistenceOperationType.Save);

            // 结果表中存在则移除并返回数据与成功
            if (_resultTable.TryRemove(resultKey, out ResultEntry entry) == true)
            {
                if (entry.OperationType != PersistenceOperationType.Save)
                {
                    GD.PushError($"[ChunkPersistence] key为保存操作的结果项,内部却并非保存操作，虽该次操作失败但错误结果已被移除，结果项: {resultKey}");
                    return false;
                }
                //若结果为错误类型则转换为失败
                return entry.Result as bool? ?? false;
            }

            // 2. 查看是否有任务正在运行
            if (_activeTasks.ContainsKey(uid))
            {
                return false;
            }

            // 3. 检查并发限制
            if (_activeTasks.Count >= MAX_CONCURRENT_TASKS)
            {
                return false;
            }

            // 4. 创建新任务 (需要主线程快照)
            if (chunk.Data == null) return true; // 无数据直接视为成功
            int[] snapshot = chunk.Data.Clone();

            CreateSaveTask(uid, GetChunkFilePath(chunk, saveDir), snapshot);
            return false;
        }


        /// <summary>
        /// 创建异步保存任务
        /// </summary>
        /// <param name="uid">区块唯一标识符</param>
        /// <param name="path">保存路径</param>
        /// <param name="snapshot">区块数据快照</param>
        private static void CreateSaveTask(string uid, string path, int[] snapshot)
        {
            string resultKey = GetResultKey(uid, PersistenceOperationType.Save);
            Task task = Task.Run(async () =>
            {
                // 等待信号量许可，控制并发保存数量
                await _concurrencySemaphore.WaitAsync();
                bool success = false;
                try
                {
                    // 将区块数据快照序列化并写入文件
                    string json = JsonConvert.SerializeObject(snapshot);
                    using (StreamWriter writer = new(path))
                    {
                        await writer.WriteAsync(json);
                    }
                    success = true;
                }
                catch (Exception e)
                {
                    // 记录异步保存错误
                    GD.PushError($"[ChunkPersistence] 异步保存区块({uid})失败: {e.Message}");
                    success = false;
                }
                finally
                {
                    // 存入结果表
                    ResultEntry entry = new (success,Time.GetTicksMsec(),PersistenceOperationType.Save);
                    _resultTable[resultKey] = entry;

                    // 释放信号量并移除活跃任务
                    _concurrencySemaphore.Release();
                    _activeTasks.TryRemove(uid, out _);
                }
            });

            // 注册活跃任务，供后续查询任务状态
            _activeTasks[uid] = task;
        }

        /// <summary>
        /// 清除过期的结果
        /// </summary>
        /// <param name="maxAgeMs">最大存活时间（毫秒），默认为DEFAULT_RESULT_EXPIRATION_MS即20000ms</param>
        public static void ClearExpiredResults(ulong maxAgeMs = DEFAULT_RESULT_EXPIRATION_MS)
        {
            ulong currentTime = Time.GetTicksMsec();
            List<string> toRemove = [];

            foreach (KeyValuePair<string, ResultEntry> pair in _resultTable)
            {
                if (currentTime - pair.Value.Timestamp > maxAgeMs)
                {
                    toRemove.Add(pair.Key);
                }
            }

            foreach (string uid in toRemove)
            {
                _resultTable.TryRemove(uid, out _);
            }
        }
    }
}
