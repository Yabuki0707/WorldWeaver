const translations = {
    'zh-CN': {
        title: '计数器系统文档',
        subtitle: '线程安全的自增计数与持久化解决方案',

        themeLabel: '主题',
        langLabel: '语言',

        themes: {
            cyber: '赛博',
            natural: '自然',
            pink: '粉红',
            night: '黑夜',
            day: '白日',
            code: '代码',
            reading: '阅读'
        },

        nav: {
            overview: '概述',
            lifecycle: '流程',
            design: '设计',
            api: 'API',
            examples: '示例',
            saveIsolation: '存档',
            warnings: '注意'
        },

        overview: '系统概述',
        overviewDesc: '计数器系统提供线程安全的自增计数功能，支持数据持久化，适用于游戏开发中实体ID分配、资源计数等场景。',

        counterClass: 'Counter 计数器类',
        counterDesc: '提供线程安全的自增计数功能，用于生成唯一标识符。',

        managerClass: 'CounterHistoryManager 历史数据管理器',
        managerDesc: '管理计数器的持久化数据，支持存档保存与恢复。',

        lifecycle: '生命周期流程',
        simpleFlow: '简略流程',
        detailedFlow: '深入流程',

        flow: {
            createManager: {
                name: '创建管理器',
                detail: '创建 CounterHistoryManager 实例，指定文件路径用于持久化存储。管理器会自动从文件加载历史数据到「历史数据表」，若文件不存在则创建空表。每个存档应使用独立的管理器实例。'
            },
            createCounter: {
                name: '创建计数器',
                detail: '创建 Counter 实例，指定计数器名称（持久化标识）和可选的管理器实例。计数器会自动生成 UID（运行时标识），从管理器获取历史数据初始化，并向管理器注册。'
            },
            useCounter: {
                name: '使用计数器',
                detail: '调用 GetAndIncrement() 方法获取唯一标识符。每次调用返回当前值并按步长自增。推荐使用此方法而非直接读取 CountValue。计数器操作是线程安全的。'
            },
            saveData: {
                name: '保存数据',
                detail: '调用 manager.Save() 方法将所有已注册计数器的当前值保存到文件。数据以 JSON 格式存储，键为计数器名称，值为当前计数值。应在游戏存档或退出时调用。'
            },
            releaseResource: {
                name: '释放资源',
                detail: '调用 counter.Dispose() 方法释放资源。计数器会从管理器的注册表中注销，并从全局实例映射表中移除。不再使用的计数器应及时释放以避免内存泄漏。'
            }
        },

        phase1: '第一阶段：初始化',
        phase1Step1: '创建管理器实例',
        phase1Step1Desc: '指定文件路径（如 "save1/counters.json"），自动从文件加载历史数据到「历史数据表」，若文件不存在则创建空表。',
        phase1Step2: '创建计数器实例',
        phase1Step2Desc1: '生成唯一 UID（运行时标识，每次创建都不同）',
        phase1Step2Desc2: '指定计数器名称（持久化标识，恒定不变）',
        phase1Step2Desc3: '从管理器查询历史数据：有历史数据则采用，无历史数据则使用 long.MinValue + 1 初始化',
        phase1Step2Desc4: '自动向管理器注册（加入「注册表」）',
        phase1Step2Desc5: '加入全局实例映射表（支持 UID 查询）',

        phase2: '第二阶段：运行使用',
        phase2Desc: '在游戏运行过程中使用计数器生成唯一标识符。',

        phase3: '第三阶段：持久化',
        phase3Desc1: '调用 manager.Save()',
        phase3Desc2: '遍历注册表中所有计数器',
        phase3Desc3: '获取每个计数器的当前值',
        phase3Desc4: '序列化为 JSON 写入文件',

        phase4: '第四阶段：资源释放',
        phase4Desc1: '调用 counter.Dispose()',
        phase4Desc2: '从管理器注销（移出注册表）',
        phase4Desc3: '从实例映射表移除',

        designPoints: '设计要点',

        dualIdentifier: '双标识符设计',
        dualIdentifierName: '名称（CounterName）',
        dualIdentifierNameDesc: '恒定不变，用于持久化，跨存档保持一致',
        dualIdentifierUID: 'UID',
        dualIdentifierUIDDesc: '运行时生成，用于实例查询，每次启动都不同',

        minValueSemantic: 'long.MinValue 的特殊语义',
        minValueDesc1: '不作为有效序号',
        minValueDesc2: 'GetHistoryValue() 返回此值表示「无历史数据」',
        minValueDesc3: '计数器初始化时检测到此值则使用 long.MinValue + 1',

        threadSafe: '线程安全',
        threadSafeDesc: '所有读写操作都使用锁保护',

        saveIsolation: '存档隔离方案',
        saveIsolationDesc: '每个存档使用独立的管理器实例，不同存档指向不同文件路径，计数器数据互不干扰',

        apiReference: 'API 参考',
        counterAPI: 'Counter 类',
        managerAPI: 'CounterHistoryManager 类',

        property: '属性',
        method: '方法',
        static: '静态',

        propName: 'CounterName',
        propNameDesc: '计数器名称，恒定不变的唯一标识符，用于持久化。',
        propUID: 'CounterUID',
        propUIDDesc: '运行时唯一标识符，用于实例查询，每次创建都不同。',
        propManager: 'Manager',
        propManagerDesc: '关联的历史数据管理器实例，可为 null。',
        propStep: 'IncrementStep',
        propStepDesc: '自增幅度，默认为 1。负值取绝对值，超过最大值则设为 1。',
        propValue: 'CountValue',
        propValueDesc: '当前计数值（下一个待使用的序号）。不推荐直接读取，应使用 GetAndIncrement()。',

        apiConstructor: 'Counter 构造函数',
        apiConstructorParams: 'counterName: string, manager?: CounterHistoryManager, incrementStep?: long',
        apiConstructorDesc: '创建计数器实例。若提供管理器则自动注册并尝试获取历史数据。',

        apiGetAndIncrement: 'GetAndIncrement()',
        apiGetAndIncrementReturn: 'long',
        apiGetAndIncrementDesc: '返回当前值并按步长自增。推荐使用此方法获取唯一标识符。',

        apiGetAndIncrementOne: 'GetAndIncrementOne()',
        apiGetAndIncrementOneReturn: 'long',
        apiGetAndIncrementOneDesc: '返回当前值并自增 1。',

        apiIncrement: 'Increment()',
        apiIncrementDesc: '仅自增，不返回值。',

        apiIncrementOne: 'IncrementOne()',
        apiIncrementOneDesc: '自增 1，不返回值。',

        apiGetValue: 'GetValue()',
        apiGetValueReturn: 'long',
        apiGetValueDesc: '获取当前计数值。不推荐使用，应使用 GetAndIncrement()。',

        apiSetValue: 'SetValue(value)',
        apiSetValueDesc: '设置计数值。会破坏自增语义，可能导致 ID 重复，谨慎使用。',

        apiDispose: 'Dispose()',
        apiDisposeDesc: '释放资源，从管理器和实例映射表中注销。',

        apiGetByUID: 'GetCounterByUID(uid)',
        apiGetByUIDReturn: 'Counter',
        apiGetByUIDDesc: '静态方法，通过 UID 查询计数器实例。',

        mgrPropRegCount: 'RegisteredCount',
        mgrPropRegCountDesc: '已注册的计数器数量。',
        mgrPropHistoryCount: 'HistoryDataCount',
        mgrPropHistoryCountDesc: '历史数据条目数量。',

        mgrConstructor: 'CounterHistoryManager 构造函数',
        mgrConstructorParams: 'filePath: string',
        mgrConstructorDesc: '创建管理器实例，绑定文件路径并加载历史数据。',

        mgrSave: 'Save()',
        mgrSaveReturn: 'bool',
        mgrSaveDesc: '保存所有已注册计数器的当前值到文件。',

        mgrRegister: 'Register(counter)',
        mgrRegisterReturn: 'bool',
        mgrRegisterDesc: '注册计数器（通常由 Counter 构造函数自动调用）。',

        mgrUnregister: 'Unregister(counterName)',
        mgrUnregisterReturn: 'bool',
        mgrUnregisterDesc: '注销计数器（通常由 Dispose 自动调用）。',

        mgrHasHistory: 'HasHistoryData(counterName)',
        mgrHasHistoryReturn: 'bool',
        mgrHasHistoryDesc: '检查是否存在历史数据。',

        mgrGetHistory: 'GetHistoryValue(counterName)',
        mgrGetHistoryReturn: 'long',
        mgrGetHistoryDesc: '获取历史值，无数据则返回 long.MinValue。',

        mgrClear: 'Clear()',
        mgrClearDesc: '清空所有数据。',

        examples: '使用示例',
        example1: '基础使用（无持久化）',
        example1Desc: '创建一个简单的计数器，不关联管理器，数据不会持久化。',
        example2: '带持久化的实体ID分配',
        example2Desc: '为游戏实体分配唯一ID，支持存档保存与恢复。',
        example3: '多存档场景',
        example3Desc: '多个存档槽位，每个存档独立管理计数器数据。',

        saveIsolationTitle: '存档隔离详解',
        saveIsolationIntro: '在游戏开发中，多个存档需要独立的计数器数据。通过为每个存档创建独立的管理器实例实现隔离：',
        saveIsolationTip: '切换存档时，只需切换对应的管理器实例，计数器数据自动隔离。',

        warning: '注意事项',
        warningMinValue: 'long.MinValue 不作为有效序号，仅表示「无历史数据」或错误状态。计数器初始化值为 long.MinValue + 1。',
        warningManagerNull: '管理器参数可为 null，此时计数器不进行持久化，适用于临时计数场景。',
        warningValue: 'CountValue 中的值是"下一个待使用"的序号，直接读取通常无意义。',
        warningSetValue: 'SetValue() 会破坏自增语义，可能导致 ID 重复，仅在特殊场景使用。',
        warningSave: '记得在适当的时机调用 Save() 保存数据，如游戏退出或存档时。',
        warningDispose: '计数器不再使用时应调用 Dispose() 释放资源。',

        footer: '计数器系统 - 线程安全的自增计数与持久化解决方案',

        navBar: '导航条',
        hideNavBar: '隐藏导航条',
        showNavBar: '显示导航条',

        audio: {
            on: '开启音频',
            off: '关闭音频',
            toggle: '音频开关'
        },

        randomTip: {
            button: '随机显示一条注意事项',
            title: '随机注意事项'
        }
    },

    'en': {
        title: 'Counter System Documentation',
        subtitle: 'Thread-safe Auto-increment Counter with Persistence',

        themeLabel: 'Theme',
        langLabel: 'Language',

        themes: {
            cyber: 'Cyber',
            natural: 'Natural',
            pink: 'Pink',
            night: 'Night',
            day: 'Day',
            code: 'Code',
            reading: 'Reading'
        },

        nav: {
            overview: 'Overview',
            lifecycle: 'Lifecycle',
            design: 'Design',
            api: 'API',
            examples: 'Examples',
            saveIsolation: 'Save',
            warnings: 'Notes'
        },

        overview: 'Overview',
        overviewDesc: 'The Counter System provides thread-safe auto-increment counting functionality with persistence support, suitable for entity ID allocation, resource counting, and other scenarios in game development.',

        counterClass: 'Counter Class',
        counterDesc: 'Provides thread-safe auto-increment counting for generating unique identifiers.',

        managerClass: 'CounterHistoryManager Class',
        managerDesc: 'Manages persistent counter data, supporting save and restore operations.',

        lifecycle: 'Lifecycle',
        simpleFlow: 'Simple Flow',
        detailedFlow: 'Detailed Flow',

        flow: {
            createManager: {
                name: 'Create Manager',
                detail: 'Create a CounterHistoryManager instance, specifying a file path for persistent storage. The manager automatically loads history data from the file into the "History Data Table". If the file doesn\'t exist, an empty table is created. Each save should use an independent manager instance.'
            },
            createCounter: {
                name: 'Create Counter',
                detail: 'Create a Counter instance, specifying the counter name (persistent identifier) and an optional manager instance. The counter automatically generates a UID (runtime identifier), retrieves history data from the manager for initialization, and registers with the manager.'
            },
            useCounter: {
                name: 'Use Counter',
                detail: 'Call the GetAndIncrement() method to obtain unique identifiers. Each call returns the current value and increments by step. This method is recommended over directly reading CountValue. Counter operations are thread-safe.'
            },
            saveData: {
                name: 'Save Data',
                detail: 'Call the manager.Save() method to save the current values of all registered counters to file. Data is stored in JSON format, with counter names as keys and current count values as values. Should be called during game save or exit.'
            },
            releaseResource: {
                name: 'Release Resources',
                detail: 'Call the counter.Dispose() method to release resources. The counter will be unregistered from the manager\'s registry and removed from the global instance map. Counters no longer in use should be released promptly to avoid memory leaks.'
            }
        },

        phase1: 'Phase 1: Initialization',
        phase1Step1: 'Create Manager Instance',
        phase1Step1Desc: 'Specify file path (e.g., "save1/counters.json"), automatically load history data from file into "History Data Table", create empty table if file doesn\'t exist.',
        phase1Step2: 'Create Counter Instance',
        phase1Step2Desc1: 'Generate unique UID (runtime identifier, different each time)',
        phase1Step2Desc2: 'Specify counter name (persistent identifier, constant)',
        phase1Step2Desc3: 'Query history data from manager: use history value if exists, otherwise initialize with long.MinValue + 1',
        phase1Step2Desc4: 'Automatically register with manager (add to "Registry")',
        phase1Step2Desc5: 'Add to global instance map (supports UID query)',

        phase2: 'Phase 2: Runtime Usage',
        phase2Desc: 'Use counters during game runtime to generate unique identifiers.',

        phase3: 'Phase 3: Persistence',
        phase3Desc1: 'Call manager.Save()',
        phase3Desc2: 'Iterate through all counters in registry',
        phase3Desc3: 'Get current value of each counter',
        phase3Desc4: 'Serialize to JSON and write to file',

        phase4: 'Phase 4: Resource Cleanup',
        phase4Desc1: 'Call counter.Dispose()',
        phase4Desc2: 'Unregister from manager (remove from registry)',
        phase4Desc3: 'Remove from instance map',

        designPoints: 'Design Points',

        dualIdentifier: 'Dual Identifier Design',
        dualIdentifierName: 'Name (CounterName)',
        dualIdentifierNameDesc: 'Constant, used for persistence, consistent across saves',
        dualIdentifierUID: 'UID',
        dualIdentifierUIDDesc: 'Generated at runtime, used for instance query, different each startup',

        minValueSemantic: 'Special Semantics of long.MinValue',
        minValueDesc1: 'Not used as valid sequence number',
        minValueDesc2: 'GetHistoryValue() returns this value to indicate "no history data"',
        minValueDesc3: 'Counter initializes with long.MinValue + 1 when detecting this value',

        threadSafe: 'Thread Safety',
        threadSafeDesc: 'All read/write operations are protected by locks',

        saveIsolation: 'Save Isolation',
        saveIsolationDesc: 'Each save uses independent manager instance with different file paths, counter data is isolated',

        apiReference: 'API Reference',
        counterAPI: 'Counter Class',
        managerAPI: 'CounterHistoryManager Class',

        property: 'Property',
        method: 'Method',
        static: 'Static',

        propName: 'CounterName',
        propNameDesc: 'Counter name, constant unique identifier for persistence.',
        propUID: 'CounterUID',
        propUIDDesc: 'Runtime unique identifier for instance query, different each creation.',
        propManager: 'Manager',
        propManagerDesc: 'Associated history manager instance, can be null.',
        propStep: 'IncrementStep',
        propStepDesc: 'Increment step, default is 1. Negative values use absolute value, exceeding max uses 1.',
        propValue: 'CountValue',
        propValueDesc: 'Current count value (next unused sequence number). Direct reading not recommended, use GetAndIncrement() instead.',

        apiConstructor: 'Counter Constructor',
        apiConstructorParams: 'counterName: string, manager?: CounterHistoryManager, incrementStep?: long',
        apiConstructorDesc: 'Create counter instance. Automatically registers and retrieves history data if manager is provided.',

        apiGetAndIncrement: 'GetAndIncrement()',
        apiGetAndIncrementReturn: 'long',
        apiGetAndIncrementDesc: 'Returns current value and increments by step. Recommended method for obtaining unique identifiers.',

        apiGetAndIncrementOne: 'GetAndIncrementOne()',
        apiGetAndIncrementOneReturn: 'long',
        apiGetAndIncrementOneDesc: 'Returns current value and increments by 1.',

        apiIncrement: 'Increment()',
        apiIncrementDesc: 'Increments without returning value.',

        apiIncrementOne: 'IncrementOne()',
        apiIncrementOneDesc: 'Increments by 1 without returning value.',

        apiGetValue: 'GetValue()',
        apiGetValueReturn: 'long',
        apiGetValueDesc: 'Gets current count value. Not recommended, use GetAndIncrement() instead.',

        apiSetValue: 'SetValue(value)',
        apiSetValueDesc: 'Sets count value. Breaks auto-increment semantics, may cause ID duplication, use with caution.',

        apiDispose: 'Dispose()',
        apiDisposeDesc: 'Releases resources, unregisters from manager and instance map.',

        apiGetByUID: 'GetCounterByUID(uid)',
        apiGetByUIDReturn: 'Counter',
        apiGetByUIDDesc: 'Static method, queries counter instance by UID.',

        mgrPropRegCount: 'RegisteredCount',
        mgrPropRegCountDesc: 'Number of registered counters.',
        mgrPropHistoryCount: 'HistoryDataCount',
        mgrPropHistoryCountDesc: 'Number of history data entries.',

        mgrConstructor: 'CounterHistoryManager Constructor',
        mgrConstructorParams: 'filePath: string',
        mgrConstructorDesc: 'Creates manager instance, binds file path and loads history data.',

        mgrSave: 'Save()',
        mgrSaveReturn: 'bool',
        mgrSaveDesc: 'Saves current values of all registered counters to file.',

        mgrRegister: 'Register(counter)',
        mgrRegisterReturn: 'bool',
        mgrRegisterDesc: 'Registers counter (usually called automatically by Counter constructor).',

        mgrUnregister: 'Unregister(counterName)',
        mgrUnregisterReturn: 'bool',
        mgrUnregisterDesc: 'Unregisters counter (usually called automatically by Dispose).',

        mgrHasHistory: 'HasHistoryData(counterName)',
        mgrHasHistoryReturn: 'bool',
        mgrHasHistoryDesc: 'Checks if history data exists.',

        mgrGetHistory: 'GetHistoryValue(counterName)',
        mgrGetHistoryReturn: 'long',
        mgrGetHistoryDesc: 'Gets history value, returns long.MinValue if no data.',

        mgrClear: 'Clear()',
        mgrClearDesc: 'Clears all data.',

        examples: 'Examples',
        example1: 'Basic Usage (No Persistence)',
        example1Desc: 'Create a simple counter without manager, data will not persist.',
        example2: 'Entity ID Allocation with Persistence',
        example2Desc: 'Allocate unique IDs for game entities with save/restore support.',
        example3: 'Multiple Save Slots',
        example3Desc: 'Multiple save slots with independent counter data.',

        saveIsolationTitle: 'Save Isolation Details',
        saveIsolationIntro: 'In game development, multiple saves need independent counter data. Isolation is achieved by creating independent manager instances for each save:',
        saveIsolationTip: 'When switching saves, simply switch the corresponding manager instance, counter data is automatically isolated.',

        warning: 'Notes',
        warningMinValue: 'long.MinValue is not a valid sequence number, it only means "no history data" or error state. Counter initializes with long.MinValue + 1.',
        warningManagerNull: 'Manager parameter can be null, in which case the counter won\'t persist, suitable for temporary counting scenarios.',
        warningValue: 'CountValue contains "next unused" sequence number, direct reading is usually meaningless.',
        warningSetValue: 'SetValue() breaks auto-increment semantics, may cause ID duplication, use only in special scenarios.',
        warningSave: 'Remember to call Save() at appropriate times, such as game exit or save.',
        warningDispose: 'Call Dispose() when counter is no longer needed to release resources.',

        footer: 'Counter System - Thread-safe Auto-increment Counter with Persistence',

        navBar: 'Navigation',
        hideNavBar: 'Hide Navigation',
        showNavBar: 'Show Navigation',

        audio: {
            on: 'Turn on Audio',
            off: 'Turn off Audio',
            toggle: 'Audio Toggle'
        },

        randomTip: {
            button: 'Show a random note',
            title: 'Random Note'
        }
    },

    'ja': {
        title: 'カウンターシステムドキュメント',
        subtitle: 'スレッドセーフな自動増分カウンターと永続化ソリューション',

        themeLabel: 'テーマ',
        langLabel: '言語',

        themes: {
            cyber: 'サイバー',
            natural: 'ナチュラル',
            pink: 'ピンク',
            night: 'ナイト',
            day: 'デイ',
            code: 'コード',
            reading: 'リーディング'
        },

        nav: {
            overview: '概要',
            lifecycle: 'ライフサイクル',
            design: '設計',
            api: 'API',
            examples: '例',
            saveIsolation: 'セーブ',
            warnings: '注意'
        },

        overview: 'システム概要',
        overviewDesc: 'カウンターシステムはスレッドセーフな自動増分カウント機能を提供し、データの永続化をサポートします。ゲーム開発におけるエンティティID割り当て、リソースカウントなどのシナリオに適しています。',

        counterClass: 'Counter クラス',
        counterDesc: 'スレッドセーフな自動増分カウント機能を提供し、一意識別子を生成します。',

        managerClass: 'CounterHistoryManager クラス',
        managerDesc: 'カウンターの永続化データを管理し、セーブと復元をサポートします。',

        lifecycle: 'ライフサイクル',
        simpleFlow: '簡易フロー',
        detailedFlow: '詳細フロー',

        flow: {
            createManager: {
                name: 'マネージャー作成',
                detail: 'CounterHistoryManager インスタンスを作成し、永続化ストレージ用のファイルパスを指定します。マネージャーは自動的にファイルから履歴データを「履歴データテーブル」に読み込みます。ファイルが存在しない場合は空のテーブルが作成されます。各セーブは独立したマネージャーインスタンスを使用すべきです。'
            },
            createCounter: {
                name: 'カウンター作成',
                detail: 'Counter インスタンスを作成し、カウンター名（永続化識別子）とオプションのマネージャーインスタンスを指定します。カウンターは自動的に UID（実行時識別子）を生成し、マネージャーから履歴データを取得して初期化し、マネージャーに登録します。'
            },
            useCounter: {
                name: 'カウンター使用',
                detail: 'GetAndIncrement() メソッドを呼び出して一意識別子を取得します。各呼び出しは現在の値を返し、ステップ分増分します。CountValue を直接読み取るよりもこのメソッドの使用が推奨されます。カウンター操作はスレッドセーフです。'
            },
            saveData: {
                name: 'データ保存',
                detail: 'manager.Save() メソッドを呼び出して、すべての登録済みカウンターの現在値をファイルに保存します。データは JSON 形式で保存され、カウンター名がキー、現在のカウント値が値となります。ゲームのセーブまたは終了時に呼び出すべきです。'
            },
            releaseResource: {
                name: 'リソース解放',
                detail: 'counter.Dispose() メソッドを呼び出してリソースを解放します。カウンターはマネージャーの登録テーブルから登録解除され、グローバルインスタンスマップから削除されます。使用されなくなったカウンターは速やかに解放してメモリリークを防ぐべきです。'
            }
        },

        phase1: 'フェーズ1：初期化',
        phase1Step1: 'マネージャーインスタンスの作成',
        phase1Step1Desc: 'ファイルパスを指定（例："save1/counters.json"）、ファイルから履歴データを「履歴データテーブル」に自動読み込み、ファイルが存在しない場合は空のテーブルを作成。',
        phase1Step2: 'カウンタインスタンスの作成',
        phase1Step2Desc1: '一意のUIDを生成（実行時識別子、毎回異なる）',
        phase1Step2Desc2: 'カウンタ名を指定（永続化識別子、不変）',
        phase1Step2Desc3: 'マネージャーから履歴データを照会：履歴データがあれば採用、なければ long.MinValue + 1 で初期化',
        phase1Step2Desc4: 'マネージャーに自動登録（「登録テーブル」に追加）',
        phase1Step2Desc5: 'グローバルインスタンスマップに追加（UID照会をサポート）',

        phase2: 'フェーズ2：実行時使用',
        phase2Desc: 'ゲーム実行中にカウンターを使用して一意識別子を生成します。',

        phase3: 'フェーズ3：永続化',
        phase3Desc1: 'manager.Save() を呼び出し',
        phase3Desc2: '登録テーブル内のすべてのカウンターを反復',
        phase3Desc3: '各カウンターの現在値を取得',
        phase3Desc4: 'JSON にシリアライズしてファイルに書き込み',

        phase4: 'フェーズ4：リソース解放',
        phase4Desc1: 'counter.Dispose() を呼び出し',
        phase4Desc2: 'マネージャーから登録解除（登録テーブルから削除）',
        phase4Desc3: 'インスタンスマップから削除',

        designPoints: '設計ポイント',

        dualIdentifier: '二重識別子設計',
        dualIdentifierName: '名前（CounterName）',
        dualIdentifierNameDesc: '不変、永続化に使用、セーブ間で一貫性を維持',
        dualIdentifierUID: 'UID',
        dualIdentifierUIDDesc: '実行時に生成、インスタンス照会に使用、毎回の起動で異なる',

        minValueSemantic: 'long.MinValue の特殊な意味',
        minValueDesc1: '有効なシーケンス番号として使用されない',
        minValueDesc2: 'GetHistoryValue() は「履歴データなし」を示すためにこの値を返す',
        minValueDesc3: 'カウンターはこの値を検出すると long.MinValue + 1 で初期化',

        threadSafe: 'スレッドセーフ',
        threadSafeDesc: 'すべての読み書き操作はロックで保護',

        saveIsolation: 'セーブ分離',
        saveIsolationDesc: '各セーブは独立したマネージャーインスタンスを使用、異なるファイルパスを指し、カウンターデータは相互に干渉しない',

        apiReference: 'API リファレンス',
        counterAPI: 'Counter クラス',
        managerAPI: 'CounterHistoryManager クラス',

        property: 'プロパティ',
        method: 'メソッド',
        static: '静的',

        propName: 'CounterName',
        propNameDesc: 'カウンタ名、永続化用の不変の一意識別子。',
        propUID: 'CounterUID',
        propUIDDesc: '実行時の一意識別子、インスタンス照会に使用、毎回の作成で異なる。',
        propManager: 'Manager',
        propManagerDesc: '関連付けられた履歴マネージャーインスタンス、null 可能。',
        propStep: 'IncrementStep',
        propStepDesc: '増分ステップ、デフォルトは1。負の値は絶対値を使用、最大値を超えると1に設定。',
        propValue: 'CountValue',
        propValueDesc: '現在のカウント値（次の未使用シーケンス番号）。直接読み取りは非推奨、GetAndIncrement() を使用してください。',

        apiConstructor: 'Counter コンストラクタ',
        apiConstructorParams: 'counterName: string, manager?: CounterHistoryManager, incrementStep?: long',
        apiConstructorDesc: 'カウンタインスタンスを作成。マネージャーが提供されると自動的に登録し履歴データを取得。',

        apiGetAndIncrement: 'GetAndIncrement()',
        apiGetAndIncrementReturn: 'long',
        apiGetAndIncrementDesc: '現在の値を返しステップ分増分。一意識別子を取得する推奨メソッド。',

        apiGetAndIncrementOne: 'GetAndIncrementOne()',
        apiGetAndIncrementOneReturn: 'long',
        apiGetAndIncrementOneDesc: '現在の値を返し1増分。',

        apiIncrement: 'Increment()',
        apiIncrementDesc: '値を返さずに増分のみ。',

        apiIncrementOne: 'IncrementOne()',
        apiIncrementOneDesc: '値を返さずに1増分。',

        apiGetValue: 'GetValue()',
        apiGetValueReturn: 'long',
        apiGetValueDesc: '現在のカウント値を取得。非推奨、GetAndIncrement() を使用してください。',

        apiSetValue: 'SetValue(value)',
        apiSetValueDesc: 'カウント値を設定。自動増分のセマンティクスを破壊、ID重複の可能性、注意して使用。',

        apiDispose: 'Dispose()',
        apiDisposeDesc: 'リソースを解放、マネージャーとインスタンスマップから登録解除。',

        apiGetByUID: 'GetCounterByUID(uid)',
        apiGetByUIDReturn: 'Counter',
        apiGetByUIDDesc: '静的メソッド、UIDでカウンタインスタンスを照会。',

        mgrPropRegCount: 'RegisteredCount',
        mgrPropRegCountDesc: '登録されたカウンターの数。',
        mgrPropHistoryCount: 'HistoryDataCount',
        mgrPropHistoryCountDesc: '履歴データエントリの数。',

        mgrConstructor: 'CounterHistoryManager コンストラクタ',
        mgrConstructorParams: 'filePath: string',
        mgrConstructorDesc: 'マネージャーインスタンスを作成、ファイルパスをバインドし履歴データを読み込み。',

        mgrSave: 'Save()',
        mgrSaveReturn: 'bool',
        mgrSaveDesc: 'すべての登録済みカウンターの現在値をファイルに保存。',

        mgrRegister: 'Register(counter)',
        mgrRegisterReturn: 'bool',
        mgrRegisterDesc: 'カウンターを登録（通常はCounterコンストラクタが自動的に呼び出し）。',

        mgrUnregister: 'Unregister(counterName)',
        mgrUnregisterReturn: 'bool',
        mgrUnregisterDesc: 'カウンターの登録を解除（通常はDisposeが自動的に呼び出し）。',

        mgrHasHistory: 'HasHistoryData(counterName)',
        mgrHasHistoryReturn: 'bool',
        mgrHasHistoryDesc: '履歴データが存在するかチェック。',

        mgrGetHistory: 'GetHistoryValue(counterName)',
        mgrGetHistoryReturn: 'long',
        mgrGetHistoryDesc: '履歴値を取得、データがない場合は long.MinValue を返す。',

        mgrClear: 'Clear()',
        mgrClearDesc: 'すべてのデータをクリア。',

        examples: '使用例',
        example1: '基本使用（永続化なし）',
        example1Desc: 'マネージャーなしでシンプルなカウンターを作成、データは永続化されない。',
        example2: '永続化付きエンティティID割り当て',
        example2Desc: 'ゲームエンティティに一意IDを割り当て、セーブ/復元をサポート。',
        example3: '複数セーブスロット',
        example3Desc: '複数のセーブスロット、各スロットが独立したカウンターデータを持つ。',

        saveIsolationTitle: 'セーブ分離の詳細',
        saveIsolationIntro: 'ゲーム開発では、複数のセーブが独立したカウンターデータを必要とします。各セーブに独立したマネージャーインスタンスを作成することで分離を実現：',
        saveIsolationTip: 'セーブ切り替え時は、対応するマネージャーインスタンスを切り替えるだけで、カウンターデータは自動的に分離されます。',

        warning: '注意事項',
        warningMinValue: 'long.MinValue は有効なシーケンス番号ではなく、「履歴データなし」またはエラー状態を意味します。カウンターの初期値は long.MinValue + 1 です。',
        warningManagerNull: 'マネージャーパラメータは null 可能で、その場合カウンターは永続化されず、一時的なカウントシナリオに適しています。',
        warningValue: 'CountValueには「次の未使用」シーケンス番号が含まれ、直接読み取りは通常無意味です。',
        warningSetValue: 'SetValue() は自動増分のセマンティクスを破壊し、ID重複の可能性があるため、特別なシナリオでのみ使用してください。',
        warningSave: 'ゲーム終了やセーブ時など、適切なタイミングで Save() を呼び出してください。',
        warningDispose: 'カウンターが不要になったら Dispose() を呼び出してリソースを解放してください。',

        footer: 'カウンターシステム - スレッドセーフな自動増分カウンターと永続化ソリューション',

        navBar: 'ナビゲーション',
        hideNavBar: 'ナビゲーションを隠す',
        showNavBar: 'ナビゲーションを表示',

        audio: {
            on: 'オーディオをオン',
            off: 'オーディオをオフ',
            toggle: 'オーディオトグル'
        },

        randomTip: {
            button: 'ランダムなノートを表示',
            title: 'ランダムノート'
        }
    },

    'zh-TW': {
        title: '計數器系統文件',
        subtitle: '執行緒安全的自動遞增計數與持久化解決方案',

        themeLabel: '主題',
        langLabel: '語言',

        themes: {
            cyber: '賽博',
            natural: '自然',
            pink: '粉紅',
            night: '黑夜',
            day: '白日',
            code: '程式碼',
            reading: '閱讀'
        },

        nav: {
            overview: '概述',
            lifecycle: '流程',
            design: '設計',
            api: 'API',
            examples: '範例',
            saveIsolation: '存檔',
            warnings: '注意'
        },

        overview: '系統概述',
        overviewDesc: '計數器系統提供執行緒安全的自動遞增計數功能，支援資料持久化，適用於遊戲開發中實體ID分配、資源計數等場景。',

        counterClass: 'Counter 計數器類別',
        counterDesc: '提供執行緒安全的自動遞增計數功能，用於產生唯一識別碼。',

        managerClass: 'CounterHistoryManager 歷史資料管理器',
        managerDesc: '管理計數器的持久化資料，支援存檔儲存與復原。',

        lifecycle: '生命週期流程',
        simpleFlow: '簡略流程',
        detailedFlow: '深入流程',

        flow: {
            createManager: {
                name: '建立管理器',
                detail: '建立 CounterHistoryManager 實例，指定檔案路徑用於持久化儲存。管理器會自動從檔案載入歷史資料到「歷史資料表」，若檔案不存在則建立空表。每個存檔應使用獨立的管理器實例。'
            },
            createCounter: {
                name: '建立計數器',
                detail: '建立 Counter 實例，指定計數器名稱（持久化識別碼）和可選的管理器實例。計數器會自動產生 UID（執行時識別碼），從管理器獲取歷史資料初始化，並向管理器註冊。'
            },
            useCounter: {
                name: '使用計數器',
                detail: '呼叫 GetAndIncrement() 方法獲取唯一識別碼。每次呼叫返回目前值並按步長遞增。推薦使用此方法而非直接讀取 CountValue。計數器操作是執行緒安全的。'
            },
            saveData: {
                name: '儲存資料',
                detail: '呼叫 manager.Save() 方法將所有已註冊計數器的目前值儲存到檔案。資料以 JSON 格式儲存，鍵為計數器名稱，值為目前計數值。應在遊戲存檔或退出時呼叫。'
            },
            releaseResource: {
                name: '釋放資源',
                detail: '呼叫 counter.Dispose() 方法釋放資源。計數器會從管理器的註冊表中註銷，並從全域實例映射表中移除。不再使用的計數器應及時釋放以避免記憶體洩漏。'
            }
        },

        phase1: '第一階段：初始化',
        phase1Step1: '建立管理器實例',
        phase1Step1Desc: '指定檔案路徑（如 "save1/counters.json"），自動從檔案載入歷史資料到「歷史資料表」，若檔案不存在則建立空表。',
        phase1Step2: '建立計數器實例',
        phase1Step2Desc1: '產生唯一 UID（執行時識別碼，每次建立都不同）',
        phase1Step2Desc2: '指定計數器名稱（持久化識別碼，恆定不變）',
        phase1Step2Desc3: '從管理器查詢歷史資料：有歷史資料則採用，無歷史資料則使用 long.MinValue + 1 初始化',
        phase1Step2Desc4: '自動向管理器註冊（加入「註冊表」）',
        phase1Step2Desc5: '加入全域實例映射表（支援 UID 查詢）',

        phase2: '第二階段：執行使用',
        phase2Desc: '在遊戲執行過程中使用計數器產生唯一識別碼。',

        phase3: '第三階段：持久化',
        phase3Desc1: '呼叫 manager.Save()',
        phase3Desc2: '遍歷註冊表中所有計數器',
        phase3Desc3: '取得每個計數器的目前值',
        phase3Desc4: '序列化為 JSON 寫入檔案',

        phase4: '第四階段：資源釋放',
        phase4Desc1: '呼叫 counter.Dispose()',
        phase4Desc2: '從管理器註銷（移出註冊表）',
        phase4Desc3: '從實例映射表移除',

        designPoints: '設計要點',

        dualIdentifier: '雙識別碼設計',
        dualIdentifierName: '名稱（CounterName）',
        dualIdentifierNameDesc: '恆定不變，用於持久化，跨存檔保持一致',
        dualIdentifierUID: 'UID',
        dualIdentifierUIDDesc: '執行時產生，用於實例查詢，每次啟動都不同',

        minValueSemantic: 'long.MinValue 的特殊語意',
        minValueDesc1: '不作為有效序號',
        minValueDesc2: 'GetHistoryValue() 傳回此值表示「無歷史資料」',
        minValueDesc3: '計數器初始化時偵測到此值則使用 long.MinValue + 1',

        threadSafe: '執行緒安全',
        threadSafeDesc: '所有讀寫操作都使用鎖保護',

        saveIsolation: '存檔隔離方案',
        saveIsolationDesc: '每個存檔使用獨立的管理器實例，不同存檔指向不同檔案路徑，計數器資料互不干擾',

        apiReference: 'API 參考',
        counterAPI: 'Counter 類別',
        managerAPI: 'CounterHistoryManager 類別',

        property: '屬性',
        method: '方法',
        static: '靜態',

        propName: 'CounterName',
        propNameDesc: '計數器名稱，恆定不變的唯一識別碼，用於持久化。',
        propUID: 'CounterUID',
        propUIDDesc: '執行時唯一識別碼，用於實例查詢，每次建立都不同。',
        propManager: 'Manager',
        propManagerDesc: '關聯的歷史資料管理器實例，可為 null。',
        propStep: 'IncrementStep',
        propStepDesc: '遞增幅度，預設為 1。負值取絕對值，超過最大值則設為 1。',
        propValue: 'CountValue',
        propValueDesc: '目前計數值（下一個待使用的序號）。不建議直接讀取，應使用 GetAndIncrement()。',

        apiConstructor: 'Counter 建構函式',
        apiConstructorParams: 'counterName: string, manager?: CounterHistoryManager, incrementStep?: long',
        apiConstructorDesc: '建立計數器實例。若提供管理器則自動註冊並嘗試取得歷史資料。',

        apiGetAndIncrement: 'GetAndIncrement()',
        apiGetAndIncrementReturn: 'long',
        apiGetAndIncrementDesc: '傳回目前值並按步長遞增。建議使用此方法取得唯一識別碼。',

        apiGetAndIncrementOne: 'GetAndIncrementOne()',
        apiGetAndIncrementOneReturn: 'long',
        apiGetAndIncrementOneDesc: '傳回目前值並遞增 1。',

        apiIncrement: 'Increment()',
        apiIncrementDesc: '僅遞增，不傳回值。',

        apiIncrementOne: 'IncrementOne()',
        apiIncrementOneDesc: '遞增 1，不傳回值。',

        apiGetValue: 'GetValue()',
        apiGetValueReturn: 'long',
        apiGetValueDesc: '取得目前計數值。不建議使用，應使用 GetAndIncrement()。',

        apiSetValue: 'SetValue(value)',
        apiSetValueDesc: '設定計數值。會破壞遞增語意，可能導致 ID 重複，謹慎使用。',

        apiDispose: 'Dispose()',
        apiDisposeDesc: '釋放資源，從管理器和實例映射表中註銷。',

        apiGetByUID: 'GetCounterByUID(uid)',
        apiGetByUIDReturn: 'Counter',
        apiGetByUIDDesc: '靜態方法，透過 UID 查詢計數器實例。',

        mgrPropRegCount: 'RegisteredCount',
        mgrPropRegCountDesc: '已註冊的計數器數量。',
        mgrPropHistoryCount: 'HistoryDataCount',
        mgrPropHistoryCountDesc: '歷史資料條目數量。',

        mgrConstructor: 'CounterHistoryManager 建構函式',
        mgrConstructorParams: 'filePath: string',
        mgrConstructorDesc: '建立管理器實例，綁定檔案路徑並載入歷史資料。',

        mgrSave: 'Save()',
        mgrSaveReturn: 'bool',
        mgrSaveDesc: '儲存所有已註冊計數器的目前值到檔案。',

        mgrRegister: 'Register(counter)',
        mgrRegisterReturn: 'bool',
        mgrRegisterDesc: '註冊計數器（通常由 Counter 建構函式自動呼叫）。',

        mgrUnregister: 'Unregister(counterName)',
        mgrUnregisterReturn: 'bool',
        mgrUnregisterDesc: '註銷計數器（通常由 Dispose 自動呼叫）。',

        mgrHasHistory: 'HasHistoryData(counterName)',
        mgrHasHistoryReturn: 'bool',
        mgrHasHistoryDesc: '檢查是否存在歷史資料。',

        mgrGetHistory: 'GetHistoryValue(counterName)',
        mgrGetHistoryReturn: 'long',
        mgrGetHistoryDesc: '取得歷史值，無資料則傳回 long.MinValue。',

        mgrClear: 'Clear()',
        mgrClearDesc: '清空所有資料。',

        examples: '使用範例',
        example1: '基本使用（無持久化）',
        example1Desc: '建立一個簡單的計數器，不關聯管理器，資料不會持久化。',
        example2: '帶持久化的實體ID分配',
        example2Desc: '為遊戲實體分配唯一ID，支援存檔儲存與復原。',
        example3: '多存檔場景',
        example3Desc: '多個存檔槽位，每個存檔獨立管理計數器資料。',

        saveIsolationTitle: '存檔隔離詳解',
        saveIsolationIntro: '在遊戲開發中，多個存檔需要獨立的計數器資料。透過為每個存檔建立獨立的管理器實例實現隔離：',
        saveIsolationTip: '切換存檔時，只需切換對應的管理器實例，計數器資料自動隔離。',

        warning: '注意事項',
        warningMinValue: 'long.MinValue 不作為有效序號，僅表示「無歷史資料」或錯誤狀態。計數器初始值為 long.MinValue + 1。',
        warningManagerNull: '管理器參數可為 null，此時計數器不進行持久化，適用於臨時計數場景。',
        warningValue: 'CountValue 中的值是「下一個待使用」的序號，直接讀取通常無意義。',
        warningSetValue: 'SetValue() 會破壞遞增語意，可能導致 ID 重複，僅在特殊場景使用。',
        warningSave: '記得在適當的時機呼叫 Save() 儲存資料，如遊戲退出或存檔時。',
        warningDispose: '計數器不再使用時應呼叫 Dispose() 釋放資源。',

        footer: '計數器系統 - 執行緒安全的自動遞增計數與持久化解決方案',

        navBar: '導航條',
        hideNavBar: '隱藏導航條',
        showNavBar: '顯示導航條',

        audio: {
            on: '開啟音訊',
            off: '關閉音訊',
            toggle: '音訊開關'
        },

        randomTip: {
            button: '隨機顯示一條注意事項',
            title: '隨機注意事項'
        }
    }
};

function t(key) {
    const lang = currentLang || 'zh-CN';
    const keys = key.split('.');
    let value = translations[lang];
    for (const k of keys) {
        if (value && value[k] !== undefined) {
            value = value[k];
        } else {
            return key;
        }
    }
    return value;
}
