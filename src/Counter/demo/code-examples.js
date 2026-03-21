const codeExamples = {
    'zh-CN': {
        example1: `<span class="code-comment">// 示例1：基础使用（无持久化）</span>
<span class="code-comment">// 适用于临时计数场景，数据不会保存</span>

<span class="code-type">Counter</span> tempCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"TempID"</span>);

<span class="code-comment">// 获取唯一ID</span>
<span class="code-keyword">long</span> id1 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 返回 long.MinValue + 1</span>
<span class="code-keyword">long</span> id2 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 返回 long.MinValue + 2</span>
<span class="code-keyword">long</span> id3 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 返回 long.MinValue + 3</span>`,

        example2: `<span class="code-comment">// 示例2：带持久化的实体ID分配</span>
<span class="code-comment">// 游戏实体（敌人、道具、子弹等）需要唯一ID</span>

<span class="code-keyword">public class</span> <span class="code-type">GameManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _counterManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _entityIdCounter;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _bulletIdCounter;
    
    <span class="code-keyword">public void</span> <span class="code-method">Initialize</span>()
    {
        <span class="code-comment">// 创建管理器，绑定存档文件</span>
        _counterManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(<span class="code-string">"save/entity_counters.json"</span>);
        
        <span class="code-comment">// 创建实体ID计数器</span>
        _entityIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"EntityID"</span>, _counterManager);
        
        <span class="code-comment">// 子弹ID计数器，每次自增10（预分配ID段）</span>
        _bulletIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"BulletID"</span>, _counterManager, incrementStep: <span class="code-number">10</span>);
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Enemy</span> <span class="code-method">SpawnEnemy</span>()
    {
        <span class="code-keyword">long</span> entityId = _entityIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Enemy</span>(entityId);  <span class="code-comment">// 敌人获得唯一ID</span>
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Bullet</span> <span class="code-method">FireBullet</span>()
    {
        <span class="code-keyword">long</span> bulletId = _bulletIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Bullet</span>(bulletId);  <span class="code-comment">// 子弹ID: 1, 11, 21, 31...</span>
    }
    
    <span class="code-keyword">public void</span> <span class="code-method">SaveGame</span>()
    {
        _counterManager.<span class="code-method">Save</span>();  <span class="code-comment">// 保存计数器数据到文件</span>
    }
}`,

        example3: `<span class="code-comment">// 示例3：多存档场景</span>
<span class="code-comment">// 每个存档槽位独立管理计数器数据</span>

<span class="code-keyword">public class</span> <span class="code-type">SaveSlotManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _currentManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _playerActionId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _questId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _itemId;
    
    <span class="code-comment">// 切换存档</span>
    <span class="code-keyword">public void</span> <span class="code-method">LoadSlot</span>(<span class="code-keyword">int</span> slotNumber)
    {
        <span class="code-comment">// 每个存档使用独立的文件</span>
        <span class="code-keyword">string</span> savePath = $<span class="code-string">"saves/slot{slotNumber}/counters.json"</span>;
        _currentManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(savePath);
        
        <span class="code-comment">// 创建该存档的计数器</span>
        _playerActionId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"PlayerAction"</span>, _currentManager);
        _questId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"QuestID"</span>, _currentManager);
        _itemId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"ItemID"</span>, _currentManager);
        
        <span class="code-comment">// 计数器会自动从文件恢复历史数据</span>
        <span class="code-type">GD</span>.<span class="code-method">Print</span>($<span class="code-string">"加载存档 {slotNumber}，当前任务ID: {_questId.CountValue}"</span>);
    }
    
    <span class="code-comment">// 保存当前存档</span>
    <span class="code-keyword">public void</span> <span class="code-method">SaveCurrentSlot</span>()
    {
        _currentManager?.<span class="code-method">Save</span>();
    }
    
    <span class="code-comment">// 玩家完成任务</span>
    <span class="code-keyword">public</span> <span class="code-type">Quest</span> <span class="code-method">CreateQuest</span>()
    {
        <span class="code-keyword">long</span> questId = _questId.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Quest</span>(questId);
    }
}

<span class="code-comment">// 使用示例</span>
<span class="code-type">SaveSlotManager</span> saveManager = <span class="code-keyword">new</span> <span class="code-type">SaveSlotManager</span>();
saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">1</span>);  <span class="code-comment">// 加载存档1</span>
<span class="code-comment">// ... 游戏进行中 ...</span>
saveManager.<span class="code-method">SaveCurrentSlot</span>();  <span class="code-comment">// 保存</span>

saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">2</span>);  <span class="code-comment">// 切换到存档2（计数器数据独立）</span>`,

        saveIsolation: `<span class="code-comment">// 存档隔离：文件结构示例</span>
<span class="code-comment">//</span>
<span class="code-comment">// saves/</span>
<span class="code-comment">// ├── slot1/</span>
<span class="code-comment">// │   ├── counters.json    ← 存档1的计数器数据</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// ├── slot2/</span>
<span class="code-comment">// │   ├── counters.json    ← 存档2的计数器数据（独立）</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// └── slot3/</span>
<span class="code-comment">//     ├── counters.json    ← 存档3的计数器数据（独立）</span>
<span class="code-comment">//     └── player_data.json</span>

<span class="code-comment">// counters.json 内容示例</span>
{
    <span class="code-string">"EntityID"</span>: <span class="code-number">1523</span>,
    <span class="code-string">"BulletID"</span>: <span class="code-number">840</span>,
    <span class="code-string">"QuestID"</span>: <span class="code-number">15</span>,
    <span class="code-string">"ItemID"</span>: <span class="code-number">67</span>
}`
    },
    
    'en': {
        example1: `<span class="code-comment">// Example 1: Basic Usage (No Persistence)</span>
<span class="code-comment">// Suitable for temporary counting, data won't be saved</span>

<span class="code-type">Counter</span> tempCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"TempID"</span>);

<span class="code-comment">// Get unique IDs</span>
<span class="code-keyword">long</span> id1 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 1</span>
<span class="code-keyword">long</span> id2 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 2</span>
<span class="code-keyword">long</span> id3 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 3</span>`,

        example2: `<span class="code-comment">// Example 2: Entity ID Allocation with Persistence</span>
<span class="code-comment">// Game entities (enemies, items, bullets) need unique IDs</span>

<span class="code-keyword">public class</span> <span class="code-type">GameManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _counterManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _entityIdCounter;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _bulletIdCounter;
    
    <span class="code-keyword">public void</span> <span class="code-method">Initialize</span>()
    {
        <span class="code-comment">// Create manager, bind to save file</span>
}`
    },
    
    'en': {
        example1: `<span class="code-comment">// Example 1: Basic Usage (No Persistence)</span>
<span class="code-comment">// Suitable for temporary counting, data won't be saved</span>

<span class="code-type">Counter</span> tempCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"TempID"</span>);

<span class="code-comment">// Get unique IDs</span>
<span class="code-keyword">long</span> id1 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 1</span>
<span class="code-keyword">long</span> id2 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 2</span>
<span class="code-keyword">long</span> id3 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// Returns long.MinValue + 3</span>`,

        example2: `<span class="code-comment">// Example 2: Entity ID Allocation with Persistence</span>
<span class="code-comment">// Game entities (enemies, items, bullets, etc.) need unique IDs</span>

<span class="code-keyword">public class</span> <span class="code-type">GameManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _counterManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _entityIdCounter;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _bulletIdCounter;
    
    <span class="code-keyword">public void</span> <span class="code-method">Initialize</span>()
    {
        <span class="code-comment">// Create manager, bind to save file</span>
        _counterManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(<span class="code-string">"save/entity_counters.json"</span>);
        
        <span class="code-comment">// Create entity ID counter</span>
        _entityIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"EntityID"</span>, _counterManager);
        
        <span class="code-comment">// Bullet ID counter, increment by 10 (pre-allocate ID ranges)</span>
        _bulletIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"BulletID"</span>, _counterManager, incrementStep: <span class="code-number">10</span>);
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Enemy</span> <span class="code-method">SpawnEnemy</span>()
    {
        <span class="code-keyword">long</span> entityId = _entityIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Enemy</span>(entityId);  <span class="code-comment">// Enemy gets unique ID</span>
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Bullet</span> <span class="code-method">FireBullet</span>()
    {
        <span class="code-keyword">long</span> bulletId = _bulletIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Bullet</span>(bulletId);  <span class="code-comment">// Bullet IDs: 1, 11, 21, 31...</span>
    }
    
    <span class="code-keyword">public void</span> <span class="code-method">SaveGame</span>()
    {
        _counterManager.<span class="code-method">Save</span>();  <span class="code-comment">// Save counter data to file</span>
    }
}`,

        example3: `<span class="code-comment">// Example 3: Multiple Save Slots</span>
<span class="code-comment">// Each save slot independently manages counter data</span>

<span class="code-keyword">public class</span> <span class="code-type">SaveSlotManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _currentManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _playerActionId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _questId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _itemId;
    
    <span class="code-comment">// Switch save slot</span>
    <span class="code-keyword">public void</span> <span class="code-method">LoadSlot</span>(<span class="code-keyword">int</span> slotNumber)
    {
        <span class="code-comment">// Each save uses independent file</span>
        <span class="code-keyword">string</span> savePath = $<span class="code-string">"saves/slot{slotNumber}/counters.json"</span>;
        _currentManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(savePath);
        
        <span class="code-comment">// Create counters for this save</span>
        _playerActionId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"PlayerAction"</span>, _currentManager);
        _questId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"QuestID"</span>, _currentManager);
        _itemId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"ItemID"</span>, _currentManager);
        
        <span class="code-comment">// Counters automatically restore history data from file</span>
        <span class="code-type">GD</span>.<span class="code-method">Print</span>($<span class="code-string">"Loaded slot {slotNumber}, current quest ID: {_questId.CountValue}"</span>);
    }
    
    <span class="code-comment">// Save current slot</span>
    <span class="code-keyword">public void</span> <span class="code-method">SaveCurrentSlot</span>()
    {
        _currentManager?.<span class="code-method">Save</span>();
    }
    
    <span class="code-comment">// Player completes quest</span>
    <span class="code-keyword">public</span> <span class="code-type">Quest</span> <span class="code-method">CreateQuest</span>()
    {
        <span class="code-keyword">long</span> questId = _questId.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Quest</span>(questId);
    }
}

<span class="code-comment">// Usage example</span>
<span class="code-type">SaveSlotManager</span> saveManager = <span class="code-keyword">new</span> <span class="code-type">SaveSlotManager</span>();
saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">1</span>);  <span class="code-comment">// Load save 1</span>
<span class="code-comment">// ... game in progress ...</span>
saveManager.<span class="code-method">SaveCurrentSlot</span>();  <span class="code-comment">// Save</span>

saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">2</span>);  <span class="code-comment">// Switch to save 2 (counter data is independent)</span>`,

        saveIsolation: `<span class="code-comment">// Save Isolation: File Structure Example</span>
<span class="code-comment">//</span>
<span class="code-comment">// saves/</span>
<span class="code-comment">// ├── slot1/</span>
<span class="code-comment">// │   ├── counters.json    ← Save 1's counter data</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// ├── slot2/</span>
<span class="code-comment">// │   ├── counters.json    ← Save 2's counter data (independent)</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// └── slot3/</span>
<span class="code-comment">//     ├── counters.json    ← Save 3's counter data (independent)</span>
<span class="code-comment">//     └── player_data.json</span>

<span class="code-comment">// counters.json content example</span>
{
    <span class="code-string">"EntityID"</span>: <span class="code-number">1523</span>,
    <span class="code-string">"BulletID"</span>: <span class="code-number">210</span>,
    <span class="code-string">"QuestID"</span>: <span class="code-number">47</span>
}`
    },
    
    'ja': {
        example1: `<span class="code-comment">// 例1：基本使用（永続化なし）</span>
<span class="code-comment">// 一時的なカウントに適用、データは保存されない</span>

<span class="code-type">Counter</span> tempCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"TempID"</span>);

<span class="code-comment">// 一意IDを取得</span>
<span class="code-keyword">long</span> id1 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// long.MinValue + 1 を返す</span>
<span class="code-keyword">long</span> id2 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// long.MinValue + 2 を返す</span>
<span class="code-keyword">long</span> id3 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// long.MinValue + 3 を返す</span>`,

        example2: `<span class="code-comment">// 例2：永続化付きエンティティID割り当て</span>
<span class="code-comment">// ゲームエンティティ（敵、アイテム、弾丸など）は一意IDが必要</span>

<span class="code-keyword">public class</span> <span class="code-type">GameManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _counterManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _entityIdCounter;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _bulletIdCounter;
    
    <span class="code-keyword">public void</span> <span class="code-method">Initialize</span>()
    {
        <span class="code-comment">// マネージャーを作成、セーブファイルにバインド</span>
        _counterManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(<span class="code-string">"save/entity_counters.json"</span>);
        
        <span class="code-comment">// エンティティIDカウンターを作成</span>
        _entityIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"EntityID"</span>, _counterManager);
        
        <span class="code-comment">// 弾丸IDカウンター、10ずつ増分（ID範囲を事前割り当て）</span>
        _bulletIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"BulletID"</span>, _counterManager, incrementStep: <span class="code-number">10</span>);
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Enemy</span> <span class="code-method">SpawnEnemy</span>()
    {
        <span class="code-keyword">long</span> entityId = _entityIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Enemy</span>(entityId);  <span class="code-comment">// 敵が一意IDを取得</span>
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Bullet</span> <span class="code-method">FireBullet</span>()
    {
        <span class="code-keyword">long</span> bulletId = _bulletIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Bullet</span>(bulletId);  <span class="code-comment">// 弾丸ID: 1, 11, 21, 31...</span>
    }
    
    <span class="code-keyword">public void</span> <span class="code-method">SaveGame</span>()
    {
        _counterManager.<span class="code-method">Save</span>();  <span class="code-comment">// カウンターデータをファイルに保存</span>
    }
}`,

        example3: `<span class="code-comment">// 例3：複数セーブスロット</span>
<span class="code-comment">// 各セーブスロットが独立してカウンターデータを管理</span>

<span class="code-keyword">public class</span> <span class="code-type">SaveSlotManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _currentManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _playerActionId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _questId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _itemId;
    
    <span class="code-comment">// セーブスロットを切り替え</span>
    <span class="code-keyword">public void</span> <span class="code-method">LoadSlot</span>(<span class="code-keyword">int</span> slotNumber)
    {
        <span class="code-comment">// 各セーブは独立したファイルを使用</span>
        <span class="code-keyword">string</span> savePath = $<span class="code-string">"saves/slot{slotNumber}/counters.json"</span>;
        _currentManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(savePath);
        
        <span class="code-comment">// このセーブのカウンターを作成</span>
        _playerActionId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"PlayerAction"</span>, _currentManager);
        _questId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"QuestID"</span>, _currentManager);
        _itemId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"ItemID"</span>, _currentManager);
        
        <span class="code-comment">// カウンターは自動的にファイルから履歴データを復元</span>
        <span class="code-type">GD</span>.<span class="code-method">Print</span>($<span class="code-string">"スロット {slotNumber} をロード、現在のクエストID: {_questId.CountValue}"</span>);
    }
    
    <span class="code-keyword">public void</span> <span class="code-method">SaveCurrentSlot</span>()
    {
        _currentManager?.<span class="code-method">Save</span>();
    }
    
    <span class="code-comment">// プレイヤーがクエストを完了</span>
    <span class="code-keyword">public</span> <span class="code-type">Quest</span> <span class="code-method">CreateQuest</span>()
    {
        <span class="code-keyword">long</span> questId = _questId.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Quest</span>(questId);
    }
}

<span class="code-comment">// 使用例</span>
<span class="code-type">SaveSlotManager</span> saveManager = <span class="code-keyword">new</span> <span class="code-type">SaveSlotManager</span>();
saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">1</span>);  <span class="code-comment">// セーブ1をロード</span>
<span class="code-comment">// ... ゲーム進行中 ...</span>
saveManager.<span class="code-method">SaveCurrentSlot</span>();  <span class="code-comment">// 保存</span>

saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">2</span>);  <span class="code-comment">// セーブ2に切り替え（カウンターデータは独立）</span>`,

        saveIsolation: `<span class="code-comment">// セーブ分離：ファイル構造例</span>
<span class="code-comment">//</span>
<span class="code-comment">// saves/</span>
<span class="code-comment">// ├── slot1/</span>
<span class="code-comment">// │   ├── counters.json    ← セーブ1のカウンターデータ</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// ├── slot2/</span>
<span class="code-comment">// │   ├── counters.json    ← セーブ2のカウンターデータ（独立）</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// └── slot3/</span>
<span class="code-comment">//     ├── counters.json    ← セーブ3のカウンターデータ（独立）</span>
<span class="code-comment">//     └── player_data.json</span>

<span class="code-comment">// counters.json 内容例</span>
{
    <span class="code-string">"EntityID"</span>: <span class="code-number">1523</span>,
    <span class="code-string">"BulletID"</span>: <span class="code-number">210</span>,
    <span class="code-string">"QuestID"</span>: <span class="code-number">47</span>
}`
    },
    
    'zh-TW': {
        example1: `<span class="code-comment">// 範例1：基本使用（無持久化）</span>
<span class="code-comment">// 適用於臨時計數場景，資料不會儲存</span>

<span class="code-type">Counter</span> tempCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"TempID"</span>);

<span class="code-comment">// 取得唯一ID</span>
<span class="code-keyword">long</span> id1 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 傳回 long.MinValue + 1</span>
<span class="code-keyword">long</span> id2 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 傳回 long.MinValue + 2</span>
<span class="code-keyword">long</span> id3 = tempCounter.<span class="code-method">GetAndIncrement</span>();  <span class="code-comment">// 傳回 long.MinValue + 3</span>`,

        example2: `<span class="code-comment">// 範例2：帶持久化的實體ID分配</span>
<span class="code-comment">// 遊戲實體（敵人、道具、子彈等）需要唯一ID</span>

<span class="code-keyword">public class</span> <span class="code-type">GameManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _counterManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _entityIdCounter;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _bulletIdCounter;
    
    <span class="code-keyword">public void</span> <span class="code-method">Initialize</span>()
    {
        <span class="code-comment">// 建立管理器，綁定存檔檔案</span>
        _counterManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(<span class="code-string">"save/entity_counters.json"</span>);
        
        <span class="code-comment">// 建立實體ID計數器</span>
        _entityIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"EntityID"</span>, _counterManager);
        
        <span class="code-comment">// 子彈ID計數器，每次遞增10（預分配ID段）</span>
        _bulletIdCounter = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"BulletID"</span>, _counterManager, incrementStep: <span class="code-number">10</span>);
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Enemy</span> <span class="code-method">SpawnEnemy</span>()
    {
        <span class="code-keyword">long</span> entityId = _entityIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Enemy</span>(entityId);  <span class="code-comment">// 敵人獲得唯一ID</span>
    }
    
    <span class="code-keyword">public</span> <span class="code-type">Bullet</span> <span class="code-method">FireBullet</span>()
    {
        <span class="code-keyword">long</span> bulletId = _bulletIdCounter.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Bullet</span>(bulletId);  <span class="code-comment">// 子彈ID: 1, 11, 21, 31...</span>
    }
    
    <span class="code-keyword">public void</span> <span class="code-method">SaveGame</span>()
    {
        _counterManager.<span class="code-method">Save</span>();  <span class="code-comment">// 儲存計數器資料到檔案</span>
    }
}`,

        example3: `<span class="code-comment">// 範例3：多存檔場景</span>
<span class="code-comment">// 每個存檔槽位獨立管理計數器資料</span>

<span class="code-keyword">public class</span> <span class="code-type">SaveSlotManager</span>
{
    <span class="code-keyword">private</span> <span class="code-type">CounterHistoryManager</span> _currentManager;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _playerActionId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _questId;
    <span class="code-keyword">private</span> <span class="code-type">Counter</span> _itemId;
    
    <span class="code-comment">// 切換存檔</span>
    <span class="code-keyword">public void</span> <span class="code-method">LoadSlot</span>(<span class="code-keyword">int</span> slotNumber)
    {
        <span class="code-comment">// 每個存檔使用獨立的檔案</span>
        <span class="code-keyword">string</span> savePath = $<span class="code-string">"saves/slot{slotNumber}/counters.json"</span>;
        _currentManager = <span class="code-keyword">new</span> <span class="code-type">CounterHistoryManager</span>(savePath);
        
        <span class="code-comment">// 建立該存檔的計數器</span>
        _playerActionId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"PlayerAction"</span>, _currentManager);
        _questId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"QuestID"</span>, _currentManager);
        _itemId = <span class="code-keyword">new</span> <span class="code-type">Counter</span>(<span class="code-string">"ItemID"</span>, _currentManager);
        
        <span class="code-comment">// 計數器會自動從檔案復原歷史資料</span>
        <span class="code-type">GD</span>.<span class="code-method">Print</span>($<span class="code-string">"載入存檔 {slotNumber}，目前任務ID: {_questId.CountValue}"</span>);
    }
    
    <span class="code-comment">// 儲存目前存檔</span>
    <span class="code-keyword">public void</span> <span class="code-method">SaveCurrentSlot</span>()
    {
        _currentManager?.<span class="code-method">Save</span>();
    }
    
    <span class="code-comment">// 玩家完成任務</span>
    <span class="code-keyword">public</span> <span class="code-type">Quest</span> <span class="code-method">CreateQuest</span>()
    {
        <span class="code-keyword">long</span> questId = _questId.<span class="code-method">GetAndIncrement</span>();
        <span class="code-keyword">return new</span> <span class="code-type">Quest</span>(questId);
    }
}

<span class="code-comment">// 使用範例</span>
<span class="code-type">SaveSlotManager</span> saveManager = <span class="code-keyword">new</span> <span class="code-type">SaveSlotManager</span>();
saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">1</span>);  <span class="code-comment">// 載入存檔1</span>
<span class="code-comment">// ... 遊戲進行中 ...</span>
saveManager.<span class="code-method">SaveCurrentSlot</span>();  <span class="code-comment">// 儲存</span>

saveManager.<span class="code-method">LoadSlot</span>(<span class="code-number">2</span>);  <span class="code-comment">// 切換到存檔2（計數器資料獨立）</span>`,

        saveIsolation: `<span class="code-comment">// 存檔隔離：檔案結構範例</span>
<span class="code-comment">//</span>
<span class="code-comment">// saves/</span>
<span class="code-comment">// ├── slot1/</span>
<span class="code-comment">// │   ├── counters.json    ← 存檔1的計數器資料</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// ├── slot2/</span>
<span class="code-comment">// │   ├── counters.json    ← 存檔2的計數器資料（獨立）</span>
<span class="code-comment">// │   └── player_data.json</span>
<span class="code-comment">// └── slot3/</span>
<span class="code-comment">//     ├── counters.json    ← 存檔3的計數器資料（獨立）</span>
<span class="code-comment">//     └── player_data.json</span>

<span class="code-comment">// counters.json 內容範例</span>
{
    <span class="code-string">"EntityID"</span>: <span class="code-number">1523</span>,
    <span class="code-string">"BulletID"</span>: <span class="code-number">210</span>,
    <span class="code-string">"QuestID"</span>: <span class="code-number">47</span>
}`
    }
};

function getCodeExample(key) {
    const lang = currentLang || 'zh-CN';
    return codeExamples[lang][key] || codeExamples['zh-CN'][key] || '';
}
